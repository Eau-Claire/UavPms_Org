"""Generate deterministic MF01 SQL/manifest; never connects to a database."""
import argparse
import json
from pathlib import Path
from uuid import NAMESPACE_URL, uuid5


def uid(key):
    return str(uuid5(NAMESPACE_URL, 'uavpms:mf01:mock:' + key))


def quote(value):
    if isinstance(value, bool):
        return 'true' if value else 'false'
    if isinstance(value, (int, float)):
        return str(value)
    return "'" + str(value).replace("'", "''") + "'"


def generate(towers_per_line=30):
    # ERD v2.2: infrastructure is only Regions -> Towers. A "corridor" is a synthetic
    # grouping of towers used to build missions; it is not stored in the database.
    if not 2 <= towers_per_line <= 500:
        raise ValueError('towers_per_line must be between 2 and 500')
    sql = ["\\set ON_ERROR_STOP on", "BEGIN;", """DO $$ BEGIN
IF current_database() !~ '^mf01_test[a-zA-Z0-9_]*$' THEN
  RAISE EXCEPTION 'MF01 fixtures require a disposable database named mf01_test*';
END IF;
END $$;"""]
    manifest = {'synthetic': True, 'regions': [], 'users': [], 'missions': [], 'counts': {}}

    inserted_ids = {}

    def insert(table, key, values, geometry=None, now_columns=()):
        ident = uid(key)
        values = {'Id': ident, **values, 'IsDeleted': False}
        columns = list(values) + ['CreatedAt'] + list(now_columns)
        expressions = [quote(v) for v in values.values()] + ['now()'] * (1 + len(now_columns))
        if geometry:
            column, wkt = geometry
            columns.append(column)
            expressions.append(f'ST_GeomFromText({quote(wkt)},4326)')
        sql.append('INSERT INTO "' + table + '" (' + ','.join('"'+c+'"' for c in columns) + ') VALUES (' + ','.join(expressions) + ') ON CONFLICT ("Id") DO NOTHING;')
        inserted_ids.setdefault(table, []).append(ident)
        manifest['counts'][table] = manifest['counts'].get(table, 0) + 1
        return ident

    def user(key, role):
        # Supplied through psql, never embedded in generated files.
        ident = uid(key)
        email = key.lower() + '@mf01.example.test'
        sql.append(f'''INSERT INTO "Users" ("Id","Email","PasswordHash","FullName","Phone","Status","IsEmailVerified","CreatedAt","IsDeleted")
VALUES ('{ident}','{email}',crypt(:'mf01_password',gen_salt('bf',10)),'MOCK {key}','', 'Active',true,now(),false) ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "UserRoles" ("UserId","RoleId","AssignedAt") SELECT '{ident}',"Id",now() FROM "Roles" WHERE "RoleName"='{role}' ON CONFLICT DO NOTHING;''')
        inserted_ids.setdefault('Users', []).append(ident)
        manifest['users'].append({'id': ident, 'email': email, 'role': role})
        return ident

    sql.append('CREATE EXTENSION IF NOT EXISTS pgcrypto;')
    sql.append('''DO $$ BEGIN IF (SELECT count(DISTINCT "RoleName") FROM "Roles" WHERE "RoleName" IN ('SystemAdmin','Manager','Inspector')) <> 3 THEN RAISE EXCEPTION 'Apply application migrations and seed roles first'; END IF; END $$;''')
    user('MF01-ADMIN', 'SystemAdmin')
    user('MF01-MANAGER-NO-SCOPE', 'Manager')
    user('MF01-INSPECTOR-UNASSIGNED', 'Inspector')
    # Synthetic EVNSPC-style operating areas; never seed generic national zones.
    for region_code, longitude, latitude in [('NINHTHUAN',108.9,11.5), ('DONGNAI',107.1,10.9), ('CAMAU',105.0,9.1)]:
        key = 'MF01-' + region_code
        ring = f'{longitude} {latitude},{longitude+.3} {latitude},{longitude+.3} {latitude+.3},{longitude} {latitude+.3},{longitude} {latitude}'
        region = insert('Regions',key,{'Code':key,'Name':'MOCK '+region_code,'Type':'Region'},('Geom',f'POLYGON(({ring}))'))
        manager = user(key+'-MANAGER','Manager')
        inspectors = [user(key+f'-INSPECTOR-{i}','Inspector') for i in range(1,4)]
        insert('UserGeographicScopes',key+'-SCOPE',{'UserId':manager,'RegionId':region})
        info = {'id':region,'code':key,'managerId':manager,'inspectors':inspectors,'corridors':[], 'polygon':{'type':'Polygon','coordinates':[[[longitude,latitude],[longitude+.3,latitude],[longitude+.3,latitude+.3],[longitude,latitude+.3],[longitude,latitude]]]}}
        manifest['regions'].append(info)
        for group_no in range(1,7):
            for corridor_no in range(1,4):
                corridor_key = key+f'-G{group_no}-C{corridor_no}'
                x,y = round(longitude+.01*corridor_no,6),round(latitude+.03*group_no,6)
                points = [(round(x+.15*i/(towers_per_line-1),6),round(y+.005*i/(towers_per_line-1),6)) for i in range(towers_per_line)]
                tower_ids = []
                for index,(a,b) in enumerate(points):
                    tower_key = corridor_key+f'-T{index+1:03}'
                    tower_ids.append(insert('Towers',tower_key,{'RegionId':region,'TowerCode':tower_key,'PoleType':'Tension' if index%2 else 'Suspension','Address':'MOCK '+region_code},('Geom',f'POINT({a} {b})')))
                # The last tower of each corridor is never a mission target.
                info['corridors'].append({'code':corridor_key,'towerIds':tower_ids,'unassignedTowerId':tower_ids[-1]})
                drone = insert('UAVs',corridor_key+'-UAV',{'UavCode':corridor_key+'-UAV','Name':'MOCK '+corridor_key+'-UAV','Model':'MOCK','SerialNumber':corridor_key+'-UAV','Category':'Hexacopter','AssignedRegionId':region,'FlightStatus':'Idle','OperationalStatus':'Available','TechnicalHealth':'Healthy','BatteryLevel':100,'FlightEnduranceMinutes':30,'OperationalRangeKm':10,'Version':1})
                status = ['Pending','Executing','Completed','Cancelled'][(group_no*3+corridor_no)%4]
                inspector = inspectors[(corridor_no-1)%3]
                mission = insert('Missions',corridor_key+'-MISSION',{'MissionCode':corridor_key+'-MISSION','Title':'MOCK '+corridor_key,'ManagerId':manager,'RegionId':region,'UavId':drone,'Status':status,'MissionType':'AdHoc','Description':'Synthetic MF01 regional fixture'})
                # Missions have no InspectorId column in ERD v2.2; the inspector is an assignment.
                insert('MissionAssignments',corridor_key+'-ASSIGNMENT',{'MissionId':mission,'UserId':inspector,'AssignmentRole':'INSPECTOR','Status':'Active','ResponseStatus':'Pending','IsRequired':True,'AssignedById':manager},now_columns=('AssignedAt',))
                targets = tower_ids[:min(10,len(tower_ids)-1)]
                for index,tower in enumerate(targets):
                    insert('MissionTargets',corridor_key+f'-TARGET-{index}',{'MissionId':mission,'TowerId':tower,'Sequence':index+1,'InspectionStatus':'Pending'})
                manifest['missions'].append({'id':mission,'regionId':region,'inspectorId':inspector,'status':status,'targetTowerIds':targets})
    manifest['cases'] = {
        'mixedRegionTowerIds':[r['corridors'][0]['towerIds'][0] for r in manifest['regions']],
        'emptyPolygon':{'type':'Polygon','coordinates':[[[109,12],[109.01,12],[109.01,12.01],[109,12.01],[109,12]]]},
        'crossRegionPolygon':{'type':'Polygon','coordinates':[[[105,10],[109,10],[109,22],[105,22],[105,10]]]},
        'invalidPolygon':{'type':'Polygon','coordinates':[[[105,10],[106,11],[106,10],[105,11],[105,10]]]}}
    # Verify expected IDs before committing, including reused rows on rerun.
    for table, ids in inserted_ids.items():
        id_list = ','.join(quote(ident) for ident in ids)
        sql.append(f"""DO $$ BEGIN
IF (SELECT count(*) FROM \"{table}\" WHERE \"Id\" IN ({id_list}) AND NOT \"IsDeleted\") <> {len(ids)} THEN
RAISE EXCEPTION 'MF01 verification failed: {table}';
END IF; END $$;""")
    sql.append('COMMIT;')
    return '\n'.join(sql)+'\n',manifest


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--towers-per-line',type=int,default=30)
    args=parser.parse_args()
    sql,manifest=generate(args.towers_per_line)
    args.output.mkdir(parents=True,exist_ok=True)
    (args.output/'seed.sql').write_text(sql)
    (args.output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print(json.dumps(manifest['counts'],indent=2))


if __name__=='__main__':
    main()
