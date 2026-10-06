using DragonEngineLibrary;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace KiryuBalanceAddon
{
    public sealed class Mod : DragonEngineMod
    {
        private const string Lab8ModName = "Like A Brawler 8";
        private const string Lab8ModTypeName = "LikeABrawler2.Mod";
        private const string Lab8BattleTypeName = "LikeABrawler2.BrawlerBattleManager";

        private static Mod Instance;

        private static Type Lab8ModType;
        private static Type Lab8BattleType;
        private static FieldInfo GamemodeField;
        private static FieldInfo BattlingField;
        private static FieldInfo PlayerFighterField;

        private static float KiryuAttackPowerMultiplier = 0.05f;
        private static long EligibleSinceMs;
        private static long LastVerifyMs;

        private static bool Applied;
        private static Fighter ModifiedFighter;
        private static uint OriginalAttackPower;
        private static uint OriginalSPAttackPower;
        private static uint TargetAttackPower;
        private static uint TargetSPAttackPower;

        public override void OnModInit()
        {
            base.OnModInit();
            Instance = this;
            LoadSettings();

            try
            {
                File.WriteAllText(
                    Path.Combine(ModPath, "kiryu_balance_loaded.txt"),
                    $"Loaded at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n" +
                    $"Mode=KiryuAttackPower\r\n" +
                    $"KiryuAttackPowerMultiplier={KiryuAttackPowerMultiplier:0.###}\r\n");
            }
            catch { }

            DragonEngine.RegisterJob(Update, DEJob.Update);
            DragonEngine.Log($"Kiryu Balance Addon V6 loaded. Kiryu AP multiplier: {KiryuAttackPowerMultiplier:0.###}");
        }

        public override bool OnModUnload()
        {
            RestoreIfSafe();
            return true;
        }

        private static string SettingsPath =>
            Path.Combine(Instance.ModPath, "kiryu_balance.ini");

        private static void LoadSettings()
        {
            KiryuAttackPowerMultiplier = 0.05f;

            try
            {
                if (!File.Exists(SettingsPath))
                {
                    File.WriteAllText(
                        SettingsPath,
                        "# Kiryu Balance Addon V6\r\n" +
                        "# 0.05 = 5% of Kiryu's normal AttackPower/SPAttackPower.\r\n" +
                        "KiryuAttackPowerMultiplier=0.05\r\n");
                    return;
                }

                foreach (string rawLine in File.ReadAllLines(SettingsPath))
                {
                    string line = rawLine.Trim();
                    if (!line.StartsWith("KiryuAttackPowerMultiplier=", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string value = line.Substring(line.IndexOf('=') + 1).Trim();
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                        KiryuAttackPowerMultiplier = Math.Clamp(parsed, 0.01f, 1.0f);
                }
            }
            catch (Exception ex)
            {
                DragonEngine.Log($"Kiryu Balance Addon V6 settings error: {ex.Message}");
            }
        }

        private static bool EnsureLab8Access()
        {
            if (Lab8ModType != null && Lab8BattleType != null &&
                GamemodeField != null && BattlingField != null && PlayerFighterField != null)
                return true;

            Assembly lab8 = ModManager.GetModMainAssembly(Lab8ModName);
            if (lab8 == null)
                return false;

            Lab8ModType = lab8.GetType(Lab8ModTypeName);
            Lab8BattleType = lab8.GetType(Lab8BattleTypeName);

            if (Lab8ModType == null || Lab8BattleType == null)
                return false;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            GamemodeField = Lab8ModType.GetField("Gamemode", flags);
            BattlingField = Lab8BattleType.GetField("Battling", flags);
            PlayerFighterField = Lab8BattleType.GetField("PlayerFighter", flags);

            return GamemodeField != null && BattlingField != null && PlayerFighterField != null;
        }

        private static bool TryGetEligibleKiryu(out Fighter fighter)
        {
            fighter = default;

            if (!EnsureLab8Access())
                return false;

            object battlingObj = BattlingField.GetValue(null);
            object gamemodeObj = GamemodeField.GetValue(null);

            if (battlingObj is not bool battling || !battling)
                return false;

            if (gamemodeObj is not int gamemode || gamemode != 1)
                return false;

            object fighterObj = PlayerFighterField.GetValue(null);
            if (fighterObj is not Fighter lab8Fighter)
                return false;

            if (!lab8Fighter.IsValid())
                return false;

            if (lab8Fighter.Character.Attributes.player_id != Player.ID.kiryu)
                return false;

            fighter = lab8Fighter;
            return true;
        }

        private static void Update()
        {
            try
            {
                long now = Environment.TickCount64;

                if (!TryGetEligibleKiryu(out Fighter fighter))
                {
                    EligibleSinceMs = 0;
                    RestoreIfSafe();
                    return;
                }

                if (EligibleSinceMs == 0)
                {
                    EligibleSinceMs = now;
                    return;
                }

                // Let LAB8 finish its own battle initialization first.
                if (now - EligibleSinceMs < 1500)
                    return;

                ECBattleStatus status = fighter.GetStatus();

                if (!Applied)
                {
                    OriginalAttackPower = status.AttackPower;
                    OriginalSPAttackPower = status.SPAttackPower;

                    TargetAttackPower = Math.Max(1u, (uint)Math.Round(OriginalAttackPower * (double)KiryuAttackPowerMultiplier));
                    TargetSPAttackPower = Math.Max(1u, (uint)Math.Round(OriginalSPAttackPower * (double)KiryuAttackPowerMultiplier));

                    status.AttackPower = TargetAttackPower;
                    status.SPAttackPower = TargetSPAttackPower;

                    ModifiedFighter = fighter;
                    Applied = true;
                    LastVerifyMs = now;

                    string line =
                        $"[{DateTime.Now:HH:mm:ss}] Kiryu AP {OriginalAttackPower}->{TargetAttackPower}, " +
                        $"SP {OriginalSPAttackPower}->{TargetSPAttackPower}, multiplier={KiryuAttackPowerMultiplier:0.###}";

                    File.AppendAllText(
                        Path.Combine(Instance.ModPath, "kiryu_balance_applied.log"),
                        line + Environment.NewLine);

                    DragonEngine.Log("Kiryu Balance Addon V6: " + line);
                    return;
                }

                // If the game recalculates Kiryu's battle stats, re-apply only once per second.
                if (now - LastVerifyMs >= 1000)
                {
                    LastVerifyMs = now;

                    if (status.AttackPower != TargetAttackPower)
                        status.AttackPower = TargetAttackPower;

                    if (status.SPAttackPower != TargetSPAttackPower)
                        status.SPAttackPower = TargetSPAttackPower;
                }
            }
            catch (Exception ex)
            {
                DragonEngine.Log($"Kiryu Balance Addon V6 update error: {ex}");
            }
        }

        private static void RestoreIfSafe()
        {
            if (!Applied)
                return;

            try
            {
                if (ModifiedFighter.IsValid())
                {
                    ECBattleStatus status = ModifiedFighter.GetStatus();
                    status.AttackPower = OriginalAttackPower;
                    status.SPAttackPower = OriginalSPAttackPower;
                }
            }
            catch
            {
                // Battle fighter may already be gone; its stats will be rebuilt next battle.
            }
            finally
            {
                Applied = false;
                OriginalAttackPower = 0;
                OriginalSPAttackPower = 0;
                TargetAttackPower = 0;
                TargetSPAttackPower = 0;
                ModifiedFighter = default;
            }
        }
    }
}
