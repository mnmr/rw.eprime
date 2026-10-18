"""Shared WSL backend. Owns an authenticated Xvfb display and child sessions.

The Windows host retains the managed run lock and feeds this supervisor a
heartbeat. EOF or a missed heartbeat tears down both child sessions. Commands
use the runtime's process/token-scoped Mono named pipe (an AF_UNIX socket).
"""
import base64
import ctypes
import fcntl
import json
import os
from pathlib import Path
import re
import select
import signal
import socket
import struct
import subprocess
import sys
import tempfile
import time

RUNS_ROOT = Path('/mnt/d/Code/RimWorld/AutomationProfiles/Shared/Runs')


def build_game_arguments(root, token):
    arguments = [str(root / 'Game/RimWorldLinux'), '-savedatafolder=' + str(root),
                 '-automationtoken=' + token, '-logFile', str(root / 'Player.log'),
                 '-screen-fullscreen', '0', '-screen-width', '1920', '-screen-height', '1080']
    launch = json.loads((root / 'launch.json').read_text(encoding='utf-8-sig'))
    for argument in launch.get('gameArguments', []):
        if not isinstance(argument, str) or not re.fullmatch(r'-pickle-[a-z-]+(=[^\s"]*)?', argument):
            raise ValueError('Unsupported game argument: ' + repr(argument))
        arguments.append(argument)
    return arguments

class ChildSession:
    def __init__(self, arguments, **kwargs):
        parent = os.getpid()
        def isolate():
            os.setsid()
            if ctypes.CDLL(None).prctl(1, signal.SIGKILL, 0, 0, 0) != 0 or os.getppid() != parent:
                os._exit(1)
        self.process = subprocess.Popen(arguments, preexec_fn=isolate, **kwargs)
        self.pid = self.process.pid
        self.pidfd = os.pidfd_open(self.pid)
        self.closed = False

    def exited(self):
        # Do not reap here: retaining the leader, even as a zombie, prevents its
        # process-group identity being reused until all owned children are killed.
        return self.closed or bool(select.select([self.pidfd], [], [], 0)[0])

    def close(self):
        if self.closed:
            return
        try:
            os.killpg(self.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        self.process.wait()
        os.close(self.pidfd)
        self.closed = True

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()


def read_identity(pid):
    try:
        root = Path('/proc') / str(int(pid))
        stat = (root / 'stat').read_text().rsplit(')', 1)[1].split()
        return {'pid': int(pid), 'start': stat[19], 'executable': os.readlink(root / 'exe'),
                'arguments': (root / 'cmdline').read_bytes().rstrip(b'\0').decode().split('\0')}
    except (FileNotFoundError, ProcessLookupError):
        return None


def matches_identity(identity, executable, profile, token, start):
    return bool(identity and identity['start'] == start and identity['executable'] == executable
                and '-savedatafolder=' + profile in identity['arguments']
                and '-automationtoken=' + token in identity['arguments'])


def read_exact(connection, count):
    data = bytearray()
    while len(data) < count:
        part = connection.recv(count - len(data))
        if not part:
            raise EOFError('Automation pipe closed before its reply completed.')
        data.extend(part)
    return bytes(data)


def pipe_request(path, command, pid):
    with socket.socket(socket.AF_UNIX) as connection:
        connection.settimeout(35)
        connection.connect(path)
        data = json.dumps(command).encode()
        if len(data) > 65536:
            raise ValueError('Automation request exceeds 64 KiB.')
        connection.sendall(struct.pack('<I', len(data)) + data)
        length = struct.unpack('<I', read_exact(connection, 4))[0]
        if not 2 <= length <= 67108864:
            raise ValueError('Invalid automation reply length.')
        reply = json.loads(read_exact(connection, length))
        if reply.get('processId') != pid:
            raise ValueError('Automation reply process identity mismatch.')
        if not reply.get('ok'):
            raise ValueError('Game automation failed: ' + str(reply.get('error')))
        return reply


def write_json(path, value):
    temporary = str(path) + '.' + str(os.getpid()) + '.tmp'
    Path(temporary).write_text(json.dumps(value))
    os.replace(temporary, path)


def claim_removal(root, owner):
    with (root / 'linux.lock').open('a') as lease:
        fcntl.flock(lease, fcntl.LOCK_EX | fcntl.LOCK_NB)
        # Publish while retaining the Linux lease: a late supervisor must see
        # the tombstone after it acquires the same lease and before it launches.
        write_json(root / 'removing.json', {'owner': owner, 'utc': time.time()})


def lease_is_idle(root):
    with (root / 'linux.lock').open('a') as lease:
        try:
            fcntl.flock(lease, fcntl.LOCK_EX | fcntl.LOCK_NB)
            return True
        except BlockingIOError:
            return False


def validate(profile, token, owner, allow_removing=False):
    if not re.fullmatch(r'rimworld-shared-[0-9a-f]{32}', token):
        raise ValueError('Invalid managed token.')
    if profile != str(RUNS_ROOT / token[16:]):
        raise ValueError('Invalid managed profile.')
    root = Path(profile)
    if root.resolve() != root or any(p.is_symlink() for p in [root, *root.parents]):
        raise ValueError('Managed profile ancestors cannot be links.')
    record = json.loads((root / 'run.json').read_text(encoding='utf-8-sig'))
    if record['runId'] != token[16:] or record['owner'] != owner or record.get('backend') != 'wsl':
        raise ValueError('Run ownership/backend mismatch.')
    if not allow_removing and ((root / 'removing.json').exists() or (root.parent / ('.removing-' + root.name + '.json')).exists()):
        raise ValueError('Run is being removed.')
    return root


def active_game(root, token):
    path = root / 'linux-host.json'
    if not path.exists():
        return None
    state = json.loads(path.read_text())
    if state['token'] != token:
        raise ValueError('Linux host token mismatch.')
    identity = read_identity(state['gameProcessId'])
    if matches_identity(identity, str(root / 'Game/RimWorldLinux'), str(root), token, state['gameStart']):
        return identity
    return None


def supervise(root, token, owner):
    # A separate Linux lease rejects duplicate supervisors. The Windows host
    # simultaneously owns run.lock, which Windows discovery/removal understand.
    with (root / 'linux.lock').open('a') as lease:
        fcntl.flock(lease, fcntl.LOCK_EX | fcntl.LOCK_NB)
        if (root / 'linux-started.json').exists():
            raise ValueError('Linux run is single-use.')
        validate(str(root), token, owner)
        write_json(root / 'linux-started.json', {'token': token})
        with tempfile.TemporaryDirectory(prefix='rimworld-') as private:
            display_root = Path(private) / token
            display_root.mkdir(mode=0o700)
            authority = display_root / 'Xauthority'
            cookie = os.urandom(16)
            # FamilyWild auth records allow the allocated private display number
            # to be chosen by Xvfb. No TCP listener and no unauthenticated access.
            fields = [b'', b'', b'MIT-MAGIC-COOKIE-1', cookie]
            authority.write_bytes(struct.pack('>H', 65535) + b''.join(struct.pack('>H', len(x)) + x for x in fields))
            authority.chmod(0o600)
            read_fd, write_fd = os.pipe()
            with (root / 'Xvfb.log').open('wb') as display_log:
                with ChildSession(['Xvfb', '-displayfd', str(write_fd), '-screen', '0', '1920x1080x24',
                                   '-nolisten', 'tcp', '-nolisten', 'unix', '-auth', str(authority)], pass_fds=(write_fd,),
                                  stdin=subprocess.DEVNULL, stdout=display_log, stderr=display_log):
                    os.close(write_fd)
                    try:
                        if not select.select([read_fd], [], [], 10)[0]:
                            raise TimeoutError('Private X display did not start.')
                        display = ':' + os.read(read_fd, 32).decode().strip()
                    finally:
                        os.close(read_fd)
                    if not re.fullmatch(r':[0-9]+', display):
                        raise ValueError('Xvfb returned an invalid display.')
                    environment = dict(os.environ, DISPLAY=display, XAUTHORITY=str(authority),
                                       RIMWORLD_AUTOMATION_DISPLAY=display, RIMWORLD_AUTOMATION_TOKEN=token,
                                       XDG_RUNTIME_DIR=str(display_root), TMPDIR='/tmp',
                                       XDG_CONFIG_HOME=str(root / 'NativeConfig'), XDG_DATA_HOME=str(root / 'NativeData'),
                                       XDG_CACHE_HOME=str(root / 'NativeCache'))
                    renderer = json.loads((root / 'run.json').read_text(encoding='utf-8-sig')).get('wslRenderer', 'Default')
                    environment.pop('LIBGL_ALWAYS_SOFTWARE', None)
                    environment.pop('MESA_D3D12_DEFAULT_ADAPTER_NAME', None)
                    if renderer == 'Software':
                        environment['LIBGL_ALWAYS_SOFTWARE'] = '1'
                    elif renderer == 'NVIDIA':
                        environment['MESA_D3D12_DEFAULT_ADAPTER_NAME'] = 'NVIDIA'
                    environment.pop('WAYLAND_DISPLAY', None)
                    environment.pop('PULSE_SERVER', None)
                    arguments = build_game_arguments(root, token)
                    with (root / 'linux-console.log').open('wb') as game_log:
                        with ChildSession(arguments, cwd=root / 'Game', env=environment,
                                          stdin=subprocess.DEVNULL, stdout=game_log, stderr=game_log) as game:
                            identity = read_identity(game.pid)
                            write_json(root / 'linux-host.json', {'token': token, 'owner': owner, 'gameProcessId': game.pid,
                                       'gameStart': identity['start'], 'display': display, 'renderer': renderer})
                            last_heartbeat = time.monotonic()
                            while not game.exited():
                                if select.select([sys.stdin], [], [], 1)[0]:
                                    if not os.read(sys.stdin.fileno(), 4096):
                                        raise RuntimeError('Windows host disconnected.')
                                    last_heartbeat = time.monotonic()
                                if time.monotonic() - last_heartbeat > 15:
                                    raise TimeoutError('Windows host heartbeat expired.')
                            # Preserve the child's result while still retaining
                            # its process-group leader for deterministic teardown.
                            status = os.waitid(os.P_PID, game.pid, os.WEXITED | os.WNOWAIT)
                            return status.si_status


def main():
    command, profile, token, owner = sys.argv[1:5]
    root = validate(profile, token, owner, allow_removing=command == 'remove')
    if command == 'run':
        return supervise(root, token, owner)
    if command == 'idle':
        print(json.dumps(lease_is_idle(root)))
        return 0
    identity = active_game(root, token)
    if command == 'remove':
        if identity is not None:
            raise ValueError('Stop the Linux game before removing its files.')
        claim_removal(root, owner)
        return 0
    if command == 'inspect':
        print(json.dumps(identity))
        return 0
    if identity is None:
        if command == 'stop':
            return 0
        raise ValueError('The exact Linux game process is no longer running.')
    with os.fdopen(os.pidfd_open(identity['pid']), 'rb', closefd=True) as pidfd:
        if active_game(root, token) != identity:
            raise ValueError('Linux game identity changed before control.')
        if command == 'stop':
            signal.pidfd_send_signal(pidfd.fileno(), signal.SIGTERM)
            if not select.select([pidfd], [], [], 10)[0]:
                signal.pidfd_send_signal(pidfd.fileno(), signal.SIGKILL)
            return 0
        if command != 'request':
            raise ValueError('Unsupported Linux host command.')
        request = json.loads(base64.b64decode(sys.argv[5]))
        pipe = '/tmp/CoreFxPipe_rimworld-automation-' + str(identity['pid']) + '-' + token
        print(json.dumps(pipe_request(pipe, request, identity['pid'])))
        return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except Exception as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
