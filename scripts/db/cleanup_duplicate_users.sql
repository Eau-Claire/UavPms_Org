-- Clean up Users rows that block the UNIQUE indexes IX_Users_Email and IX_Users_Phone (plan item 1.4).
--
-- Usage (inside the DB container):
--   psql -X -v ON_ERROR_STOP=1 -v apply=false -U <user> -d <db> -f cleanup_duplicate_users.sql   # dry run, rolls back
--   psql -X -v ON_ERROR_STOP=1 -v apply=true  -U <user> -d <db> -f cleanup_duplicate_users.sql   # commits
--
-- Rules (no account is merged, no row is hard-deleted, foreign keys are left untouched):
--   * Duplicate email (case/space-insensitive, same as IdentityService login): keep one account per email,
--     ranked by not deleted, Status = 'Active', most referencing rows (all FKs to Users), oldest CreatedAt.
--     The kept account's email is normalised to lower(trim(Email)) like IdentityService does.
--     Every other account: Email = 'dup-<id>@invalid.local', Status = 'Inactive', soft-deleted.
--   * Empty email: Email = 'no-email-<id>@invalid.local' (account otherwise unchanged).
--   * Duplicate non-empty phone: keep it on the account ranked first (same ranking, accounts with a
--     placeholder email ranked last), set Phone = '' on the others.

\if :{?apply}
\else
\set apply false
\endif

\set QUIET on
\pset footer off
BEGIN;

-- Number of rows that reference each user through a single-column foreign key to Users.
CREATE TEMP TABLE user_refs ON COMMIT DROP AS SELECT "Id" AS user_id, 0::bigint AS refs FROM "Users";
DO $$
DECLARE r record;
BEGIN
    FOR r IN
        SELECT c.conrelid::regclass AS tbl, a.attname AS col
        FROM pg_constraint c
        JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = c.conkey[1]
        WHERE c.contype = 'f' AND c.confrelid = 'public."Users"'::regclass AND array_length(c.conkey, 1) = 1
    LOOP
        EXECUTE format(
            'UPDATE user_refs u SET refs = u.refs + s.n FROM (SELECT %I AS uid, count(*) AS n FROM %s WHERE %I IS NOT NULL GROUP BY 1) s WHERE s.uid = u.user_id',
            r.col, r.tbl, r.col);
    END LOOP;
END $$;

CREATE TEMP TABLE email_plan ON COMMIT DROP AS
SELECT u."Id", u."Email", u."Status", u."IsDeleted", u."CreatedAt", r.refs,
       row_number() OVER (PARTITION BY lower(btrim(u."Email"))
                          ORDER BY u."IsDeleted", (u."Status" = 'Active') DESC, r.refs DESC, u."CreatedAt", u."Id") AS rank
FROM "Users" u
JOIN user_refs r ON r.user_id = u."Id"
WHERE lower(btrim(u."Email")) IN (
    SELECT lower(btrim("Email")) FROM "Users" WHERE btrim("Email") <> '' GROUP BY 1 HAVING count(*) > 1);

\echo
\echo '========== DUPLICATE EMAILS =========='
SELECT "Id", "Email", "Status", "IsDeleted", "CreatedAt", refs,
       CASE WHEN rank > 1 THEN 'DISABLE -> dup-' || "Id" || '@invalid.local'
            WHEN "Email" <> lower(btrim("Email")) THEN 'KEEP -> ' || lower(btrim("Email"))
            ELSE 'KEEP' END AS action
FROM email_plan ORDER BY lower(btrim("Email")), rank;

\echo '========== EMPTY EMAILS =========='
SELECT u."Id", u."FullName", u."Status", u."IsDeleted", u."CreatedAt", r.refs,
       'SET no-email-' || u."Id" || '@invalid.local' AS action
FROM "Users" u JOIN user_refs r ON r.user_id = u."Id"
WHERE btrim(u."Email") = '' ORDER BY u."CreatedAt";

UPDATE "Users" u
SET "Email" = 'dup-' || u."Id" || '@invalid.local',
    "Status" = 'Inactive',
    "IsDeleted" = true,
    "DeletedAt" = COALESCE(u."DeletedAt", now()),
    "UpdatedAt" = now()
FROM email_plan p
WHERE p."Id" = u."Id" AND p.rank > 1;

-- Kept account: same normalisation as IdentityService (Trim + lower), so login by email finds it.
UPDATE "Users" u
SET "Email" = lower(btrim(u."Email")), "UpdatedAt" = now()
FROM email_plan p
WHERE p."Id" = u."Id" AND p.rank = 1 AND u."Email" <> lower(btrim(u."Email"));

UPDATE "Users"
SET "Email" = 'no-email-' || "Id" || '@invalid.local', "UpdatedAt" = now()
WHERE btrim("Email") = '';

-- Phone is ranked after the email step so a disabled duplicate account, or one without a real email,
-- loses the phone first.
CREATE TEMP TABLE phone_plan ON COMMIT DROP AS
SELECT u."Id", u."Email", u."Phone", u."Status", u."IsDeleted", r.refs,
       row_number() OVER (PARTITION BY u."Phone"
                          ORDER BY u."IsDeleted", (u."Email" LIKE '%@invalid.local'), (u."Status" = 'Active') DESC,
                                   r.refs DESC, u."CreatedAt", u."Id") AS rank
FROM "Users" u
JOIN user_refs r ON r.user_id = u."Id"
WHERE u."Phone" IN (SELECT "Phone" FROM "Users" WHERE "Phone" <> '' GROUP BY 1 HAVING count(*) > 1);

\echo '========== DUPLICATE PHONES =========='
SELECT "Id", "Email", "Phone", "Status", "IsDeleted", refs,
       CASE WHEN rank = 1 THEN 'KEEP' ELSE 'CLEAR PHONE' END AS action
FROM phone_plan ORDER BY "Phone", rank;

UPDATE "Users" u SET "Phone" = '', "UpdatedAt" = now()
FROM phone_plan p
WHERE p."Id" = u."Id" AND p.rank > 1;

\echo '========== CHECK (all must be 0) =========='
SELECT
    (SELECT count(*) FROM (SELECT "Email" FROM "Users" GROUP BY 1 HAVING count(*) > 1) d) AS duplicate_emails,
    (SELECT count(*) FROM (SELECT lower(btrim("Email")) FROM "Users" GROUP BY 1 HAVING count(*) > 1) d) AS duplicate_emails_ci,
    (SELECT count(*) FROM "Users" WHERE btrim("Email") = '') AS empty_emails,
    (SELECT count(*) FROM (SELECT "Phone" FROM "Users" WHERE "Phone" <> '' GROUP BY 1 HAVING count(*) > 1) d) AS duplicate_phones;

\echo '========== SUMMARY =========='
SELECT (SELECT count(*) FROM email_plan WHERE rank > 1) AS accounts_disabled,
       (SELECT count(*) FROM phone_plan WHERE rank > 1) AS phones_cleared;

\if :apply
COMMIT;
\echo 'APPLIED: changes committed.'
\else
ROLLBACK;
\echo 'DRY RUN: nothing was changed (rolled back).'
\endif
