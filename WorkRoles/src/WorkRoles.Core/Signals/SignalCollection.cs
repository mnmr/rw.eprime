using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkRoles.Core.Signals
{
    public static class SignalCollection
    {
        public static IReadOnlyList<Signal> Collect<TContext>(
            TContext context,
            IReadOnlyList<Func<TContext, IEnumerable<Signal>>> providers,
            Action<int, Exception> onFailure = null)
        {
            if (providers == null) throw new ArgumentNullException(nameof(providers));
            var result = new List<Signal>();
            for (int i = 0; i < providers.Count; i++)
            {
                try
                {
                    Func<TContext, IEnumerable<Signal>> provider = providers[i];
                    if (provider == null) continue;
                    IEnumerable<Signal> provided = provider(context);
                    if (provided == null) continue;
                    foreach (Signal signal in provided)
                        if (signal != null) result.Add(signal);
                }
                catch (Exception exception)
                {
                    onFailure?.Invoke(i, exception);
                }
            }
            result.Sort(SignalComparer.Instance);
            return new ReadOnlyCollection<Signal>(result);
        }
    }
}
