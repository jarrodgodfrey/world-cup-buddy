using WorldCupBuddy.Models;

namespace WorldCupBuddy.Services;

/// <summary>
/// Monte Carlo tournament engine. Simulates the group stage + knockout bracket
/// thousands of times using an Elo win model and aggregates per-team advancement
/// probabilities. The hot loop uses flat arrays (no LINQ) to stay well under
/// the 3-second budget for 10,000 iterations.
/// </summary>
public class SimulationService
{
    public const int DefaultIterations = 10_000;

    // Flat team tables, indexed 0..31.
    private readonly string[] _names;
    private readonly int[] _ratings;
    private readonly string[] _flags;
    private readonly string[] _groupOf;

    // Group membership: 8 groups × 4 team indices.
    private readonly int[][] _groups;

    public SimulationService()
    {
        var teams = WorldCupData.Teams;
        _names = teams.Select(t => t.Name).ToArray();
        _ratings = teams.Select(t => t.Rating).ToArray();
        _flags = teams.Select(t => t.Flag).ToArray();
        _groupOf = teams.Select(t => t.Group).ToArray();

        _groups = WorldCupData.GroupNames
            .Select(g => Enumerable.Range(0, _names.Length).Where(i => _groupOf[i] == g).ToArray())
            .ToArray();
    }

    /// <summary>
    /// Head-to-head: simulate a single match <paramref name="teamA"/> vs
    /// <paramref name="teamB"/> <paramref name="iterations"/> times using a
    /// Poisson goals model derived from the Elo gap (neutral venue), and
    /// aggregate a rich set of insights. Returns null if a team isn't found.
    /// </summary>
    public MatchPrediction? PredictMatch(string teamA, string teamB,
        int iterations = DefaultIterations, int? seed = null)
    {
        var a = Array.FindIndex(_names, x => x.Equals(teamA, StringComparison.OrdinalIgnoreCase));
        var b = Array.FindIndex(_names, x => x.Equals(teamB, StringComparison.OrdinalIgnoreCase));
        if (a < 0 || b < 0 || a == b) return null;

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();

        // Expected goals from the Elo gap around a World-Cup-ish 2.6 total.
        const double baseTotal = 2.6;
        var supremacy = Math.Clamp((_ratings[a] - _ratings[b]) / 250.0, -2.2, 2.2);
        var lambdaA = Math.Max(0.2, baseTotal / 2.0 + supremacy / 2.0);
        var lambdaB = Math.Max(0.2, baseTotal / 2.0 - supremacy / 2.0);

        // Penalty-shootout edge for the stronger side when a winner is required.
        var pAelo = 1.0 / (1.0 + Math.Pow(10, (_ratings[b] - _ratings[a]) / 400.0));
        var pAdecider = 0.5 + (pAelo - 0.5) * 0.5; // regress toward a coin flip

        int winA = 0, draw = 0, winB = 0, advA = 0, advB = 0;
        int over25 = 0, btts = 0, csA = 0, csB = 0;
        long goalsA = 0, goalsB = 0;

        const int cap = 5; // scoreline grid 0..5 each (6+ lumped into 5)
        var grid = new int[(cap + 1) * (cap + 1)];

        for (var i = 0; i < iterations; i++)
        {
            var gA = SamplePoisson(lambdaA, rng);
            var gB = SamplePoisson(lambdaB, rng);
            goalsA += gA;
            goalsB += gB;

            if (gA > gB) { winA++; advA++; }
            else if (gB > gA) { winB++; advB++; }
            else { draw++; if (rng.NextDouble() < pAdecider) advA++; else advB++; }

            if (gA + gB >= 3) over25++;
            if (gA >= 1 && gB >= 1) btts++;
            if (gB == 0) csA++;
            if (gA == 0) csB++;

            grid[Math.Min(gA, cap) * (cap + 1) + Math.Min(gB, cap)]++;
        }

        double Pct(int c) => 100.0 * c / iterations;

        var top = new List<ScorelineProb>();
        for (var x = 0; x <= cap; x++)
        for (var y = 0; y <= cap; y++)
        {
            var c = grid[x * (cap + 1) + y];
            if (c == 0) continue;
            var sx = x == cap ? "5+" : x.ToString();
            var sy = y == cap ? "5+" : y.ToString();
            top.Add(new ScorelineProb { Score = $"{sx}–{sy}", Pct = Pct(c) });
        }
        top = top.OrderByDescending(s => s.Pct).Take(6).ToList();

        var pred = new MatchPrediction
        {
            TeamA = _names[a], FlagA = _flags[a], RatingA = _ratings[a],
            TeamB = _names[b], FlagB = _flags[b], RatingB = _ratings[b],
            Iterations = iterations,
            RunAt = DateTime.Now,
            WinPctA = Pct(winA), DrawPct = Pct(draw), WinPctB = Pct(winB),
            AdvancePctA = Pct(advA), AdvancePctB = Pct(advB),
            AvgGoalsA = (double)goalsA / iterations,
            AvgGoalsB = (double)goalsB / iterations,
            Over25Pct = Pct(over25), Under25Pct = 100.0 - Pct(over25),
            BttsPct = Pct(btts),
            CleanSheetPctA = Pct(csA), CleanSheetPctB = Pct(csB),
            TopScorelines = top,
            FairOddsA = FairAmerican(Pct(winA) / 100.0),
            FairOddsDraw = FairAmerican(Pct(draw) / 100.0),
            FairOddsB = FairAmerican(Pct(winB) / 100.0),
        };
        pred.Narrative = BuildMatchNarrative(pred);
        return pred;
    }

    private static int SamplePoisson(double lambda, Random rng)
    {
        // Knuth's algorithm — fine for the small lambdas here.
        var l = Math.Exp(-lambda);
        var k = 0;
        var p = 1.0;
        do { k++; p *= rng.NextDouble(); } while (p > l);
        return k - 1;
    }

    private static string FairAmerican(double prob)
    {
        if (prob <= 0.0001) return "+9999";
        if (prob >= 0.9999) return "-9999";
        var dec = 1.0 / prob;
        return dec >= 2.0
            ? $"+{Math.Round((dec - 1.0) * 100.0):0}"
            : $"-{Math.Round(100.0 / (dec - 1.0)):0}";
    }

    private static string BuildMatchNarrative(MatchPrediction p)
    {
        var favName = p.WinPctA >= p.WinPctB ? p.TeamA : p.TeamB;
        var favPct = Math.Max(p.WinPctA, p.WinPctB);
        var margin = Math.Abs(p.WinPctA - p.WinPctB);

        string framing = margin < 8 ? "a genuine coin-flip"
            : margin < 22 ? "a close call"
            : margin < 40 ? "a clear lean"
            : "a heavy mismatch";

        var drawLine = p.DrawPct >= 26 ? " Expect a tight, low-event game — draws are very live." : "";
        return $"It's {framing}: {favName} win in regulation {favPct:0.0}% of the time " +
               $"(draw {p.DrawPct:0.0}%). Projected goals {p.AvgGoalsA:0.0}–{p.AvgGoalsB:0.0}, " +
               $"with {(p.Over25Pct >= 50 ? "Over" : "Under")} 2.5 favored.{drawLine}";
    }

    public BracketSimulation Run(string? selectedTeam, int iterations = DefaultIterations, int? seed = null)
    {
        var n = _names.Length;
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();

        // Aggregate counters per team.
        var reachR16 = new int[n];
        var reachQf = new int[n];
        var reachSf = new int[n];
        var reachFinal = new int[n];
        var wins = new int[n];

        var selectedIdx = selectedTeam is null
            ? -1
            : Array.FindIndex(_names, x => x.Equals(selectedTeam, StringComparison.OrdinalIgnoreCase));

        // Opponent frequency tables for the selected team's path (R16/QF/SF/Final).
        var oppR16 = new int[n];
        var oppQf = new int[n];
        var oppSf = new int[n];
        var oppFinal = new int[n];

        // Reusable per-iteration scratch buffers.
        var winners = new int[8];   // group winners
        var runners = new int[8];   // group runners-up
        var pts = new int[4];
        var tiebreak = new double[4];
        var r16 = new int[16];
        var qf = new int[8];
        var sf = new int[4];
        var fin = new int[2];

        for (var iter = 0; iter < iterations; iter++)
        {
            // ---- Group stage ----
            for (var g = 0; g < 8; g++)
            {
                var grp = _groups[g];
                for (var k = 0; k < 4; k++) { pts[k] = 0; tiebreak[k] = rng.NextDouble(); }

                // Round robin: 6 matches.
                for (var a = 0; a < 4; a++)
                for (var b = a + 1; b < 4; b++)
                {
                    var res = GroupResult(_ratings[grp[a]], _ratings[grp[b]], rng);
                    if (res > 0) pts[a] += 3;
                    else if (res < 0) pts[b] += 3;
                    else { pts[a] += 1; pts[b] += 1; }
                }

                // Rank the 4 teams by points, then random tiebreak (proxy for GD).
                var order = new[] { 0, 1, 2, 3 };
                Array.Sort(order, (x, y) =>
                {
                    if (pts[y] != pts[x]) return pts[y] - pts[x];
                    return tiebreak[y].CompareTo(tiebreak[x]);
                });

                winners[g] = grp[order[0]];
                runners[g] = grp[order[1]];
            }

            // Everyone who finished top-2 reached the Round of 16.
            for (var g = 0; g < 8; g++)
            {
                reachR16[winners[g]]++;
                reachR16[runners[g]]++;
            }

            // ---- Round of 16 (standard cross-group seeding) ----
            // WA RB | WC RD | WE RF | WG RH | WB RA | WD RC | WF RE | WH RG
            r16[0] = winners[0]; r16[1] = runners[1];
            r16[2] = winners[2]; r16[3] = runners[3];
            r16[4] = winners[4]; r16[5] = runners[5];
            r16[6] = winners[6]; r16[7] = runners[7];
            r16[8] = winners[1]; r16[9] = runners[0];
            r16[10] = winners[3]; r16[11] = runners[2];
            r16[12] = winners[5]; r16[13] = runners[4];
            r16[14] = winners[7]; r16[15] = runners[6];

            if (selectedIdx >= 0)
                RecordOpponent(r16, selectedIdx, oppR16);

            // R16 -> QF
            for (var i = 0; i < 8; i++)
                qf[i] = KnockoutWinner(r16[2 * i], r16[2 * i + 1], rng);
            for (var i = 0; i < 8; i++) reachQf[qf[i]]++;

            if (selectedIdx >= 0)
                RecordOpponent(qf, selectedIdx, oppQf);

            // QF -> SF
            for (var i = 0; i < 4; i++)
                sf[i] = KnockoutWinner(qf[2 * i], qf[2 * i + 1], rng);
            for (var i = 0; i < 4; i++) reachSf[sf[i]]++;

            if (selectedIdx >= 0)
                RecordOpponent(sf, selectedIdx, oppSf);

            // SF -> Final
            fin[0] = KnockoutWinner(sf[0], sf[1], rng);
            fin[1] = KnockoutWinner(sf[2], sf[3], rng);
            reachFinal[fin[0]]++;
            reachFinal[fin[1]]++;

            if (selectedIdx >= 0)
                RecordOpponent(fin, selectedIdx, oppFinal);

            // Final -> Champion
            var champ = KnockoutWinner(fin[0], fin[1], rng);
            wins[champ]++;
        }

        // ---- Aggregate ----
        var results = new List<TeamSimResult>(n);
        for (var i = 0; i < n; i++)
        {
            double Pct(int c) => 100.0 * c / iterations;
            results.Add(new TeamSimResult
            {
                Team = _names[i],
                Flag = _flags[i],
                Group = _groupOf[i],
                Rating = _ratings[i],
                GroupExitPct = 100.0 - Pct(reachR16[i]),
                R16Pct = Pct(reachR16[i]),
                QfPct = Pct(reachQf[i]),
                SfPct = Pct(reachSf[i]),
                FinalPct = Pct(reachFinal[i]),
                WinPct = Pct(wins[i]),
            });
        }
        results.Sort((a, b) => b.WinPct.CompareTo(a.WinPct));

        var sim = new BracketSimulation
        {
            Teams = results,
            Iterations = iterations,
            RunAt = DateTime.Now,
        };

        if (selectedIdx >= 0)
            sim.SelectedPath = BuildPath(selectedIdx, iterations,
                reachR16, reachQf, reachSf, reachFinal, wins,
                oppR16, oppQf, oppSf, oppFinal);

        return sim;
    }

    // ---- Match models -----------------------------------------------------

    /// <summary>Elo win probability for A over B, with a small upset wobble.</summary>
    private static double WinProb(int ratingA, int ratingB, Random rng)
    {
        var p = 1.0 / (1.0 + Math.Pow(10, (ratingB - ratingA) / 400.0));
        p += rng.NextDouble() * 0.10 - 0.05; // ±5% upset factor
        return p < 0.05 ? 0.05 : p > 0.95 ? 0.95 : p;
    }

    /// <summary>Group match result: +1 A wins, -1 B wins, 0 draw.</summary>
    private static int GroupResult(int ratingA, int ratingB, Random rng)
    {
        var pA = WinProb(ratingA, ratingB, rng);

        // Closer matchups draw more often; ~26% peak, tapering with mismatch.
        var drawProb = 0.30 - 0.40 * Math.Abs(pA - 0.5);
        if (drawProb < 0.06) drawProb = 0.06;

        var pAWin = pA * (1.0 - drawProb);
        var roll = rng.NextDouble();
        if (roll < pAWin) return 1;
        if (roll < pAWin + drawProb) return 0;
        return -1;
    }

    /// <summary>Knockout match: no draws. Slight regression toward 50/50 for extra time.</summary>
    private int KnockoutWinner(int teamA, int teamB, Random rng)
    {
        var p = WinProb(_ratings[teamA], _ratings[teamB], rng);
        p = 0.5 + (p - 0.5) * 0.90; // extra-time regression
        return rng.NextDouble() < p ? teamA : teamB;
    }

    // ---- Path tracking ----------------------------------------------------

    /// <summary>If the selected team is in this round's slot array, tally its opponent.</summary>
    private static void RecordOpponent(int[] round, int team, int[] oppCounts)
    {
        for (var i = 0; i < round.Length; i++)
        {
            if (round[i] != team) continue;
            var partner = (i % 2 == 0) ? round[i + 1] : round[i - 1];
            oppCounts[partner]++;
            return;
        }
    }

    private TeamPath BuildPath(
        int idx, int iterations,
        int[] reachR16, int[] reachQf, int[] reachSf, int[] reachFinal, int[] wins,
        int[] oppR16, int[] oppQf, int[] oppSf, int[] oppFinal)
    {
        double Pct(int c) => 100.0 * c / iterations;

        (string name, string flag) Likely(int[] counts)
        {
            var best = -1; var bestCount = 0;
            for (var i = 0; i < counts.Length; i++)
                if (counts[i] > bestCount) { bestCount = counts[i]; best = i; }
            return best < 0 ? ("—", "") : (_names[best], _flags[best]);
        }

        var r16Opp = Likely(oppR16);
        var qfOpp = Likely(oppQf);
        var sfOpp = Likely(oppSf);
        var finOpp = Likely(oppFinal);

        var nodes = new List<PathNode>
        {
            new() { Round = "R16",      ReachProb = Pct(reachR16[idx]),   LikelyOpponent = r16Opp.name, LikelyOpponentFlag = r16Opp.flag },
            new() { Round = "QF",       ReachProb = Pct(reachQf[idx]),    LikelyOpponent = qfOpp.name,  LikelyOpponentFlag = qfOpp.flag },
            new() { Round = "SF",       ReachProb = Pct(reachSf[idx]),    LikelyOpponent = sfOpp.name,  LikelyOpponentFlag = sfOpp.flag },
            new() { Round = "Final",    ReachProb = Pct(reachFinal[idx]), LikelyOpponent = finOpp.name, LikelyOpponentFlag = finOpp.flag },
            new() { Round = "Champion", ReachProb = Pct(wins[idx]),       LikelyOpponent = "—",         LikelyOpponentFlag = "" },
        };

        return new TeamPath
        {
            Team = _names[idx],
            Flag = _flags[idx],
            Rating = _ratings[idx],
            Group = _groupOf[idx],
            Nodes = nodes,
            Narrative = BuildNarrative(_names[idx], _groupOf[idx], nodes, r16Opp.name, finOpp.name)
        };
    }

    private static string BuildNarrative(
        string team, string group, List<PathNode> nodes, string r16Opp, string finalOpp)
    {
        var winPct = nodes[^1].ReachProb;
        var finalPct = nodes[3].ReachProb;
        var r16Pct = nodes[0].ReachProb;

        string outlook = winPct switch
        {
            >= 20 => "a genuine title favorite",
            >= 10 => "a serious contender",
            >= 4 => "a live dark horse",
            >= 1 => "a plucky outsider",
            _ => "a long shot looking for magic"
        };

        var opener = r16Pct >= 75
            ? $"{team} are expected to cruise out of Group {group}"
            : r16Pct >= 50
                ? $"{team} should fight their way out of Group {group}"
                : $"{team} face an uphill battle just to escape Group {group}";

        var r16Line = string.IsNullOrEmpty(r16Opp) || r16Opp == "—"
            ? "a knockout opener that could go any number of ways"
            : $"a likely Round of 16 date with {r16Opp}";

        var finalLine = finalPct >= 5
            ? $" Reach the final ({finalPct:0.0}% of the time){(string.IsNullOrEmpty(finalOpp) || finalOpp == "—" ? "" : $", where {finalOpp} loom as the probable obstacle")},"
            : " The path to the final is steep,";

        return $"{opener}, where the bracket points toward {r16Line}. " +
               $"Across 10,000 simulations they emerge as {outlook}, lifting the trophy {winPct:0.0}% of the time." +
               $"{finalLine} and every upset along the way reshapes the run to glory.";
    }
}
