# Mingle

A question-and-conversation game for friends and couples, in Armenian, Russian and English.
Phase 1: pass-and-play on one phone, in the browser, offline-capable (PWA).

## Run it

```bash
dotnet run --project src/Mingle.Client --launch-profile http
```

Then open http://localhost:5273. Tests:

```bash
dotnet test
```

The SDK is pinned to .NET 10 in `global.json`.

## Layout

| Path | What it is |
| --- | --- |
| `src/Mingle.Engine` | Pure game rules: cards and card kinds, `Game.Apply` reducer, per-player views, card selector, plural rules, deck JSON reader. No I/O. |
| `src/Mingle.Client` | Blazor WebAssembly PWA: Home, New game, Play screens; UI strings in `Resources/Strings*.resx`. |
| `src/Mingle.Client/wwwroot/data` | Starter decks (`icebreakers.json`, `couples.json`), 20 cards each in hy/ru/en. |
| `tests/Mingle.Engine.Tests` | xUnit tests for the engine and for the shipped decks. |

## How the game works

- `Game.Start(players, settings, decks, seed)` builds the card pool (selected kinds, depth range, languages every player reads) and deals the first card.
- `Game.Apply(state, action)` handles `SubmitAnswer`, `PassCard`, `NextCard`, `ReactToCard` and `EndGame`, and throws `GameRuleException` for anything against the rules.
- Card kinds are data: a response type (free, choice, player vote, scale) and an audience (follows turn style, everyone, about the turn player). Talk mode turns free answers into spoken ones.
- `GameViews.ForPlayer` / `ForTable` never expose other players' answers before the reveal.
- Answers are not kept after a card; only the card, who passed, and likes are recorded.

## Writing cards

Deck files are validated by `DeckJson` and by `StarterContentTests`: every card needs all three languages,
`wyr` cards need exactly two options per language, and starter cards avoid `{player}` placeholders because
Armenian and Russian change names by grammatical case.
