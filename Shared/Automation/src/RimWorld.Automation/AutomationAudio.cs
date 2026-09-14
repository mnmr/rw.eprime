using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimWorld.Automation;

// Registered only after the profile, token and private desktop are verified.
// The zero-volume profile covers startup before the runtime initializes. Keep
// this process silent when settings tests reapply preferences, without changing
// their requested values or suppressing the game's sound playback behavior.
[HarmonyPatch(typeof(PrefsData), nameof(PrefsData.Apply))]
internal static class AutomationAudio
{
    [HarmonyPostfix]
    internal static void Mute()
    {
        if (UnityData.IsInMainThread) AudioListener.volume = 0f;
    }
}
