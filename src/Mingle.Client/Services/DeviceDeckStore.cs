using Microsoft.JSInterop;

namespace Mingle.Client.Services;

/// <summary>A deck saved on this device: always its id, and the edit key if this device made it.</summary>
public sealed record DeviceDeck(string Id, string? EditKey)
{
    public bool CanEdit => !string.IsNullOrEmpty(EditKey);
}

/// <summary>
/// The decks this device made or added from a link, kept in the browser. There are no accounts yet,
/// so the edit key here is the only way to change a deck: clearing the browser's data loses it.
/// </summary>
public sealed class DeviceDeckStore(IJSRuntime js)
{
    private const string StorageKey = "mingle.myDecks";
    private List<DeviceDeck>? decks;

    public async Task<IReadOnlyList<DeviceDeck>> ListAsync()
    {
        if (decks is null)
        {
            var saved = await js.InvokeAsync<string?>("mingle.get", StorageKey) ?? "";
            // One deck per line: "id|editKey" (the key is empty for decks added from a link).
            decks = [.. saved.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split('|'))
                .Where(parts => parts[0].Length > 0)
                .Select(parts => new DeviceDeck(parts[0], parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null))
                .DistinctBy(d => d.Id)];
        }
        return decks;
    }

    public async Task<DeviceDeck?> FindAsync(string id) => (await ListAsync()).FirstOrDefault(d => d.Id == id);

    /// <summary>Adds a deck, or upgrades a view-only entry with its edit key.</summary>
    public async Task AddAsync(string id, string? editKey)
    {
        var list = (await ListAsync()).ToList();
        var existing = list.FindIndex(d => d.Id == id);
        if (existing >= 0) list[existing] = list[existing] with { EditKey = editKey ?? list[existing].EditKey };
        else list.Insert(0, new DeviceDeck(id, editKey));
        await SaveAsync(list);
    }

    public async Task RemoveAsync(string id) => await SaveAsync([.. (await ListAsync()).Where(d => d.Id != id)]);

    private async Task SaveAsync(List<DeviceDeck> list)
    {
        decks = list;
        await js.InvokeVoidAsync("mingle.set", StorageKey, string.Join("\n", list.Select(d => $"{d.Id}|{d.EditKey}")));
    }
}
