from pathlib import Path
import json
import os
import subprocess
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import translations
import update_translations as updater
import test_translations as fixtures


class TranslationUpdates(unittest.TestCase):
    setUp = fixtures.TranslationChecks.setUp
    write = fixtures.TranslationChecks.write

    def files(self):
        return {p.relative_to(self.root): p.read_bytes()
                for p in self.root.rglob('*') if p.is_file() and 'temp' not in p.relative_to(self.root).parts}

    def snapshot(self):
        translations.snapshot_locale(self.root, 'Danish', ('Example',))

    def update(self, translator, **kwargs):
        return updater.update(self.root, ['Danish'], translator, mods=('Example',), **kwargs)

    def test_shared_library_text_updates_once_without_copying_into_consuming_mods(self):
        self.write('Shared/Translations/Languages/English/Keyed/Welcome.xml',
                   '<LanguageData><RS_ConflictHeading>Potential mod conflicts detected</RS_ConflictHeading></LanguageData>')
        updater.update(self.root, ['Danish'],
                       lambda locale, entries: {entry['key']: 'Mulige modkonflikter registreret' for entry in entries},
                       mods=('Shared',))
        target, issues = translations.catalog(self.root, 'Danish', ('Shared',))
        self.assertEqual({'Shared/Keyed/RS_ConflictHeading': 'Mulige modkonflikter registreret'}, target)
        self.assertEqual([], issues)
        self.assertTrue((self.root / 'Shared/Translations/FallbackLanguages/Danish/Keyed/Welcome.xml').exists())
        self.assertFalse((self.root / 'Shared/mod').exists())
        updater.update(self.root, ['Danish'], lambda *_: self.fail('Shared text should already be current'), mods=('Shared',))

    def test_updates_changed_and_missing_content_retires_old_entries_and_preserves_owner_text(self):
        self.write('Example/mod/FallbackLanguages/Danish/Keyed/Owner.xml',
                   '<LanguageData><!-- Keep this comment. --><Owner>Bondens rolle</Owner></LanguageData>')
        self.write('Example/mod/Languages/English/Keyed/Owner.xml',
                   '<LanguageData><Owner>Pawn role</Owner></LanguageData>')
        self.snapshot()
        owner = self.root / 'Example/mod/FallbackLanguages/Danish/Keyed/Owner.xml'
        owner_before = owner.read_bytes()
        self.write('Example/mod/Languages/English/Keyed/Main.xml',
                   '<LanguageData><Count>{0} of {1} items</Count><New>New label</New></LanguageData>')
        self.write('Example/mod/1.7/Defs/Roles.xml',
                   '<Defs><RoleDef><defName>Doctor</defName><label>doctor</label></RoleDef></Defs>')
        self.write('Example/mod/Help/English/new/02-more.md',
                   '---\ntitle: More\n---\nUse `Doctor` [here](topic:details).\n@demo:example\n')
        (self.root / 'Example/mod/Help/English/start/01-welcome.md').unlink()
        expected = {'Example/Keyed/Count': '{0} af {1} genstande',
                    'Example/Keyed/New': 'Ny etiket',
                    'Example/DefInjected/RoleDef/Doctor.label': 'læge',
                    'Example/Help/new/02-more.md':
                    '---\ntitle: Mere\n---\nBrug `Doctor` [her](topic:details).\n@demo:example\n'}
        requested = {}

        def translate(locale, entries):
            self.assertEqual('Danish', locale)
            requested.update({entry['key']: entry for entry in entries})
            return {entry['key']: expected[entry['key']] for entry in entries}

        self.update(translate)
        self.assertEqual(set(expected), set(requested))
        self.assertEqual('Af {1}: {0}', requested['Example/Keyed/Count']['current'])
        target, issues = translations.catalog(self.root, 'Danish', ('Example',))
        self.assertEqual({**expected, 'Example/Keyed/Owner': 'Bondens rolle'}, target)
        self.assertEqual([], issues)
        self.assertEqual([], translations.check_locale(self.root, 'Danish', ('Example',), True))
        self.assertEqual(owner_before, owner.read_bytes())
        self.assertFalse((self.root / 'Example/mod/Help/Danish/start/01-welcome.md').exists())
        before = self.files()
        self.update(lambda *_: self.fail('Current translations must not call Codex'))
        self.assertEqual(before, self.files())

    def test_failed_later_locale_does_not_publish_earlier_locale_or_advance_hashes(self):
        self.snapshot()
        self.write('Example/mod/Languages/English/Keyed/Main.xml',
                   '<LanguageData><Count>{0} of {1} items</Count><Title>Example</Title></LanguageData>')
        before = self.files()

        def translate(locale, entries):
            if locale == 'German':
                raise RuntimeError('Translation service unavailable')
            return {entry['key']: '{0} af {1} genstande' for entry in entries}

        with self.assertRaisesRegex(RuntimeError, 'unavailable'):
            updater.update(self.root, ['Danish', 'German'], translate, mods=('Example',))
        self.assertEqual(before, self.files())
        self.assertTrue(any(i.code == 'stale' for i in translations.check_locale(
            self.root, 'Danish', ('Example',))))

    def test_rejects_incomplete_responses_and_broken_placeholders_without_writing(self):
        self.snapshot()
        self.write('Example/mod/Languages/English/Keyed/Main.xml',
                   '<LanguageData><Count>{0} of {1} items</Count><Title>Example</Title></LanguageData>')
        before = self.files()
        for response in ({}, {'Example/Keyed/Count': 'Mangler pladsholdere'},
                         {'Example/Keyed/Count': '{0} af {1}', '../outside': 'unsafe'}):
            with self.subTest(response=response):
                with self.assertRaises(ValueError):
                    self.update(lambda *_: response)
                self.assertEqual(before, self.files())

    def test_dry_run_previews_without_calling_codex_or_acknowledging_sources(self):
        before = self.files()
        self.update(lambda *_: self.fail('Dry run must not call Codex'), dry_run=True)
        self.assertEqual(before, self.files())
        self.assertFalse(translations.manifest_path(self.root, 'Danish').exists())

    def test_missing_snapshot_requires_translation_of_all_entries_and_detects_concurrent_edits(self):
        source, _ = translations.catalog(self.root, 'English', ('Example',))
        target, _ = translations.catalog(self.root, 'Danish', ('Example',))

        def translate(locale, entries):
            self.assertEqual(set(source), {entry['key'] for entry in entries})
            self.write('Example/mod/FallbackLanguages/Danish/Keyed/Main.xml',
                       '<LanguageData><Count>{0} af {1}</Count><Title>Ejerens rettelse</Title></LanguageData>')
            return {entry['key']: target[entry['key']] for entry in entries}

        with self.assertRaisesRegex(ValueError, 'changed during'):
            self.update(translate)
        actual, _ = translations.catalog(self.root, 'Danish', ('Example',))
        self.assertEqual('Ejerens rettelse', actual['Example/Keyed/Title'])
        self.assertFalse(translations.manifest_path(self.root, 'Danish').exists())

    def test_refuses_ambiguous_xml_before_requesting_translations(self):
        self.write('Example/mod/FallbackLanguages/Danish/Keyed/Main.xml',
                   '<LanguageData><Title>En</Title><Title>To</Title></LanguageData>')
        before = self.files()
        with self.assertRaisesRegex(ValueError, 'duplicate'):
            self.update(lambda *_: self.fail('Duplicate keys need manual repair'))
        self.assertEqual(before, self.files())

    def test_write_failure_restores_translations_and_source_snapshot(self):
        self.snapshot()
        self.write('Example/mod/Languages/English/Keyed/Main.xml',
                   '<LanguageData><Count>{0} of {1} items</Count><Title>Example</Title></LanguageData>')
        before = self.files()
        real_replace = updater.replace_file
        failed = False

        def fail_manifest_once(path, data):
            nonlocal failed
            if path.suffix == '.json' and not failed:
                failed = True
                raise OSError('Disk write failed')
            real_replace(path, data)

        with patch.object(updater, 'replace_file', side_effect=fail_manifest_once):
            with self.assertRaisesRegex(OSError, 'Disk write failed'):
                self.update(lambda *_: {'Example/Keyed/Count': '{0} af {1} genstande'})
        self.assertEqual(before, self.files())

    def test_codex_process_receives_unicode_and_schema_and_returns_validated_text(self):
        # A real subprocess exercises the CLI transport without paid/network calls.
        fake = self.root / 'fake_codex.py'
        fake.write_text('''import json, pathlib, sys
args = sys.argv[1:]
schema = json.loads(pathlib.Path(args[args.index('--output-schema') + 1]).read_text())
assert schema['properties']['translations']['type'] == 'array'
prompt = sys.stdin.buffer.read().decode('utf-8')
assert 'Bønder' in prompt and 'Danish' in prompt
pathlib.Path(args[args.index('--output-last-message') + 1]).write_text(
    json.dumps({'translations': [{'key': 'Example/Keyed/Title', 'text': 'Bøndernes roller'}]},
               ensure_ascii=False), encoding='utf-8')
''', encoding='utf-8')
        translate = updater.CodexTranslator(command=[sys.executable, str(fake)])
        result = translate('Danish', [{'key': 'Example/Keyed/Title',
                                     'english': 'Pawn roles', 'current': 'Bønder'}])
        self.assertEqual({'Example/Keyed/Title': 'Bøndernes roller'}, result)

    def test_desktop_cli_is_used_without_path_and_path_install_takes_precedence(self):
        profile = self.root / 'profile'
        local = profile / 'AppData/Local'
        bundled = local / 'OpenAI/Codex/bin'
        old, current = bundled / 'old/codex.exe', bundled / 'current/codex.exe'
        on_path = self.root / 'command bin/codex.exe'
        for timestamp, path in enumerate((old, current, on_path), 1):
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b'fixture')
            path.chmod(0o755)
            os.utime(path, (timestamp, timestamp))
        (bundled / 'incomplete-update').mkdir()
        launched = []

        def run(command, **kwargs):
            launched.append(command[0])
            output = Path(command[command.index('--output-last-message') + 1])
            output.write_text(json.dumps({'translations': [
                {'key': 'Example/Keyed/Title', 'text': 'Eksempel'}]}), encoding='utf-8')
            return subprocess.CompletedProcess(command, 0, '', '')

        entries = [{'key': 'Example/Keyed/Title', 'english': 'Example', 'current': ''}]
        with patch.object(sys, 'platform', 'win32'), patch.object(Path, 'home', return_value=profile), \
                patch.dict(os.environ, {'PATH': '', 'LOCALAPPDATA': str(local), 'PATHEXT': '.exe'}), \
                patch.object(updater.subprocess, 'run', side_effect=run):
            self.assertEqual({'Example/Keyed/Title': 'Eksempel'}, updater.CodexTranslator()('Danish', entries))
            # Sandboxed processes can redirect LOCALAPPDATA away from the install.
            os.environ['LOCALAPPDATA'] = str(self.root / 'redirected')
            updater.CodexTranslator()('Danish', entries)
            os.environ['PATH'] = str(on_path.parent)
            updater.CodexTranslator()('Danish', entries)
        self.assertEqual([str(current), str(current), str(on_path)], launched)


if __name__ == '__main__':
    unittest.main()
