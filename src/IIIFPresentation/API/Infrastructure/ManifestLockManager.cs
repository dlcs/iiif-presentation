using System.Collections.Concurrent;

namespace API.Infrastructure;

/// <summary>
/// Per-key non-blocking mutex. Acquisition is non-blocking: <see cref="TryAcquire"/> returns
/// <c>null</c> immediately if the key is already locked. The returned <see cref="IDisposable"/>
/// releases the lock on disposal.
/// </summary>
public interface IManifestLockManager
{
    IDisposable? TryAcquire(string key);
}

/// <summary>
/// Backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/> with ref-counted entries, so entries
/// are removed once no thread holds or is waiting on a given key, keeping memory bounded to the set
/// of currently-active keys.
/// </summary>
public sealed class ManifestLockManager : IManifestLockManager
{
    private readonly ConcurrentDictionary<string, Entry> entries = new();

    public IDisposable? TryAcquire(string key)
    {
        while (true)
        {
            var entry = entries.GetOrAdd(key, _ => new Entry());
            Interlocked.Increment(ref entry.RefCount);

            // Verify the entry we incremented is still the live one for this key.
            // A concurrent release may have removed it between GetOrAdd and Increment.
            if (!entries.TryGetValue(key, out var current) || !ReferenceEquals(current, entry))
            {
                Interlocked.Decrement(ref entry.RefCount);
                continue;
            }

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

    private void Decrement(string key, Entry entry)
    {
        if (Interlocked.Decrement(ref entry.RefCount) == 0
            && entries.TryRemove(new KeyValuePair<string, Entry>(key, entry)))
        {
            entry.Semaphore.Dispose();
        }
    }

    private sealed class Entry
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
