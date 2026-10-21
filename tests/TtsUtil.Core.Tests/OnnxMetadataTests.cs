using System.Text;
using TtsUtil.Core.Tts;
using Xunit;

namespace TtsUtil.Core.Tests;

public sealed class OnnxMetadataTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"onnx-{Guid.NewGuid():N}.onnx");

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void ThePairsAreReadFromAroundTheGraph()
    {
        File.WriteAllBytes(_path, Model(("model_type", "kitten-tts"), ("sample_rate", "24000")));

        var pairs = OnnxMetadata.Read(_path);

        Assert.Equal("kitten-tts", pairs["model_type"]);
        Assert.Equal("24000", pairs["sample_rate"]);
    }

    [Fact]
    public void AFileThatIsNotAModelHasNoPairs()
    {
        File.WriteAllBytes(_path, new byte[4096]);

        Assert.Empty(OnnxMetadata.Read(_path));
    }

    [Fact]
    public void AModelCutShortKeepsThePairsBeforeTheCut()
    {
        var whole = Model(("model_type", "vits"));
        File.WriteAllBytes(_path, whole.Concat(new byte[] { 0x3A, 0xFF, 0xFF }).ToArray());

        Assert.Equal("vits", OnnxMetadata.Read(_path)["model_type"]);
    }

    [Fact]
    public void AMissingFileHasNoPairs()
    {
        Assert.Empty(OnnxMetadata.Read(_path + ".gone"));
    }

    /// <summary>A minimal ModelProto: ir_version, a stand in graph, then metadata_props.</summary>
    internal static byte[] Model(params (string Key, string Value)[] pairs)
    {
        var bytes = new List<byte> { 0x08, 0x07 };
        Field(bytes, 7, new byte[300]);

        foreach (var (key, value) in pairs)
        {
            var entry = new List<byte>();
            Field(entry, 1, Encoding.UTF8.GetBytes(key));
            Field(entry, 2, Encoding.UTF8.GetBytes(value));
            Field(bytes, 14, entry.ToArray());
        }

        return bytes.ToArray();
    }

    private static void Field(List<byte> bytes, int number, byte[] body)
    {
        Varint(bytes, (ulong)(number << 3 | 2));
        Varint(bytes, (ulong)body.Length);
        bytes.AddRange(body);
    }

    private static void Varint(List<byte> bytes, ulong value)
    {
        while (value >= 0x80)
        {
            bytes.Add((byte)(value | 0x80));
            value >>= 7;
        }

        bytes.Add((byte)value);
    }
}
