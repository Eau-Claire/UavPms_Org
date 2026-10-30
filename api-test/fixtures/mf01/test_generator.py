"""Offline integrity checks; run with python -m unittest discover -s api-test/fixtures/mf01."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('mf01_mock', Path(__file__).parents[2] / 'scripts/generate_mf01_mock.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class MockDatasetTests(unittest.TestCase):
    def test_default_volume_and_repeatability(self):
        sql, data = module.generate()
        self.assertEqual((sql, data), module.generate())
        self.assertEqual(data['counts']['Towers'], 1620)
        self.assertEqual(data['counts']['MissionTargets'], 540)
        self.assertEqual(data['counts']['MissionAssignments'], 54)
        self.assertEqual(len(data['users']), 15)
        self.assertIn("current_database() !~ '^mf01_test[a-zA-Z0-9_]*$'", sql)

    def test_only_erd_v2_2_tables_are_seeded(self):
        sql, data = module.generate(2)
        for removed in ('AssetComponents', 'Substations', 'TransmissionLines', 'LineSegments', 'ManagementUnits'):
            self.assertNotIn(removed, data['counts'])
            self.assertNotIn('"' + removed + '"', sql)
        for removed_column in ('"AssetId"', '"InspectorId"', '"LineAssetId"', '"RegionName"', '"Username"'):
            self.assertNotIn(removed_column, sql)

    def test_regions_and_mission_targets_are_isolated(self):
        _, data = module.generate()
        seen = set()
        for region in data['regions']:
            towers = {tower for corridor in region['corridors'] for tower in corridor['towerIds']}
            self.assertFalse(seen & towers)
            seen |= towers
            unassigned = {corridor['unassignedTowerId'] for corridor in region['corridors']}
            for mission in data['missions']:
                if mission['regionId'] != region['id']:
                    continue
                self.assertIn(mission['inspectorId'], region['inspectors'])
                self.assertTrue(set(mission['targetTowerIds']) <= towers)
                self.assertFalse(set(mission['targetTowerIds']) & unassigned)
                self.assertEqual(len(mission['targetTowerIds']),len(set(mission['targetTowerIds'])))
        self.assertEqual(len(seen), 1620)
        self.assertEqual(len(set(data['cases']['mixedRegionTowerIds'])), 3)

    def test_small_dataset_leaves_one_unassigned_tower_per_corridor(self):
        _, data = module.generate(2)
        self.assertEqual(data['counts']['Towers'],108)
        self.assertTrue(all(len(m['targetTowerIds']) == 1 for m in data['missions']))

    def test_volume_bounds(self):
        for count in (0,1,501):
            with self.assertRaises(ValueError):
                module.generate(count)


if __name__ == '__main__':
    unittest.main()
