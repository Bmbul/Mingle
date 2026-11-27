using System.Collections.Immutable;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using Mingle.Contracts;
using Mingle.Engine;

namespace Mingle.Client.Services;

/// <summary>
/// This device's connection to an online room. Seats are saved in the browser, so a reload or a
/// dropped connection puts the player straight back in their seat.
/// </summary>
public sealed class RoomClient(NavigationManager nav, IJSRuntime js) : IAsyncDisposable
{
    private sealed record Seat(string PlayerId, string Token)
    {
        public override string ToString() => $"{PlayerId}|{Token}";

        public static Seat? Parse(string? text) =>
            text?.Split('|') is [var id, var token] && id.Length > 0 && token.Length > 0 ? new Seat(id, token) : null;
    }

    private HubConnection? hub;
    private string? currentCode;

    public RoomView? View { get; private set; }
    public bool Reconnecting { get; private set; }
    public string? Error { get; private set; }

    public event Action? Changed;

    public async Task<JoinResult> CreateRoom(string name, Lang lang, RoomSettings settings)
    {
        var result = await (await Hub()).InvokeAsync<JoinResult>(nameof(IGameHub.CreateRoom), name, lang, settings);
        await Remember(result);
        return result;
    }

    public async Task<JoinResult> JoinRoom(string code, string name, Lang lang)
    {
        var result = await (await Hub()).InvokeAsync<JoinResult>(nameof(IGameHub.JoinRoom), code, name, lang);
        await Remember(result);
        return result;
    }

    /// <summary>Takes back this device's saved seat in the room, if there is one.</summary>
    public async Task<bool> RejoinSaved(string code)
    {
        code = code.ToUpperInvariant();
        if (View?.Code == code) return true;

        var saved = await js.InvokeAsync<string?>("mingle.get", SeatKey(code));
        if (Seat.Parse(saved) is not { } seat) return false;

        var ok = await (await Hub()).InvokeAsync<bool>(nameof(IGameHub.Rejoin), code, seat.PlayerId, seat.Token);
        if (ok) currentCode = code;
        else await js.InvokeVoidAsync("mingle.remove", SeatKey(code));
        return ok;
    }

    public Task UpdateSettings(RoomSettings settings) => Send(nameof(IGameHub.UpdateSettings), settings);
    public Task BringDeck(string? deckId) => Send(nameof(IGameHub.BringDeck), deckId);
    public Task PickTopics(ImmutableArray<string> codes) => Send(nameof(IGameHub.PickTopics), codes);
    public Task StartGame() => Send(nameof(IGameHub.StartGame));
    public Task SubmitAnswer(Answer answer) => Send(nameof(IGameHub.SubmitAnswer), answer);
    public Task Pass() => Send(nameof(IGameHub.Pass));
    public Task Next() => Send(nameof(IGameHub.Next));
    public Task React(bool? liked) => Send(nameof(IGameHub.React), liked);
    public Task EndGame() => Send(nameof(IGameHub.EndGame));
    public Task WriteFacts(ImmutableArray<string> facts, int marked) => Send(nameof(IGameHub.WriteFacts), facts, marked);
    public Task SwapPrompt() => Send(nameof(IGameHub.SwapPrompt));
    public Task BeginFacts() => Send(nameof(IGameHub.BeginFacts));
    public Task VoteFact(string choice) => Send(nameof(IGameHub.VoteFact), choice);
    public Task RevealFact() => Send(nameof(IGameHub.RevealFact));
    public Task NextFact() => Send(nameof(IGameHub.NextFact));
    public Task WriteGuess(string text) => Send(nameof(IGameHub.WriteGuess), text);
    public Task RevealGuesses() => Send(nameof(IGameHub.RevealGuesses));
    public Task MarkGuess(string playerId, bool right) => Send(nameof(IGameHub.MarkGuess), playerId, right);
    public Task NextGuess() => Send(nameof(IGameHub.NextGuess));
    public Task AnswerOdd(string text) => Send(nameof(IGameHub.AnswerOdd), text);
    public Task StartOddVote() => Send(nameof(IGameHub.StartOddVote));
    public Task VoteOdd(string playerId) => Send(nameof(IGameHub.VoteOdd), playerId);
    public Task RevealOdd() => Send(nameof(IGameHub.RevealOdd));
    public Task NextOdd() => Send(nameof(IGameHub.NextOdd));
    public Task AnswerMatch(string text) => Send(nameof(IGameHub.AnswerMatch), text);
    public Task RevealMatch() => Send(nameof(IGameHub.RevealMatch));
    public Task JoinMatch(string key, string intoKey) => Send(nameof(IGameHub.JoinMatch), key, intoKey);
    public Task NextMatch() => Send(nameof(IGameHub.NextMatch));

    public async Task Leave()
    {
        if (currentCode is { } code)
        {
            await Send(nameof(IGameHub.Leave));
            await js.InvokeVoidAsync("mingle.remove", SeatKey(code));
        }
        currentCode = null;
        View = null;
        Changed?.Invoke();
    }

    public void ClearError()
    {
        Error = null;
        Changed?.Invoke();
    }

    private async Task Send(string method, params object?[] args)
    {
        try
        {
            Error = null;
            await (await Hub()).InvokeCoreAsync(method, args);
        }
        catch (HubException ex)
        {
            // The server sends "An unexpected error occurred invoking 'X' on the server. HubException: <reason>".
            var reason = ex.Message;
            var marker = reason.IndexOf("HubException: ", StringComparison.Ordinal);
            Error = marker >= 0 ? reason[(marker + "HubException: ".Length)..] : reason;
            Changed?.Invoke();
        }
    }

    private async Task<HubConnection> Hub()
    {
        if (hub is not null)
        {
            if (hub.State == HubConnectionState.Disconnected) await hub.StartAsync();
            return hub;
        }

        hub = new HubConnectionBuilder()
            .WithUrl(nav.ToAbsoluteUri(HubRoutes.Game.TrimStart('/')))
            .WithAutomaticReconnect()
            .AddJsonProtocol(o => ContractsJsonContext.Register(o.PayloadSerializerOptions))
            .Build();

        hub.On<RoomView>(nameof(IGameClient.RoomUpdated), view =>
        {
            View = view;
            Changed?.Invoke();
        });
        hub.Reconnecting += _ =>
        {
            Reconnecting = true;
            Changed?.Invoke();
            return Task.CompletedTask;
        };
        hub.Reconnected += async _ =>
        {
            Reconnecting = false;
            // A new connection has no seat yet; claim it again with the saved token.
            if (currentCode is { } code)
            {
                View = null;
                await RejoinSaved(code);
            }
            Changed?.Invoke();
        };

        await hub.StartAsync();
        return hub;
    }

    private async Task Remember(JoinResult result)
    {
        currentCode = result.RoomCode;
        await js.InvokeVoidAsync("mingle.set", SeatKey(result.RoomCode), new Seat(result.PlayerId, result.Token).ToString());
    }

    private static string SeatKey(string code) => $"mingle.seat.{code}";

    public async ValueTask DisposeAsync()
    {
        if (hub is not null) await hub.DisposeAsync();
    }
}
