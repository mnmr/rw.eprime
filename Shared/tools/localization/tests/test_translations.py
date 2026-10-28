import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
spec = importlib.util.spec_from_file_location('translations', Path(__file__).parents[1] / 'translations.py')
translations = importlib.util.module_from_spec(spec)
spec.loader.exec_module(translations)


class TranslationChecks(unittest.TestCase):
    def setUp(self):
        temp_root = Path(__file__).resolve().parents[4] / 'temp/localization-tests'
        temp_root.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=temp_root)
        self.root = Path(self.temp.name)
        assert self.root.resolve().is_relative_to(temp_root.resolve())
        self.addCleanup(self.temp.cleanup)
        self.write('Example/mod/Languages/English/Keyed/Main.xml',
                   '<LanguageData><Count>{0} of {1}</Count><Title>Example</Title></LanguageData>')
        self.write('Example/mod/FallbackLanguages/Danish/Keyed/Main.xml',
                   '<LanguageData><Title>Eksempel</Title><Count>Af {1}: {0}</Count></LanguageData>')
        self.write('Example/mod/Help/English/start/01-welcome.md',
                   '---\ntitle: Welcome\n---\nRead [here](topic:details).\n\n![Image](image.png)\n\n@demo:example\n')
        self.write('Example/mod/Help/Danish/start/01-welcome.md',
                   '---\ntitle: Velkommen\n---\nLæs [her](topic:details).\n\n![Billede](image.png)\n\n@demo:example\n')

    def write(self, relative, text):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding='utf-8')

    def check(self):
        return translations.check_locale(self.root, 'Danish', ('Example',))

    def test_complete_translation_accepts_reordered_placeholders_but_reports_loss_and_duplicates(self):
        self.assertEqual([], self.check())
        self.write('Example/mod/FallbackLanguages/Danish/Keyed/Main.xml',
                   '<LanguageData><Count>{0} af {0}</Count><Count>Duplikat</Count></LanguageData>')
        codes = {issue.code for issue in self.check()}
        self.assertTrue({'duplicate', 'missing', 'placeholder'} <= codes, codes)

    def test_help_checks_preserve_destinations_and_demo_identifiers(self):
        self.write('Example/mod/Help/Danish/start/01-welcome.md',
                   '---\ntitle: Velkommen\n---\nLæs [her](topic:forkert).\n\n![Billede](billede.png)\n\n@demo:eksempel\n')
        codes = {issue.code for issue in self.check()}
        self.assertTrue({'links', 'directive'} <= codes, codes)

    def test_source_snapshot_reports_changed_and_added_english_content(self):
        translations.snapshot_locale(self.root, 'Danish', ('Example',))
        self.assertEqual([], self.check())
        self.write('Example/mod/Languages/English/Keyed/Main.xml',
                   '<LanguageData><Count>{0} of {1} items</Count><Title>Example</Title><New>New label</New></LanguageData>')
        issues = self.check()
        self.assertTrue(any(i.code == 'stale' and 'Count' in i.path for i in issues))
        self.assertTrue(any(i.code == 'missing' and 'New' in i.path for i in issues))

    def test_invalid_snapshot_shapes_report_errors_instead_of_crashing_or_appearing_current(self):
        self.snapshot_path = translations.manifest_path(self.root, 'Danish')
        self.snapshot_path.parent.mkdir(parents=True, exist_ok=True)
        for recorded in ([], None, 'invalid', {'key': 123}):
            with self.subTest(recorded=recorded):
                self.snapshot_path.write_text(json.dumps({'source_sha256': recorded}), encoding='utf-8')
                self.assertTrue(any(i.code == 'manifest' for i in self.check()))

    def test_empty_source_tree_is_not_a_successful_check(self):
        issues = translations.check_locale(self.root / 'missing-repository', 'Danish', ('Example',))
        self.assertTrue(any(i.code == 'source' and i.severity == 'error' for i in issues))

    def test_brand_titles_skip_review_but_prose_changed_titles_and_missing_entries_do_not(self):
        mods = ('Implanner', 'QualityJobs', 'Readouts')
        entries = {
            'Implanner': "<IMP_WelcomeTitle>EPrime's Implanner</IMP_WelcomeTitle>",
            'QualityJobs': "<QJ_WelcomeTitle>EPrime's Quality Jobs</QJ_WelcomeTitle>",
            'Readouts': "<EPR.Title>EPrime's Readouts</EPR.Title>"
                        "<EPR.WelcomeTitle>EPrime's Readouts</EPR.WelcomeTitle>"
                        "<EPR.Body>EPrime's Readouts shows your resources.</EPR.Body>"
                        "<EPR.OtherTitle>EPrime's Readouts</EPR.OtherTitle>",
        }
        for mod, content in entries.items():
            for folder in ('Languages/English', 'FallbackLanguages/Danish'):
                self.write(f'{mod}/mod/{folder}/Keyed/Main.xml',
                           '<LanguageData>' + content + '</LanguageData>')
        issues = translations.check_locale(self.root, 'Danish', mods)
        self.assertEqual([
            ('unchanged', 'Readouts/Keyed/EPR.Body', 'warning'),
            ('unchanged', 'Readouts/Keyed/EPR.OtherTitle', 'warning'),
        ], [(i.code, i.path, i.severity) for i in issues])

        # An exemption must not hide prose added to a formerly brand-only key.
        for folder in ('Languages/English', 'FallbackLanguages/Danish'):
            self.write(f'Readouts/mod/{folder}/Keyed/Main.xml',
                       "<LanguageData><EPR.Title>EPrime's Readouts shows your resources.</EPR.Title></LanguageData>")
        self.write('Implanner/mod/FallbackLanguages/Danish/Keyed/Main.xml', '<LanguageData/>')
        issues = translations.check_locale(self.root, 'Danish', mods)
        self.assertEqual([
            ('missing', 'Implanner/Keyed/IMP_WelcomeTitle', 'error'),
            ('unchanged', 'Readouts/Keyed/EPR.Title', 'warning'),
        ], [(i.code, i.path, i.severity) for i in issues])

    def test_reviewed_french_cognates_are_exempt_only_for_the_reviewed_locale_and_wording(self):
        mods = ('Implanner', 'WorkRoles')
        entries = {
            'Implanner': '<IMP_ExportSummary>{0} plans, {1} implants</IMP_ExportSummary>',
            'WorkRoles': '<WR_RecClassificationSection>Classification</WR_RecClassificationSection>',
        }
        for mod, content in entries.items():
            for folder in ('Languages/English', 'FallbackLanguages/French', 'FallbackLanguages/Danish'):
                self.write(f'{mod}/mod/{folder}/Keyed/Main.xml',
                           '<LanguageData>' + content + '</LanguageData>')
        self.assertEqual([], translations.check_locale(self.root, 'French', mods))
        issues = translations.check_locale(self.root, 'Danish', mods)
        self.assertEqual([
            ('unchanged', 'Implanner/Keyed/IMP_ExportSummary'),
            ('unchanged', 'WorkRoles/Keyed/WR_RecClassificationSection'),
        ], [(i.code, i.path) for i in issues])
        for folder in ('Languages/English', 'FallbackLanguages/French'):
            self.write(f'WorkRoles/mod/{folder}/Keyed/Main.xml',
                       '<LanguageData><WR_RecClassificationSection>Classification details</WR_RecClassificationSection></LanguageData>')
        issues = translations.check_locale(self.root, 'French', mods)
        self.assertEqual([('unchanged', 'WorkRoles/Keyed/WR_RecClassificationSection')],
                         [(i.code, i.path) for i in issues])

    def test_definition_coverage_includes_root_defs_and_nested_translation_files(self):
        self.write('Example/mod/Defs/Roles.xml',
                   '<Defs><RoleDef><defName>Doctor</defName><label>doctor</label>'
                   '<description>Treat patients</description><seedLabel>Doctor</seedLabel></RoleDef></Defs>')
        self.write('Example/mod/1.6/Defs/Roles.xml',
                   '<Defs><RoleDef><defName>Nurse</defName><label>nurse</label></RoleDef></Defs>')
        self.write('Example/mod/FallbackLanguages/Danish/DefInjected/RoleDef/Medical/Labels.xml',
                   '<LanguageData><Doctor.label>læge</Doctor.label>'
                   '<Nurse.label>sygeplejerske</Nurse.label></LanguageData>')
        issues = self.check()
        self.assertEqual([('missing', 'Example/DefInjected/RoleDef/Doctor.description')],
                         [(i.code, i.path) for i in issues])
        self.write('Example/mod/FallbackLanguages/Danish/DefInjected/RoleDef/Medical/Descriptions.xml',
                   '<LanguageData><Doctor.description>Behandl patienter</Doctor.description></LanguageData>')
        self.assertEqual([], self.check())

    def test_malformed_formatting_and_changed_literal_identifiers_are_reported(self):
        self.write('Example/mod/Languages/English/Keyed/Formatting.xml',
                   '<LanguageData><Notice>&lt;b&gt;Use {0}&lt;/b&gt;</Notice></LanguageData>')
        self.write('Example/mod/FallbackLanguages/Danish/Keyed/Formatting.xml',
                   '<LanguageData><Notice>&lt;b&gt;Brug {0}&lt;/b&gt;</Notice></LanguageData>')
        self.write('Example/mod/Help/English/start/01-welcome.md',
                   '---\ntitle: Welcome\n---\nKeep `seedLabel` unchanged.\n')
        self.write('Example/mod/Help/Danish/start/01-welcome.md',
                   '---\ntitle: Velkommen\n---\nBevar `seedLabel` uændret.\n')
        self.assertEqual([], self.check())
        self.write('Example/mod/FallbackLanguages/Danish/Keyed/Formatting.xml',
                   '<LanguageData><Notice>&lt;/b&gt;Brug {0}&lt;b&gt;</Notice></LanguageData>')
        self.write('Example/mod/Help/Danish/start/01-welcome.md',
                   '---\nother: value\n---\ntitle: Not frontmatter\n---\nBevar `grundnavn` uændret.\n')
        codes = {issue.code for issue in self.check()}
        self.assertTrue({'markup', 'code', 'frontmatter'} <= codes, codes)


class TranslationCliLocales(unittest.TestCase):
    active = ('German', 'French', 'Spanish', 'Italian', 'PortugueseBrazilian',
              'SpanishLatin', 'ChineseSimplified', 'ChineseTraditional',
              'Ukrainian', 'Japanese', 'Korean', 'Turkish', 'Vietnamese')
    retired = ('Danish', 'Dutch', 'Swedish', 'Finnish', 'Norwegian', 'Portuguese')

    def setUp(self):
        temp_root = Path(__file__).resolve().parents[4] / 'temp/localization-tests'
        temp_root.mkdir(parents=True, exist_ok=True)
        temporary = tempfile.TemporaryDirectory(dir=temp_root)
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.tools = Path(__file__).resolve().parents[1]
        for mod in ('WorkRoles', 'Readouts', 'QualityJobs', 'Implanner', 'Shared'):
            base = f'{mod}/Translations' if mod == 'Shared' else f'{mod}/mod'
            self.write(f'{base}/Languages/English/Keyed/Main.xml',
                       b'<LanguageData><Greeting>Hello</Greeting></LanguageData>')
            for locale in self.active:
                self.write(f'{base}/FallbackLanguages/{locale}/Keyed/Main.xml',
                           b'<LanguageData><Greeting>Bonjour</Greeting></LanguageData>')
            for locale in self.retired:
                # Archived content is historical evidence, not an input to the
                # active commands. Invalid XML makes accidental scans visible.
                self.write(f'Shared/Translations/Retired/{base}/FallbackLanguages/{locale}/Keyed/Main.xml',
                           b'frozen archived XML\r\n\xff')
                if mod != 'Shared':
                    self.write(f'Shared/Translations/Retired/{base}/Help/{locale}/start.md',
                               b'frozen archived help\r\n')
        for locale in self.active:
            translations.snapshot_locale(self.root, locale)
        for locale in self.retired:
            self.write(f'Shared/Translations/Retired/Shared/Translations/Manifests/{locale}.json',
                       b'frozen English source hashes\r\n')

    def write(self, relative, content):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content)

    def tree(self):
        return {path.relative_to(self.root): path.read_bytes() if path.is_file() else None
                for path in self.root.rglob('*')}

    def run_cli(self, tool, *args):
        return subprocess.run([sys.executable, str(self.tools / tool), *args,
                               '--root', str(self.root)], capture_output=True,
                              text=True, encoding='utf-8', timeout=30)

    def test_default_commands_use_only_active_locales_and_preserve_retired_archive(self):
        before = self.tree()
        commands = [('translations.py', command) for command in ('check', 'inventory', 'snapshot')]
        commands += [('update_translations.py', '--dry-run'),
                     ('update_translations.py', '--codex', str(self.root / 'must-not-run-codex.exe'))]
        for tool, *args in commands:
            with self.subTest(tool=tool, args=args):
                result = self.run_cli(tool, *args)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                reported = tuple(line.split(':', 1)[0] for line in result.stdout.splitlines()
                                 if line.split(':', 1)[0] in self.active + self.retired)
                self.assertEqual(self.active, reported)
                self.assertEqual(before, self.tree())

    def test_explicit_retired_locales_are_rejected_before_any_writes(self):
        before = self.tree()
        for locale in self.retired:
            commands = [('translations.py', command, '--json', str(self.root / 'must-not-write.json'))
                        for command in ('check', 'inventory', 'snapshot')]
            commands += [('update_translations.py', '--codex', str(self.root / 'must-not-run-codex.exe'))]
            for tool, *args in commands:
                with self.subTest(locale=locale, tool=tool, args=args):
                    result = self.run_cli(tool, *args, '--locale', locale)
                    self.assertEqual(2, result.returncode, result.stdout + result.stderr)
                    self.assertIn('invalid choice', result.stderr)
                    self.assertEqual(before, self.tree())


if __name__ == '__main__':
    unittest.main()
