using System.Security.Cryptography;
using System.Text;

namespace Mingle.DAL.Seeding;

/// <summary>
/// Name-based UUIDs (RFC 9562, version 5) for starter content: the same seed key ("card:ice-01")
/// always gives the same id, so importing again finds what is already in the database.
/// Content made by players or the AI uses ordinary random ids instead.
/// </summary>
public static class StableId
{
    // A fixed namespace for Mingle's seed content; never change it, or every seeded id changes.
    private static readonly Guid Namespace = new("6f1d3c55-9b8e-4c2a-9a5f-3c1f0e8b7d21");

    public static Guid For(string kind, string key) => Create(Namespace, $"{kind}:{key}");

    private static Guid Create(Guid ns, string name)
    {
        var nsBytes = ns.ToByteArray(bigEndian: true);
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var hash = SHA1.HashData([.. nsBytes, .. nameBytes]);

        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50); // version 5
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC variant
        return new Guid(bytes, bigEndian: true);
    }
}
