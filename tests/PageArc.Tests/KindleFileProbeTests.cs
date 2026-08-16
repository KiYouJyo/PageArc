using System.Buffers.Binary;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class KindleFileProbeTests
{
    [Theory]
    [InlineData((ushort)0, false)]
    [InlineData((ushort)1, true)]
    [InlineData((ushort)2, true)]
    public async Task Probe_ReadsPalmDocEncryptionAndMobiMagic(ushort encryption, bool expectedEncrypted)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pagearc-probe-{Guid.NewGuid():N}.mobi");
        try
        {
            var bytes = new byte[160];
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(76, 2), 1);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(78, 4), 100);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(112, 2), encryption);
            "MOBI"u8.CopyTo(bytes.AsSpan(116, 4));
            await File.WriteAllBytesAsync(path, bytes);

            var result = await KindleFileProbe.ProbeAsync(path);
            Assert.True(result.IsMobi);
            Assert.Equal(encryption, result.EncryptionType);
            Assert.Equal(expectedEncrypted, result.IsEncrypted);
            Assert.Equal(100, result.FirstRecordOffset);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Probe_RejectsNonMobiPalmDatabase()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pagearc-probe-{Guid.NewGuid():N}.mobi");
        try
        {
            var bytes = new byte[160];
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(76, 2), 1);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(78, 4), 100);
            "TEXT"u8.CopyTo(bytes.AsSpan(116, 4));
            await File.WriteAllBytesAsync(path, bytes);

            var result = await KindleFileProbe.ProbeAsync(path);
            Assert.False(result.IsMobi);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
