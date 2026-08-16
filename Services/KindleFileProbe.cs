using System.Buffers.Binary;

namespace PageArc.Services;

public sealed record KindleFileProbeResult(bool IsMobi, ushort EncryptionType, long FirstRecordOffset)
{
    public bool IsEncrypted => EncryptionType != 0;
}

public static class KindleFileProbe
{
    private const int PdbRecordTableOffset = 78;
    private const int PalmDocEncryptionOffset = 12;
    private const int MobiMagicOffset = 16;

    public static async Task<KindleFileProbeResult> ProbeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("Kindle source file not found.", filePath);
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        if (stream.Length < 96) return new KindleFileProbeResult(false, 0, 0);

        var pdb = new byte[96];
        await ReadExactlyAsync(stream, pdb, cancellationToken);
        var firstRecordOffset = (long)BinaryPrimitives.ReadUInt32BigEndian(pdb.AsSpan(PdbRecordTableOffset, 4));
        if (firstRecordOffset + MobiMagicOffset + 4 > stream.Length)
            return new KindleFileProbeResult(false, 0, firstRecordOffset);

        stream.Position = firstRecordOffset;
        var palmDoc = new byte[MobiMagicOffset + 4];
        await ReadExactlyAsync(stream, palmDoc, cancellationToken);
        var encryption = BinaryPrimitives.ReadUInt16BigEndian(palmDoc.AsSpan(PalmDocEncryptionOffset, 2));
        var isMobi = palmDoc.AsSpan(MobiMagicOffset, 4).SequenceEqual("MOBI"u8);
        return new KindleFileProbeResult(isMobi, encryption, firstRecordOffset);
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer[read..], cancellationToken);
            if (count == 0) throw new EndOfStreamException();
            read += count;
        }
    }
}
