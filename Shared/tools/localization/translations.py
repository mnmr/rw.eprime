"""Validate shipped translation data; record the English revision it translates.

Uses only Python's standard library. No network or translation service calls.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
from typing import NamedTuple
import xml.etree.ElementTree as ET

MODS = ('WorkRoles', 'Readouts', 'QualityJobs', 'Implanner', 'Shared')
# Only active bundled locales are CLI inputs. Retired catalogs and their frozen
# English-source manifests live outside content roots in Translations/Retired.
LOCALES = ('German', 'French', 'Spanish', 'Italian', 'PortugueseBrazilian',
           'SpanishLatin', 'ChineseSimplified', 'ChineseTraditional',
           'Ukrainian', 'Japanese', 'Korean', 'Turkish', 'Vietnamese')
# Brand-only titles intentionally keep their names in every language. Pin both
# key and English text so later prose under the same key is still reviewed.
BRAND_TITLES = {
    'Implanner/Keyed/IMP_WelcomeTitle': "EPrime's Implanner",
    'QualityJobs/Keyed/QJ_WelcomeTitle': "EPrime's Quality Jobs",
    'Readouts/Keyed/EPR.Title': "EPrime's Readouts",
    'Readouts/Keyed/EPR.WelcomeTitle': "EPrime's Readouts",
}
# Reviewed wording that happens to be identical in this specific language.
# Do not waive reviews of the same key in another locale or after a text edit.
UNCHANGED_TRANSLATIONS = {
    ('French', 'Implanner/Keyed/IMP_ExportSummary'): '{0} plans, {1} implants',
    ('French', 'WorkRoles/Keyed/WR_RecClassificationSection'): 'Classification',
}
PLACEHOLDER = re.compile(r'(?<!\{)\{[^{}]+\}(?!\})')
LINK = re.compile(r'\]\(([^)\n]+)\)')
DIRECTIVE = re.compile(r'^\s*(@[A-Za-z][^\r\n]*)$', re.MULTILINE)
RICH_TAG = re.compile(r'</?(?:b|i|color|size|material|quad)(?:\s[^>]*|=[^>]*)?>')
CODE = re.compile(r'`([^`]+)`')
FRONTMATTER = re.compile(r'\A---\r?\n(.*?)\r?\n---(?:\r?\n|\Z)', re.DOTALL)


class Issue(NamedTuple):
    code: str
    path: str
    message: str
    severity: str = 'error'


def _xml(path, prefix, issues):
    entries = {}
    try:
        tree = ET.parse(path)
        if tree.getroot().tag != 'LanguageData':
            issues.append(Issue('xml', str(path), 'Expected LanguageData root'))
        for entry in tree.getroot():
            key = prefix + entry.tag
            if key in entries:
                issues.append(Issue('duplicate', key, 'Duplicate XML key'))
                continue
            entries[key] = entry.text or ''
            if len(entry):
                issues.append(Issue('xml', key, 'Nested XML: escape rich-text angle brackets'))
    except (ET.ParseError, OSError, UnicodeError) as error:
        issues.append(Issue('xml', str(path), str(error)))
    return entries


def _merge(target, entries, issues):
    for key, text in entries.items():
        if key in target:
            issues.append(Issue('duplicate', key, 'Key occurs in more than one file'))
        else:
            target[key] = text


def content_root(root, mod):
    return root / mod / ('Translations' if mod == 'Shared' else 'mod')


def catalog(root, locale, mods):
    """Flatten actual game content by semantic key, independent of XML order."""
    entries, issues = {}, []
    for mod in mods:
        base = content_root(root, mod)
        keyed = base / ('Languages/English' if locale == 'English' else f'FallbackLanguages/{locale}') / 'Keyed'
        for path in sorted(keyed.rglob('*.xml')):
            _merge(entries, _xml(path, f'{mod}/Keyed/', issues), issues)
        if locale == 'English':
            definition_files = set((base / 'Defs').rglob('*.xml'))
            definition_files.update(base.glob('*/Defs/**/*.xml'))
            for path in sorted(definition_files):
                try:
                    defs = ET.parse(path).getroot()
                except (ET.ParseError, OSError) as error:
                    issues.append(Issue('xml', str(path), str(error)))
                    continue
                for definition in defs:
                    name = definition.findtext('defName')
                    if not name:
                        continue
                    for field in ('label', 'description'):
                        value = definition.findtext(field)
                        if value is not None:
                            key = f'{mod}/DefInjected/{definition.tag}/{name}.{field}'
                            if key in entries and entries[key] != value:
                                issues.append(Issue('source', key, 'Version-specific English field differs'))
                            entries[key] = value
        else:
            injected = base / 'FallbackLanguages' / locale / 'DefInjected'
            for path in sorted(injected.glob('*/**/*.xml')):
                definition_type = path.relative_to(injected).parts[0]
                _merge(entries, _xml(path, f'{mod}/DefInjected/{definition_type}/', issues), issues)
        help_root = base / 'Help' / locale
        for path in sorted(help_root.rglob('*.md')):
            key = f'{mod}/Help/{path.relative_to(help_root).as_posix()}'
            try:
                entries[key] = path.read_text(encoding='utf-8-sig')
            except (OSError, UnicodeError) as error:
                issues.append(Issue('text', key, str(error)))
    return entries, issues


def manifest_path(root, locale):
    return root / 'Shared/Translations/Manifests' / f'{locale}.json'


def _digest(text):
    return hashlib.sha256(text.encode('utf-8')).hexdigest()


def _balanced_rich_tags(text):
    stack = []
    for match in RICH_TAG.finditer(text):
        tag = match.group()
        name = re.match(r'</?([a-z]+)', tag).group(1)
        if name == 'quad' or tag.endswith('/>'):
            continue
        if tag.startswith('</'):
            if not stack or stack.pop() != name:
                return False
        else:
            stack.append(name)
    return not stack


def check_locale(root, locale, mods=MODS, require_manifest=False):
    source, issues = catalog(root, 'English', mods)
    for mod in mods:
        if not any(key.startswith(mod + '/') for key in source):
            issues.append(Issue('source', mod, 'No English localization source found; check the repository root'))
    target, target_issues = catalog(root, locale, mods)
    issues.extend(target_issues)
    for key in sorted(source.keys() - target.keys()):
        issues.append(Issue('missing', key, f'No {locale} translation'))
    for key in sorted(target.keys() - source.keys()):
        issues.append(Issue('extra', key, 'No corresponding English source'))
    for key in sorted(source.keys() & target.keys()):
        original, translated = source[key], target[key]
        if original.strip() and not translated.strip():
            issues.append(Issue('empty', key, 'Translation is empty'))
        if translated.strip() == 'TODO' or '\ufffd' in translated:
            issues.append(Issue('unfinished', key, 'Placeholder or invalid Unicode replacement character'))
        if Counter(PLACEHOLDER.findall(original)) != Counter(PLACEHOLDER.findall(translated)):
            issues.append(Issue('placeholder', key, 'Format placeholders or their multiplicity differ'))
        if re.findall(r'(?:\\n)+', original) != re.findall(r'(?:\\n)+', translated):
            issues.append(Issue('newline', key, 'Literal paragraph/newline escapes differ'))
        if (Counter(RICH_TAG.findall(original)) != Counter(RICH_TAG.findall(translated))
                or not _balanced_rich_tags(translated)):
            issues.append(Issue('markup', key, 'Rich-text tags differ or are unbalanced'))
        if '/Help/' in key:
            header = FRONTMATTER.match(translated)
            if not header or not re.search(r'^title:[ \t]*\S[^\r\n]*$', header[1], re.MULTILINE):
                issues.append(Issue('frontmatter', key, 'Missing help title/frontmatter'))
            if CODE.findall(original) != CODE.findall(translated):
                issues.append(Issue('code', key, 'Literal code spans differ'))
            if LINK.findall(original) != LINK.findall(translated):
                issues.append(Issue('links', key, 'Help link/image destinations differ'))
            if DIRECTIVE.findall(original) != DIRECTIVE.findall(translated):
                issues.append(Issue('directive', key, 'Help demo/directive identifier differs'))
        # Exact equality is only a review signal: many valid cognates and brand
        # names are identical. It must never force deliberately wrong wording.
        if (original.strip() == translated.strip()
                and len(re.findall(r'[A-Za-z]', original)) > 12
                and BRAND_TITLES.get(key) != original.strip()
                and UNCHANGED_TRANSLATIONS.get((locale, key)) != original.strip()):
            issues.append(Issue('unchanged', key, 'Text matches English; check whether intentional', 'warning'))
    path = manifest_path(root, locale)
    if path.exists():
        try:
            recorded = json.loads(path.read_text(encoding='utf-8'))['source_sha256']
            if (not isinstance(recorded, dict)
                    or any(not isinstance(value, str) or not re.fullmatch(r'[0-9a-f]{64}', value)
                           for value in recorded.values())):
                raise ValueError('source_sha256 must map entry keys to SHA-256 hex strings')
            for key, original in source.items():
                if recorded.get(key) != _digest(original):
                    issues.append(Issue('stale', key, 'English changed or was added since the translation snapshot'))
            for key in recorded.keys() - source.keys():
                issues.append(Issue('retired', key, 'English source was removed since the snapshot'))
        except (OSError, ValueError, KeyError, TypeError) as error:
            issues.append(Issue('manifest', str(path), str(error)))
    elif require_manifest:
        issues.append(Issue('manifest', str(path), 'Missing English source snapshot'))
    return issues


def snapshot_locale(root, locale, mods=MODS):
    # The explicit snapshot operation acknowledges source revisions only after
    # structural coverage is complete. It does not claim human review or fluency.
    issues = [i for i in check_locale(root, locale, mods)
              if i.severity == 'error' and i.code not in ('stale', 'retired')]
    if issues:
        raise ValueError(f'{locale}: {len(issues)} errors prevent source snapshot: {issues[0]}')
    source, _ = catalog(root, 'English', mods)
    path = manifest_path(root, locale)
    path.parent.mkdir(parents=True, exist_ok=True)
    value = {'schema_version': 1, 'locale': locale,
             'translation_method': 'automated; Danish/German translations include owner corrections',
             'source_sha256': {key: _digest(text) for key, text in sorted(source.items())}}
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('check', 'snapshot', 'inventory'))
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[3])
    parser.add_argument('--locale', action='append', choices=LOCALES)
    parser.add_argument('--no-manifest', action='store_true', help='Draft checks before a source snapshot exists')
    parser.add_argument('--json', type=Path, help='Write the complete machine-readable result')
    args = parser.parse_args()
    results = {}
    failures = 0
    for locale in args.locale or LOCALES:
        if args.command == 'inventory':
            source, _ = catalog(args.root, 'English', MODS)
            target, issues = catalog(args.root, locale, MODS)
            covered = sum(bool(target.get(k, '').strip()) for k in source)
            results[locale] = {'covered': covered, 'expected': len(source), 'parse_issues': len(issues)}
            print(f'{locale}: {covered}/{len(source)} entries ({len(issues)} parse issues)')
            continue
        if args.command == 'snapshot':
            try:
                snapshot_locale(args.root, locale)
            except ValueError as error:
                print(error)
                failures += 1
                continue
        issues = check_locale(args.root, locale, require_manifest=not args.no_manifest)
        errors = sum(i.severity == 'error' for i in issues)
        warnings = len(issues) - errors
        failures += errors
        results[locale] = [i._asdict() for i in issues]
        print(f'{locale}: {errors} errors, {warnings} unchanged-English review signals')
        for issue in issues[:25]:
            print(f'  {issue.severity} {issue.code}: {issue.path}: {issue.message}')
        if len(issues) > 25:
            print(f'  ... {len(issues) - 25} more (use --json for full output)')
    if args.json:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(json.dumps(results, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return 1 if failures else 0


if __name__ == '__main__':
    raise SystemExit(main())
