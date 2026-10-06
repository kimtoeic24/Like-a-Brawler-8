using DragonEngineLibrary;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace KiryuBalanceAddon
{
    public sealed class Mod : DragonEngineMod
    {
        private const string Lab8ModName = "Like A Brawler 8";
        private const string Lab8ModTypeName = "LikeABrawler2.Mod";

        private static readonly HashSet<uint> ScaledEnemyUids = new();
        private static Mod Instance;
        private static float EnemyHpMultiplier = 3.0f;
        private static bool ErrorLogged;

        public override void OnModInit()
        {
            base.OnModInit();

            Instance = this;
            LoadSettings();

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
                        "# Only applies while Like A Brawler 8 reports realtime mode and the main player is Kiryu.\r\n" +
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
                        EnemyHpMultiplier = Math.Clamp(parsed, 1.0f, 10.0f);
                }
            }
            catch (Exception ex)
            {
                EnemyHpMultiplier = 3.0f;
                DragonEngine.Log($"Kiryu Balance Addon settings error; using 3.0x. {ex.Message}");
            }
        }

        private static bool IsKiryuRealtime()
        {
            Fighter player = FighterManager.GetPlayer();

            if (!player.IsValid())
                return false;

            if (player.Character.Attributes.player_id != Player.ID.kiryu)
                return false;

            Assembly lab8Assembly = ModManager.GetModMainAssembly(Lab8ModName);
            if (lab8Assembly == null)
                return false;

            Type lab8ModType = lab8Assembly.GetType(Lab8ModTypeName);
            FieldInfo gamemodeField = lab8ModType?.GetField("Gamemode", BindingFlags.Public | BindingFlags.Static);

            if (gamemodeField == null)
                return false;

            object value = gamemodeField.GetValue(null);
            return value is int gamemode && gamemode == 1;
        }

        private static void Update()
        {
            try
            {
                Fighter[] enemies = FighterManager.GetAllEnemies();

                // No live enemy fighters means the battle is over.
                if (enemies.Length == 0)
                {
                    ScaledEnemyUids.Clear();
                    ErrorLogged = false;
                    return;
                }

                if (!IsKiryuRealtime())
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

                    long newMax = Math.Max(oldMax, (long)Math.Round(oldMax * (double)EnemyHpMultiplier));
                    long newCurrent = Math.Max(1, (long)Math.Round(oldCurrent * ((double)newMax / oldMax)));

                    // Mark only immediately before the write. If the status was not ready,
                    // this enemy will be retried on a later frame instead of being skipped forever.
                    ScaledEnemyUids.Add(uid);

                    status.SetHPMax(newMax);
                    status.CurrentHP = newCurrent;

                    DragonEngine.Log($"Kiryu Balance Addon: enemy {uid} HP {oldCurrent}/{oldMax} -> {newCurrent}/{newMax}");
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