using System.Collections.Concurrent;

namespace API.Infrastructure;

/// <summary>
/// Per-key non-blocking mutex. Acquisition is non-blocking: <see cref="TryAcquire"/> returns
/// <c>null</c> immediately if the key is already locked. The returned <see cref="IDisposable"/>
/// releases the lock on disposal.
/// </summary>
public interface ILockManager
{
    IDisposable? TryAcquire(string key);
}

/// <summary>
/// Backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/> with ref-counted entries, so entries
/// are removed once no thread holds or is waiting on a given key, keeping memory bounded to the set
/// of currently-active keys.
/// </summary>
public sealed class LockManager : ILockManager
{
    private readonly ConcurrentDictionary<string, KeyLock> entries = new();

    public IDisposable? TryAcquire(string key)
    {
        // Retry if the entry we latched was concurrently evicted before we could increment its ref count.
        while (true)
        {
            var entry = entries.GetOrAdd(key, _ => new KeyLock());
            Interlocked.Increment(ref entry.RefCount);

            // Verify the entry we incremented is still the live one for this key.
            // A concurrent release may have removed it between GetOrAdd and Increment.
            if (!entries.TryGetValue(key, out var current) || !ReferenceEquals(current, entry))
            {
                Interlocked.Decrement(ref entry.RefCount);
                continue;
            }

            // The semaphore (initialised 1,1) is the actual mutex; the dictionary only tracks entry lifetime.
            // Wait(0) = non-blocking: returns true if acquired, false if already held by another caller.
            if (!entry.Semaphore.Wait(0))
            {
                Decrement(key, entry);
                return null;
            }

            return new LockHandle(() =>
            {
                entry.Semaphore.Release();
                Decrement(key, entry);
            });
        }
    }

    private void Decrement(string key, KeyLock entry)
    {
        // KeyValuePair overload ensures we only remove the entry we own, not a replacement inserted by a new caller.
        // Disposal is safe here: RefCount==0 means no thread is inside Wait(0) or holding the semaphore.
        if (Interlocked.Decrement(ref entry.RefCount) == 0
            && entries.TryRemove(new KeyValuePair<string, KeyLock>(key, entry)))
        {
            entry.Semaphore.Dispose();
        }
    }

    private sealed class KeyLock
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int RefCount;
    }

    private sealed class LockHandle(Action release) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            release();
        }
    }
}
