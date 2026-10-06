using System;
using System.Globalization;
using System.IO;

namespace LikeABrawler2
{
    internal static class IniSettings
    {
        public static bool ShowPlayerDamage = true;
        public static bool ShowEnemyDamage = true;
        public static int IsIchibanRealtime = 1;
        public static int IsKiryuRealtime = 1;
        public static float PartyMemberSkillMPReqRatio = 0.5f;
        public static int PartyMemberSkillChance = 20;
        public static float PartyMemberSkillTime = 30;
        public static float KiryuDamageMultiplier = 0.25f;
        // Heat gained per successful hit, expressed as a percent of max Heat.
        // Example: 1.25 = 1.25% of max Heat per hit.
        public static float HeatGainPerHitPercent = 1.25f;
        // Seconds between each 1-point Heat drain tick while in Extreme Heat.
        public static float ExtremeHeatDrainInterval = 0.75f;

        public static bool AllowResurgenceMusic = false;

        public static string IniPath()
        {
            return Path.Combine(Mod.Instance.ModPath, "mod_settings.ini");
        }

        public static void Read()
        {
            Ini ini = new Ini(IniPath());
            ShowPlayerDamage = ini.GetValue("ShowPlayerDamage", "Display") == "1";
            ShowEnemyDamage = ini.GetValue("ShowEnemyDamage", "Display") == "1";
            IsIchibanRealtime = int.Parse(ini.GetValue("IchibanRealtime", "Gameplay", "1"));
            IsKiryuRealtime = int.Parse(ini.GetValue("KiryuRealtime", "Gameplay", "1"));
            PartyMemberSkillMPReqRatio = float.Parse(ini.GetValue("PartyMemberSkillMPRequirementRatio", "Party", "0.5"), CultureInfo.InvariantCulture);
            PartyMemberSkillChance = int.Parse(ini.GetValue("PartyMemberSkillChance", "Party", "20"));
            PartyMemberSkillTime = float.Parse(ini.GetValue("PartyMemberSkillTime", "Party", "30"), CultureInfo.InvariantCulture);
            AllowResurgenceMusic = ini.GetValue("PlayDragonResurgenceMusic", "Gameplay", "1") == "1";
            KiryuDamageMultiplier = float.Parse(ini.GetValue("KiryuDamageMultiplier", "Gameplay", "0.25"), CultureInfo.InvariantCulture);
            KiryuDamageMultiplier = Math.Clamp(KiryuDamageMultiplier, 0.05f, 1.0f);
            HeatGainPerHitPercent = float.Parse(ini.GetValue("HeatGainPerHitPercent", "Gameplay", "1.25"), CultureInfo.InvariantCulture);
            HeatGainPerHitPercent = Math.Clamp(HeatGainPerHitPercent, 0.0f, 100.0f);
            ExtremeHeatDrainInterval = float.Parse(ini.GetValue("ExtremeHeatDrainInterval", "Gameplay", "0.75"), CultureInfo.InvariantCulture);
            ExtremeHeatDrainInterval = Math.Clamp(ExtremeHeatDrainInterval, 0.05f, 10.0f);
            BrawlerUIManager.UseClassicGauge = ini.GetValue("UseClassicGauge", "Gameplay", "0") == "1";
        }

        public static void Write()
        {
            Ini ini = new Ini(IniPath());
            ini.WriteValue("ShowPlayerDamage", "Display", Convert.ToByte(ShowPlayerDamage).ToString());
            ini.WriteValue("ShowEnemyDamage", "Display", Convert.ToByte(ShowEnemyDamage).ToString());
            ini.WriteValue("IchibanRealtime", "Gameplay", IsIchibanRealtime.ToString());
            ini.WriteValue("KiryuRealtime", "Gameplay", IsKiryuRealtime.ToString());
            ini.WriteValue("AllowResurgenceMusic", "Gameplay", Convert.ToByte(AllowResurgenceMusic).ToString());
            ini.WriteValue("UseClassicGauge", "Gameplay", Convert.ToByte(BrawlerUIManager.UseClassicGauge).ToString());
            ini.WriteValue("KiryuDamageMultiplier", "Gameplay", KiryuDamageMultiplier.ToString(CultureInfo.InvariantCulture));
            ini.WriteValue("HeatGainPerHitPercent", "Gameplay", HeatGainPerHitPercent.ToString(CultureInfo.InvariantCulture));
            ini.WriteValue("ExtremeHeatDrainInterval", "Gameplay", ExtremeHeatDrainInterval.ToString(CultureInfo.InvariantCulture));
            ini.WriteValue("PartyMemberSkillMPRequirementRatio", "Party", PartyMemberSkillMPReqRatio.ToString(CultureInfo.InvariantCulture));
            ini.WriteValue("PartyMemberSkillChance", "Party", PartyMemberSkillChance.ToString());
            ini.WriteValue("PartyMemberSkillTime", "Party", PartyMemberSkillTime.ToString(CultureInfo.InvariantCulture));
            ini.Save();
        }

        public static bool IsPlayerRealtime()
        {
            if (Mod.MainPlayer.IsKasuga())
                return IsIchibanRealtime == 1;
            else if (Mod.MainPlayer.IsKiryu())
                return IsKiryuRealtime == 1;

            else return IsIchibanRealtime == 1;
        }
    }
}
