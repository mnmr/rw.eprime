using System;
using System.IO;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;
using Multiplayer.API;
using RimWorld;
using Verse;
using WorkRoles.Core;
using WorkRoles.UI;

namespace WorkRoles
{
    internal static class SetupDefaults
    {
        // Owner: player profile/process. Key: process save-data directory.
        // Value: detached default XML, immutable between explicit writes.
        // Dependencies: Save/Clear only; external changes require restart.
        // Refresh: first new-world setup or explicit editor/default request.
        // Equality: identical XML preserves the file and cached string.
        // Teardown: one bounded string; no game/Unity references.
        private static bool loaded;
        private static string? savedXml, loadError;
        private static string FilePath => Path.Combine(GenFilePaths.ConfigFolderPath, "WorkRoles.Default.xml");

        // Owner: exact loaded RoleStore. Key: store identity. Value: local write
        // permission, dirty state, player baseline and pending window/actions.
        // Dependencies: explicit editor open, relevant published model changes,
        // committed durable preference writes, explicit default choices.
        // Refresh: events only; Root.Update drains pending actions. Equality:
        // equal captures never write. Teardown: Game.Dispose/world clear unsubscribes and
        // releases the store, window, baseline and pending requests.
        private static RoleStore? owner;
        private static SetupSession session = new SetupSession();
        private static string? playerBaseline;
        private static Window? pendingEditor;
        private static bool openPending, flushPending, savePending, clearPending, dialogPending;
        private static bool autoForSave, continuation, processing;
        private static readonly Action<ModelDomain> modelChanged = ModelChanged;
        private const ModelDomain Authored = ModelDomain.Catalog | ModelDomain.RolePresentation
            | ModelDomain.RoleWork | ModelDomain.RoleCapability | ModelDomain.RoleActivation
            | ModelDomain.RoleRules | ModelDomain.RoleRecommendation | ModelDomain.Preferences;

        internal static bool HasDefault { get { EnsureLoaded(); return savedXml != null; } }
        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                string xml = File.ReadAllText(FilePath, Encoding.UTF8);
                var document = RoleFile.Parse(xml);
                if (document.error != null || !document.hasRoles)
                { loadError = document.error ?? "Default contains no roles section"; return; }
                savedXml = RoleFile.Build(document);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { loadError = e.Message; }
        }

        internal static bool TrySeed(RoleStore store)
        {
            if (!store.IsNewWorld || MP.IsInMultiplayer && !MP.IsHosting) return false;
            EnsureLoaded();
            if (savedXml == null) return false;
            return RoleCommands.ApplyNewGameSetup(savedXml);
        }

        private static void Bind(RoleStore store)
        {
            if (ReferenceEquals(owner, store)) return;
            ResetSession();
            owner = store;
            owner.Revisions.Published += modelChanged;
        }

        internal static bool GateEditor(Window editor)
        {
            var store = RoleStore.Current;
            if (store == null || continuation) return false;
            Bind(store);
            if (pendingEditor != null) return true;
            if (session.Checked) return false;
            pendingEditor = editor; openPending = true;
            return true;
        }

        private static void ModelChanged(ModelDomain domains) { if ((domains & Authored) != 0) Changed(); }
        internal static void Changed()
        { if (ReferenceEquals(owner, RoleStore.Current) && owner != null) session.Changed(); }
        internal static void PlayerPreferencesWritten()
        {
            if (owner == null || !ReferenceEquals(owner, RoleStore.Current)) return;
            string current = RoleSetupBridge.CapturePlayer().ToXml("PlayerSettings").ToString();
            if (playerBaseline != null && playerBaseline != current) session.Changed();
            playerBaseline = current;
        }
        internal static void QueueFlush() { flushPending = true; }
        internal static void QueueDefaultDialog() { dialogPending = true; }
        internal static void QueueClear() { clearPending = true; }
        internal static void QueueSave(bool autoUpdate)
        {
            var store = RoleStore.Current;
            if (store == null) return;
            Bind(store); autoForSave = autoUpdate; savePending = true;
        }

        internal static void ProcessPending()
        {
            if (processing || !(openPending || flushPending || savePending || clearPending || dialogPending)
                || Scribe.mode != LoadSaveMode.Inactive) return;
            processing = true;
            try
            {
                if (owner != null && !ReferenceEquals(owner, RoleStore.Current)) { ResetSession(); return; }
                if (clearPending)
                {
                    clearPending = false;
                    try
                    {
                        if (File.Exists(FilePath)) File.Delete(FilePath);
                        savedXml = loadError = null; loaded = true;
                        WorkRolesMod.Settings.autoUpdateDefault = false; session.Disable();
                        WorkRolesMod.Settings.Write();
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { Report(e.Message); }
                }
                if (savePending)
                {
                    savePending = false; EnsureLoaded();
                    if (SaveCurrent())
                    {
                        WorkRolesMod.Settings.autoUpdateDefault = autoForSave;
                        session.Choose(autoForSave); session.Written(); WorkRolesMod.Settings.Write();
                    }
                }
                if (flushPending)
                {
                    flushPending = false;
                    if (session.Dirty && WorkRolesMod.Settings.autoUpdateDefault) SaveCurrent();
                }
                if (openPending && owner != null && Current.ProgramState == ProgramState.Playing)
                {
                    openPending = false;
                    if (continuation) { OpenPendingEditor(); return; }
                    EnsureLoaded();
                    playerBaseline = RoleSetupBridge.CapturePlayer().ToXml("PlayerSettings").ToString();
                    if (loadError != null) { Report(loadError); loadError = null; }
                    bool conflict = session.CheckOnOpen(WorkRolesMod.Settings.autoUpdateDefault,
                        savedXml != null, savedXml != null && Comparable(RoleIO.BuildXml(owner)) == Comparable(savedXml));
                    if (conflict) Find.WindowStack.Add(new Dialog_SetupChoice(true));
                    else OpenPendingEditor();
                }
                if (dialogPending)
                {
                    dialogPending = false;
                    Find.WindowStack.Add(new Dialog_SetupChoice(false));
                }
            }
            finally { processing = false; }
        }

        private static string Comparable(string xml)
        {
            var root = XElement.Parse(xml);
            root.Element("PlayerSettings")?.Remove();
            return root.ToString(SaveOptions.DisableFormatting);
        }

        internal static void Decide(bool useCurrent, bool disableAuto)
        {
            if (owner == null || !ReferenceEquals(owner, RoleStore.Current)) return;
            session.Choose(useCurrent);
            if (disableAuto)
            { WorkRolesMod.Settings.autoUpdateDefault = false; session.Disable(); WorkRolesMod.Settings.Write(); }
            if (useCurrent) flushPending = true;
            continuation = true; openPending = true;
        }

        private static void OpenPendingEditor()
        {
            Window? editor = pendingEditor; pendingEditor = null;
            continuation = true;
            try { if (editor != null) Find.WindowStack.Add(editor); }
            finally { continuation = false; }
        }

        private static bool SaveCurrent()
        {
            if (owner == null || !ReferenceEquals(owner, RoleStore.Current)) return false;
            string xml = RoleIO.BuildXml(owner);
            if (xml == savedXml) { session.Written(); return true; }
            string path = FilePath, temporary = path + ".tmp";
            try
            {
                File.WriteAllText(temporary, xml, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                savedXml = xml; loaded = true; loadError = null;
                playerBaseline = RoleSetupBridge.CapturePlayer().ToXml("PlayerSettings").ToString();
                session.Written(); return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { Report(e.Message); return false; }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            }
        }

        private static void Report(string error) => Messages.Message("WR_SetupFailed".Translate(error), MessageTypeDefOf.RejectInput, false);
        internal static void Dispose()
        {
            if (Scribe.mode == LoadSaveMode.Inactive && session.Dirty && WorkRolesMod.Settings.autoUpdateDefault) SaveCurrent();
            ResetSession();
        }
        private static void ResetSession()
        {
            if (owner != null) owner.Revisions.Published -= modelChanged;
            owner = null; pendingEditor = null; playerBaseline = null; session = new SetupSession();
            openPending = flushPending = savePending = clearPending = dialogPending = continuation = false;
        }
    }

    [HarmonyPatch(typeof(Root), nameof(Root.Update))]
    internal static class Patch_SetupPendingWork { private static void Postfix() => SetupDefaults.ProcessPending(); }
    [HarmonyPatch(typeof(Game), nameof(Game.Dispose))]
    internal static class Patch_SetupDisposal
    { private static void Prefix(Game __instance) { if (ReferenceEquals(Current.Game, __instance)) SetupDefaults.Dispose(); } }
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add))]
    internal static class Patch_SetupEditorGate
    {
        [HarmonyPriority(Priority.Last)]
        private static bool Prefix(Window window) => !(window is MainTabWindow_WorkRoles) || !SetupDefaults.GateEditor(window);
    }
}
