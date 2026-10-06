using DragonEngineLibrary;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace KiryuBalanceAddon
{
    public sealed class Mod : DragonEngineMod
    {
        private const string Lab8ModName = "Like A Brawler 8";
        private const string Lab8ModTypeName = "LikeABrawler2.Mod";

        private static readonly HashSet<uint> ScaledEnemyUids = new();
        private static readonly Dictionary<uint, EnemyObservation> EnemyObservations = new();

        private sealed class EnemyObservation
        {
            public long LastMaxHp;
            public long LastChangeMs;
        }
        private static Mod Instance;
        private static float EnemyHpMultiplier = 3.0f;
        private static bool ErrorLogged;
        private static long LastDiagnosticMs;

        public override void OnModInit()
        {
            base.OnModInit();

            Instance = this;
            LoadSettings();

            try
            {
                string loadedPath = Path.Combine(ModPath, "kiryu_balance_loaded.txt");
                File.WriteAllText(
                    loadedPath,
                    $"Loaded at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n" +
                    $"ModPath={ModPath}\r\n" +
                    $"EnemyHpMultiplier={EnemyHpMultiplier:0.##}\r\n");
            }
            catch (Exception ex)
            {
                DragonEngine.Log($"Kiryu Balance Addon: failed to write load marker: {ex}");
            }

            DragonEngine.RegisterJob(Update, DEJob.Update);
            DragonEngine.Log($"Kiryu Balance Addon loaded. Enemy HP multiplier: {EnemyHpMultiplier:0.##}x");
        }

        private static string SettingsPath =>
            Path.Combine(Instance.ModPath, "kiryu_balance.ini");

        private static void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    File.WriteAllText(SettingsPath,
                        "# Kiryu Balance Addon\r\n" +
                        "# Applies only when the current player is Kiryu. No LAB8 internal state is read.\r\n" +
                        "EnemyHpMultiplier=3.0\r\n");
                    EnemyHpMultiplier = 3.0f;
                    return;
                }

                foreach (string rawLine in File.ReadAllLines(SettingsPath))
                {
                    string line = rawLine.Trim();

                    if (line.Length == 0 || line.StartsWith("#") || !line.StartsWith("EnemyHpMultiplier=", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string value = line.Substring(line.IndexOf('=') + 1).Trim();

                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                        EnemyHpMultiplier = Math.Clamp(parsed, 1.0f, 100.0f);
                }
            }
            catch (Exception ex)
            {
                EnemyHpMultiplier = 3.0f;
                DragonEngine.Log($"Kiryu Balance Addon settings error; using 3.0x. {ex.Message}");
            }
        }

        private static bool IsKiryu()
        {
            Fighter player = FighterManager.GetPlayer();

            if (!player.IsValid())
                return false;

            return player.Character.Attributes.player_id == Player.ID.kiryu;
        }

        private static string AppliedLogPath =>
            Path.Combine(Instance.ModPath, "kiryu_balance_applied.log");

        private static void Update()
        {
            try
            {
                Fighter[] enemies = FighterManager.GetAllEnemies();

                long diagNow = Environment.TickCount64;
                if (diagNow - LastDiagnosticMs >= 1000)
                {
                    LastDiagnosticMs = diagNow;

                    Fighter player = FighterManager.GetPlayer();
                    string playerState = "invalid";

                    if (player.IsValid())
                        playerState = player.Character.Attributes.player_id.ToString();

                    try
                    {
                        File.AppendAllText(
                            Path.Combine(Instance.ModPath, "kiryu_balance_state.log"),
                            $"[{DateTime.Now:HH:mm:ss}] player={playerState}, enemies={enemies.Length}, scaled={ScaledEnemyUids.Count}, multiplier={EnemyHpMultiplier:0.##}" +
                            Environment.NewLine);
                    }
                    catch (Exception ex)
                    {
                        if (!ErrorLogged)
                        {
                            DragonEngine.Log($"Kiryu Balance Addon diagnostic log error: {ex}");
                            ErrorLogged = true;
                        }
                    }
                }

                // No live enemy fighters means the battle is over.
                if (enemies.Length == 0)
                {
                    ScaledEnemyUids.Clear();
                    EnemyObservations.Clear();
                    ErrorLogged = false;
                    return;
                }

                if (!IsKiryu())
                    return;

                foreach (Fighter enemy in enemies)
                {
                    if (!enemy.IsValid() || enemy.IsDead())
                        continue;

                    uint uid = enemy.Character.UID;

                    if (ScaledEnemyUids.Contains(uid))
                        continue;

                    ECBattleStatus status = enemy.GetStatus();
                    long oldMax = status.MaxHP;
                    long oldCurrent = status.CurrentHP;

                    if (oldMax <= 0 || oldCurrent <= 0)
                        continue;

                    long now = Environment.TickCount64;

                    if (!EnemyObservations.TryGetValue(uid, out EnemyObservation observation))
                    {
                        EnemyObservations[uid] = new EnemyObservation
                        {
                            LastMaxHp = oldMax,
                            LastChangeMs = now
                        };
                        continue;
                    }

                    // LAB8 may rebalance HP during fighter initialization.
                    // Wait until max HP has remained unchanged for at least 750 ms
                    // so this add-on runs after LAB8's own initialization instead of before it.
                    if (observation.LastMaxHp != oldMax)
                    {
                        observation.LastMaxHp = oldMax;
                        observation.LastChangeMs = now;
                        continue;
                    }

                    if (now - observation.LastChangeMs < 750)
                        continue;

                    long newMax = Math.Max(oldMax, (long)Math.Round(oldMax * (double)EnemyHpMultiplier));
                    long newCurrent = Math.Max(1, (long)Math.Round(oldCurrent * (double)EnemyHpMultiplier));

                    ScaledEnemyUids.Add(uid);
                    EnemyObservations.Remove(uid);

                    status.SetHPMax(newMax);
                    status.CurrentHP = newCurrent;

                    string applied = $"[{DateTime.Now:HH:mm:ss}] enemy {uid}: HP {oldCurrent}/{oldMax} -> {newCurrent}/{newMax} ({EnemyHpMultiplier:0.##}x)";
                    DragonEngine.Log("Kiryu Balance Addon V4: " + applied);
                    File.AppendAllText(AppliedLogPath, applied + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // Safety first: never let this optional add-on crash the game loop.
                if (!ErrorLogged)
                {
                    DragonEngine.Log($"Kiryu Balance Addon update skipped after error: {ex}");
                    ErrorLogged = true;
                }
            }
        }
    }
}