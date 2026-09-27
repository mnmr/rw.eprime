using RimShared.Common;

namespace RimShared.Common.Tests;

/// Export file names always end in ".xml" so import pickers that list
/// "*.xml" can find them; a name that already ends that way is kept as typed.
public class XmlFileNameTests
{
    [Test]
    public async Task NamesGainTheXmlExtensionOnlyWhenMissing()
    {
        await Assert.That(XmlFileName.WithExtension("MyLayout")).IsEqualTo("MyLayout.xml");
        await Assert.That(XmlFileName.WithExtension("Readouts.xml")).IsEqualTo("Readouts.xml");
        await Assert.That(XmlFileName.WithExtension("Backup.XML")).IsEqualTo("Backup.XML");
        await Assert.That(XmlFileName.WithExtension("notes.txt")).IsEqualTo("notes.txt.xml");
    }
}
