using System;

namespace WorkRoles.Core
{
    /// <summary>
    /// Lifecycle state for metadata derived from replaceable game definitions.
    /// Invalidation drops the owned object graph and permits a later generation
    /// to retry an integration that had disabled itself for the old generation.
    /// </summary>
    public sealed class DefinitionOwnedCache<T> where T : class
    {
        public bool Initialized { get; private set; }
        public bool Disabled { get; private set; }
        public T Value { get; private set; }

        public void Publish(T value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Initialized = true;
            Disabled = false;
        }

        public void Disable()
        {
            Value = null;
            Initialized = true;
            Disabled = true;
        }

        public void Invalidate()
        {
            Value = null;
            Initialized = false;
            Disabled = false;
        }
    }
}
