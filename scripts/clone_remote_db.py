#!/usr/bin/env python3
import json
import re
import sys
import requests

BASE_URL = "http://116.118.3.58:3001"
USERNAME = "admin"
PASSWORD = "123"

GEOMETRY_COLUMNS = {
    "InspectionMedia": ["CaptureLocation"],
    "Missions": ["Boundary"],
    "AssetComponents": ["Location"],
    "Regions": ["Geom"],
    "Substations": ["Geom"],
    "Towers": ["Geom"],
    "TransmissionLines": ["Geom"],
    "LineSegments": ["Geometry"],
    "UAVs": ["CurrentLocation"],
    "PreMissionAssessments": ["ProposedBoundary"],
}

# Tables to ignore (PostGIS internals or temporary hangfire tables)
IGNORE_TABLES = {"spatial_ref_sys"}


def quote_val(val, is_geom=False):
    if val is None:
        return "NULL"
    if is_geom:
        if isinstance(val, str) and val.strip():
            escaped = val.replace("'", "''")
            return f"ST_GeomFromEWKT('{escaped}')"
        return "NULL"
    if isinstance(val, bool):
        return "true" if val else "false"
    if isinstance(val, (int, float)):
        return str(val)
    if isinstance(val, (dict, list)):
        escaped = json.dumps(val).replace("'", "''")
        return f"'{escaped}'"
    val_str = str(val).replace("'", "''")
    return f"'{val_str}'"


def main():
    session = requests.Session()
    print("1. Fetching login page...")
    r = session.get(f"{BASE_URL}/login")
    r.raise_for_status()

    csrf_match = re.search(r'name="_csrf"\s+value="([^"]+)"', r.text)
    if not csrf_match:
        print("ERROR: Could not find _csrf in login page")
        sys.exit(1)
    csrf_token = csrf_match.group(1)

    print("2. Logging in...")
    login_resp = session.post(
        f"{BASE_URL}/login",
        data={"username": USERNAME, "password": PASSWORD, "_csrf": csrf_token},
        allow_redirects=False,
    )
    if login_resp.status_code not in (200, 302):
        print(f"ERROR: Login failed with status {login_resp.status_code}")
        sys.exit(1)

    print("3. Fetching table list and API CSRF...")
    tables_resp = session.get(f"{BASE_URL}/api/tables")
    tables_resp.raise_for_status()
    tables_data = tables_resp.json()
    api_csrf = tables_data.get("csrf", "")

    def run_sql(query):
        resp = session.post(
            f"{BASE_URL}/api/sql",
            headers={"Content-Type": "application/json", "X-CSRF-Token": api_csrf},
            json={"sql": query},
        )
        resp.raise_for_status()
        data = resp.json()
        if not data.get("success"):
            raise Exception(f"SQL failed: {data.get('error')}")
        results = data.get("results")
        if results and len(results) > 0:
            return results[0].get("rows", [])
        return data.get("rows", [])

    print("4. Fetching list of local public tables from docker...")
    import subprocess
    res = subprocess.run(
        ["docker", "exec", "-i", "uavpms-db", "psql", "-U", "uav_admin", "-d", "uav_pms_db", "-t", "-c",
         "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE';"],
        capture_output=True, text=True, check=True
    )
    local_tables = set(line.strip() for line in res.stdout.splitlines() if line.strip() and line.strip() != "spatial_ref_sys")

    table_query = """
    SELECT table_name 
    FROM information_schema.tables 
    WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
    ORDER BY table_name;
    """
    raw_tables = run_sql(table_query)
    all_tables = [t["table_name"] for t in raw_tables if t["table_name"] in local_tables]
    print(f"Matched {len(all_tables)} tables between remote and local.")

    out_file = "scripts/production_dump.sql"
    print(f"5. Extracting data to {out_file}...")

    with open(out_file, "w", encoding="utf-8") as f:
        f.write("-- UAV PMS Production Data Dump (from 116.118.3.58:3001)\n")
        f.write("SET session_replication_role = 'replica';\n\n")

        # First truncate existing tables in local
        for table in all_tables:
            f.write(f'TRUNCATE TABLE "{table}" CASCADE;\n')
        f.write("\n")

        for table in all_tables:
            geom_cols = set(GEOMETRY_COLUMNS.get(table, []))

            # Get columns
            col_query = f"""
            SELECT column_name, data_type, udt_name 
            FROM information_schema.columns 
            WHERE table_schema = 'public' AND table_name = '{table}'
            ORDER BY ordinal_position;
            """
            cols_info = run_sql(col_query)
            if not cols_info:
                print(f"  Skipping {table} (no columns)")
                continue

            col_names = [c["column_name"] for c in cols_info]

            select_parts = []
            for col in col_names:
                if col in geom_cols:
                    select_parts.append(f'ST_AsEWKT("{col}") AS "{col}"')
                else:
                    select_parts.append(f'"{col}"')

            # Count rows
            count_rows = run_sql(f'SELECT count(*) AS c FROM "{table}";')
            total = int(count_rows[0]["c"]) if count_rows else 0
            if total == 0:
                print(f"  {table}: 0 rows")
                continue

            print(f"  {table}: {total} rows... downloading")
            f.write(f"-- Table: {table} ({total} rows)\n")

            batch_size = 500
            for offset in range(0, total, batch_size):
                fetch_query = f'SELECT {", ".join(select_parts)} FROM "{table}" LIMIT {batch_size} OFFSET {offset};'
                rows = run_sql(fetch_query)
                if not rows:
                    break

                for row in rows:
                    col_quoted = [f'"{c}"' for c in col_names]
                    val_quoted = [quote_val(row.get(c), is_geom=(c in geom_cols)) for c in col_names]
                    stmt = f'INSERT INTO "{table}" ({", ".join(col_quoted)}) VALUES ({", ".join(val_quoted)});\n'
                    f.write(stmt)

            f.write("\n")

        f.write("SET session_replication_role = 'origin';\n")
        f.write("-- Complete dump\n")

    print(f"\nDone! Saved dump to {out_file}.")


if __name__ == "__main__":
    main()
