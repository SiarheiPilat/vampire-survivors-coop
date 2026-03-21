using UnityEngine;

namespace VampireSurvivors.Menu
{
    /// <summary>
    /// Persistent cross-run statistics stored in PlayerPrefs.
    /// Tracks kill count, gold, best survive time, best level, and Orologion pickups.
    ///
    /// Character unlock conditions (simplified from wiki unlock progression):
    ///   antonio, imelda, pasqualina, gennaro, arca, porta, lama — always unlocked (first tier)
    ///   mortaccio    — total kills ≥ 500
    ///   yattacavallo — total kills ≥ 2000
    ///   krochi        — best single-run survive time ≥ 10 min
    ///   dommario      — total gold collected ≥ 1000
    ///   giovanna      — best single-run level ≥ 10
    ///   pugnala       — best single-run level ≥ 15
    ///   poppea        — best single-run survive time ≥ 20 min
    ///   clerici       — best single-run survive time ≥ 25 min
    ///   bianzi        — collected Orologion ≥ 5 times total
    /// </summary>
    public static class PersistentProgress
    {
        const string K_TotalKills     = "stat_total_kills";
        const string K_TotalGold      = "stat_total_gold";
        const string K_BestSurviveMin = "stat_best_survive_min";
        const string K_BestLevel      = "stat_best_level";
        const string K_OrologionCount = "stat_orologion_count";

        public static int  TotalKills     => PlayerPrefs.GetInt(K_TotalKills,     0);
        public static int  TotalGold      => PlayerPrefs.GetInt(K_TotalGold,      0);
        public static int  BestSurviveMin => PlayerPrefs.GetInt(K_BestSurviveMin, 0);
        public static int  BestLevel      => PlayerPrefs.GetInt(K_BestLevel,      0);
        public static int  OrologionCount => PlayerPrefs.GetInt(K_OrologionCount, 0);

        // ── Unlock check ────────────────────────────────────────────────────────

        public static bool IsUnlocked(string charId) => charId switch
        {
            // Tier 1 — always available
            "antonio"     => true,
            "imelda"      => true,
            "pasqualina"  => true,
            "gennaro"     => true,
            "arca"        => true,
            "porta"       => true,
            "lama"        => true,

            // Tier 2 — progression gates
            "mortaccio"    => TotalKills     >= 500,
            "yattacavallo" => TotalKills     >= 2000,
            "krochi"       => BestSurviveMin >= 10,
            "dommario"     => TotalGold      >= 1000,
            "giovanna"     => BestLevel      >= 10,
            "pugnala"      => BestLevel      >= 15,
            "poppea"       => BestSurviveMin >= 20,
            "clerici"      => BestSurviveMin >= 25,
            "bianzi"       => OrologionCount >= 5,

            _ => true,   // future characters default to unlocked
        };

        public static string UnlockHint(string charId) => charId switch
        {
            "mortaccio"    => $"Kill {500   - TotalKills    } more enemies",
            "yattacavallo" => $"Kill {2000  - TotalKills    } more enemies",
            "krochi"       => $"Survive {10  - BestSurviveMin} more minutes",
            "dommario"     => $"Collect {1000 - TotalGold     } more gold",
            "giovanna"     => $"Reach level {10  - BestLevel  }",
            "pugnala"      => $"Reach level {15  - BestLevel  }",
            "poppea"       => $"Survive {20  - BestSurviveMin} more minutes",
            "clerici"      => $"Survive {25  - BestSurviveMin} more minutes",
            "bianzi"       => $"Collect Orologion {5 - OrologionCount} more times",
            _              => "",
        };

        // ── Stat persistence ────────────────────────────────────────────────────

        /// <summary>
        /// Call at the end of every run (game over or victory).
        /// Adds kills + gold to cumulative totals; updates bests.
        /// </summary>
        public static void SaveRunStats(int kills, int gold, float survivedSeconds, int maxLevel)
        {
            PlayerPrefs.SetInt(K_TotalKills,  TotalKills + kills);
            PlayerPrefs.SetInt(K_TotalGold,   TotalGold  + gold);

            int surviveMin = Mathf.FloorToInt(survivedSeconds / 60f);
            if (surviveMin > BestSurviveMin)
                PlayerPrefs.SetInt(K_BestSurviveMin, surviveMin);

            if (maxLevel > BestLevel)
                PlayerPrefs.SetInt(K_BestLevel, maxLevel);

            PlayerPrefs.Save();
        }

        /// <summary>Increments the Orologion pickup counter. Call from OrologionPickupSystem.</summary>
        public static void IncrementOrologion()
        {
            PlayerPrefs.SetInt(K_OrologionCount, OrologionCount + 1);
            PlayerPrefs.Save();
        }

        /// <summary>Wipes all persistent progress (dev/debug helper).</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(K_TotalKills);
            PlayerPrefs.DeleteKey(K_TotalGold);
            PlayerPrefs.DeleteKey(K_BestSurviveMin);
            PlayerPrefs.DeleteKey(K_BestLevel);
            PlayerPrefs.DeleteKey(K_OrologionCount);
            PlayerPrefs.Save();
        }
    }
}
