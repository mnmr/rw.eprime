using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using RimShared.Common;
using RimWorld.IO;
using Verse;

namespace RimShared.GameLib.Localization
{
    /// <summary>
    /// Loads FallbackLanguages only after RimWorld has loaded every normal
    /// translation mod. Community text therefore wins in either mod order.
    /// This source is compiled into each consumer, so installation is owned by
    /// one process-wide Harmony ID rather than an assembly-local singleton.
    /// No language/mod/world objects are retained between load operations.
    /// </summary>
    public static class BundledTranslationFallback
    {
        public const string HarmonyId = "rimshared.bundled-translation-fallback";

        public static void Install()
        {
            MethodInfo load = AccessTools.Method(typeof(LoadedLanguage), nameof(LoadedLanguage.LoadData));
            Patches? patches = Harmony.GetPatchInfo(load);
            if (patches != null && patches.Owners.Contains(HarmonyId)) return;
            var harmony = new Harmony(HarmonyId);
            harmony.Patch(load,
                prefix: new HarmonyMethod(typeof(BundledTranslationFallback), nameof(BeforeLoad)),
                postfix: new HarmonyMethod(typeof(BundledTranslationFallback), nameof(AfterLoad))
                { priority = Priority.Last });
        }

        /// <summary>Process shutdown/test teardown; call only when all consumers stop.</summary>
        public static void Uninstall()
        {
            new Harmony(HarmonyId).Unpatch(
                AccessTools.Method(typeof(LoadedLanguage), nameof(LoadedLanguage.LoadData)),
                HarmonyPatchType.All, HarmonyId);
        }

        private static void BeforeLoad(bool ___dataIsLoaded, out bool __state)
        {
            __state = !___dataIsLoaded;
        }

        private static void AfterLoad(LoadedLanguage __instance, bool __state)
        {
            if (!__state || string.Equals(__instance.folderName, "English", StringComparison.OrdinalIgnoreCase))
                return;
            string[] languages = LanguageFolders.Candidates(
                __instance.folderName, __instance.LegacyFolderName);
            List<ModContentPack> mods = LoadedModManager.RunningModsListForReading;
            for (int m = 0; m < mods.Count; m++)
            {
                ModContentPack mod = mods[m];
                foreach (string contentRoot in mod.foldersToLoadDescendingOrder)
                {
                    string fallbackRoot = Path.Combine(contentRoot, "FallbackLanguages");
                    for (int l = 0; l < languages.Length; l++)
                    {
                        string root = Path.Combine(fallbackRoot, languages[l]);
                        if (!Directory.Exists(root)) continue;
                        LoadKeyed(__instance, Path.Combine(root, "Keyed"));
                        LoadDefs(__instance, Path.Combine(root, "DefInjected"));
                    }
                }
            }
        }

        private static string[] Files(LoadedLanguage language, string directory, bool directories)
        {
            try
            {
                if (!Directory.Exists(directory)) return Array.Empty<string>();
                string[] paths = directories
                    ? Directory.GetDirectories(directory)
                    : Directory.GetFiles(directory, "*.xml", SearchOption.AllDirectories);
                Array.Sort(paths, StringComparer.Ordinal);
                return paths;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                language.loadErrors.Add("Could not read bundled translations from " + directory + ": " + exception.Message);
                return Array.Empty<string>();
            }
        }

        private static void LoadKeyed(LoadedLanguage language, string directory)
        {
            string[] paths = Files(language, directory, false);
            for (int f = 0; f < paths.Length; f++)
            {
                string path = paths[f];
                var entries = new List<DirectXmlLoaderSimple.XmlKeyValuePair>();
                try
                {
                    // Parse the whole file before publishing any of its values.
                    foreach (DirectXmlLoaderSimple.XmlKeyValuePair entry in
                        DirectXmlLoaderSimple.ValuesFromXmlFile(File.ReadAllText(path)))
                        entries.Add(entry);
                }
                catch (Exception exception) when (exception is IOException
                    || exception is UnauthorizedAccessException || exception is XmlException)
                {
                    language.loadErrors.Add("Could not load bundled translation file " + path + ": " + exception.Message);
                    continue;
                }
                for (int e = 0; e < entries.Count; e++)
                {
                    DirectXmlLoaderSimple.XmlKeyValuePair entry = entries[e];
                    if (entry.value == LoadedLanguage.PlaceholderText) continue;
                    if (language.keyedReplacements.TryGetValue(entry.key, out LoadedLanguage.KeyedReplacement? existing)
                        && !existing.isPlaceholder) continue;
                    language.keyedReplacements[entry.key] = new LoadedLanguage.KeyedReplacement
                    {
                        key = entry.key,
                        value = entry.value,
                        fileSource = Path.GetFileName(path),
                        fileSourceFullPath = path,
                        fileSourceLine = entry.lineNumber
                    };
                }
            }
        }

        private static void LoadDefs(LoadedLanguage language, string directory)
        {
            string[] typeDirectories = Files(language, directory, true);
            for (int t = 0; t < typeDirectories.Length; t++)
            {
                string typeName = Path.GetFileName(typeDirectories[t]);
                Type? type = GenTypes.GetTypeInAnyAssembly(typeName);
                if (type == null || !typeof(Def).IsAssignableFrom(type))
                {
                    language.loadErrors.Add("Unknown bundled translation def type: " + typeName);
                    continue;
                }
                DefInjectionPackage? destination = null;
                for (int p = 0; p < language.defInjections.Count; p++)
                    if (language.defInjections[p].defType == type)
                    {
                        destination = language.defInjections[p];
                        break;
                    }
                if (destination == null)
                {
                    destination = new DefInjectionPackage(type);
                    language.defInjections.Add(destination);
                }
                var coverage = new TranslationCoverage();
                foreach (KeyValuePair<string, DefInjectionPackage.DefInjection> existing in destination.injections)
                    if (!existing.Value.isPlaceholder)
                        coverage.Add(NormalizedPath(existing.Key), existing.Value.IsFullListInjection);

                string[] paths = Files(language, typeDirectories[t], false);
                for (int f = 0; f < paths.Length; f++)
                {
                    string text;
                    try { text = File.ReadAllText(paths[f]); }
                    catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                    {
                        language.loadErrors.Add("Could not read bundled translation file " + paths[f] + ": " + exception.Message);
                        continue;
                    }
                    // Use the game's parser: it handles escaped newlines,
                    // legacy paths, renamed defs, and full-list translations.
                    var parsed = new DefInjectionPackage(type);
                    parsed.AddDataFromFile(AbstractFilesystem.GetDirectory(Path.GetDirectoryName(paths[f]))
                        .GetFile(Path.GetFileName(paths[f])), out bool xmlError, text);
                    if (xmlError || parsed.loadErrors.Count != 0)
                    {
                        language.loadErrors.AddRange(parsed.loadErrors);
                        continue;
                    }
                    foreach (KeyValuePair<string, DefInjectionPackage.DefInjection> entry in parsed.injections)
                    {
                        if (entry.Value.isPlaceholder) continue;
                        string normalized = NormalizedPath(entry.Key);
                        if (coverage.Covers(normalized, entry.Value.IsFullListInjection)) continue;
                        destination.injections[entry.Key] = entry.Value;
                        coverage.Add(normalized, entry.Value.IsFullListInjection);
                    }
                }
            }
        }

        private static string NormalizedPath(string path)
        {
            return TKeySystem.TryGetNormalizedPath(path, out string normalized) ? normalized : path;
        }
    }
}
