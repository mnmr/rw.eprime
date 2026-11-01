using Multiplayer.API;

namespace RimShared.GameLib
{
    /// Multiplayer sync-command state that is safe with any bundled API stub.
    /// RimWorld loads only the first 0MultiplayerAPI assembly in mod order, so
    /// another mod's older stub replaces ours. Members newer than API 0.3
    /// (IsExecutingSyncCommand* arrived in 0.4, InInterface in 0.6) then throw
    /// MissingMethodException when the calling method is JIT-compiled, even in
    /// single player. Call newer members only through this class.
    internal static class MpSyncState
    {
        // ponytail: a stub older than 0.4 reports false; real Multiplayer
        // sessions behind such a stub lose the replay guard, and Multiplayer
        // itself is unlikely to work with that stub anyway.
        private static readonly System.Func<bool>? executing = Bind("IsExecutingSyncCommand");
        private static readonly System.Func<bool>? executingBySelf = Bind("IsExecutingSyncCommandIssuedBySelf");

        private static System.Func<bool>? Bind(string property)
        {
            var getter = typeof(MP).GetProperty(property,
                System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Static)?.GetGetMethod();
            if (getter == null || getter.ReturnType != typeof(bool)) return null;
            return (System.Func<bool>)System.Delegate.CreateDelegate(typeof(System.Func<bool>), getter);
        }

        /// True while any synchronized command is replaying on this client.
        internal static bool IsExecutingSyncCommand => executing != null && executing();

        /// True while replaying a command this client issued. False when the
        /// stub cannot tell, so per-player side effects never run on every client.
        internal static bool IsExecutingSyncCommandIssuedBySelf => executingBySelf != null && executingBySelf();
    }
}
