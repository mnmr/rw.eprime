import importlib.util
from pathlib import Path
import tempfile
import unittest

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


if __name__ == '__main__':
    unittest.main()
