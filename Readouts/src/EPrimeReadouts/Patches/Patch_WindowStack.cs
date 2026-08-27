using EPrimeReadouts.UI;
using HarmonyLib;
using Verse;

namespace EPrimeReadouts.Patches
{
    /// Every KeyBindingDef check — KeyDownEvent, JustPressed, IsDown, all of
    /// them, including Input-polled camera dolly — returns false while
    /// WindowStack.AnySearchWidgetFocused is true. Vanilla only consults the
    /// CommonSearchWidget of open windows; the readout's search field lives
    /// outside the window stack, so it reports through the same gate here.
    /// Typed characters then reach only the text field, never game shortcuts.
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.AnySearchWidgetFocused),
        MethodType.Getter)]
    public static class Patch_WindowStack
    {
        public static void Postfix(ref bool __result)
        {
            if (!__result) __result = ReadoutPanel.SearchFieldCapturesInput;
        }
    }
}
