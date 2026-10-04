using RimShared.Tests.Support;

namespace EPrimeReadouts.Core.Tests;

/// <summary>
/// Validates Readouts' shipped English help content with the shared
/// <see cref="HelpContentChecks"/>: titles, images, and resolvable topic
/// links.
/// </summary>
public class HelpContentFilesTests
{
    [Test]
    public Task ShippedEnglishTopicsParseWithTitlesAndResolvableLinks() =>
        HelpContentChecks.VerifyEnglish(HelpContentChecks.FindEnglishRoot());
}
