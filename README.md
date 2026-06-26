# ⚽ World Cup Buddy

A World Cup 2026 **betting-intelligence, simulation, and fan** app — built with **Blazor Server (.NET 8)**.

> ESPN meets a trading terminal.

It pairs live sportsbook odds and Monte Carlo simulation with AI-powered personalization: tell it about yourself by **voice**, and the whole app tailors itself to your team.

---

## Features

- **📊 Edge Finder** — Compares the sharp **Pinnacle** line against **DraftKings, FanDuel & BetMGM**, removes the vig, and surfaces **+EV bets** (Match Winner & Totals). Odds cells are **clickable** and link out to the sportsbook. Live data via [The Odds API](https://the-odds-api.com); falls back to a realistic mock dataset. Includes a "How does this work?" explainer.
- **🏆 Tournament Simulator** — A Monte Carlo engine that runs **10,000 simulations** of the full group stage + knockout bracket across 32 teams (Elo win model), then maps your team's **path to glory** with charts and a sortable probability table.
- **⚔️ Match Predictor** — Pick any two teams and run **10,000 head-to-head simulations** using a **Poisson** goals model. Outputs Win/Draw/Win % with **fair odds**, a knockout "who advances" view, average goals, Over/Under 2.5, BTTS, clean sheets, and the most-likely scorelines.
- **🍺 Watch Parties** — Uses **Claude** to find real bars & restaurants in your city to watch the game (tailored to your vibe), and shows each on an embedded **Google Map**.
- **🛒 Kit Locker** — Shop your team's jerseys, hats & gear. Real products (image, price, buy-link) via **SerpApi Google Shopping** when a key is set; keyless Google-Shopping search-link tiles otherwise.
- **🎙️ Build Profile (voice)** — Speak once (Web Speech API); **Claude** extracts your favorite team, players, location, watch vibe, and risk tolerance. The profile then drives the whole app — Edge Finder jumps to your team, the Simulator auto-runs it, Watch Parties uses your city, Kit Locker stocks your gear. Persists across reloads via `localStorage`.
- **Splash screen** — Animated hero with all six features, soccer-ball particles, and an auto-transition countdown.

---

## Tech stack

| Layer | Technology |
|---|---|
| Framework | **.NET 8**, ASP.NET Core, **Blazor Server** (global Interactive Server render mode; SignalR/WebSocket circuit) |
| UI | **MudBlazor 8.15.0** (tables, charts) + hand-written CSS (custom properties, no Bootstrap); Inter font |
| JS interop | Web Speech API (voice), `localStorage` (profile persistence), splash countdown |
| AI | **Anthropic Claude API** (`claude-opus-4-8`) via raw HTTP `/v1/messages` with forced tool-use — profile extraction & venue finder |
| Data APIs | **The Odds API** (odds), **SerpApi** (Google Shopping), **Google Maps** keyless embed |
| Domain | Elo model, Monte Carlo (tournament + Poisson head-to-head), EV/vig math |
| Secrets | `appsettings.json` + **.NET user-secrets** (local), Azure app settings (prod) |
| Hosting | **Azure App Service** (Linux, .NET 8); **Dockerfile** included |

> **Runtime note:** the project targets `net8.0`. If only the .NET 9 runtime is installed, `<RollForward>LatestMajor</RollForward>` (in the `.csproj`) lets it run on .NET 9 without a separate .NET 8 install.

---

## Run it

```bash
dotnet run
```

Then open the URL shown in the console (e.g. `https://localhost:7080`).

The app runs immediately with **graceful fallbacks** — every external integration works without a key (mock odds, keyword profile parsing, sample venues, search-link gear tiles). Add keys to unlock the live/AI features.

### Configuration & secrets

Keys are read from configuration (`appsettings.json` keys shown below). **Don't commit real keys** — store them in **.NET user-secrets** locally:

```bash
dotnet user-secrets set "OddsApi:ApiKey"      "<the-odds-api-key>"
dotnet user-secrets set "Anthropic:ApiKey"    "<anthropic-api-key>"
dotnet user-secrets set "Shopping:SerpApiKey" "<serpapi-key>"
```

| Setting | Enables | Get a key |
|---|---|---|
| `OddsApi:ApiKey` | Live Edge Finder odds | [the-odds-api.com](https://the-odds-api.com) (free tier) |
| `Anthropic:ApiKey` | Voice profile + Watch Party venue search | [console.anthropic.com](https://console.anthropic.com) |
| `Shopping:SerpApiKey` | Real Kit Locker products | [serpapi.com](https://serpapi.com) (free tier ~100/mo) |

> **Browser support:** voice capture uses the Web Speech API (Chrome/Edge) and requires HTTPS (localhost is fine); a text fallback is provided.

---

## Project structure

```
Components/
  Layout/      MainLayout, NavMenu, SplashLayout
  Pages/       Home (splash), EdgeFinder, Simulator, MatchPredictor,
               Social (Watch Parties), KitLocker, Profile
  Shared/      MatchCard, BracketView, TeamPathCard, BellCurve, InfoModal, LoadingSpinner
Services/      OddsService, SimulationService, ProfileService,
               SocialService, ShopService, ProfileState
Models/        Match, OddsComparison, EVBet, TeamSimResult, BracketSimulation,
               MatchPrediction, UserProfile, SocialVenue, ShopProduct, WorldCupData
wwwroot/       css/app.css, app.js, images/
WcbTheme.cs    MudBlazor theme + chart palette
```

---

## How the math works

**Edge Finder**
- American → implied probability: `+odds → 100/(odds+100)`, `−odds → |odds|/(|odds|+100)`
- Pinnacle probabilities are normalized (divided by their sum) to strip the vig → "true" probability
- American → decimal: `+odds → odds/100 + 1`, `−odds → 100/|odds| + 1`
- **EV** = `(Pinnacle true probability × public decimal odds) − 1`
- Flagged **Value** at EV > 3%, **Strong Value** at EV > 7%

**Tournament Simulator**
- Elo win probability: `1 / (1 + 10^((ratingB − ratingA) / 400))` with a ±5% upset factor
- Group stage allows draws (closer matchups draw more often); knockouts regress slightly toward 50/50 for extra time
- Top 2 of each group advance into a standard cross-seeded 16-team bracket; 10,000 runs aggregate each team's round-by-round odds

**Match Predictor**
- Expected goals from the Elo gap around a ~2.6 total; per-team **Poisson** goal sampling (neutral venue)
- 10,000 sims aggregate Win/Draw/Win, knockout advancement (ET/penalties edge to the stronger side), goals markets, and scoreline distribution
- "Fair" American odds are derived from the simulated probabilities

---

## Deployment (Azure App Service)

A live instance was provisioned in resource group `rg-world-cup-buddy` (Linux, .NET 8). Publish + zip-deploy:

```bash
dotnet publish -c Release -o ./publish
(cd publish && zip -r -q ../app.zip .)
az webapp deploy -g rg-world-cup-buddy -n world-cup-buddy --type zip --src-path app.zip
```

Set production secrets as app settings (double-underscore delimiter):

```bash
az webapp config appsettings set -g rg-world-cup-buddy -n world-cup-buddy --settings \
  OddsApi__ApiKey="..." Anthropic__ApiKey="..." Shopping__SerpApiKey="..."
```

WebSockets must be enabled on the Web App (required for the Blazor Server circuit).

### Docker

```bash
docker build -t world-cup-buddy .
docker run -p 8080:8080 world-cup-buddy
```

---

## Notes & limitations

- **Fallbacks everywhere:** the app never shows a blank screen — missing keys/quota gracefully degrade to mock/sample/search-link data.
- **AI venue & product data** comes from Claude's knowledge / SerpApi, not a live places database — usually accurate but verify before relying on it.
- **Bet deep-linking:** odds cells link to each sportsbook's soccer page; true bet-slip deep links require sportsbook affiliate/partner access.
- **Simulations** are Elo/Poisson approximations (no live form, injuries, or lineups) — a fun, directionally-sound projection, not a betting model.
