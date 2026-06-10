using API.Infrastructure;

namespace API.Tests.Infrastructure;

public class LockManagerTests
{
    private readonly LockManager sut = new();

    [Fact]
    public void TryAcquire_ReturnsHandle_WhenKeyFree()
    {
        // Arrange / Act
        using var handle = sut.TryAcquire("key");

        // Assert
        handle.Should().NotBeNull();
    }

    [Fact]
    public void TryAcquire_ReturnsNull_WhenKeyAlreadyLocked()
    {
        // Arrange
        using var first = sut.TryAcquire("key");

        // Act
        using var second = sut.TryAcquire("key");

        // Assert
        second.Should().BeNull();
    }

    [Fact]
    public void TryAcquire_ReturnsHandle_AfterPreviousHandleDisposed()
    {
        // Arrange
        var first = sut.TryAcquire("key");
        first!.Dispose();

        // Act
        using var second = sut.TryAcquire("key");

        // Assert
        second.Should().NotBeNull();
    }

    [Fact]
    public void TryAcquire_DifferentKeys_DoNotInterfere()
    {
        // Arrange / Act
        using var first = sut.TryAcquire("key-1");
        using var second = sut.TryAcquire("key-2");

        // Assert
        first.Should().NotBeNull();
        second.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        // Arrange
        var handle = sut.TryAcquire("key");

        // Act
        var act = () =>
        {
            handle!.Dispose();
            handle.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void TryAcquire_CanAcquireRepeatedly_AfterSequentialDispose()
    {
        // Arrange / Act / Assert
        for (var i = 0; i < 10; i++)
        {
            var handle = sut.TryAcquire("key");
            handle.Should().NotBeNull($"iteration {i} should succeed");
            handle!.Dispose();
        }
    }

    [Fact]
    public async Task TryAcquire_OnlyOneSucceeds_UnderConcurrentAccess()
    {
        for (var round = 0; round < 5; round++)
        {
            // Arrange
            var acquiredCount = 0;
            var handles = new IDisposable?[20];

            // Act
            await Parallel.ForEachAsync(Enumerable.Range(0, 20), async (i, _) =>
            {
                await Task.Yield();
                handles[i] = sut.TryAcquire("shared-key");
                if (handles[i] != null) Interlocked.Increment(ref acquiredCount);
            });

            // Assert
            acquiredCount.Should().Be(1, $"round {round}");

            foreach (var handle in handles) handle?.Dispose();
        }
    }
}
