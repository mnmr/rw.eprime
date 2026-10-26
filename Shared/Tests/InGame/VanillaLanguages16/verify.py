"""Verify the owned, completed native-language evidence and copied probe bytes."""
import hashlib
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

root = Path(sys.argv[1] if len(sys.argv) > 1 else
            'WorkRoles/temp/vanilla-language-audit-2026-10-26/1.6')
locales = ['English', 'Danish', 'German', 'French', 'Spanish', 'Italian',
           'PortugueseBrazilian', 'Dutch', 'Swedish', 'Finnish', 'Norwegian',
           'Portuguese', 'SpanishLatin', 'ChineseSimplified', 'ChineseTraditional']
expected = ['brrainz.harmony', 'ludeon.rimworld', 'eprime.sharedautomation',
            'ludeon.rimworld.royalty', 'ludeon.rimworld.ideology',
            'ludeon.rimworld.biotech', 'ludeon.rimworld.anomaly',
            'ludeon.rimworld.odyssey', 'rimshared.vanillalanguages16.probe']
probe = Path(__file__).parent / 'mod/1.6/Assemblies/Probe.dll'
probe_hash = hashlib.sha256(probe.read_bytes()).hexdigest()
native = json.loads((root / 'native-content-manifest.json').read_text(encoding='utf-8-sig'))
game_root = Path(native['gameRoot'])
assembly_hash = hashlib.sha256((game_root / 'RimWorldLinux_Data/Managed/Assembly-CSharp.dll').read_bytes()).hexdigest()
assert assembly_hash == native['assemblySha256'].lower()
for item in native['files']:
    if item.get('missing'):
        continue
    assert hashlib.sha256((game_root / item['path']).read_bytes()).hexdigest() == item['sha256'].lower()
results = []
for locale in locales:
    folder = root / locale
    audit = ET.parse(folder / 'VanillaLanguageAudit/audit.xml').getroot()
    assert audit.get('status') == 'complete' and audit.get('language') == locale
    assert audit.get('playDataLoaded') == 'True'
    assert audit.get('captureBoundary') == 'appended final post-load callback after native injection and static constructors'
    assert all(section.get('status') == 'ok' for section in audit.findall('section'))
    assert len(audit.findall('section')) == 14
    assert [mod.get('packageId') for mod in audit.findall('mod')] == expected
    report = (folder / 'VanillaLanguageAudit/TranslationReport.txt').read_text(encoding='utf-8-sig')
    total = sum(int(re.search(r'^=+ ' + title + r' \((\d+)\)', report, re.MULTILINE).group(1))
                for title in ('General load errors', 'Def-injected translations load errors'))
    log = (folder / 'Player.log').read_text(encoding='utf-8-sig', errors='replace')
    warnings = [int(number) for number in re.findall(r'Translation data for language .*? has (\d+) errors\.', log)]
    assert warnings == ([total] if total else [])
    assert audit.get('activeAnyError') == str(total > 0)
    run = json.loads((folder / 'run.json').read_text(encoding='utf-8-sig'))
    launch = json.loads((folder / 'launch.json').read_text(encoding='utf-8-sig'))
    host = json.loads((folder / 'host.json').read_text(encoding='utf-8-sig'))
    assert host['state'] == 'stopped' and host['exitCode'] == 0
    assert launch['activeMods'] == expected
    assert launch['gameArguments'] == ['-pickle-no-http']
    assert launch['saveSha256'] == run['configuration']['sha256']
    copied = next(item for item in run['snapshots']['mods']['mod']['files']
                  if item['path'].replace('\\', '/') == '1.6/Assemblies/Probe.dll')
    assert copied['sha256'].lower() == probe_hash
    assert not Path(run['profile']).exists(), f"Owned run remains: {run['profile']}"
    results.append({'locale': locale, 'runId': run['runId'], 'engineVersion': audit.get('version'),
                    'startupWarningCount': total, 'nativeSectionsCompleted': 14,
                    'hostExitCode': host['exitCode'], 'copiedProbeSha256': probe_hash,
                    'removed': True})
output = {'passed': True, 'locales': len(results), 'allOfficialDlcActive': True,
          'zeroLocalizationProbe': True, 'noSaveLoaded': True,
          'nativeContentHashesUnchanged': True, 'versionFile': native['engineVersion'],
          'nativeAssemblySha256': assembly_hash, 'results': results}
(root / 'verification.json').write_text(json.dumps(output, indent=2), encoding='utf-8')
print(f'Verified {len(results)} native language reports, warning counts, copied probe hashes, clean exits, and run removal.')
