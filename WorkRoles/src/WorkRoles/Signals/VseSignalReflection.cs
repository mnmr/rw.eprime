using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles.Core.Signals;

namespace WorkRoles.Signals
{
    internal static class VseSignalReflection
    {
        internal sealed class PassionFact
        {
            public Def Def;
            public string IconPath;
            public string AuthorTier;
            public bool IsBad;
            public float LearnRate;
            public float ForgetRate;
            public float OtherLearnRate;
        }

        internal sealed class ExpertiseFact
        {
            public Def Def;
            public SkillDef Skill;
            public int Level;
            public float StatMultiplier;
            public string FullDescription;
        }

        private static bool passionInitialized;
        private static bool passionDisabled;
        private static Array passions;
        private static FieldInfo passionIconPath;
        private static FieldInfo passionColor;
        private static FieldInfo passionIsBad;
        private static FieldInfo passionLearnRate;
        private static FieldInfo passionForgetRate;
        private static FieldInfo passionOtherLearnRate;

        private static bool expertiseInitialized;
        private static bool expertiseDisabled;
        private static MethodInfo expertiseOf;
        private static PropertyInfo allExpertise;
        private static FieldInfo recordDef;
        private static PropertyInfo recordLevel;
        private static MethodInfo fullDescription;
        private static FieldInfo defSkill;
        private static FieldInfo settings;
        private static FieldInfo statMultiplier;

        internal static PassionFact Passion(Passion passion)
        {
            int index = (int)passion;
            if (index <= 2) return null;
            EnsurePassions();
            if (passionDisabled || passions == null || index >= passions.Length) return null;
            try
            {
                object value = passions.GetValue(index);
                var def = value as Def;
                if (def == null) return null;
                return new PassionFact
                {
                    Def = def,
                    IconPath = passionIconPath.GetValue(value) as string,
                    AuthorTier = passionColor.GetValue(value)?.ToString(),
                    IsBad = (bool)passionIsBad.GetValue(value),
                    LearnRate = Convert.ToSingle(passionLearnRate.GetValue(value)),
                    ForgetRate = Convert.ToSingle(passionForgetRate.GetValue(value)),
                    OtherLearnRate = Convert.ToSingle(passionOtherLearnRate.GetValue(value)),
                };
            }
            catch (Exception exception)
            {
                DisablePassions(exception);
                return null;
            }
        }

        internal static IReadOnlyList<ExpertiseFact> Expertises(Pawn pawn)
        {
            EnsureExpertise();
            if (expertiseDisabled || expertiseOf == null || pawn?.skills == null)
                return Array.Empty<ExpertiseFact>();
            try
            {
                object tracker = expertiseOf.Invoke(null, new object[] { pawn });
                if (tracker == null || !(allExpertise.GetValue(tracker) is IList records) || records.Count == 0)
                    return Array.Empty<ExpertiseFact>();

                float multiplier = ReadStatMultiplier();
                var result = new List<ExpertiseFact>(records.Count);
                foreach (object record in records)
                {
                    if (record == null) continue;
                    var def = recordDef.GetValue(record) as Def;
                    var skill = def == null ? null : defSkill.GetValue(def) as SkillDef;
                    if (def == null || skill == null) continue;
                    result.Add(new ExpertiseFact
                    {
                        Def = def,
                        Skill = skill,
                        Level = Convert.ToInt32(recordLevel.GetValue(record)),
                        StatMultiplier = multiplier,
                        FullDescription = fullDescription.Invoke(record, null) as string,
                    });
                }
                return result;
            }
            catch (Exception exception)
            {
                DisableExpertise(exception);
                return Array.Empty<ExpertiseFact>();
            }
        }

        private static void EnsurePassions()
        {
            if (passionInitialized) return;
            passionInitialized = true;
            try
            {
                Type manager = AccessTools.TypeByName(VseSignalApi.PassionManagerType);
                Type defType = AccessTools.TypeByName(VseSignalApi.PassionDefType);
                if (manager == null || defType == null) return;
                passions = AccessTools.Field(manager, VseSignalApi.PassionsMember)?.GetValue(null) as Array;
                passionIconPath = AccessTools.Field(defType, "iconPath");
                passionColor = AccessTools.Field(defType, "color");
                passionIsBad = AccessTools.Field(defType, "isBad");
                passionLearnRate = AccessTools.Field(defType, "learnRateFactor");
                passionForgetRate = AccessTools.Field(defType, "forgetRateFactor");
                passionOtherLearnRate = AccessTools.Field(defType, "learnRateFactorOther");
                if (passions == null || passionIconPath == null || passionColor == null
                    || passionIsBad == null || passionLearnRate == null
                    || passionForgetRate == null || passionOtherLearnRate == null)
                    throw new MissingMemberException("VSE passion API is not in the audited 1.6 shape.");
            }
            catch (Exception exception)
            {
                DisablePassions(exception);
            }
        }

        private static void EnsureExpertise()
        {
            if (expertiseInitialized) return;
            expertiseInitialized = true;
            try
            {
                Type trackersType = AccessTools.TypeByName(VseSignalApi.ExpertiseTrackersType);
                Type trackerType = AccessTools.TypeByName(VseSignalApi.ExpertiseTrackerType);
                Type recordType = AccessTools.TypeByName(VseSignalApi.ExpertiseRecordType);
                Type expertiseDefType = AccessTools.TypeByName(VseSignalApi.ExpertiseDefType);
                if (trackersType == null || trackerType == null || recordType == null || expertiseDefType == null)
                    return;

                expertiseOf = AccessTools.Method(trackersType, VseSignalApi.ExpertiseMethod,
                    new[] { typeof(Pawn) });
                allExpertise = AccessTools.Property(trackerType, VseSignalApi.AllExpertiseMember);
                recordDef = AccessTools.Field(recordType, VseSignalApi.ExpertiseDefMember);
                recordLevel = AccessTools.Property(recordType, VseSignalApi.ExpertiseLevelMember);
                fullDescription = AccessTools.Method(recordType, VseSignalApi.ExpertiseDescriptionMethod,
                    Type.EmptyTypes);
                defSkill = AccessTools.Field(expertiseDefType, VseSignalApi.ExpertiseSkillMember);

                Type skillsMod = AccessTools.TypeByName(VseSignalApi.SkillsModType);
                settings = AccessTools.Field(skillsMod, VseSignalApi.SettingsMember);
                statMultiplier = settings == null ? null
                    : AccessTools.Field(settings.FieldType, VseSignalApi.StatMultiplierMember);

                if (expertiseOf == null || allExpertise == null || recordDef == null
                    || recordLevel == null || fullDescription == null || defSkill == null)
                    throw new MissingMemberException("VSE expertise API is not in the audited 1.6 shape.");
            }
            catch (Exception exception)
            {
                DisableExpertise(exception);
            }
        }

        private static float ReadStatMultiplier()
        {
            if (settings == null || statMultiplier == null) return 1f;
            object currentSettings = settings.GetValue(null);
            return currentSettings == null ? 1f : Convert.ToSingle(statMultiplier.GetValue(currentSettings));
        }

        private static void DisablePassions(Exception exception)
        {
            if (!passionDisabled)
                Log.Warning("[WorkRoles] VSE signal passion integration disabled: " + exception.Message);
            passionDisabled = true;
            passions = null;
        }

        private static void DisableExpertise(Exception exception)
        {
            if (!expertiseDisabled)
                Log.Warning("[WorkRoles] VSE signal expertise integration disabled: " + exception.Message);
            expertiseDisabled = true;
            expertiseOf = null;
        }
    }
}
