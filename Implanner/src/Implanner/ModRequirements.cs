using System;
using Verse;

namespace Implanner
{
    /// Bridges the engine-free MayRequire hooks in PlansXml to the game:
    /// import filtering checks the active mod list, export derivation resolves
    /// each implant kind's owning content pack. Both are cached static
    /// delegates so no call site allocates a method-group delegate.
    public static class ModRequirements
    {
        /// TryImport's isModActive hook.
        public static readonly Func<string, bool> IsModActive = IsActive;

        /// Export's packageIdOf hook: implant HediffDef name → owning
        /// packageId, or null for base-game content and unresolvable names.
        public static readonly Func<string, string?> PackageIdOf = ResolvePackageId;

        /// Matches vanilla's MayRequire evaluation on def list nodes
        /// (ModLister.AllModsActiveNoSuffix): case-insensitive and tolerant of
        /// the "_steam" packageId postfix on Workshop installs, which plain
        /// ModsConfig.IsActive is not.
        private static bool IsActive(string packageId) =>
            ModLister.GetActiveModWithIdentifier(packageId, ignorePostfix: true) != null;

        private static string? ResolvePackageId(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return null;
            // Implant kinds travel as HediffDef names (no '@' category form).
            HediffDef? def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
            ModContentPack? pack = def?.modContentPack;
            // PackageIdPlayerFacing: suffix-free, so an export from a Workshop
            // install imports cleanly against any install source of the mod.
            return pack == null || pack.IsCoreMod ? null : pack.PackageIdPlayerFacing;
        }
    }
}
