namespace WorkRoles.Core
{
    /// <summary>
    /// Holds one deferred value. User updates take precedence over automatic
    /// updates until the value is consumed or cleared.
    /// </summary>
    public sealed class PendingUpdate<T>
    {
        private T value;
        private bool hasValue;
        private bool userValue;

        public void QueueAutomatic(T next)
        {
            if (hasValue && userValue) return;
            value = next;
            hasValue = true;
            userValue = false;
        }

        public void QueueUser(T next)
        {
            value = next;
            hasValue = true;
            userValue = true;
        }

        public bool TryGetUser(out T pendingUser)
        {
            if (hasValue && userValue)
            {
                pendingUser = value;
                return true;
            }
            pendingUser = default(T);
            return false;
        }

        public bool TryConsume(out T next)
        {
            if (!hasValue)
            {
                next = default(T);
                return false;
            }
            next = value;
            Clear();
            return true;
        }

        public void Clear()
        {
            value = default(T);
            hasValue = false;
            userValue = false;
        }
    }
}
