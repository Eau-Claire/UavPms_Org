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
        self.assertEqual(data['counts']['AssetComponents'], 1620)
        self.assertEqual(data['counts']['MissionTargets'], 540)
        self.assertEqual(len(data['users']), 15)
        self.assertIn("current_database() !~ '^mf01_test[a-zA-Z0-9_]*$'", sql)

    def test_regions_and_mission_targets_are_isolated(self):
        _, data = module.generate()
        seen = set()
        for region in data['regions']:
            assets = {asset for line in region['lines'] for asset in line['assetIds']}
            self.assertFalse(seen & assets)
            seen |= assets
            inactive = {line['inactiveAssetId'] for line in region['lines']}
            for mission in data['missions']:
                if mission['regionId'] != region['id']:
                    continue
                self.assertIn(mission['inspectorId'], region['inspectors'])
                self.assertTrue(set(mission['targetAssetIds']) <= assets)
                self.assertFalse(set(mission['targetAssetIds']) & inactive)
                self.assertEqual(len(mission['targetAssetIds']),len(set(mission['targetAssetIds'])))
        self.assertEqual(len(seen), 1620)
        self.assertEqual(len(set(data['cases']['mixedRegionAssetIds'])), 3)

    def test_small_dataset_excludes_inactive_targets(self):
        _, data = module.generate(2)
        self.assertEqual(data['counts']['Towers'],108)
        self.assertTrue(all(len(m['targetAssetIds']) == 1 for m in data['missions']))

    def test_volume_bounds(self):
        for count in (0,1,501):
            with self.assertRaises(ValueError):
                module.generate(count)


if __name__ == '__main__':
    unittest.main()
