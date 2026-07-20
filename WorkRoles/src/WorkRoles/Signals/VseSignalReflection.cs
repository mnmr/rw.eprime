using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles.Core;
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

        private sealed class PassionMetadata
        {
            internal object Value;
            internal Def Def;
            internal string PackageId;
            internal string DefName;
            internal string IconPath;
            internal string AuthorTier;
        }

        private struct PassionMutableState
        {
            internal bool IsBad;
            internal float LearnRate;
            internal float ForgetRate;
            internal float OtherLearnRate;
        }

        private static readonly DefinitionOwnedCache<PassionMetadata[]> passionDefinitions =
            new DefinitionOwnedCache<PassionMetadata[]>();
        private static PassionMutableState[] passionMutableState;
        private static Func<object, bool> passionIsBadRead;
        private static Func<object, float> passionLearnRateRead;
        private static Func<object, float> passionForgetRateRead;
        private static Func<object, float> passionOtherLearnRateRead;

        private static bool expertiseInitialized;
        private static bool expertiseDisabled;
        private static Func<Pawn, object> expertiseTrackerRead;
        private static Func<object, IList> allExpertiseRead;
        private static Func<object, Def> recordDefRead;
        private static Func<object, int> recordLevelRead;
        private static Func<object, float> recordXpSinceLastLevelRead;
        private static Func<object, float> recordXpRequiredForLevelUpRead;
        private static Func<object, SkillDef> expertiseSkillRead;
        private static Func<object> settingsRead;
        private static Func<object, float> statMultiplierRead;
        private static Func<object, bool> crossSkillEffectsRead;
        private static MethodInfo fullDescription;

        private static long lastGlobalObservationEpoch = long.MinValue;
        private static float observedStatMultiplier = 1f;
        private static bool observedCrossSkillEffects;

        /// Samples globally shared VSE settings and mutable passion definitions
        /// once per bounded observation epoch. Every pawn signature in that
        /// epoch consumes the same values; known mutations invalidate directly.
        internal static void ObserveGlobalInputs(long epoch)
        {
            if (lastGlobalObservationEpoch == epoch) return;

            EnsurePassions();
            EnsureExpertise();
            observedStatMultiplier = 1f;
            observedCrossSkillEffects = false;

            PassionMetadata[] passionMetadata = passionDefinitions.Value;
            if (!passionDefinitions.Disabled && passionMetadata != null)
            {
                try
                {
                    for (int i = 0; i < passionMetadata.Length; i++)
                    {
                        PassionMetadata metadata = passionMetadata[i];
                        if (metadata == null) continue;
                        passionMutableState[i] = new PassionMutableState
                        {
                            IsBad = passionIsBadRead(metadata.Value),
                            LearnRate = passionLearnRateRead(metadata.Value),
                            ForgetRate = passionForgetRateRead(metadata.Value),
                            OtherLearnRate = passionOtherLearnRateRead(metadata.Value),
                        };
                    }
                }
                catch (Exception exception)
                {
                    DisablePassions(exception);
                }
            }

            if (!expertiseDisabled && settingsRead != null)
            {
                try
                {
                    object currentSettings = settingsRead();
                    if (currentSettings == null)
                    {
                        observedCrossSkillEffects = true;
                    }
                    else
                    {
                        observedStatMultiplier = statMultiplierRead(currentSettings);
                        observedCrossSkillEffects = crossSkillEffectsRead(currentSettings);
                    }
                }
                catch (Exception exception)
                {
                    DisableExpertise(exception);
                }
            }

            lastGlobalObservationEpoch = epoch;
        }

        internal static void ResetGlobalObservation()
        {
            lastGlobalObservationEpoch = long.MinValue;
        }

        /// Definition replacement must release both reflected objects/Defs and
        /// every compiled reader whose declaring metadata belonged to that load.
        internal static void InvalidateDefinitions()
        {
            passionDefinitions.Invalidate();
            passionMutableState = null;
            passionIsBadRead = null;
            passionLearnRateRead = null;
            passionForgetRateRead = null;
            passionOtherLearnRateRead = null;

            expertiseInitialized = false;
            expertiseDisabled = false;
            expertiseTrackerRead = null;
            allExpertiseRead = null;
            recordDefRead = null;
            recordLevelRead = null;
            recordXpSinceLastLevelRead = null;
            recordXpRequiredForLevelUpRead = null;
            expertiseSkillRead = null;
            settingsRead = null;
            statMultiplierRead = null;
            crossSkillEffectsRead = null;
            fullDescription = null;

            observedStatMultiplier = 1f;
            observedCrossSkillEffects = false;
            lastGlobalObservationEpoch = long.MinValue;
        }

        internal static PassionFact Passion(Passion passion)
        {
            ObserveGlobalInputs(PawnSignalSnapshotCache.ObservationEpoch);
            int index = (int)passion;
            PassionMetadata[] passionMetadata = passionDefinitions.Value;
            if (index <= 2 || passionDefinitions.Disabled || passionMetadata == null
                || index >= passionMetadata.Length)
                return null;

            PassionMetadata metadata = passionMetadata[index];
            if (metadata == null) return null;
            PassionMutableState state = passionMutableState[index];
            return new PassionFact
            {
                Def = metadata.Def,
                IconPath = metadata.IconPath,
                AuthorTier = metadata.AuthorTier,
                IsBad = state.IsBad,
                LearnRate = state.LearnRate,
                ForgetRate = state.ForgetRate,
                OtherLearnRate = state.OtherLearnRate,
            };
        }

        /// Hot signature path: immutable definition metadata is precomputed and
        /// mutable mechanics come from the epoch-shared typed sample.
        internal static void AppendPassionSignature(Passion passion,
            ref MutableSignalSignatureBuilder builder)
        {
            ObserveGlobalInputs(PawnSignalSnapshotCache.ObservationEpoch);
            int index = (int)passion;
            PassionMetadata[] passionMetadata = passionDefinitions.Value;
            if (index <= 2 || passionDefinitions.Disabled || passionMetadata == null
                || index >= passionMetadata.Length)
                return;

            PassionMetadata metadata = passionMetadata[index];
            if (metadata == null) return;
            PassionMutableState state = passionMutableState[index];
            builder.AddModdedPassion(
                metadata.PackageId,
                metadata.DefName,
                state.IsBad,
                state.LearnRate,
                state.ForgetRate,
                state.OtherLearnRate,
                metadata.IconPath,
                metadata.AuthorTier);
        }

        internal static IReadOnlyList<ExpertiseFact> Expertises(Pawn pawn)
        {
            ObserveGlobalInputs(PawnSignalSnapshotCache.ObservationEpoch);
            if (expertiseDisabled || expertiseTrackerRead == null || pawn?.skills == null)
                return Array.Empty<ExpertiseFact>();
            try
            {
                object tracker = expertiseTrackerRead(pawn);
                IList records = tracker == null ? null : allExpertiseRead(tracker);
                if (records == null || records.Count == 0)
                    return Array.Empty<ExpertiseFact>();

                var result = new List<ExpertiseFact>(records.Count);
                for (int i = 0; i < records.Count; i++)
                {
                    object record = records[i];
                    if (record == null) continue;
                    Def def = recordDefRead(record);
                    SkillDef skill = def == null ? null : expertiseSkillRead(def);
                    if (def == null || skill == null) continue;
                    result.Add(new ExpertiseFact
                    {
                        Def = def,
                        Skill = skill,
                        Level = recordLevelRead(record),
                        StatMultiplier = observedStatMultiplier,
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

        /// Hot signature path: raw records are read through typed delegates and
        /// the global settings sample is shared by every pawn in the epoch.
        internal static void AppendExpertiseSignature(Pawn pawn,
            ref MutableSignalSignatureBuilder builder)
        {
            ObserveGlobalInputs(PawnSignalSnapshotCache.ObservationEpoch);
            builder.AddProviderCondition("vse:cross-skill", observedCrossSkillEffects);
            if (expertiseDisabled || expertiseTrackerRead == null || pawn?.skills == null) return;
            try
            {
                object tracker = expertiseTrackerRead(pawn);
                IList records = tracker == null ? null : allExpertiseRead(tracker);
                if (records == null) return;
                for (int i = 0; i < records.Count; i++)
                {
                    object record = records[i];
                    if (record == null) continue;
                    Def def = recordDefRead(record);
                    SkillDef skill = def == null ? null : expertiseSkillRead(def);
                    if (def == null || skill == null) continue;
                    builder.AddExpertise(
                        SignalUiFactory.PackageId(def),
                        def.defName,
                        skill.defName,
                        recordLevelRead(record),
                        recordXpSinceLastLevelRead(record),
                        recordXpRequiredForLevelUpRead(record),
                        observedStatMultiplier);
                }
            }
            catch (Exception exception)
            {
                DisableExpertise(exception);
            }
        }

        internal static bool CrossSkillEffectsEnabled()
        {
            ObserveGlobalInputs(PawnSignalSnapshotCache.ObservationEpoch);
            return observedCrossSkillEffects;
        }

        private static void EnsurePassions()
        {
            if (passionDefinitions.Initialized) return;
            try
            {
                Type manager = AccessTools.TypeByName(VseSignalApi.PassionManagerType);
                Type defType = AccessTools.TypeByName(VseSignalApi.PassionDefType);
                if (manager == null && defType == null)
                {
                    passionMutableState = Array.Empty<PassionMutableState>();
                    passionDefinitions.Publish(Array.Empty<PassionMetadata>());
                    return;
                }
                if (manager == null || defType == null || defType.IsValueType
                    || !typeof(Def).IsAssignableFrom(defType))
                    throw AuditedShape("passion types");

                FieldInfo passionsField = RequiredField(manager,
                    VseSignalApi.PassionsMember, defType.MakeArrayType(), true);
                FieldInfo iconPath = RequiredField(defType, "iconPath", typeof(string), false);
                FieldInfo color = AccessTools.Field(defType, "color");
                FieldInfo isBad = RequiredField(defType, "isBad", typeof(bool), false);
                FieldInfo learnRate = RequiredField(defType,
                    "learnRateFactor", typeof(float), false);
                FieldInfo forgetRate = RequiredField(defType,
                    "forgetRateFactor", typeof(float), false);
                FieldInfo otherLearnRate = RequiredField(defType,
                    "learnRateFactorOther", typeof(float), false);
                if (color == null || color.IsStatic || !color.FieldType.IsEnum
                    || Enum.GetUnderlyingType(color.FieldType) != typeof(int))
                    throw AuditedShape("passion color");

                Func<object, string> iconPathRead = CompileInstanceField<string>(iconPath, defType);
                Func<object, int> colorRead = CompileEnumField(color, defType);
                passionIsBadRead = CompileInstanceField<bool>(isBad, defType);
                passionLearnRateRead = CompileInstanceField<float>(learnRate, defType);
                passionForgetRateRead = CompileInstanceField<float>(forgetRate, defType);
                passionOtherLearnRateRead = CompileInstanceField<float>(otherLearnRate, defType);

                Array definitions = passionsField.GetValue(null) as Array;
                if (definitions == null) throw AuditedShape("passion definitions");
                var passionMetadata = new PassionMetadata[definitions.Length];
                passionMutableState = new PassionMutableState[definitions.Length];
                for (int i = 0; i < definitions.Length; i++)
                {
                    object value = definitions.GetValue(i);
                    Def def = value as Def;
                    if (def == null) continue;
                    int colorValue = colorRead(value);
                    string authorTier = Enum.GetName(color.FieldType, colorValue)
                        ?? colorValue.ToString();
                    passionMetadata[i] = new PassionMetadata
                    {
                        Value = value,
                        Def = def,
                        PackageId = SignalUiFactory.PackageId(def),
                        DefName = def.defName,
                        IconPath = iconPathRead(value),
                        AuthorTier = authorTier,
                    };
                }
                passionDefinitions.Publish(passionMetadata);
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
                if (trackersType == null && trackerType == null && recordType == null
                    && expertiseDefType == null)
                    return;
                if (trackersType == null || trackerType == null || recordType == null
                    || expertiseDefType == null || trackerType.IsValueType
                    || recordType.IsValueType || expertiseDefType.IsValueType
                    || !typeof(Def).IsAssignableFrom(expertiseDefType))
                    throw AuditedShape("expertise types");

                MethodInfo expertiseOf = AccessTools.Method(trackersType,
                    VseSignalApi.ExpertiseMethod, new[] { typeof(Pawn) });
                PropertyInfo allExpertise = AccessTools.Property(trackerType,
                    VseSignalApi.AllExpertiseMember);
                FieldInfo recordDef = RequiredField(recordType,
                    VseSignalApi.ExpertiseDefMember, expertiseDefType, false);
                PropertyInfo recordLevel = RequiredProperty(recordType,
                    VseSignalApi.ExpertiseLevelMember, typeof(int));
                FieldInfo recordXpSinceLastLevel = RequiredField(recordType,
                    VseSignalApi.ExpertiseXpSinceLastLevelMember, typeof(float), false);
                FieldInfo recordXpRequiredForLevelUp = RequiredField(recordType,
                    VseSignalApi.ExpertiseXpRequiredForLevelUpMember, typeof(float), false);
                fullDescription = AccessTools.Method(recordType,
                    VseSignalApi.ExpertiseDescriptionMethod, Type.EmptyTypes);
                FieldInfo defSkill = RequiredField(expertiseDefType,
                    VseSignalApi.ExpertiseSkillMember, typeof(SkillDef), false);

                if (expertiseOf == null || !expertiseOf.IsStatic
                    || expertiseOf.ReturnType != trackerType
                    || allExpertise == null || allExpertise.GetGetMethod(true) == null
                    || allExpertise.GetGetMethod(true).IsStatic
                    || !typeof(IList).IsAssignableFrom(allExpertise.PropertyType)
                    || fullDescription == null || fullDescription.IsStatic
                    || fullDescription.ReturnType != typeof(string))
                    throw AuditedShape("expertise members");

                Type skillsMod = AccessTools.TypeByName(VseSignalApi.SkillsModType);
                if (skillsMod == null) throw AuditedShape("settings type");
                FieldInfo settings = AccessTools.Field(skillsMod, VseSignalApi.SettingsMember);
                if (settings == null || !settings.IsStatic || settings.FieldType.IsValueType)
                    throw AuditedShape("settings member");
                FieldInfo statMultiplier = RequiredField(settings.FieldType,
                    VseSignalApi.StatMultiplierMember, typeof(float), false);
                FieldInfo crossSkillEffectsSetting = RequiredField(settings.FieldType,
                    VseSignalApi.CrossSkillEffectsSettingMember, typeof(bool), false);

                expertiseTrackerRead = CompileExpertiseTracker(expertiseOf);
                allExpertiseRead = CompileInstanceProperty<IList>(allExpertise, trackerType);
                recordDefRead = CompileInstanceField<Def>(recordDef, recordType);
                recordLevelRead = CompileInstanceProperty<int>(recordLevel, recordType);
                recordXpSinceLastLevelRead = CompileInstanceField<float>(
                    recordXpSinceLastLevel, recordType);
                recordXpRequiredForLevelUpRead = CompileInstanceField<float>(
                    recordXpRequiredForLevelUp, recordType);
                expertiseSkillRead = CompileInstanceField<SkillDef>(defSkill, expertiseDefType);
                settingsRead = CompileStaticReferenceField(settings);
                statMultiplierRead = CompileInstanceField<float>(
                    statMultiplier, settings.FieldType);
                crossSkillEffectsRead = CompileInstanceField<bool>(
                    crossSkillEffectsSetting, settings.FieldType);
            }
            catch (Exception exception)
            {
                DisableExpertise(exception);
            }
        }

        private static Func<Pawn, object> CompileExpertiseTracker(MethodInfo method)
        {
            ParameterExpression pawn = Expression.Parameter(typeof(Pawn), "pawn");
            Expression call = Expression.Call(method, pawn);
            return Expression.Lambda<Func<Pawn, object>>(
                Expression.Convert(call, typeof(object)), pawn).Compile();
        }

        private static Func<object> CompileStaticReferenceField(FieldInfo field)
        {
            Expression value = Expression.Field(null, field);
            return Expression.Lambda<Func<object>>(
                Expression.Convert(value, typeof(object))).Compile();
        }

        private static Func<object, T> CompileInstanceField<T>(FieldInfo field,
            Type declaringType)
        {
            ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
            Expression value = Expression.Field(
                Expression.Convert(instance, declaringType), field);
            if (value.Type != typeof(T)) value = Expression.Convert(value, typeof(T));
            return Expression.Lambda<Func<object, T>>(value, instance).Compile();
        }

        private static Func<object, T> CompileInstanceProperty<T>(PropertyInfo property,
            Type declaringType)
        {
            ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
            Expression value = Expression.Property(
                Expression.Convert(instance, declaringType), property);
            if (value.Type != typeof(T)) value = Expression.Convert(value, typeof(T));
            return Expression.Lambda<Func<object, T>>(value, instance).Compile();
        }

        private static Func<object, int> CompileEnumField(FieldInfo field,
            Type declaringType)
        {
            ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
            Expression value = Expression.Field(
                Expression.Convert(instance, declaringType), field);
            value = Expression.Convert(value, typeof(int));
            return Expression.Lambda<Func<object, int>>(value, instance).Compile();
        }

        private static FieldInfo RequiredField(Type owner, string name,
            Type fieldType, bool isStatic)
        {
            FieldInfo field = AccessTools.Field(owner, name);
            if (field == null || field.IsStatic != isStatic || field.FieldType != fieldType)
                throw AuditedShape(owner.FullName + "." + name);
            return field;
        }

        private static PropertyInfo RequiredProperty(Type owner, string name,
            Type propertyType)
        {
            PropertyInfo property = AccessTools.Property(owner, name);
            MethodInfo getter = property?.GetGetMethod(true);
            if (property == null || getter == null || getter.IsStatic
                || property.PropertyType != propertyType)
                throw AuditedShape(owner.FullName + "." + name);
            return property;
        }

        private static MissingMemberException AuditedShape(string member) =>
            new MissingMemberException("VSE " + member
                + " is not in the audited 1.6 shape.");

        private static void DisablePassions(Exception exception)
        {
            if (!passionDefinitions.Disabled)
                Log.Warning("[WorkRoles] VSE signal passion integration disabled: "
                    + exception.Message);
            passionDefinitions.Disable();
            passionMutableState = null;
            passionIsBadRead = null;
            passionLearnRateRead = null;
            passionForgetRateRead = null;
            passionOtherLearnRateRead = null;
        }

        private static void DisableExpertise(Exception exception)
        {
            if (!expertiseDisabled)
                Log.Warning("[WorkRoles] VSE signal expertise integration disabled: "
                    + exception.Message);
            expertiseDisabled = true;
            expertiseTrackerRead = null;
            allExpertiseRead = null;
            recordDefRead = null;
            recordLevelRead = null;
            recordXpSinceLastLevelRead = null;
            recordXpRequiredForLevelUpRead = null;
            expertiseSkillRead = null;
            settingsRead = null;
            statMultiplierRead = null;
            crossSkillEffectsRead = null;
            fullDescription = null;
            observedStatMultiplier = 1f;
            observedCrossSkillEffects = false;
        }
    }
}
