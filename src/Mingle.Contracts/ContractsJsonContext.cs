using System.Text.Json;
using System.Text.Json.Serialization;
using Mingle.Engine;

namespace Mingle.Contracts;

/// <summary>
/// Compile-time JSON code for every type sent through the game hub. The published browser app is
/// trimmed, and trimming removes what reflection-based JSON needs (e.g. for ImmutableHashSet),
/// so both the server and the client serialize hub messages with this context.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(JoinResult))]
[JsonSerializable(typeof(RoomView))]
[JsonSerializable(typeof(RoomSettings))]
[JsonSerializable(typeof(Answer))]
[JsonSerializable(typeof(Lang))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool?))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(System.Collections.Immutable.ImmutableArray<string>))]
public sealed partial class ContractsJsonContext : JsonSerializerContext
{
    /// <summary>Puts the generated code first in a serializer's lookup chain.</summary>
    public static void Register(JsonSerializerOptions options) =>
        options.TypeInfoResolverChain.Insert(0, Default);
}
