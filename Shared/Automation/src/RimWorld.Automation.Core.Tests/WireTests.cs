using System.Runtime.Serialization;
using System.Text;
using RimWorld.Automation.Core;

public class WireTests
{
    [Test]
    public async Task MalformedFramesCannotPublishACommand()
    {
        using var nullCommand = Frame("null");
        await Assert.That(() => Wire.Read(nullCommand)).Throws<SerializationException>();
        using var truncated = Frame("{\"command\":\"click\"}");
        truncated.SetLength(truncated.Length - 1);
        await Assert.That(() => Wire.Read(truncated)).Throws<EndOfStreamException>();
        using var oversized = new MemoryStream(BitConverter.GetBytes(65537));
        await Assert.That(() => Wire.Read(oversized)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task FramedTextPreservesUnicodeAndLiteralControlNotation()
    {
        using var frame = Frame("{\"command\":\"type\",\"text\":\"^aStål{SPACE}#\"}");
        var command = Wire.Read(frame);
        await Assert.That(command.command).IsEqualTo("type");
        await Assert.That(command.text).IsEqualTo("^aStål{SPACE}#");
    }

    private static MemoryStream Frame(string json)
    {
        var data = Encoding.UTF8.GetBytes(json);
        var stream = new MemoryStream();
        stream.Write(BitConverter.GetBytes(data.Length));
        stream.Write(data);
        stream.Position = 0;
        return stream;
    }
}
