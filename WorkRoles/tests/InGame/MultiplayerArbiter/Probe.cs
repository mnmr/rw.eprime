using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRolesArbiterVerification
{
    // Real two-instance multiplayer check. After the save loads, the probe
    // hosts a Multiplayer server with the Arbiter enabled: Multiplayer starts
    // a second, headless game process that joins over 127.0.0.1 and simulates
    // in lockstep. Multiplayer compares both simulations continuously and
    // reports any divergence as a desync, with stack traces.
    [StaticConstructorOnStartup]
    public static class Probe
    {
        // The run has a fixed 5-minute wall-clock budget after game-ready
        // (owner, 2026-11-01); the probe ends its observation shortly before.
        private const float ObserveSeconds = 280f;
        private static int stage, frames;
        private static bool done;
        private static float hostStart, lastStatus, lastSpeedFix, arbiterSince = -1f;
        private static int arbiterTick;
        private static string Root => GenFilePaths.SaveDataFolderPath;
        private static readonly Type? MpType = AccessTools.TypeByName("Multiplayer.Client.Multiplayer");

        static Probe()
        {
            // The arbiter is the same game with the same mods: stay inert there.
            if (Environment.GetCommandLineArgs().Contains("-arbiter")) { done = true; return; }
            var harmony = new Harmony("eprime.workroles.arbiterprobe");
            harmony.Patch(AccessTools.Method(typeof(Root_Play), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
            harmony.Patch(AccessTools.Method(typeof(Root_Entry), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(EntryUpdate)));
            // Diagnostic: who allocates bill ids (the counter diverged by one).
            harmony.Patch(AccessTools.Method(typeof(UniqueIDsManager), nameof(UniqueIDsManager.GetNextBillID)),
                postfix: new HarmonyMethod(typeof(Probe), nameof(BillIdAllocated)));
            // Diagnostic: every world-context random draw with its callers,
            // to diff host and client (the world random state diverged by one).
            var worldTick = AccessTools.Method(AccessTools.TypeByName("Multiplayer.Client.AsyncTime.AsyncWorldTimeComp"), "Tick");
            if (worldTick != null)
                harmony.Patch(worldTick, prefix: new HarmonyMethod(typeof(Probe), nameof(WorldTickStart)),
                    finalizer: new HarmonyMethod(typeof(Probe), nameof(WorldTickEnd)));
            foreach (string getter in new[] { "Value", "Int" })
                harmony.Patch(AccessTools.PropertyGetter(typeof(Rand), getter), postfix: new HarmonyMethod(typeof(Probe), nameof(RandDrawn)));
        }

        private static readonly AccessTools.FieldRef<System.Collections.Generic.Stack<ulong>> RandStack =
            AccessTools.StaticFieldRefAccess<System.Collections.Generic.Stack<ulong>>(AccessTools.Field(typeof(Rand), "stateStack"));
        private static bool inWorldTick;
        private static int worldDraws;
        private static StreamWriter? drawLog;
        private static void WorldTickStart() => inWorldTick = true;
        private static void WorldTickEnd() => inWorldTick = false;
        private static void RandDrawn()
        {
            if (!inWorldTick || worldDraws >= 300000) return;
            worldDraws++;
            drawLog ??= new StreamWriter(Path.Combine(Root, "world-rand.txt"), append: false);
            string frames = string.Join(" <- ", new System.Diagnostics.StackTrace(2, false).GetFrames()
                .Select(f => f.GetMethod()).Where(m => m != null).Take(8)
                .Select(m => m!.DeclaringType?.Name + "." + m.Name));
            // Draws inside a pushed random state are not in Multiplayer's traces.
            drawLog.WriteLine(Find.TickManager.TicksGame + (RandStack().Count > 1 ? " PUSHED " : " ") + frames);
            if (worldDraws % 500 == 0) drawLog.Flush();
        }

        private static int billIdLogs;
        private static void BillIdAllocated(int __result)
        {
            if (++billIdLogs > 40 || Current.Game == null) return;
            string frames = string.Join(" <- ", new System.Diagnostics.StackTrace(1, false).GetFrames()
                .Select(f => f.GetMethod()).Where(m => m != null).Take(14)
                .Select(m => m!.DeclaringType?.Name + "." + m.Name));
            Status("BILLID " + __result + " tick " + Find.TickManager?.TicksGame + " session=" + (Session != null) + " : " + frames);
        }

        // A player joins from the main menu: Multiplayer's game loop patches
        // assume no single-player game is running while it connects.
        private static int entryFrames;
        private static float lastConnect = -100f;
        private static void EntryUpdate()
        {
            if (done || !IsClient || LongEventHandler.AnyEventNowOrWaiting) return;
            if (stage == 1 && Session == null) stage = 10; // refused or dropped: try again
            if (stage != 10 || ++entryFrames < 10 || Time.realtimeSinceStartup - lastConnect < 3f) return;
            foreach (Window window in Find.WindowStack.Windows.ToArray())
                if (window.GetType().Name == "DisconnectedWindow") window.Close(false);
            lastConnect = Time.realtimeSinceStartup;
            try { Connect(); }
            catch (Exception ex) { done = true; Log.Error("[ArbiterProbe] FAILED " + ex); Mark("FAILED " + ex.Message); }
        }

        private static void Connect()
        {
            AccessTools.Field(MpType, "username").SetValue(null, "WrClient");
            Type connectorType = AccessTools.AllTypes().First(t => t.Name == "LiteNetConnector");
            var args = new object?[] { ServerAddress, null };
            if (!(bool)AccessTools.Method(connectorType, "TryParse").Invoke(null, args))
                throw new InvalidOperationException("Cannot parse " + ServerAddress);
            AccessTools.Method(AccessTools.TypeByName("Multiplayer.Client.ClientUtil"), "TryConnectWithWindow")
                .Invoke(null, new[] { args[1], (object)false });
            Status("client: connecting to " + ServerAddress + " from the main menu");
            stage = 1; Mark("RUNNING connecting");
        }

        private static void Mark(string text)
        {
            drawLog?.Flush();
            File.WriteAllText(Path.Combine(Root, "arbiter-stage.txt"), text);
        }
        private static void Status(string text)
        {
            Log.Message("[ArbiterProbe] " + text);
            File.AppendAllText(Path.Combine(Root, "arbiter-status.txt"), DateTime.UtcNow.ToString("o") + " " + text + "\n");
        }
        // The same synced command the time-control buttons send.
        private static void SendSpeed(TimeSpeed speed) =>
            AccessTools.Method(AccessTools.TypeByName("Multiplayer.Client.AsyncTime.MpTimeControls"), "SendTimeChange")
                .Invoke(null, new[] { AccessTools.Property(MpType, "AsyncWorldTime").GetValue(null), (object)speed });
        private static object? Session => AccessTools.Field(MpType, "session")?.GetValue(null);
        private static T Member<T>(object target, string name)
        {
            var type = target.GetType();
            var field = AccessTools.Field(type, name);
            if (field != null) return (T)field.GetValue(target);
            return (T)AccessTools.Property(type, name).GetValue(target);
        }

        // Host listens on loopback only; the client run connects here.
        // Per-run loopback port written by the runner (parallel runs).
        private static string ServerAddress => "127.0.0.1:" + (File.Exists(Path.Combine(Root, "mp-port.txt"))
            ? File.ReadAllText(Path.Combine(Root, "mp-port.txt")).Trim() : "30502");
        // Mod-set bisection runs may load the probe without WorkRoles.
        private static readonly bool WorkRolesLoaded = ModsConfig.IsActive("mnmr.workroles");
        private static bool IsClient => File.Exists(Path.Combine(Root, "mp-role.txt"))
            && File.ReadAllText(Path.Combine(Root, "mp-role.txt")).Trim() == "client";

        private static void Update()
        {
            if (done || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null
                || LongEventHandler.AnyEventNowOrWaiting) return;
            try
            {
                if (IsClient) { ClientUpdate(); return; }
                if (IsPrep) { if (++frames >= 3) Prepare(); return; }
                switch (stage)
                {
                    case 0: if (++frames >= 3) Host(); break;
                    case 1: StartTime(); break;
                    case 2: Watch(); break;
                }
            }
            catch (Exception ex)
            {
                done = true;
                Log.Error("[ArbiterProbe] FAILED " + ex);
                Mark("FAILED " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void CheckWorkRoles()
        {
            var store = WorkRoles.RoleStore.Current;
            int managed = store == null ? 0 : Find.CurrentMap.mapPawns.FreeColonistsSpawned.Count(store.IsManaged);
            Status("WorkRoles store seeded=" + store?.seeded + " roles=" + store?.roles.Count
                + " managed colonists=" + managed + "/" + Find.CurrentMap.mapPawns.FreeColonistsSpawnedCount);
            if (managed == 0) throw new InvalidOperationException("No WorkRoles-managed colonists to exercise");
        }

        private static bool IsPrep => File.Exists(Path.Combine(Root, "mp-role.txt"))
            && File.ReadAllText(Path.Combine(Root, "mp-role.txt")).Trim() == "prep";

        // Fixture preparation: the source save loaded with only vanilla, the
        // DLCs and our mods. Jobs from removed mods load without a definition;
        // end them through the job tracker, then save the clean fixture.
        private static void Prepare()
        {
            done = true;
            int cleared = 0;
            foreach (Map map in Find.Maps)
                foreach (Pawn pawn in map.mapPawns.AllPawns.ToArray())
                    if (pawn.jobs?.curJob != null && (pawn.jobs.curJob.def == null || pawn.jobs.curDriver == null))
                    {
                        pawn.jobs.curJob = null; pawn.jobs.curDriver = null;
                        pawn.jobs.ClearQueuedJobs(); pawn.pather?.StopDead(); cleared++;
                    }
            GameDataSaveLoader.SaveGame("Fisso-NAM-clean");
            Status("prep: cleared " + cleared + " broken jobs; saved Fisso-NAM-clean with " + string.Join(", ",
                ModsConfig.ActiveModsInLoadOrder.Select(m => m.PackageId)));
            Mark("EXPORTED");
        }

        private static void Host()
        {
            if (MpType == null) throw new InvalidOperationException("Multiplayer is not loaded");
            Status("active mods: " + string.Join(", ", ModsConfig.ActiveModsInLoadOrder.Select(m => m.PackageId)));
            if (WorkRolesLoaded) CheckWorkRoles();
            // The arbiter shares this save-data folder; vanilla would load the
            // run's autostart save there instead of joining the host.
            foreach (FileInfo file in new DirectoryInfo(GenFilePaths.SavedGamesFolderPath).GetFiles("*.rws"))
                if (string.Equals(Path.GetFileNameWithoutExtension(file.Name), "autostart", StringComparison.OrdinalIgnoreCase))
                    file.MoveTo(Path.Combine(file.DirectoryName, "AutostartLoaded.rws.bak"));

            Type settingsType = AccessTools.TypeByName("Multiplayer.Common.ServerSettings");
            object settings = Activator.CreateInstance(settingsType);
            void Set(string name, object value) => AccessTools.Field(settingsType, name).SetValue(settings, value);
            Set("gameName", "WorkRoles arbiter check");
            Set("lan", false); Set("direct", true); Set("steam", false);
            Set("directAddress", ServerAddress);
            Set("arbiter", true); Set("desyncTraces", true);
            Set("pauseOnJoin", false); Set("pauseOnDesync", true);
            Set("pauseOnLetter", Enum.Parse(AccessTools.TypeByName("Multiplayer.Common.PauseOnLetter"), "Never"));
            Set("autosaveInterval", 1000f);
            // No fresh world snapshot when the arbiter joins: it joins from the
            // hosting snapshot and replays the commands since.
            Set("autoJoinPoint", Enum.Parse(AccessTools.TypeByName("Multiplayer.Common.AutoJoinPointFlags"), "Desync"));
            var hostWindow = AccessTools.TypeByName("Multiplayer.Client.HostWindow");
            if (!(bool)AccessTools.Method(hostWindow, "TryStartLocalServer").Invoke(null, new[] { settings }))
                throw new InvalidOperationException("Local server did not start");
            hostStart = Time.realtimeSinceStartup;
            AccessTools.Method(AccessTools.TypeByName("Multiplayer.Client.HostUtil"), "HostServer")
                .Invoke(null, new[] { settings, (object)false });
            Status("hosting requested");
            stage = 1; Mark("RUNNING hosting");
        }

        private static void StartTime()
        {
            object? session = Session;
            if (session == null || Member<object>(session, "client") == null) return;
            if (AccessTools.Property(MpType, "AsyncWorldTime").GetValue(null) == null) return;
            // Never paused (owner). 1x while the arbiter and client replay up to
            // the host, so they can catch up; 3x for the whole observation.
            SendSpeed(TimeSpeed.Normal);
            Status("hosted at tick " + Find.TickManager.TicksGame + "; 1x until the arbiter and client have caught up");
            stage = 2; Mark("RUNNING waiting for arbiter");
        }

        private static void Watch()
        {
            object session = Session!;
            float now = Time.realtimeSinceStartup;
            bool arbiter = Member<bool>(session, "ArbiterPlaying");
            if (Member<bool>(session, "desynced")) { SaveDesync(); return; }
            if (arbiter && arbiterSince < 0f)
            {
                arbiterSince = now; arbiterTick = Find.TickManager.TicksGame;
                Status("arbiter playing at tick " + arbiterTick);
                Mark("RUNNING play with arbiter");
            }
            // Act once the client has caught up too (or give up waiting after 150 s).
            bool client = PlayerPlaying(session, "WrClient");
            if (client && !clientSeen) { clientSeen = true; Status("client caught up and playing at tick " + Find.TickManager.TicksGame); }
            if (arbiterSince >= 0f && (client || now - hostStart > 150f))
            {
                if (exerciseStart < 0f) exerciseStart = now;
                if (WorkRolesLoaded) Exercise(now - exerciseStart, pawnRank: 0);
            }
            TimeSpeed wanted = arbiter && client ? TimeSpeed.Superfast : TimeSpeed.Normal;
            if (Find.TickManager.CurTimeSpeed != wanted && now - lastSpeedFix > 2f)
            {
                lastSpeedFix = now;
                Status("speed was " + Find.TickManager.CurTimeSpeed + "; setting " + wanted);
                SendSpeed(wanted);
            }
            if (now - lastStatus > 15f)
            {
                lastStatus = now;
                Status("tick " + Find.TickManager.TicksGame + " speed " + Find.TickManager.CurTimeSpeed + " arbiter=" + arbiter + " client=" + client);
                if (WorkRolesLoaded) LogState();
            }
            if (now - hostStart >= ObserveSeconds)
            {
                done = true;
                if (WorkRolesLoaded) LogState();
                if (arbiterSince < 0f) { Mark("FAILED arbiter never started playing"); return; }
                Status("no desync over " + (Find.TickManager.TicksGame - arbiterTick) + " ticks with the arbiter");
                Mark("SUCCESS");
            }
        }

        // WorkRoles UI actions, issued from the UI context through the same
        // synced commands the Colonists and Roles tabs call: assign a role to
        // a colonist, then switch a role off and on again for everyone. Host
        // and client use different colonists (pawnRank) and timings.
        private static int exerciseStep;
        private static Pawn? exercisePawn;
        private static int exerciseRole = -1;
        private static float exerciseStart = -1f;
        private static bool clientSeen;

        private static bool PlayerPlaying(object session, string username)
        {
            foreach (object player in Member<System.Collections.IEnumerable>(session, "players"))
                if (Member<string>(player, "username") == username && Member<object>(player, "status").ToString() == "Playing")
                    return true;
            return false;
        }

        // The state both sides' commands touch, for comparing host and client.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void LogState()
        {
            var store = WorkRoles.RoleStore.Current;
            if (store == null || Find.CurrentMap == null) return;
            // Loops, not lambdas: compiler closures over WorkRoles types become
            // nested types that break type scanning when WorkRoles is absent.
            var text = new System.Text.StringBuilder("STATE tick " + Find.TickManager.TicksGame);
            foreach (Pawn pawn in Find.CurrentMap.mapPawns.FreeColonistsSpawned.Where(store.IsManaged).OrderBy(p => p.thingIDNumber).Take(2))
            {
                text.Append(' ').Append(pawn.thingIDNumber).Append("=[");
                var assignments = store.pawnSets[pawn].assignments;
                for (int i = 0; i < assignments.Count; i++) text.Append(i == 0 ? "" : ",").Append(assignments[i].roleId);
                text.Append(']');
            }
            text.Append(" disabled=[");
            foreach (var role in store.roles) if (!role.enabled) text.Append(role.id).Append(' ');
            Status(text.Append(']').ToString());
        }

        private static bool Holds(Pawn pawn, int roleId)
        {
            var assignments = WorkRoles.RoleStore.Current!.pawnSets[pawn].assignments;
            for (int i = 0; i < assignments.Count; i++) if (assignments[i].roleId == roleId) return true;
            return false;
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void Exercise(float since, int pawnRank)
        {
            var store = WorkRoles.RoleStore.Current;
            if (store == null) return;
            float offset = pawnRank * 7f;
            if (exerciseStep == 0 && since >= 10f + offset)
            {
                exercisePawn = Find.CurrentMap.mapPawns.FreeColonistsSpawned.Where(store.IsManaged)
                    .OrderBy(p => p.thingIDNumber).Skip(pawnRank).First();
                WorkRoles.Role? role = null;
                foreach (var candidate in store.roles)
                    if (candidate.enabled && !candidate.composite && !candidate.blocker && !Holds(exercisePawn, candidate.id)
                        && (role == null || candidate.id < role.id)) role = candidate;
                if (role == null) { exerciseStep = 8; return; }
                exerciseRole = role.id;
                WorkRoles.RoleCommands.AssignRole(exercisePawn, role.id);
                Status("UI: assign role " + role.label + " to " + exercisePawn.LabelShort);
                exerciseStep = 1;
            }
            else if (exerciseStep == 1 && since >= 20f + offset)
            {
                Status("UI: assigned=" + Holds(exercisePawn!, exerciseRole)
                    + "; toggling role " + exerciseRole + " off globally");
                WorkRoles.RoleCommands.ToggleRoleGlobal(exerciseRole);
                exerciseStep = 2;
            }
            else if (exerciseStep == 2 && since >= 35f + offset)
            {
                Status("UI: role enabled=" + store.RoleById(exerciseRole)?.enabled + "; toggling it back on");
                WorkRoles.RoleCommands.ToggleRoleGlobal(exerciseRole);
                exerciseStep = 3;
            }
            else if (exerciseStep == 3 && since >= 45f + offset)
            {
                Status("UI: role enabled=" + store.RoleById(exerciseRole)?.enabled + " after toggling back; opening the WorkRoles tab");
                var tab = DefDatabase<MainButtonDef>.AllDefsListForReading.First(d => d.TabWindow is WorkRoles.UI.MainTabWindow_WorkRoles);
                Find.MainTabsRoot.SetCurrentTab(tab);
                exerciseStep = 4;
            }
            else if (exerciseStep == 4 && since >= 55f + offset)
            {
                Find.MainTabsRoot.EscapeCurrentTab(false);
                // Implanner: its window, a new plan, automation pause and resume.
                Open("Implanner.UI.Dialog_Implanner");
                Call("Implanner.PlannerCommands", "CreatePlan", "MP probe plan " + pawnRank, 0);
                Call("Implanner.PlannerCommands", "SetAutomationPaused", true);
                Status("UI: Implanner window open; created a plan; paused automation");
                exerciseStep = 5;
            }
            else if (exerciseStep == 5 && since >= 65f + offset)
            {
                CloseAll();
                Call("Implanner.PlannerCommands", "SetAutomationPaused", false);
                // Readouts: its configuration window and a threshold.
                Open("EPrimeReadouts.UI.Dialog_ReadoutConfig");
                Call("EPrimeReadouts.ReadoutCommands", "SetThreshold", pawnRank == 0 ? "Steel" : "WoodLog", 100, 50);
                Status("UI: Implanner automation resumed; Readouts window open; threshold set");
                exerciseStep = 6;
            }
            else if (exerciseStep == 6 && since >= 75f + offset)
            {
                CloseAll();
                Call("EPrimeReadouts.ReadoutCommands", "ClearThreshold", pawnRank == 0 ? "Steel" : "WoodLog");
                // Quality Jobs: a real bill's configuration dialog plus its settings.
                var bills = Find.CurrentMap.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>()
                    .SelectMany(t => t.BillStack.Bills).Where(b => b.recipe.ProducedThingDef?.HasComp(typeof(CompQuality)) == true).ToList();
                if (bills.Count > pawnRank)
                {
                    Bill bill = bills[pawnRank];
                    Find.WindowStack.Add(new Dialog_BillConfig((Bill_Production)bill, ((Thing)bill.billStack.billGiver).Position));
                    Call("QualityJobs.Commands", "SetBillManaged", bill.GetUniqueLoadID(), true);
                    Call("QualityJobs.Commands", "SetBillTargetQuality", bill.GetUniqueLoadID(), 4);
                    Status("UI: Readouts threshold cleared; Quality Jobs bill " + bill.LabelCap + " managed, target Masterwork");
                }
                else Status("UI: no quality bill for Quality Jobs (" + bills.Count + ")");
                exerciseStep = 7;
            }
            else if (exerciseStep == 7 && since >= 85f + offset)
            {
                CloseAll();
                Status("UI: all mod actions done");
                exerciseStep = 8;
            }
        }

        private static void Call(string type, string method, params object[] args) =>
            AccessTools.Method(AccessTools.TypeByName(type), method).Invoke(null, args);
        private static void Open(string type) =>
            Find.WindowStack.Add((Window)Activator.CreateInstance(AccessTools.TypeByName(type)));
        private static void CloseAll()
        {
            foreach (Window window in Find.WindowStack.Windows.ToArray())
                if (window.GetType().Namespace?.StartsWith("Multiplayer") != true && !(window is MainTabWindow))
                    window.Close(false);
        }

        // Client run: joins the host over loopback, then exercises the UI too.
        private static float joinedSince = -1f;
        private static void ClientUpdate()
        {
            float now = Time.realtimeSinceStartup;
            object? session = Session;
            if (stage == 0)
            {
                // Booted alongside the host: keep the loaded save ticking (the
                // run guard needs progress) until the runner reports the server.
                if (++frames < 3) return;
                if (!File.Exists(Path.Combine(Root, "mp-server-up.txt"))) return;
                Status("client: server is up; leaving the loaded save for the main menu");
                stage = 10; Mark("RUNNING to main menu");
                GenScene.GoToMainMenu();
                return;
            }
            if (stage == 10) return;
            if (session == null)
            {
                if (joinedSince >= 0f) { done = true; Status("client: session ended (host stopped)"); Mark("SUCCESS"); }
                return;
            }
            if (Member<bool>(session, "desynced")) { SaveDesync(); return; }
            string state = Member<object>(session, "client")?.GetType().GetProperty("State")?.GetValue(Member<object>(session, "client"))?.ToString() ?? "";
            if (state != "ClientPlaying") return;
            // Commands only after replaying up to the host's present.
            if ((bool)AccessTools.Property(AccessTools.TypeByName("Multiplayer.Client.TickPatch"), "Simulating").GetValue(null)) return;
            if (joinedSince < 0f)
            {
                joinedSince = now;
                Status("client: caught up and playing at tick " + Find.TickManager.TicksGame);
                Mark("RUNNING client playing");
            }
            if (WorkRolesLoaded) Exercise(now - joinedSince, pawnRank: 1);
            if (now - lastStatus > 15f)
            {
                lastStatus = now;
                Status("client: tick " + Find.TickManager.TicksGame + " speed " + Find.TickManager.CurTimeSpeed);
                if (WorkRolesLoaded) LogState();
            }
        }

        private static void SaveDesync()
        {
            var window = Find.WindowStack.Windows.FirstOrDefault(w => w.GetType().Name == "DesyncedWindow");
            if (window == null) return; // Multiplayer opens it on the next main-thread pass
            done = true;
            object info = Member<object>(window, "desyncInfo");
            object? hostInfo = AccessTools.Field(window.GetType(), "hostInfo")?.GetValue(window);
            AccessTools.Method(info.GetType(), "Save").Invoke(info, new[] { hostInfo });
            Status("DESYNC at tick " + Find.TickManager.TicksGame + " (" + (arbiterSince >= 0f ? "after" : "before")
                + " arbiter joined); report saved to MpDesyncs");
            Mark("DESYNC");
        }
    }
}
