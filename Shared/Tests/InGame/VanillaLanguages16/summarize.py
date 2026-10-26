"""Summarize native, completed report output without reimplementing diagnostics."""
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
rows = []
for locale in locales:
    directory = root / locale
    audit_path = directory / 'VanillaLanguageAudit/audit.xml'
    if not audit_path.exists():
        continue
    audit = ET.parse(audit_path).getroot()
    if audit.get('status') != 'complete' or audit.get('language') != locale:
        raise RuntimeError(f'Invalid native report: {audit_path}')
    sections = {}
    for section in audit.findall('section'):
        method = section.get('method')
        if section.get('status') != 'ok':
            raise RuntimeError(f'Native report method failed: {locale} {method}')
        content = (audit_path.parent / (method + '.txt')).read_text(encoding='utf-8-sig')
        match = re.search(r'^=+.*\((\d+)\) =+', content, re.MULTILINE)
        sections[method] = int(match.group(1)) if match else 0
    log = (directory / 'Player.log').read_text(encoding='utf-8-sig', errors='replace')
    warning = re.findall(r'Translation data for language (.*?) has (\d+) errors\.', log)
    general = (audit_path.parent / 'AppendGeneralLoadErrors.txt').read_text(encoding='utf-8-sig')
    injection = (audit_path.parent / 'AppendDefInjectionsLoadErros.txt').read_text(encoding='utf-8-sig')
    config_errors = re.findall(r'^Config error in .*$', log, re.MULTILINE)
    fatal = re.findall(r'^(?:Exception while loading|Crash!!!|Could not execute post-long-event action|Critical error while injecting translations|Could not find (?:game|world) XML node).*$', log, re.MULTILINE)
    rows.append({
        'locale': locale, 'engineVersion': audit.get('version'),
        'activeAnyError': audit.get('activeAnyError'), 'programState': audit.get('programState'),
        'activeKeyed': int(audit.get('activeKeyed')), 'englishKeyed': int(audit.get('englishKeyed')),
        'sections': sections, 'startupTranslationWarnings': [{'language': name, 'count': int(count)} for name, count in warning],
        'nativeConfigErrorCount': len(config_errors), 'nativeConfigErrors': config_errors,
        'xmlParseExceptions': len(re.findall(r'^Exception loading from translation file.*XmlException:', general, re.MULTILINE)),
        'injectionCategories': {
            'missingOrRemovedDefs': len(re.findall(r'^Found no ', injection, re.MULTILINE)),
            'invalidFieldsOrIndices': len(re.findall(r"^Couldn't inject ", injection, re.MULTILINE)),
            'duplicateAliases': len(re.findall(r'^Duplicate def-injected translation key', injection, re.MULTILINE)),
        },
        'fatalOrLoadFailureCount': len(fatal), 'fatalOrLoadFailures': fatal,
        'activeMods': [m.get('packageId') for m in audit.findall('mod')],
        'report': str(audit_path.parent / 'TranslationReport.txt'),
    })
(root / 'summary.json').write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding='utf-8')
for row in rows:
    s = row['sections']
    print(f"{row['locale']}: general={s['AppendGeneralLoadErrors']}, injection={s['AppendDefInjectionsLoadErros']}, "
          f"missing-keyed={s['AppendMissingKeyedTranslations']}, missing-def={s['AppendMissingDefInjections']}, "
          f"arguments={s['AppendArgumentCountMismatches']}, obsolete-keyed={s['AppendUnnecessaryKeyedTranslations']}, "
          f"fatal={row['fatalOrLoadFailureCount']}")
