using System;
using System.IO;
using System.Text;
using System.Xml.Linq;
using EPrimeReadouts.Core;
using EPrimeReadouts.UI;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace EPrimeReadouts
{
    internal static class SetupDefaults
    {
        // Owner: player profile/process. Key: this process's save-data directory.
        // Value: private detached default document and serialized XML, immutable between writes.
        // Dependencies: explicit Save/Clear actions only (external edits require restart).
        // Refresh: first new-game seed or explicit configuration request, never a render/tick poll.
        // Equality: equal serialized content does not write. Teardown: bounded single document;
        // no game/Unity references. Session references below are released on Game.Dispose.
        private static bool loaded;
        private static SetupDocument? saved;
        private static string? savedXml;
        private static string? loadError;
        private static string FilePath => Path.Combine(GenFilePaths.ConfigFolderPath, "EPrimeReadouts.Default.xml");

        // Owner: one loaded game. Key: exact ReadoutStore instance, never just a filename.
        // Value: local decision/dirty state, detached retention baseline and one pending editor.
        // Dependencies: explicit editor-open request, model-domain edits and durable player edits.
        // Refresh: events only; Root.Update only drains pending user work. Equality: an unchanged
        // setup retains savedXml and does not write. Teardown: Dispose clears all game references.
        private static ReadoutStore? owner;
        private static SetupSession session = new SetupSession();
        private static SetupDocument? baseline;
        private static Window? pendingEditor;
        private static bool openPending, flushPending, preferencesPending, savePending, clearPending;
        private static bool autoForSave;
        private static bool continuation;
        private static bool defaultDialogPending;
        private static bool processing;
        internal static bool HasDefault { get { EnsureLoaded(); return saved != null; } }
        internal static readonly Func<string, bool> MemberExists = Exists;

        private static bool Exists(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            return token[0] == '@' ? DefDatabase<ThingCategoryDef>.GetNamedSilentFail(token.Substring(1)) != null
                : GameResourceCatalog.Instance.Exists(token);
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                string xml = File.ReadAllText(FilePath, Encoding.UTF8);
                if (!SetupDocument.TryRead(xml, out saved, out loadError)) return;
                savedXml = xml;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { loadError = e.Message; }
        }

        private static SetupDocument? AvailableDefault()
        {
            EnsureLoaded();
            if (savedXml == null) return null;
            return SetupDocument.TryRead(savedXml, out var result, out _, ModRequirements.IsModActive, MemberExists) ? result : null;
        }

        internal static bool TrySeed(ReadoutStore store)
        {
            // Multiplayer clients receive the creator's already-seeded world through the save.
            if (MP.IsInMultiplayer && !MP.IsHosting) return false;
            var setup = AvailableDefault();
            if (setup == null) return false;
            if (!setup.TryApply(store.Model, SetupSections.Model, store.TakePoolId, store.TakeGroupId, out _, out _)) return false;
            foreach (var group in store.Model.Groups)
                if (setup.Player != null && setup.Player.EnabledGroups.TryGetValue(group.Name, out bool enabled))
                    group.DefaultEnabled = enabled;
            // Global display preferences already belong to the player; never roll them back on load.
            return true;
        }

        private static void Bind(ReadoutStore store)
        {
            if (ReferenceEquals(owner, store)) return;
            owner = store; session = new SetupSession(); baseline = null;
        }

        internal static bool GateEditor(Window editor)
        {
            var store = ReadoutStore.Current;
            if (store == null || continuation) return false;
            Bind(store);
            if (pendingEditor != null) return true;
            if (session.Checked) return false;
            if (pendingEditor == null) pendingEditor = editor;
            openPending = true;
            return true;
        }

        internal static void ModelChanged(ReadoutStore store)
        { if (ReferenceEquals(owner, store)) session.Changed(); }
        internal static void Changed() { if (owner != null) session.Changed(); }
        internal static void QueuePreferencesWrite() { preferencesPending = true; }
        internal static void QueueFlush() { flushPending = true; }
        internal static void QueueSave(bool autoUpdate)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            Bind(store); autoForSave = autoUpdate; savePending = true;
        }
        internal static void QueueClear() { clearPending = true; }
        internal static void QueueDefaultDialog() { defaultDialogPending = true; }

        internal static void ProcessPending()
        {
            if (processing || !(openPending || flushPending || preferencesPending || savePending || clearPending || defaultDialogPending)
                || Scribe.mode != LoadSaveMode.Inactive) return;
            processing = true;
            try
            {
                if (owner != null && !ReferenceEquals(owner, ReadoutStore.Current)) { ResetSession(); return; }
                if (clearPending)
                {
                    clearPending = false;
                    try
                    {
                        if (File.Exists(FilePath)) File.Delete(FilePath);
                        saved = null; savedXml = null; loadError = null; loaded = true;
                        EPrimeReadoutsMod.Settings.autoUpdateDefault = false;
                        session.Disable(); preferencesPending = true;
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { Report(e.Message); }
                }
                if (savePending)
                {
                    savePending = false;
                    EnsureLoaded();
                    baseline ??= AvailableDefault();
                    if (SaveCurrent())
                    {
                        EPrimeReadoutsMod.Settings.autoUpdateDefault = autoForSave;
                        session.Choose(autoForSave); session.Written(); preferencesPending = true;
                    }
                }
                if (flushPending)
                {
                    flushPending = false;
                    if (session.Dirty && EPrimeReadoutsMod.Settings.autoUpdateDefault) SaveCurrent();
                }
                if (preferencesPending)
                {
                    preferencesPending = false;
                    EPrimeReadoutsMod.Persist();
                }
                if (openPending && owner != null && Current.ProgramState == ProgramState.Playing)
                {
                    openPending = false;
                    if (continuation) { OpenPendingEditor(); return; }
                    var current = Capture(owner);
                    var available = AvailableDefault();
                    baseline = available;
                    // Availability belongs to the saved default, not to whichever colony
                    // was opened. Attach local identities only to track subsequent renames.
                    if (baseline != null)
                    {
                        foreach (var pool in baseline.Pools)
                            foreach (var live in current.Pools)
                                if (PoolNameRules.Comparer.Equals(pool.Name, live.Name)) { pool.Id = live.Id; break; }
                        foreach (var group in baseline.Groups)
                            foreach (var live in current.Groups)
                                if (ReadoutGroupNames.Comparer.Equals(group.Name, live.Name)) { group.Id = live.Id; break; }
                    }
                    if (loadError != null) { Report(loadError); loadError = null; }
                    bool conflict = session.CheckOnOpen(EPrimeReadoutsMod.Settings.autoUpdateDefault,
                        available != null, available != null && Comparable(current) == Comparable(available));
                    if (conflict)
                    {
                        Find.WindowStack.Add(new Dialog_SetupChoice(true));
                    }
                    else OpenPendingEditor();
                }
                if (defaultDialogPending)
                {
                    defaultDialogPending = false;
                    Find.WindowStack.Add(new Dialog_SetupChoice(false));
                }
            }
            finally { processing = false; }
        }

        private static SetupDocument Capture(ReadoutStore store) => SetupDocument.Capture(store.Model, PlayerSetupBridge.Capture(store));

        private static string Comparable(SetupDocument setup)
        {
            var root = XElement.Parse(setup.ToXml());
            root.Element("PlayerSettings")?.Remove();
            foreach (var group in root.Element("Groups")?.Elements("Group") ?? new XElement[0])
            {
                string name = (string?)group.Attribute("Name") ?? "";
                if (setup.Player != null && setup.Player.EnabledGroups.TryGetValue(name, out bool enabled))
                    group.SetAttributeValue("DefaultEnabled", enabled ? null : "False");
            }
            foreach (var item in root.Descendants()) { item.Attribute("MayRequire")?.Remove(); item.Attribute("MayRequireAnyOf")?.Remove(); }
            return root.ToString(SaveOptions.DisableFormatting);
        }

        internal static void Decide(bool useCurrent, bool disableAuto)
        {
            if (owner == null || !ReferenceEquals(owner, ReadoutStore.Current)) return;
            session.Choose(useCurrent);
            if (disableAuto) { EPrimeReadoutsMod.Settings.autoUpdateDefault = false; session.Disable(); preferencesPending = true; }
            if (useCurrent) flushPending = true;
            // Continue on the main update after the decision window closes.
            continuation = true; openPending = true;
        }

        private static void OpenPendingEditor()
        {
            Window? editor = pendingEditor; pendingEditor = null;
            if (editor == null) return;
            continuation = true;
            try { Find.WindowStack.Add(editor); }
            finally { continuation = false; }
        }

        private static bool SaveCurrent()
        {
            if (owner == null) return false;
            var current = Capture(owner);
            // Capture before retention: future intentional deletions are compared with what was visible.
            var nextBaseline = Capture(owner);
            if (saved != null && baseline != null) current.RetainUnavailableFrom(saved, baseline);
            string xml = current.ToXml(ModRequirements.PackageIdOf);
            if (xml == savedXml) { baseline = nextBaseline; session.Written(); return true; }
            string path = FilePath, temporary = path + ".tmp";
            try
            {
                File.WriteAllText(temporary, xml, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                saved = current; savedXml = xml; baseline = nextBaseline; loaded = true; loadError = null;
                session.Written(); return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { Report(e.Message); return false; }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            }
        }

        private static void Report(string error) => Messages.Message("EPR.SetupFailed".Translate(error), MessageTypeDefOf.RejectInput, false);

        internal static void Dispose()
        {
            if (Scribe.mode == LoadSaveMode.Inactive)
            {
                if (session.Dirty && EPrimeReadoutsMod.Settings.autoUpdateDefault) SaveCurrent();
                if (preferencesPending) EPrimeReadoutsMod.Persist();
            }
            ResetSession();
        }

        private static void ResetSession()
        {
            owner = null; baseline = null; pendingEditor = null; session = new SetupSession();
            openPending = flushPending = savePending = clearPending = preferencesPending = continuation = defaultDialogPending = false;
        }
    }

    [HarmonyPatch(typeof(Root), nameof(Root.Update))]
    internal static class Patch_SetupPendingWork
    { private static void Postfix() => SetupDefaults.ProcessPending(); }

    [HarmonyPatch(typeof(Game), nameof(Game.Dispose))]
    internal static class Patch_SetupDisposal
    { private static void Prefix(Game __instance) { if (ReferenceEquals(Current.Game, __instance)) SetupDefaults.Dispose(); } }

    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add))]
    internal static class Patch_SetupEditorGate
    {
        [HarmonyPriority(Priority.Last)]
        private static bool Prefix(Window window) => !(window is Dialog_ReadoutConfig) || !SetupDefaults.GateEditor(window);
    }
}
