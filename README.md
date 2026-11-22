# Mingle

A question-and-conversation game for friends and couples, in Armenian, Russian and English.
Play on one phone (pass-and-play, works offline as a PWA) or online, where everyone joins a room from their own phone.

## Run it

### Everything in Docker (easiest way to test)

```bash
docker compose up --build
```

This starts PostgreSQL and the app. Open http://localhost:8080. To play online with your own phone,
connect it to the same Wi-Fi and open `http://<this computer's IP>:8080` (on a Mac:
`ipconfig getifaddr en0`). Create a room on one device and join it from another.
Stop with `docker compose down` (add `-v` to also delete the database).

### For development

Start only PostgreSQL in Docker, then run the API (which also serves the app) with `dotnet run`:

```bash
docker compose up -d db
dotnet run --project src/Mingle.Api --launch-profile http
```

Open http://localhost:5243. On first start the API creates the tables and imports the
starter decks from `src/Mingle.DAL/Seed/*.json`.

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
| `src/Mingle.Contracts` | Messages shared by the API and the client for online rooms (`IGameHub`, `IGameClient`, `RoomView`). |
| `src/Mingle.Api` | ASP.NET Core host: serves the client, `GET /api/decks`, and the SignalR game hub (`/hubs/game`). `Rooms/` holds the in-memory rooms. |
| `src/Mingle.DAL` | Data access: EF Core + PostgreSQL. `Entities/` and `Configurations/` hold one file per table; `Migrations/`; `Seeding/`. |
| `src/Mingle.DAL/Seed` | Starter decks (`icebreakers.json`, `couples.json`), 20 cards each in hy/ru/en, imported into an empty database. |
| `tests/Mingle.Engine.Tests` | xUnit tests for the engine and for the seed decks. |
| `tests/Mingle.Api.Tests` | Room rules, plus end-to-end hub tests with two real SignalR clients (no database needed). |
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

## Online rooms

- The host creates a room (New online room), gets a 4-letter code and an invite link (`/join/<code>`).
- The server keeps each room and its game in memory and runs the same engine; after every change each
  player receives only their own `RoomView`, so answers stay hidden until the reveal.
- Every call is tied to the caller's seat on the server, so nobody can act for another player.
- A seat survives disconnects: the browser keeps a seat token and rejoins after a reload or a dropped
  connection. Rooms nobody is connected to are removed after two hours of inactivity.
- Rooms live in one server's memory; running several instances would need a shared store such as Redis.

## Deploying

The live app runs on **Azure Container Apps** (Germany West Central, consumption plan, one copy at
most because rooms live in memory, scales to zero when idle). Images go to **GitHub Container
Registry** (`ghcr.io/bmbul/mingle`) and the database is **Neon** Postgres (Frankfurt).

Every push to `main` that passes CI publishes the image and updates the app (the `deploy` job in
`.github/workflows/ci.yml`). GitHub signs in to Azure with OIDC, so no Azure password is stored anywhere.

One-time setup:

1. Push to `main` once so CI publishes `ghcr.io/bmbul/mingle:latest`, then make the package public
   (GitHub → your profile → Packages → `mingle` → Package settings → Change visibility → Public).
2. Create a free Neon project in **AWS Europe Central 1 (Frankfurt)** and copy its *direct*
   (not pooled) connection details.
3. Sign in and run the setup script:
   ```bash
   az login
   ./infra/azure-setup.sh
   ```
4. Store the database connection string as a secret in the app yourself (it never needs to be
   shown to anyone else), then restart it:
   ```bash
   az containerapp secret set --name mingle-app --resource-group mingle-rg \
     --secrets "db-connection=Host=<neon host>;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require"
   az containerapp update --name mingle-app --resource-group mingle-rg --set-env-vars "SECRET_ROTATED=$(date +%s)"
   ```
   On first start the app creates the tables and imports the starter decks.
