/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Text;

namespace TtsUtil.Core.Tts;

/// <summary>Reads the metadata pairs an ONNX model carries, such as sherpa's model_type.
/// Walks the top level only and seeks past the graph.</summary>
public static class OnnxMetadata
{
    /// <summary>ModelProto.metadata_props, a repeated StringStringEntryProto.</summary>
    private const int MetadataField = 14;

    private const int Varint = 0;
    private const int Fixed64 = 1;
    private const int LengthDelimited = 2;
    private const int Fixed32 = 5;

    /// <summary>The pairs in the model, or an empty set for an unreadable or non ONNX file.</summary>
    public static IReadOnlyDictionary<string, string> Read(string modelPath)
    {
        var pairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var stream = new FileStream(modelPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096);

            while (stream.Position < stream.Length)
            {
                var tag = ReadVarint(stream);
                var field = (int)(tag >> 3);
                var wire = (int)(tag & 7);

                // Field zero is never written, so the file is not a model.
                if (field == 0) break;

                if (wire == LengthDelimited)
                {
                    var length = (long)ReadVarint(stream);
                    if (length < 0 || stream.Position + length > stream.Length) break;

                    if (field == MetadataField && length <= 1 << 20)
                    {
                        var entry = new byte[length];
                        stream.ReadExactly(entry);
                        ReadEntry(entry, pairs);
                    }
                    else
                    {
                        stream.Seek(length, SeekOrigin.Current);
                    }
                }
                else if (!Skip(stream, wire))
                {
                    break;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            // Whatever was read before the damage is still worth having.
        }

        return pairs;
    }

    /// <summary>One StringStringEntryProto: key is field 1, value is field 2.</summary>
    private static void ReadEntry(byte[] entry, Dictionary<string, string> pairs)
    {
        using var stream = new MemoryStream(entry, writable: false);
        string? key = null;
        string? value = null;

        while (stream.Position < stream.Length)
        {
            var tag = ReadVarint(stream);
            var wire = (int)(tag & 7);

            if (wire != LengthDelimited)
            {
                if (!Skip(stream, wire)) return;
                continue;
            }

            var length = (int)ReadVarint(stream);
            if (length < 0 || stream.Position + length > stream.Length) return;

            var text = Encoding.UTF8.GetString(entry, (int)stream.Position, length);
            stream.Seek(length, SeekOrigin.Current);

            switch (tag >> 3)
            {
                case 1: key = text; break;
                case 2: value = text; break;
            }
        }

        if (!string.IsNullOrEmpty(key)) pairs[key!] = value ?? string.Empty;
    }

    private static bool Skip(Stream stream, int wire)
    {
        switch (wire)
        {
            case Varint:
                ReadVarint(stream);
                return true;
            case Fixed64:
                stream.Seek(8, SeekOrigin.Current);
                return true;
            case Fixed32:
                stream.Seek(4, SeekOrigin.Current);
                return true;
            default:
                return false;
        }
    }

    private static ulong ReadVarint(Stream stream)
    {
        ulong value = 0;

        for (var shift = 0; shift < 64; shift += 7)
        {
            var next = stream.ReadByte();
            if (next < 0) throw new FormatException("The model ends in the middle of a number.");

            value |= (ulong)(next & 0x7F) << shift;
            if ((next & 0x80) == 0) return value;
        }

        throw new FormatException("A number in the model runs on too long.");
    }
}
