using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace RimWorld.Automation.Core;

[DataContract]
public sealed class Command
{
    [DataMember] public string command = "";
    [DataMember] public int x;
    [DataMember] public int y;
    [DataMember] public int x2;
    [DataMember] public int y2;
    [DataMember] public int button;
    [DataMember] public int notches;
    [DataMember] public string text = "";
    [DataMember] public bool cursor;
}

[DataContract]
public sealed class Reply
{
    [DataMember] public bool ok;
    [DataMember] public string error = "";
    [DataMember] public int processId;
    [DataMember] public bool ready;
    [DataMember] public int width;
    [DataMember] public int height;
    [DataMember] public float uiScale;
    [DataMember] public int x;
    [DataMember] public int y;
    [DataMember] public string png = "";
    // Transferred from the main thread, converted to base64 by the pipe worker.
    public byte[]? image;
}

public static class Wire
{
    public static Command Read(Stream stream)
    {
        byte[] header = ReadExactly(stream, 4);
        int size = BitConverter.ToInt32(header, 0);
        if (size < 2 || size > 65536) throw new InvalidDataException("Invalid command size.");
        using var data = new MemoryStream(ReadExactly(stream, size));
        return (Command?)new DataContractJsonSerializer(typeof(Command)).ReadObject(data)
            ?? throw new SerializationException("A command object is required.");
    }

    public static void Write(Stream stream, Reply reply)
    {
        if (reply.image != null) reply.png = Convert.ToBase64String(reply.image);
        using var data = new MemoryStream();
        new DataContractJsonSerializer(typeof(Reply)).WriteObject(data, reply);
        byte[] bytes = data.ToArray();
        byte[] header = BitConverter.GetBytes(bytes.Length);
        stream.Write(header, 0, header.Length);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    private static byte[] ReadExactly(Stream stream, int size)
    {
        var data = new byte[size];
        int read = 0;
        while (read < size)
        {
            int count = stream.Read(data, read, size - read);
            if (count == 0) throw new EndOfStreamException();
            read += count;
        }
        return data;
    }
}

