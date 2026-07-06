using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Textures that must be loaded on the main thread at startup.
    [StaticConstructorOnStartup]
    public static class WorkRolesTex
    {
        public static readonly Texture2D PassionMinor = ContentFinder<Texture2D>.Get("UI/Icons/PassionMinor");
        public static readonly Texture2D PassionMajor = ContentFinder<Texture2D>.Get("UI/Icons/PassionMajor");
    }
}
