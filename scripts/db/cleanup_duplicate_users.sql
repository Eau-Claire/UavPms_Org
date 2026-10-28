-- Clean up Users rows that block the UNIQUE indexes IX_Users_Email and IX_Users_Phone (plan item 1.4).
--
-- Usage (inside the DB container):
--   psql -X -v ON_ERROR_STOP=1 -v apply=false -U <user> -d <db> -f cleanup_duplicate_users.sql   # dry run, rolls back
--   psql -X -v ON_ERROR_STOP=1 -v apply=true  -U <user> -d <db> -f cleanup_duplicate_users.sql   # commits
--
-- Rules (no account is merged, disabled or hard-deleted; status and foreign keys are left untouched):
--   * Duplicate email (case/space-insensitive, same as IdentityService login): the account ranked first
--     (not deleted, Status = 'Active', most referencing rows over all FKs to Users, oldest CreatedAt) keeps the
--     email, normalised to lower(trim(Email)) like IdentityService does.
--     Every other account that is not soft-deleted gets a plus alias of the same mailbox, tagged with its
--     roles: local+<role>[-<role>...]@domain (e.g. liem+manager@gmail.com; 'user' when it has no role).
--     If that alias is already taken, '-<first 8 hex of id>' is appended to the tag.
--     Soft-deleted duplicates get Email = 'dup-<id>@invalid.local'.
--   * Empty email: Email = 'no-email-<id>@invalid.local'.
--   * Duplicate non-empty phone: keep it on the account ranked first (accounts with a placeholder or plus-alias
--     email ranked last), set Phone = '' on the others.
--   * The script raises an error (nothing is committed) if any duplicate is left.

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
SELECT p.*,
       CASE WHEN p.rank = 1 THEN p.email_key
            WHEN p."IsDeleted" THEN 'dup-' || p."Id" || '@invalid.local'
            ELSE p.local_part || '+' || p.role_tag || '@' || p.domain END AS new_email
FROM (
    SELECT u."Id", u."Email", u."Status", u."IsDeleted", u."CreatedAt", r.refs,
           lower(btrim(u."Email")) AS email_key,
           split_part(lower(btrim(u."Email")), '@', 1) AS local_part,
           COALESCE(NULLIF(substring(lower(btrim(u."Email")) FROM position('@' IN lower(btrim(u."Email"))) + 1), lower(btrim(u."Email"))), 'invalid.local') AS domain,
           COALESCE((SELECT string_agg(NULLIF(regexp_replace(lower(ro."RoleName"), '[^a-z0-9]', '', 'g'), ''), '-' ORDER BY ro."RoleName")
                     FROM "UserRoles" ur JOIN "Roles" ro ON ro."Id" = ur."RoleId"
                     WHERE ur."UserId" = u."Id"), 'user') AS role_tag,
           row_number() OVER (PARTITION BY lower(btrim(u."Email"))
                              ORDER BY u."IsDeleted", (u."Status" = 'Active') DESC, r.refs DESC, u."CreatedAt", u."Id") AS rank
    FROM "Users" u
    JOIN user_refs r ON r.user_id = u."Id"
    WHERE lower(btrim(u."Email")) IN (
        SELECT lower(btrim("Email")) FROM "Users" WHERE btrim("Email") <> '' GROUP BY 1 HAVING count(*) > 1)
) p;

-- An alias already used by another account (or produced twice, e.g. two Manager aliases of one mailbox)
-- gets the first 8 hex digits of the account id appended to its tag.
UPDATE email_plan p
SET new_email = p.local_part || '+' || p.role_tag || '-' || left(replace(p."Id"::text, '-', ''), 8) || '@' || p.domain
WHERE p.rank > 1 AND NOT p."IsDeleted"
  AND ((SELECT count(*) FROM email_plan o WHERE o.new_email = p.new_email) > 1
       OR EXISTS (SELECT 1 FROM "Users" u
                  WHERE lower(btrim(u."Email")) = p.new_email
                    AND u."Id" NOT IN (SELECT "Id" FROM email_plan)));

\echo
\echo '========== DUPLICATE EMAILS =========='
SELECT "Id", "Email", "Status", "IsDeleted", "CreatedAt", refs, role_tag,
       CASE WHEN rank = 1 AND "Email" = new_email THEN 'KEEP'
            WHEN rank = 1 THEN 'KEEP -> ' || new_email
            WHEN "IsDeleted" THEN 'RENAME (deleted) -> ' || new_email
            ELSE 'ALIAS -> ' || new_email END AS action
FROM email_plan ORDER BY email_key, rank;

\echo '========== EMPTY EMAILS =========='
SELECT u."Id", u."FullName", u."Status", u."IsDeleted", u."CreatedAt", r.refs,
       'SET no-email-' || u."Id" || '@invalid.local' AS action
FROM "Users" u JOIN user_refs r ON r.user_id = u."Id"
WHERE btrim(u."Email") = '' ORDER BY u."CreatedAt";

-- Kept account is normalised like IdentityService (Trim + lower), so login by email finds it.
UPDATE "Users" u
SET "Email" = p.new_email, "UpdatedAt" = now()
FROM email_plan p
WHERE p."Id" = u."Id" AND u."Email" <> p.new_email;

UPDATE "Users"
SET "Email" = 'no-email-' || "Id" || '@invalid.local', "UpdatedAt" = now()
WHERE btrim("Email") = '';

-- Phone is ranked after the email step so a deleted account, one without a real email, or a plus alias
-- loses the phone first.
CREATE TEMP TABLE phone_plan ON COMMIT DROP AS
SELECT u."Id", u."Email", u."Phone", u."Status", u."IsDeleted", r.refs,
       row_number() OVER (PARTITION BY u."Phone"
                          ORDER BY u."IsDeleted", (u."Email" LIKE '%@invalid.local'), (u."Email" LIKE '%+%@%'),
                                   (u."Status" = 'Active') DESC, r.refs DESC, u."CreatedAt", u."Id") AS rank
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
SELECT (SELECT count(*) FROM email_plan WHERE rank > 1 AND NOT "IsDeleted") AS accounts_aliased,
       (SELECT count(*) FROM email_plan WHERE rank > 1 AND "IsDeleted") AS deleted_accounts_renamed,
       (SELECT count(*) FROM phone_plan WHERE rank > 1) AS phones_cleared;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM "Users" GROUP BY lower(btrim("Email")) HAVING count(*) > 1)
       OR EXISTS (SELECT 1 FROM "Users" WHERE btrim("Email") = '')
       OR EXISTS (SELECT 1 FROM "Users" WHERE "Phone" <> '' GROUP BY "Phone" HAVING count(*) > 1) THEN
        RAISE EXCEPTION 'Duplicates remain after cleanup; nothing was committed. Check the CHECK section above.';
    END IF;
END $$;

\if :apply
COMMIT;
\echo 'APPLIED: changes committed.'
\else
ROLLBACK;
\echo 'DRY RUN: nothing was changed (rolled back).'
\endif
