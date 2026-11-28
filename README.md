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
dotnet run --project src/Mingle.API --launch-profile http
```

Open http://localhost:5243. On start the API runs the migrations: the schema ones and the
content ones (`ContentMigration` in `src/Mingle.DAL/Migrations`), which add the topics, stories
and starter decks. Content rows have stable ids and the SQL is an upsert, so a later content
migration that lists a card again updates it. To add or fix built-in cards, add a new content
migration; never edit one that has been deployed.

Tests (the database tests start their own PostgreSQL container, so Docker must be running):

```bash
dotnet test
```

The SDK is pinned to .NET 10 in `global.json`. EF Core migrations:
`dotnet ef migrations add <Name> --project src/Mingle.DAL --startup-project src/Mingle.API --output-dir Migrations`.

## Layout

| Path | What it is |
| --- | --- |
| `src/Mingle.Engine` | Pure game rules: cards and card kinds, `Game.Apply` reducer, per-player views, card selector, plural rules, deck JSON reader. No I/O. |
| `src/Mingle.Client` | Blazor WebAssembly PWA: Home, New game, Play screens; UI strings in `Resources/Strings*.resx`. Loads decks from `/api/decks` and keeps the last copy for offline play. |
| `src/Mingle.Contracts` | Messages shared by the API and the client for online rooms (`IGameHub`, `IGameClient`, `RoomView`). |
| `src/Mingle.API` | ASP.NET Core host: serves the client, `Controllers/` (decks API), the SignalR game hub (`/hubs/game`), `Rooms/` (in-memory rooms) and `Ai/` (card writing). |
| `src/Mingle.DAL` | Data access: EF Core + PostgreSQL. `Entities/` and `Configurations/` hold one file per table (card_kinds, topics, decks, cards, card_options, card_topics, deck_cards); texts are stored as one column per language (`text_hy`, `text_ru`, `text_en`). `Migrations/` holds the schema migrations and the content migrations (built-in topics, stories and decks; `Content/` has their base class and SQL). |
| `tests/Mingle.Engine.Tests` | xUnit tests for the engine and for the seed decks. |
| `tests/Mingle.API.Tests` | Room rules, plus end-to-end hub tests with two real SignalR clients (no database needed). |
| `tests/Mingle.DAL.Tests` | Content checks (without a database), plus migration, content-update and constraint tests against a real PostgreSQL container (Testcontainers). |

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

## Game types (online)

The host picks where the cards come from when creating a room:

- **Our topics** (default): each player gets 10 random topics, picks up to 3 (nobody sees who picked
  what), and Claude writes 20 cards about all picks in every language at the table. Without an API key,
  on an error or past the daily limit, library cards on the picked topics are used instead.
- **Everyone's decks**: each player picks a deck in the lobby; 10 random cards from each are shuffled together.
- **Decks I pick**: the host chooses the decks.

AI settings live in the `Ai` configuration section (`Ai__Model`, `Ai__Effort`, `Ai__MaxGenerationsPerDay`,
defaults: `claude-haiku-5-5`, `low`, 100). The key comes from `ANTHROPIC_API_KEY` (or `Ai__ApiKey`).

## Deck editor

Anyone can make a deck (My decks → New deck) without an account. The server returns an edit key once;
only its SHA-256 hash is stored, and the browser keeps the key (`DeviceDeckStore`), so only that device
can change or delete the deck. A deck's id is its share link (`/d/<id>`): anyone with it can view the
deck, add it to their list and play it, in pass-and-play or online rooms.

- `GET /api/decks`: starter decks. `GET /api/decks/{id}`: any deck.
- `POST /api/decks` (deck file JSON) returns `{ id, editKey }`; `PUT` and `DELETE /api/decks/{id}`
  need the `X-Edit-Key` header. Writes are rate-limited per visitor.
- Editing a starter card inside your deck saves your own copy (`forked_from_id`); the starter card stays.
- Clearing the browser's data loses the edit key; accounts can claim decks later.

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
5. Optional, for AI-written cards in "Our topics": store the Anthropic API key the same way and point
   the app at it:
   ```bash
   az containerapp secret set --name mingle-app --resource-group mingle-rg --secrets "anthropic-key=<your key>"
   az containerapp update --name mingle-app --resource-group mingle-rg \
     --set-env-vars "ANTHROPIC_API_KEY=secretref:anthropic-key"
   ```
