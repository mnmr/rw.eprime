"""End-to-end localization scenarios: preservation, context and identity."""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import translations
import update_translations as updater
import test_translations as fixtures


PAGE = 'Example/Help/start/01-welcome.md'
SOURCE = '---\ntitle: Welcome\n---\n<!-- l10n:intro -->\nChoose a setting.\n\n<!-- l10n:choices -->\n- Fast\n- Slow\n\n<!-- l10n:other -->\nKeep this paragraph.\n'
TARGET = '\ufeff---\r\ntitle: Velkommen\r\n---\r\n<!-- l10n:intro -->\r\nVælg en indstilling.\r\n\r\n<!-- l10n:choices -->\r\n- Hurtig\r\n- Langsom\r\n\r\n<!-- l10n:other -->\r\nBevar  denne formulering.\r\n'


class HelpUpdates(unittest.TestCase):
    setUp = fixtures.TranslationChecks.setUp
    write = fixtures.TranslationChecks.write

    def prepare(self):
        self.english = self.root / 'Example/mod/Help/English/start/01-welcome.md'
        self.target = self.root / 'Example/mod/Help/Danish/start/01-welcome.md'
        self.english.write_bytes(SOURCE.encode('utf-8'))
        self.target.write_bytes(TARGET.encode('utf-8'))
        translations.snapshot_locale(self.root, 'Danish', ('Example',))

    def update(self, translate):
        updater.update(self.root, ['Danish'], translate, mods=('Example',))

    def test_paragraph_update_has_page_context_and_preserves_every_other_byte(self):
        self.prepare()
        self.english.write_bytes(SOURCE.replace('Keep this paragraph.', 'Keep this useful paragraph.').encode())
        requested = []

        def translate(locale, entries):
            requested.extend(entries)
            self.assertEqual([PAGE + '#other'], [e['key'] for e in entries])
            self.assertIn('Choose a setting.', entries[0]['page_english'])
            self.assertIn('Vælg en indstilling.', entries[0]['page_current'])
            self.assertEqual('Keep this paragraph.', entries[0]['previous_english'])
            return {PAGE + '#other': 'Bevar denne nyttige formulering.'}

        self.update(translate)
        self.assertEqual(1, len(requested))
        self.assertEqual(TARGET.replace('Bevar  denne formulering.', 'Bevar denne nyttige formulering.').encode(),
                         self.target.read_bytes())
        self.update(lambda *_: self.fail('Already current: no calls or writes'))

    def test_soft_wrap_is_ignored_but_list_context_change_requests_only_dependent_blocks(self):
        self.prepare()
        self.english.write_bytes(SOURCE.replace('Keep this paragraph.', 'Keep this\nparagraph.').encode())
        self.update(lambda *_: self.fail('Soft wrapping is not a translation change'))
        self.english.write_bytes(SOURCE.replace('Choose a setting.', 'Choose a speed.').encode())
        requested = []

        def translate(locale, entries):
            requested.extend(e['key'] for e in entries)
            return {e['key']: ('Vælg en hastighed.' if e['key'].endswith('#intro') else e['current']) for e in entries}

        self.update(translate)
        self.assertEqual({PAGE + '#intro', PAGE + '#choices'}, set(requested))
        self.assertIn(b'Bevar  denne formulering.', self.target.read_bytes())

    def test_reorder_add_and_remove_blocks_preserves_surviving_wording(self):
        self.prepare()
        revised = ('---\ntitle: Welcome\n---\n<!-- l10n:other -->\nKeep this paragraph.\n\n'
                   '<!-- l10n:new -->\nNew paragraph.\n')
        self.english.write_bytes(revised.encode())
        requested = []

        def translate(locale, entries):
            requested.extend(e['key'] for e in entries)
            return {PAGE + '#new': 'Nyt afsnit.'}

        self.update(translate)
        self.assertEqual([PAGE + '#new'], requested)
        result = self.target.read_bytes()
        self.assertIn(b'Bevar  denne formulering.', result)
        self.assertNotIn(b'l10n:intro', result)
        self.assertLess(result.index(b'l10n:other'), result.index(b'l10n:new'))
        self.assertIn(b'Bevar  denne formulering.\r\n\r\n<!-- l10n:new', result)
        self.assertEqual([], [i for i in translations.check_locale(self.root, 'Danish', ('Example',), True)
                              if i.severity == 'error'])

    def test_duplicate_ids_and_unmarked_paragraphs_abort_without_acknowledging(self):
        self.prepare()
        manifest = translations.manifest_path(self.root, 'Danish')
        before = manifest.read_bytes(), self.target.read_bytes()
        for bad in (SOURCE.replace('l10n:other', 'l10n:intro'), SOURCE + '\nUnmarked paragraph.\n'):
            self.english.write_bytes(bad.encode())
            with self.assertRaises(ValueError):
                self.update(lambda *_: self.fail('Ambiguous IDs must fail before translation'))
            self.assertEqual(before, (manifest.read_bytes(), self.target.read_bytes()))

    def test_scoped_manual_acknowledgement_does_not_accept_another_changed_block(self):
        self.prepare()
        self.english.write_bytes(SOURCE.replace('Choose a setting.', 'Choose a speed.')
                                .replace('Keep this paragraph.', 'Keep this useful paragraph.').encode())
        translations.snapshot_locale(self.root, 'Danish', ('Example',), keys=[PAGE + '#other'])
        stale = {i.path for i in translations.check_locale(self.root, 'Danish', ('Example',)) if i.code == 'stale'}
        self.assertEqual({PAGE + '#intro', PAGE + '#choices'}, stale)

    def test_reorder_alone_needs_no_translator_and_invalid_output_is_atomic(self):
        self.prepare()
        source = SOURCE.replace('<!-- l10n:other -->\nKeep this paragraph.\n', '')
        source = source.replace('<!-- l10n:intro -->', '<!-- l10n:other -->\nKeep this paragraph.\n\n<!-- l10n:intro -->')
        self.english.write_bytes(source.encode())
        self.update(lambda *_: self.fail('Reordering needs no translation'))
        self.assertLess(self.target.read_bytes().index(b'l10n:other'), self.target.read_bytes().index(b'l10n:intro'))
        self.english.write_bytes(source.replace('Keep this paragraph.', 'Keep this useful paragraph.').encode())
        before = self.target.read_bytes(), translations.manifest_path(self.root, 'Danish').read_bytes()
        with self.assertRaises(ValueError):
            self.update(lambda *_: {PAGE + '#other': 'New paragraph.\n\nUnrequested extra paragraph.'})
        self.assertEqual(before, (self.target.read_bytes(), translations.manifest_path(self.root, 'Danish').read_bytes()))

    def test_legacy_migration_preserves_words_and_refuses_stale_or_ambiguous_alignment(self):
        # Existing page snapshots certify source correspondence; migration must
        # not quietly bless stale pages or guess a split/merge in another locale.
        import prepare_help
        english = self.root / 'Example/mod/Help/English/start/01-welcome.md'
        target = self.root / 'Example/mod/Help/Danish/start/01-welcome.md'
        translations.snapshot_locale(self.root, 'Danish', ('Example',))
        original = target.read_bytes()
        prepare_help.prepare(self.root, ['Danish'], ('Example',))
        import re
        self.assertEqual(original, re.sub(rb'<!-- l10n:[^\r\n]+ -->\r?\n', b'', target.read_bytes()))
        self.assertEqual([], [i for i in translations.check_locale(self.root, 'Danish', ('Example',), True) if i.severity == 'error'])
        self.update(lambda *_: self.fail('Migration must not retranslate current content'))
        before = target.read_bytes()
        # A new legacy page with mismatched paragraphs must stop before writes.
        self.write('Example/mod/Help/English/new.md', '---\ntitle: New\n---\nOne.\n\nTwo.\n')
        self.write('Example/mod/Help/Danish/new.md', '---\ntitle: Nyt\n---\nEt og to.\n')
        with self.assertRaisesRegex(ValueError, 'alignment'):
            prepare_help.prepare(self.root, ['Danish'], ('Example',))
        self.assertEqual(before, target.read_bytes())
        self.assertNotIn('l10n:', (self.root / 'Example/mod/Help/English/new.md').read_text())

    def test_stale_legacy_page_cannot_be_acknowledged_by_migration(self):
        import prepare_help
        translations.snapshot_locale(self.root, 'Danish', ('Example',))
        path = self.root / 'Example/mod/Help/English/start/01-welcome.md'
        path.write_text(path.read_text().replace('Read ', 'Please read '))
        before = translations.manifest_path(self.root, 'Danish').read_bytes(), path.read_bytes()
        with self.assertRaisesRegex(ValueError, 'stale'):
            prepare_help.prepare(self.root, ['Danish'], ('Example',))
        self.assertEqual(before, (translations.manifest_path(self.root, 'Danish').read_bytes(), path.read_bytes()))

    def test_inline_image_at_a_soft_wrap_does_not_create_an_extra_block(self):
        import help_blocks
        text = 'Read this\n![icon](icon.png) label before continuing.\n'
        marked = help_blocks.annotate('---\ntitle: Help\n---\n' + text)
        self.assertEqual(2, len(help_blocks.parse(marked)))
        self.assertEqual(help_blocks.canonical(text.replace('\n', ' ')), help_blocks.canonical(text))

    def test_duplicate_title_cannot_hide_a_change_to_the_displayed_title(self):
        self.prepare()
        self.english.write_bytes(SOURCE.replace('title: Welcome', 'title: Welcome\ntitle: Changed title').encode())
        issues = translations.check_locale(self.root, 'Danish', ('Example',), True)
        self.assertTrue(any(i.code == 'help-block' for i in issues), issues)
        with self.assertRaises(ValueError):
            self.update(lambda *_: self.fail('Ambiguous frontmatter must stop before translation'))

    def test_migration_checks_malformed_orphan_targets_before_any_writes(self):
        import prepare_help
        translations.snapshot_locale(self.root, 'Danish', ('Example',))
        self.write('Example/mod/Help/Danish/orphan.md', SOURCE.replace('l10n:other', 'l10n:intro'))
        paths = [p for p in self.root.rglob('*') if p.is_file()]
        before = {p: p.read_bytes() for p in paths}
        with self.assertRaises(ValueError):
            prepare_help.prepare(self.root, ['Danish'], ('Example',))
        self.assertEqual(before, {p: p.read_bytes() for p in paths})

    def test_list_style_change_cannot_be_accepted_with_the_old_bullets(self):
        self.prepare()
        self.english.write_bytes(SOURCE.replace('- Fast\n- Slow', '1. Fast\n2. Slow').encode())
        before = self.target.read_bytes(), translations.manifest_path(self.root, 'Danish').read_bytes()
        with self.assertRaises(ValueError):
            self.update(lambda locale, entries: {e['key']: e['current'] for e in entries})
        self.assertEqual(before, (self.target.read_bytes(), translations.manifest_path(self.root, 'Danish').read_bytes()))
        self.update(lambda *_: {PAGE + '#choices': '1. Hurtig\n2. Langsom'})
        self.assertIn(b'1. Hurtig\r\n2. Langsom', self.target.read_bytes())
