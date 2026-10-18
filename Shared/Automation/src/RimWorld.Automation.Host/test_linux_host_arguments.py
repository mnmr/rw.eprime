import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('linux_host', Path(__file__).with_name('linux-host.py'))
host = importlib.util.module_from_spec(spec)
spec.loader.exec_module(host)

class LaunchArgumentTests(unittest.TestCase):
    def test_pickle_scenario_and_report_path_reach_the_game(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            flags = ['-pickle-run=production.feature', '-pickle-report-dir=/mnt/d/run/PickleReports', '-pickle-no-http']
            (root / 'launch.json').write_text(json.dumps({'gameArguments': flags}))
            actual = host.build_game_arguments(root, 'test-token')
            self.assertEqual(actual[-3:], flags)
            self.assertIn('-savedatafolder=' + str(root), actual)

    def test_other_game_arguments_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'launch.json').write_text(json.dumps({'gameArguments': ['-savedatafolder=/other']}))
            with self.assertRaises(ValueError):
                host.build_game_arguments(root, 'test-token')

if __name__ == '__main__':
    unittest.main()
