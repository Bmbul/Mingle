using System.Collections.Immutable;
using Mingle.Contracts;
using Mingle.Engine;

namespace Mingle.Api.Rooms;

/// <summary>
/// One online room: its seats, its settings and, once started, its game. All changes go through
/// this class under a lock, so concurrent hub calls cannot corrupt the state.
/// </summary>
public sealed class Room(string code, TimeProvider clock)
{
    public const int MaxPlayers = 12;
    public const int MaxNameLength = 24;
    private const int MaxDeckIdLength = 64;

    private readonly Lock gate = new();
    private readonly List<RoomMember> members = [];
    private string? hostId;
    private RoomSettings settings = new(new GameSettings(), []);
    private GameState? game;
    private ImmutableArray<TopicInfo> topicPool = [];
    private Random topicRandom = new(0);
    private bool libraryCards;

    public string Code { get; } = code;
    public RoomStage Stage { get; private set; } = RoomStage.Lobby;
    public DateTimeOffset LastActivity { get; private set; } = clock.GetUtcNow();

    public DeckSource Source
    {
        get { lock (gate) return settings.Source; }
    }

    public bool AnyoneConnected
    {
        get { lock (gate) return members.Any(m => m.Connected); }
    }

    public RoomMember Join(string name, Lang lang, string connectionId)
    {
        lock (gate)
        {
            if (Stage != RoomStage.Lobby) throw new RoomException("The game has already started.");
            if (members.Count >= MaxPlayers) throw new RoomException("The room is full.");

            var trimmed = (name ?? "").Trim();
            if (trimmed.Length == 0) throw new RoomException("Enter your name.");
            if (trimmed.Length > MaxNameLength) trimmed = trimmed[..MaxNameLength];

            var member = new RoomMember(Guid.NewGuid().ToString("N")[..12], trimmed, lang, RoomCodes.NewToken());
            member.Connections.Add(connectionId);
            if (!topicPool.IsEmpty) member.TopicOffer = TopicDeal.Offer(topicPool, topicRandom.Next());
            members.Add(member);
            hostId ??= member.Id;
            Touch();
            return member;
        }
    }

    /// <summary>Puts a returning player back in their seat. False if the seat or token does not match.</summary>
    public bool Rejoin(string playerId, string token, string connectionId)
    {
        lock (gate)
        {
            var member = members.FirstOrDefault(m => m.Id == playerId);
            if (member is null || !string.Equals(member.Token, token, StringComparison.Ordinal)) return false;
            member.Connections.Add(connectionId);
            Touch();
            return true;
        }
    }

    public void Disconnect(string connectionId)
    {
        lock (gate)
        {
            foreach (var member in members) member.Connections.Remove(connectionId);
            Touch();
        }
    }

    /// <summary>Leaving in the lobby frees the seat; during a game the seat stays so the game can go on.</summary>
    public void Leave(string playerId)
    {
        lock (gate)
        {
            var member = Member(playerId);
            member.Connections.Clear();
            if (Stage == RoomStage.Lobby) members.Remove(member);
            if (hostId == playerId)
                hostId = (members.FirstOrDefault(m => m.Connected && m.Id != playerId) ?? members.FirstOrDefault(m => m.Id != playerId))?.Id;
            Touch();
        }
    }

    public void UpdateSettings(string playerId, RoomSettings newSettings)
    {
        lock (gate)
        {
            RequireHost(playerId);
            if (Stage != RoomStage.Lobby) throw new RoomException("Settings can only change before the game starts.");
            settings = newSettings;
            Touch();
        }
    }

    /// <summary>The topics players can be offered; every seat gets its own random offer.</summary>
    public void UseTopics(ImmutableArray<TopicInfo> pool, int seed)
    {
        lock (gate)
        {
            topicPool = pool;
            topicRandom = new Random(seed);
            foreach (var member in members) member.TopicOffer = TopicDeal.Offer(topicPool, topicRandom.Next());
        }
    }

    public void PickTopics(string playerId, IEnumerable<string> codes)
    {
        lock (gate)
        {
            if (Stage != RoomStage.Lobby) throw new RoomException("Topics can only change before the game starts.");
            var member = Member(playerId);
            var picks = (codes ?? []).Distinct(StringComparer.Ordinal).ToImmutableArray();
            if (picks.Length > TopicDeal.MaxPicks) throw new RoomException($"Pick up to {TopicDeal.MaxPicks} topics.");
            // A pick must be in the current offer, or kept from an earlier one.
            if (picks.Any(c => !member.TopicOffer.Contains(c) && !member.TopicPicks.Contains(c)))
                throw new RoomException("Pick from the topics you were offered.");
            member.TopicPicks = picks;
            Touch();
        }
    }

    public void NewTopics(string playerId)
    {
        lock (gate)
        {
            if (Stage != RoomStage.Lobby) throw new RoomException("Topics can only change before the game starts.");
            var member = Member(playerId);
            member.TopicOffer = TopicDeal.Offer(topicPool, topicRandom.Next(), avoid: [.. member.TopicOffer, .. member.TopicPicks]);
            Touch();
        }
    }

    /// <summary>
    /// Host starts an "Our topics" game: the room waits while cards are written. Returns what to write.
    /// Finish with <see cref="FinishWriting"/>, or <see cref="CancelWriting"/> if no cards could be found.
    /// </summary>
    public (ImmutableArray<TopicInfo> Topics, ImmutableArray<Lang> Languages, int PlayerCount) BeginWriting(string playerId)
    {
        lock (gate)
        {
            RequireHost(playerId);
            if (Stage != RoomStage.Lobby) throw new RoomException("The game has already started.");
            if (settings.Source != DeckSource.Topics) throw new RoomException("This room does not play topics.");
            if (members.Count < Game.MinPlayers) throw new RoomException($"At least {Game.MinPlayers} players are needed.");

            var picked = members.SelectMany(m => m.TopicPicks).ToHashSet(StringComparer.Ordinal);
            if (picked.Count == 0) throw new RoomException("Nobody has picked a topic yet.");

            Stage = RoomStage.Writing;
            Touch();
            return ([.. topicPool.Where(t => picked.Contains(t.Code))],
                    [.. members.Select(m => m.Lang).Distinct().Order()],
                    members.Count);
        }
    }

    public void FinishWriting(Deck cards, bool fromLibrary, int seed)
    {
        lock (gate)
        {
            if (Stage != RoomStage.Writing) throw new RoomException("No cards are being written.");
            try
            {
                game = Game.Start(members.Select(m => m.ToPlayer()), TopicDeal.SettingsFor(settings.Game, cards), [cards], seed);
            }
            catch (GameRuleException ex)
            {
                Stage = RoomStage.Lobby;
                throw new RoomException(ex.Message);
            }
            libraryCards = fromLibrary;
            Stage = game.Phase == GamePhase.Ended ? RoomStage.Ended : RoomStage.Playing;
            Touch();
        }
    }

    public void CancelWriting()
    {
        lock (gate)
        {
            if (Stage == RoomStage.Writing) Stage = RoomStage.Lobby;
            Touch();
        }
    }

    public void BringDeck(string playerId, string? deckId)
    {
        lock (gate)
        {
            if (Stage != RoomStage.Lobby) throw new RoomException("Decks can only change before the game starts.");
            if (deckId is { Length: > MaxDeckIdLength }) throw new RoomException("Unknown deck.");
            Member(playerId).DeckId = string.IsNullOrWhiteSpace(deckId) ? null : deckId;
            Touch();
        }
    }

    public void Start(string playerId, IEnumerable<Deck> decks, int seed)
    {
        lock (gate)
        {
            RequireHost(playerId);
            if (Stage != RoomStage.Lobby) throw new RoomException("The game has already started.");
            if (members.Count < Game.MinPlayers) throw new RoomException($"At least {Game.MinPlayers} players are needed.");
            if (settings.Source == DeckSource.Topics) throw new RoomException("Topics games start by writing cards.");

            var available = decks.ToList();
            var gameSettings = settings.Game;
            IEnumerable<Deck> chosen;
            if (settings.Source == DeckSource.Everyone)
            {
                // One entry per player who picked a deck that still exists, so a shared pick counts twice.
                var picks = members
                    .Select(m => available.FirstOrDefault(d => d.Id == m.DeckId))
                    .OfType<Deck>()
                    .ToList();
                if (picks.Count == 0) throw new RoomException("Nobody has picked a deck yet.");
                var mix = DeckMix.FromPicks(picks, [.. members.Select(m => m.Lang).Distinct()], seed);
                if (mix.Cards.IsEmpty) throw new RoomException("The chosen decks have no cards everyone can read.");
                gameSettings = DeckMix.SettingsFor(settings.Game, mix);
                chosen = [mix];
            }
            else
            {
                chosen = settings.DeckIds.IsDefaultOrEmpty ? available : available.Where(d => settings.DeckIds.Contains(d.Id));
            }

            try
            {
                game = Game.Start(members.Select(m => m.ToPlayer()), gameSettings, chosen, seed);
            }
            catch (GameRuleException ex)
            {
                throw new RoomException(ex.Message);
            }
            Stage = game.Phase == GamePhase.Ended ? RoomStage.Ended : RoomStage.Playing;
            Touch();
        }
    }

    /// <summary>Applies one game action for the calling player. The player id always comes from the server.</summary>
    public void Act(string playerId, GameAction action)
    {
        lock (gate)
        {
            if (Stage != RoomStage.Playing || game is null) throw new RoomException("No game is in progress.");
            if (action.PlayerId != playerId) throw new RoomException("Players can only act for themselves.");
            try
            {
                game = Game.Apply(game, action);
            }
            catch (GameRuleException ex)
            {
                throw new RoomException(ex.Message);
            }
            if (game.Phase == GamePhase.Ended) Stage = RoomStage.Ended;
            Touch();
        }
    }

    /// <summary>Every open connection with the view that belongs to it.</summary>
    public IReadOnlyList<(string ConnectionId, RoomView View)> ViewsForEveryone()
    {
        lock (gate)
        {
            return [.. members.SelectMany(m => m.Connections.Select(c => (c, BuildView(m))))];
        }
    }

    public RoomView ViewFor(string playerId)
    {
        lock (gate) return BuildView(Member(playerId));
    }

    private RoomView BuildView(RoomMember viewer)
    {
        var players = members
            .Select(m => new RoomPlayer(m.Id, m.Name, m.Lang, m.Id == hostId, m.Connected, m.DeckId, !m.TopicPicks.IsEmpty))
            .ToImmutableArray();

        GameView? gameView = Stage == RoomStage.Playing && game is not null ? GameViews.ForPlayer(game, viewer.Id) : null;
        GameSummary? summary = Stage == RoomStage.Ended && game is not null ? Summarize(game, viewer.Lang) : null;

        TopicChoice? topics = null;
        if (settings.Source == DeckSource.Topics && Stage == RoomStage.Lobby)
        {
            var byCode = topicPool.ToDictionary(t => t.Code);
            var shown = viewer.TopicOffer.Concat(viewer.TopicPicks.Where(p => !viewer.TopicOffer.Contains(p)));
            topics = new TopicChoice(
                [.. shown.Where(byCode.ContainsKey).Select(c => new TopicOption(c, byCode[c].NameFor(viewer.Lang)))],
                viewer.TopicPicks,
                TopicDeal.MaxPicks);
        }

        return new RoomView(Code, Stage, viewer.Id, players, settings, gameView, summary, topics, libraryCards);
    }

    private static GameSummary Summarize(GameState state, Lang lang) => new(
        state.History.Count(h => !h.Passed),
        state.History.Count(h => h.Passed),
        [.. state.History.Where(h => h.Likes > 0)
            .Select(h => new LikedCard(h.Card.KindId, h.Card.Depth, h.Card.TextFor(lang).Text))]);

    private RoomMember Member(string playerId) =>
        members.FirstOrDefault(m => m.Id == playerId) ?? throw new RoomException("You are not in this room.");

    private void RequireHost(string playerId)
    {
        if (hostId != playerId) throw new RoomException("Only the host can do that.");
    }

    private void Touch() => LastActivity = clock.GetUtcNow();
}
