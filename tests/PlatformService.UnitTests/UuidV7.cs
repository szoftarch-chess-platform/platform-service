namespace PlatformService.UnitTests;

internal static class UuidV7
{
    /// <summary>The Unix time in milliseconds in the first 48 bits of a UUID v7 (RFC 9562).</summary>
    public static long UnixMilliseconds(Guid id)
    {
        var bytes = id.ToByteArray(bigEndian: true);
        return bytes[..6].Aggregate(0L, (value, next) => (value << 8) | next);
    }
}
