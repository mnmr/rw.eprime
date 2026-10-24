using RimShared.Common.Help;

namespace RimShared.Common.Tests;

public class LocalizationFallbackTests
{
    [Test]
    public async Task SelectedLanguageFindsCanonicalAndLegacyFoldersWithoutChangingRegionalVariant()
    {
        await Assert.That(LanguageFolders.Candidates("ChineseSimplified (简体中文)", "ChineseSimplified"))
            .IsEquivalentTo(new[] { "ChineseSimplified (简体中文)", "ChineseSimplified" });
        await Assert.That(LanguageFolders.Candidates("PortugueseBrazilian (Português Brasileiro)"))
            .IsEquivalentTo(new[] { "PortugueseBrazilian (Português Brasileiro)", "PortugueseBrazilian" });
        await Assert.That(LanguageFolders.Candidates("SpanishLatin"))
            .IsEquivalentTo(new[] { "SpanishLatin" });
        await Assert.That(LanguageFolders.Candidates(null)).IsEquivalentTo(new[] { "English" });
    }

    [Test]
    public async Task CommunityDefFieldsAndWholeListsPreventFallbackFromReplacingTheirText()
    {
        var coverage = new TranslationCoverage();
        coverage.Add("WR_Builder.label", false);
        coverage.Add("WR_Builder.notes", true);
        coverage.Add("WR_Doctor.notes.1", false);

        await Assert.That(coverage.Covers("WR_Builder.label", false)).IsTrue();
        await Assert.That(coverage.Covers("WR_Builder.LABEL", false)).IsTrue();
        await Assert.That(coverage.Covers("WR_Builder.description", false)).IsFalse();
        await Assert.That(coverage.Covers("WR_Builder.notes.0", false)).IsTrue();
        await Assert.That(coverage.Covers("WR_Doctor.notes", true)).IsTrue();
        await Assert.That(coverage.Covers("WR_Doctor.notes.0", false)).IsFalse();
        await Assert.That(coverage.Covers("WR_Builder.notesExtra.0", false)).IsFalse();
    }
}
