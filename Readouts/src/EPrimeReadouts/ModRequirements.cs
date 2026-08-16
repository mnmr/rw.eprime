using System;
using Verse;

namespace EPrimeReadouts
{
    /// Bridges the engine-free MayRequire hooks in ReadoutsXml to the game:
    /// import filtering checks the active mod list, export derivation resolves
    /// each def's owning content pack. Both are cached static delegates so no
    /// call site allocates a method-group delegate.
    public static class ModRequirements
    {
        /// TryImport's isModActive hook.
        public static readonly Func<string, bool> IsModActive = IsActive;

        /// Export's packageIdOf hook: defName or "@CategoryDefName" → owning
        /// packageId, or null for base-game content and unresolvable names.
        public static readonly Func<string, string?> PackageIdOf = ResolvePackageId;

        /// Matches vanilla's MayRequire evaluation on def list nodes
        /// (ModLister.AllModsActiveNoSuffix): case-insensitive and tolerant of
        /// the "_steam" packageId postfix on Workshop installs, which plain
        /// ModsConfig.IsActive is not.
        private static bool IsActive(string packageId) =>
            ModLister.GetActiveModWithIdentifier(packageId, ignorePostfix: true) != null;

        private static string? ResolvePackageId(string member)
        {
            if (string.IsNullOrEmpty(member)) return null;
            Def? def = member[0] == '@'
                ? (Def)DefDatabase<ThingCategoryDef>.GetNamedSilentFail(member.Substring(1))
                : DefDatabase<ThingDef>.GetNamedSilentFail(member);
            ModContentPack? pack = def?.modContentPack;
            // PackageIdPlayerFacing: suffix-free, so an export from a Workshop
            // install imports cleanly against any install source of the mod.
            return pack == null || pack.IsCoreMod ? null : pack.PackageIdPlayerFacing;
        }
    }
}
