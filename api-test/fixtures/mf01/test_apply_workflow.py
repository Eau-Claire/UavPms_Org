import os
from pathlib import Path
import subprocess
import tempfile
import unittest

APPLY = Path(__file__).parents[2] / 'scripts/apply_mf01_mock.sh'


class ApplyWorkflowTests(unittest.TestCase):
    def run_script(self, database, sql_failure=False):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            log = root / 'calls'
            docker = root / 'docker'
            docker.write_text('''#!/usr/bin/env bash
printf '%s\\n' "$*" >> "$MOCK_LOG"
if [[ "$1" == inspect ]]; then echo running; exit 0; fi
if [[ "$2" == -i ]]; then cat >/dev/null; exit "${MOCK_SQL_EXIT:-0}"; fi
exit 0
''')
            docker.chmod(0o700)
            seed = root / 'seed.sql'
            seed.write_text('BEGIN; SELECT 1; COMMIT;')
            result = subprocess.run(['bash', str(APPLY), database, str(seed)], env={**os.environ, 'PATH':temp+os.pathsep+os.environ['PATH'], 'MOCK_LOG':str(log), 'MOCK_SQL_EXIT':'1' if sql_failure else '0'}, capture_output=True, text=True)
            return result, log.read_text() if log.exists() else ''

    def test_rejects_application_db_before_docker(self):
        for database in ('uavpms', 'mf01Xtest', 'mf01_test;echo unsafe', ''):
            result, log = self.run_script(database)
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(log, '')

    def test_uses_explicit_test_database(self):
        result, log = self.run_script('mf01_test_region')
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('-d mf01_test_region', log)
        self.assertIn('ON_ERROR_STOP=1', log)
        self.assertIn('transaction committed', result.stdout)

    def test_sql_failure_does_not_report_success(self):
        result, _ = self.run_script('mf01_test', sql_failure=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertNotIn('transaction committed', result.stdout)
