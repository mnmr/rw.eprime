using RimShared.Tests.Support;
using WorkRoles.Core.Help;

namespace WorkRoles.Core.Tests.Help;

/// <summary>
/// Validates WorkRoles' shipped English help content with the shared
/// <see cref="HelpContentChecks"/>: titles, images, resolvable topic links,
/// and every topic the tour visits.
/// </summary>
public class HelpContentFilesTests
{
    [Test]
    public Task ShippedEnglishTopicsParseWithTitlesAndResolvableLinks() =>
        HelpContentChecks.VerifyEnglish(
            ShippedMod.Path("Help", "English"), HelpTour.Slugs);
}
