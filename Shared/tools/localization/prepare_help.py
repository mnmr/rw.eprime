"""Add stable help IDs without rewriting translations or accepting stale text."""
import argparse
import json
from pathlib import Path
import re

import help_blocks as blocks
import translations as checks
import update_translations as updater


def prepare(root, locales=checks.LOCALES, mods=checks.MODS, dry_run=False):
    root = Path(root).resolve()
    locales = tuple(dict.fromkeys(locales))
    before = updater.content_files(root, locales, mods)
    after = dict(before)
    manifests = {locale: json.loads(checks.manifest_path(root, locale).read_text(encoding='utf-8'))
                 for locale in locales if checks.manifest_path(root, locale).exists()}
    updater.require_clean(checks.catalog(root, 'English', mods)[1], 'English')
    for locale in locales:
        updater.require_clean(checks.catalog(root, locale, mods)[1], locale)
        issues = checks.check_locale(root, locale, mods)
        updater.require_clean([i for i in issues if i.code in ('xml', 'duplicate', 'source', 'text', 'manifest')], locale)
    migrated, units, problems = 0, 0, []
    for page, source in checks.help_pages(root, 'English', mods).items():
        mod, _, relative = page.split('/', 2)
        source_path = checks.content_root(root, mod) / 'Help/English' / relative
        try:
            source_blocks = blocks.parse(source)
            marked = source if source_blocks is not None else blocks.annotate(source)
            parsed = blocks.parse(marked)
            if parsed is None:
                raise ValueError('Help needs at least one body block')
        except ValueError as error:
            problems.append(f'{page}: {error}')
            continue
        legacy_source = re.sub(blocks.MARKER, '', source).lstrip('\ufeff').replace('\r\n', '\n')
        after[source_path.relative_to(root)] = marked.encode('utf-8')
        for locale in locales:
            target_path = checks.content_root(root, mod) / 'Help' / locale / relative
            if not target_path.exists():
                continue
            current = target_path.read_bytes().decode('utf-8')
            try:
                existing = blocks.parse(current)
                if existing is not None:
                    if source_blocks is None:
                        raise ValueError('English IDs were removed; restore them before migration')
                    continue
                heading = blocks.title(current)
                body = blocks.spans(current[blocks.HEADER.match(current).end():], blocks.HEADER.match(current).end())
                if [blocks.signature(b) for b in [heading, *body]] != [blocks.signature(b) for b in parsed]:
                    raise ValueError('Ambiguous block alignment; review paragraph/list boundaries and literal anchors')
                manifest = manifests.get(locale)
                digest = manifest.get('source_sha256', {}).get(page) if manifest else None
                if digest is not None and digest != checks._digest(legacy_source):
                    raise ValueError('Legacy translation is stale; translate/review it before migrating')
                result = blocks.annotate(current, [b.id for b in parsed[1:]])
                after[target_path.relative_to(root)] = result.encode('utf-8')
                if digest is not None:
                    manifest['schema_version'] = 2
                    manifest['source_sha256'].pop(page)
                    manifest['source_sha256'].update((page + '#' + identifier, value)
                                                    for identifier, value in blocks.digests(parsed).items())
                    manifest.setdefault('help_source', {}).update((page + '#' + b.id, blocks.canonical(b.text)) for b in parsed)
            except ValueError as error:
                problems.append(f'{locale}/{page}: {error}')
        if source_blocks is None:
            migrated += 1
            units += len(parsed)
    if problems:
        raise ValueError('Help migration stopped without writing files:\n' + '\n'.join(problems))
    for locale, manifest in manifests.items():
        for name in ('source_sha256', 'help_source'):
            if name in manifest:
                manifest[name] = dict(sorted(manifest[name].items()))
        after[checks.manifest_path(root, locale).relative_to(root)] = (json.dumps(manifest, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
    changes = sum(before.get(path) != value for path, value in after.items())
    print(f'Help IDs: {migrated} pages, {units} blocks; {changes} files to change. Existing wording is preserved.')
    if dry_run:
        print('Preview only; no changes applied.')
    else:
        updater.commit_files(root, locales, mods, before, after)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[3])
    parser.add_argument('--dry-run', action='store_true')
    # IDs are shared by every locale: migration always aligns all active locales.
    args = parser.parse_args()
    try:
        prepare(args.root, dry_run=args.dry_run)
    except (ValueError, OSError) as error:
        print(f'ERROR: {error}')
        return 1
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
