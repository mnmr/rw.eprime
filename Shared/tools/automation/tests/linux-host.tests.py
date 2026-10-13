"""Real child and socket tests: ownership must hold before a Unity runtime exists."""
import importlib.util
import json
import os
from pathlib import Path
import socket
import struct
import subprocess
import sys
import tempfile
import threading
import unittest
import uuid

source = Path(__file__).parents[3] / 'Automation/src/RimWorld.Automation.Host/linux-host.py'
spec = importlib.util.spec_from_file_location('linux_host', source)
host = importlib.util.module_from_spec(spec)
spec.loader.exec_module(host)


class LinuxHostTests(unittest.TestCase):
    def test_removal_tombstone_allows_only_owned_cleanup_retry(self):
        run_id = uuid.uuid4().hex
        root = host.RUNS_ROOT / run_id
        root.mkdir()
        try:
            (root / 'run.json').write_text(json.dumps({'runId': run_id, 'owner': 'cleanup-test', 'backend': 'wsl'}))
            token = 'rimworld-shared-' + run_id
            host.claim_removal(root, 'cleanup-test')
            with self.assertRaises(ValueError):
                host.validate(str(root), token, 'cleanup-test')
            self.assertEqual(root, host.validate(str(root), token, 'cleanup-test', allow_removing=True))
            with self.assertRaises(ValueError):
                host.validate(str(root), token, 'foreign-owner', allow_removing=True)
            host.claim_removal(root, 'cleanup-test')
        finally:
            for child in root.iterdir():
                child.unlink()
            root.rmdir()

    def test_live_linux_lease_blocks_removal_until_teardown(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            import fcntl
            with (root / 'linux.lock').open('a') as lease:
                fcntl.flock(lease, fcntl.LOCK_EX | fcntl.LOCK_NB)
                self.assertFalse(host.lease_is_idle(root))
                with self.assertRaises(BlockingIOError):
                    host.claim_removal(root, 'owned-test')
                self.assertFalse((root / 'removing.json').exists())
            self.assertTrue(host.lease_is_idle(root))
            host.claim_removal(root, 'owned-test')
            self.assertEqual('owned-test', json.loads((root / 'removing.json').read_text())['owner'])

    def test_owned_child_session_is_stopped_without_touching_neighbor(self):
        with host.ChildSession(['/bin/sleep', '120']) as neighbor:
            with host.ChildSession(['/bin/sh', '-c', 'sleep 120 & wait']) as owned:
                self.assertFalse(owned.exited())
            self.assertTrue(owned.exited())
            self.assertFalse(neighbor.exited())

    def test_process_identity_requires_exact_start_profile_and_token(self):
        profile = '/mnt/d/Code/RimWorld/AutomationProfiles/Shared/Runs/' + 'a' * 32
        token = 'rimworld-shared-' + 'a' * 32
        with host.ChildSession([sys.executable, '-c', 'import time; time.sleep(120)',
                                '-savedatafolder=' + profile, '-automationtoken=' + token]) as child:
            identity = host.read_identity(child.pid)
            self.assertTrue(host.matches_identity(identity, identity['executable'], profile, token, identity['start']))
            self.assertFalse(host.matches_identity(identity, identity['executable'], profile + '-other', token, identity['start']))
            self.assertFalse(host.matches_identity(identity, identity['executable'], profile, token + '-other', identity['start']))
            self.assertFalse(host.matches_identity(identity, identity['executable'], profile, token, 'stale'))

    def test_pipe_preserves_framing_and_rejects_foreign_reply(self):
        for reply_pid in (123, 456):
            with tempfile.TemporaryDirectory() as directory:
                path = directory + '/pipe'
                listener = socket.socket(socket.AF_UNIX)
                listener.bind(path)
                listener.listen(1)
                def serve():
                    with listener.accept()[0] as connection:
                        length = struct.unpack('<I', host.read_exact(connection, 4))[0]
                        request = json.loads(host.read_exact(connection, length))
                        reply = json.dumps({'ok': True, 'processId': reply_pid, 'text': request['text']}).encode()
                        connection.sendall(struct.pack('<I', len(reply)) + reply)
                thread = threading.Thread(target=serve)
                thread.start()
                try:
                    if reply_pid == 123:
                        self.assertEqual('Försök ✓', host.pipe_request(path, {'text': 'Försök ✓'}, 123)['text'])
                    else:
                        with self.assertRaises(ValueError):
                            host.pipe_request(path, {'text': 'x'}, 123)
                finally:
                    thread.join()
                    listener.close()

    def test_owned_error_reply_preserves_the_runtime_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            path = directory + '/pipe'
            with socket.socket(socket.AF_UNIX) as listener:
                listener.bind(path)
                listener.listen(1)
                def serve():
                    with listener.accept()[0] as connection:
                        length = struct.unpack('<I', host.read_exact(connection, 4))[0]
                        host.read_exact(connection, length)
                        reply = json.dumps({'ok': False, 'processId': 123, 'error': 'Unknown automation command.'}).encode()
                        connection.sendall(struct.pack('<I', len(reply)) + reply)
                thread = threading.Thread(target=serve)
                thread.start()
                try:
                    with self.assertRaisesRegex(ValueError, 'Game automation failed: Unknown automation command.'):
                        host.pipe_request(path, {'command': 'invalid'}, 123)
                finally:
                    thread.join()


if __name__ == '__main__':
    unittest.main()
