using System.Security.Cryptography;

namespace Mingle.API.Rooms;

/// <summary>Short codes people can read out loud: no I, O, 0 or 1 to mix up.</summary>
public static class RoomCodes
{
    public const int Length = 4;
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string New() => RandomNumberGenerator.GetString(Alphabet, Length);

    public static string Normalize(string? code) => (code ?? "").Trim().ToUpperInvariant();

    /// <summary>A secret that proves a player owns a seat when they reconnect.</summary>
    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
}
