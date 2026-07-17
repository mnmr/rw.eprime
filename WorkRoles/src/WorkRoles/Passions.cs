using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles
{
    /// Single interpretation point for SkillRecord.passion values. Vanilla knows
    /// None/Minor/Major; Vanilla Skills Expanded (and mods built on it, e.g. Alpha
    /// Skills) packs extra passions into the same enum — one byte value per
    /// VSE.Passions.PassionDef, mapped via PassionManager.Passions[index]. Those
    /// defs are read once via reflection (no hard dependency) and bucketed by the
    /// tier VSE itself assigns them (PassionColor Major/Minor), so recommendations
    /// and the stats panel treat each custom passion like its nearest vanilla one.
    /// [StaticConstructorOnStartup] quiets the vanilla texture-field heuristic:
    /// the icons array is a lazy cache only ever touched from UI code (main
    /// thread), never during static init — but the scanner can't know that.
    [StaticConstructorOnStartup]
    public static class Passions
    {

        // VSE's PassionColor enum: Disabled = 0, Main = 1, Minor = 2, Major = 3.
        private const int PassionColorMinor = 2;
        private const int PassionColorMajor = 3;

        private static bool initialized;
        private static object[] defs;      // VSE PassionDef by (int)passion; null when VSE absent
        private static int[] scores;       // 2 major-tier, 1 minor-tier, 0 none/bad
        private static string[] labels;
        private static Texture2D[] icons;  // lazy: first request loads the texture
        private static bool[] iconTried;
        private static System.Reflection.PropertyInfo iconProp;
        // Rate fields are defensive reflection: absent on older VSE versions.
        private static System.Reflection.FieldInfo learnRateField;
        private static System.Reflection.FieldInfo forgetRateField;

        /// 2 = major-tier passion, 1 = minor-tier, 0 = passionless or
        /// counter-productive (e.g. VSE's Apathy).
        public static int Score(SkillRecord sr)
            => sr == null || sr.TotallyDisabled ? 0 : ScoreOf(sr.passion);

        public static int ScoreOf(Passion passion)
        {
            if (passion == Passion.Major) return 2;
            if (passion == Passion.Minor) return 1;
            if (passion == Passion.None) return 0;
            EnsureInit();
            int i = (int)passion;
            return scores != null && i < scores.Length ? scores[i] : 0;
        }

        /// Icon of a custom passion; null for vanilla values (callers draw the
        /// vanilla star textures) and for unknown bytes.
        public static Texture2D CustomIcon(Passion passion)
        {
            int i = (int)passion;
            if (i <= 2) return null;
            EnsureInit();
            if (defs == null || i >= defs.Length || defs[i] == null) return null;
            if (!iconTried[i])
            {
                iconTried[i] = true;
                try { icons[i] = iconProp?.GetValue(defs[i]) as Texture2D; }
                catch (Exception e) { Log.WarningOnce($"[WorkRoles] failed to load passion icon: {e.Message}", 0x57500 + i); }
            }
            return icons[i];
        }

        /// Label of a custom passion; null for vanilla values and unknown bytes.
        public static string CustomLabel(Passion passion)
        {
            int i = (int)passion;
            if (i <= 2) return null;
            EnsureInit();
            return labels != null && i < labels.Length ? labels[i] : null;
        }

        /// Description of a custom passion (the Def base field); null for
        /// vanilla values and unknown bytes.
        public static string CustomDescription(Passion passion)
        {
            var def = CustomDef(passion);
            return (def as Def)?.description;
        }

        /// Learn-rate multiplier: vanilla's hardcoded LearnRateFactor values,
        /// or the VSE def's learnRateFactor; null when unknown.
        public static float? LearnRateFactor(Passion passion)
        {
            if (passion == Passion.None) return 0.35f;
            if (passion == Passion.Minor) return 1f;
            if (passion == Passion.Major) return 1.5f;
            return ReadRate(learnRateField, CustomDef(passion));
        }

        /// Forget-rate multiplier of a custom passion; null when unknown
        /// (vanilla passions carry none of their own).
        public static float? ForgetRateFactor(Passion passion)
        {
            int i = (int)passion;
            if (i <= 2) return null;
            return ReadRate(forgetRateField, CustomDef(passion));
        }

        private static object CustomDef(Passion passion)
        {
            int i = (int)passion;
            if (i <= 2) return null;
            EnsureInit();
            return defs != null && i < defs.Length ? defs[i] : null;
        }

        private static float? ReadRate(System.Reflection.FieldInfo field, object def)
        {
            if (field == null || def == null) return null;
            try { return Convert.ToSingle(field.GetValue(def)); }
            catch { return null; }
        }

        private static void EnsureInit()
        {
            if (initialized) return;
            initialized = true;
            try
            {
                var manager = AccessTools.TypeByName("VSE.Passions.PassionManager");
                if (manager == null) return;
                var arr = AccessTools.Field(manager, "Passions")?.GetValue(null) as Array;
                if (arr == null || arr.Length <= 3) return; // vanilla trio only

                var defType = AccessTools.TypeByName("VSE.Passions.PassionDef");
                var colorField = AccessTools.Field(defType, "color");
                var isBadField = AccessTools.Field(defType, "isBad");
                iconProp = AccessTools.Property(defType, "Icon");
                learnRateField = AccessTools.Field(defType, "learnRateFactor");
                forgetRateField = AccessTools.Field(defType, "forgetRateFactor");

                defs = new object[arr.Length];
                arr.CopyTo(defs, 0);
                scores = new int[arr.Length];
                labels = new string[arr.Length];
                icons = new Texture2D[arr.Length];
                iconTried = new bool[arr.Length];
                // Vanilla indices 0-2 are answered before the tables are consulted.
                for (int i = 3; i < defs.Length; i++)
                {
                    var def = defs[i];
                    if (def == null) continue;
                    int tier = Convert.ToInt32(colorField.GetValue(def));
                    scores[i] = (bool)isBadField.GetValue(def) ? 0
                        : tier == PassionColorMajor ? 2
                        : tier == PassionColorMinor ? 1 : 0;
                    labels[i] = (def as Def)?.LabelCap;
                }
                Log.Message($"[WorkRoles] Vanilla Skills Expanded detected: {defs.Length - 3} custom passions integrated");
            }
            catch (Exception e)
            {
                Log.Warning($"[WorkRoles] VSE passion integration failed, custom passions will read as passionless: {e}");
                defs = null;
                scores = null;
                labels = null;
                learnRateField = null;
                forgetRateField = null;
            }
        }
    }
}
