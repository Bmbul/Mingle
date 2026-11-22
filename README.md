# Mingle

A question-and-conversation game for friends and couples, in Armenian, Russian and English.
Phase 1: pass-and-play on one phone, in the browser, offline-capable (PWA).

## Run it

Start PostgreSQL in Docker, then the API (which also serves the app):

```bash
docker compose up -d db
dotnet run --project src/Mingle.Api --launch-profile http
```

Open http://localhost:5243. On first start the API creates the tables and imports the
starter decks from `src/Mingle.DAL/Seed/*.json`.

Or run everything in containers (app on http://localhost:8080):

```bash
docker compose --profile app up --build
```

Tests (the database tests start their own PostgreSQL container, so Docker must be running):

```bash
dotnet test
```

The SDK is pinned to .NET 10 in `global.json`. EF Core migrations:
`dotnet ef migrations add <Name> --project src/Mingle.DAL --startup-project src/Mingle.Api --output-dir Migrations`.

## Layout

| Path | What it is |
| --- | --- |
| `src/Mingle.Engine` | Pure game rules: cards and card kinds, `Game.Apply` reducer, per-player views, card selector, plural rules, deck JSON reader. No I/O. |
| `src/Mingle.Client` | Blazor WebAssembly PWA: Home, New game, Play screens; UI strings in `Resources/Strings*.resx`. Loads decks from `/api/decks` and keeps the last copy for offline play. |
| `src/Mingle.Api` | ASP.NET Core host: serves the client and the HTTP API (`GET /api/decks`). |
| `src/Mingle.DAL` | Data access: EF Core + PostgreSQL. `Entities/` and `Configurations/` hold one file per table; `Migrations/`; `Seeding/`. |
| `src/Mingle.DAL/Seed` | Starter decks (`icebreakers.json`, `couples.json`), 20 cards each in hy/ru/en, imported into an empty database. |
| `tests/Mingle.Engine.Tests` | xUnit tests for the engine and for the seed decks. |
| `tests/Mingle.DAL.Tests` | Mapping tests, plus migration, seeding and constraint tests against a real PostgreSQL container (Testcontainers). |

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
