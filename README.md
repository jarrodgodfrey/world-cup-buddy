# ⚽ World Cup Buddy

A World Cup 2026 sports-betting intelligence and tournament-simulation app, built with **Blazor Server (.NET 8)**.

> ESPN meets a trading terminal.

## Features

- **📊 Edge Finder** — Compares the sharp Pinnacle line against DraftKings, FanDuel & BetMGM, removes the vig, and surfaces +EV bets (Match Winner & Totals). Live data via [The Odds API](https://the-odds-api.com), with a realistic mock dataset as a built-in fallback.
- **🏆 Tournament Simulator** — A Monte Carlo engine that runs 10,000 simulations of the full group stage + knockout bracket across 32 teams using an Elo win model, then maps your team's path to glory.
- **🍺 Watch Party Finder** — Stubbed "Coming Soon" preview.

## Run it

```bash
dotnet run
```

Then open the URL shown in the console (e.g. `https://localhost:7080`).

The app runs immediately on mock data. To use **live odds**, drop your key into
`appsettings.json`:

```json
"OddsApi": {
  "ApiKey": "YOUR_API_KEY_HERE",
  "BaseUrl": "https://api.the-odds-api.com/v4"
}
```

> **Runtime note:** the project targets `net8.0`. If only the .NET 9 runtime is
> installed, `<RollForward>LatestMajor</RollForward>` (set in the `.csproj`) lets
> it run on .NET 9 without a separate .NET 8 install.

## Docker

```bash
docker build -t world-cup-buddy .
docker run -p 8080:8080 world-cup-buddy
```

## How the math works

**Edge Finder**
- American → implied probability: `+odds → 100/(odds+100)`, `-odds → |odds|/(|odds|+100)`
- Pinnacle probabilities are normalized (divided by their sum) to strip the vig → "true" probability
- American → decimal: `+odds → odds/100 + 1`, `-odds → 100/|odds| + 1`
- **EV** = `(Pinnacle true probability × public decimal odds) − 1`
- Flagged **Value** at EV > 3%, **Strong Value** at EV > 7%

**Simulator**
- Elo win probability: `1 / (1 + 10^((ratingB − ratingA) / 400))` with a ±5% upset factor
- Group stage adds draws (closer matchups draw more often); knockouts regress slightly toward 50/50 for extra time
- Top 2 of each group advance into a standard cross-seeded 16-team bracket
