using RimShared.Common;

namespace RimShared.Common.Tests;

/// Export file pickers: typed names and directories lose characters the
/// file system rejects, and a usable name plus directory compose into one
/// path with native separators. Exports always gain ".xml" so "*.xml"
/// import lists find them; the exact typed name is kept only on request
/// (WorkRoles' lookup of files saved before ".xml" was added).
public class ExportPathRulesTests
{
    private static string Native(params string[] parts) =>
        string.Join(Path.DirectorySeparatorChar.ToString(), parts);

    [Test]
    public async Task FileNamesLoseSeparatorsDriveColonsAndInvalidChars()
    {
        await Assert.That(ExportPathRules.StripFileName("my/plans:v2\\x\0"))
            .IsEqualTo("myplansv2x");
    }

    [Test]
    public async Task DirectoriesKeepSeparatorsAndDriveColons()
    {
        await Assert.That(ExportPathRules.StripDirectory("C:\\Users/me\0"))
            .IsEqualTo("C:\\Users/me");
    }

    [Test]
    public async Task CleanTextKeepsItsIdentity()
    {
        string name = "Plans.xml";
        string dir = "C:\\Users/me";
        await Assert.That(ExportPathRules.StripFileName(name)).IsSameReferenceAs(name);
        await Assert.That(ExportPathRules.StripDirectory(dir)).IsSameReferenceAs(dir);
    }

    [Test]
    public async Task TypedNamesGainXmlAndUseNativeSeparators()
    {
        string? path = ExportPathRules.Compose("saves/Implanner", " Plans ",
            addExtension: true, out ExportPathProblem problem);
        await Assert.That(problem).IsEqualTo(ExportPathProblem.None);
        await Assert.That(path).IsEqualTo(Native("saves", "Implanner", "Plans.xml"));

        path = ExportPathRules.Compose("saves", "Backup.XML", addExtension: true, out problem);
        await Assert.That(path).IsEqualTo(Native("saves", "Backup.XML"));
    }

    [Test]
    public async Task ExactTypedNameOnRequest()
    {
        string? path = ExportPathRules.Compose("saves", "WorkRoles",
            addExtension: false, out ExportPathProblem problem);
        await Assert.That(problem).IsEqualTo(ExportPathProblem.None);
        await Assert.That(path).IsEqualTo(Native("saves", "WorkRoles"));
    }

    [Test]
    public async Task UnusableNamesAreReportedBeforeTheDirectory()
    {
        foreach (string name in new[] { "", "   ", "sub/Plans", "C:Plans" })
        {
            string? path = ExportPathRules.Compose("", name, addExtension: true,
                out ExportPathProblem problem);
            await Assert.That(path).IsNull();
            await Assert.That(problem).IsEqualTo(ExportPathProblem.BadFileName);
        }
    }

    [Test]
    public async Task UnusableDirectoriesAreReported()
    {
        foreach (string dir in new[] { "", "saves\0" })
        {
            string? path = ExportPathRules.Compose(dir, "Plans", addExtension: true,
                out ExportPathProblem problem);
            await Assert.That(path).IsNull();
            await Assert.That(problem).IsEqualTo(ExportPathProblem.BadDirectory);
        }
    }
}
