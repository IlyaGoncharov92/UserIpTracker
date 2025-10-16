using UserIPTracker.Shared;

namespace UnitTests;

// TODO: в реал проекте я бы еще потестировал что _batchSize и _maxDelay всегда срабатывают по отдельности.
public class BatchChannelTests
{
    /// <summary>
    /// Тест на многопоточку, что все данные приходят.
    /// </summary>
    [Fact]
    public async Task ConcurrentWriters_ShouldProcessAllItems()
    {
        // Arrange
        var received = new List<int>();
        var receivedLock = new object();

        async Task OnBatchReady(List<int> batch)
        {
            // имитируем небольшую асинхронную обработку
            await Task.Delay(5);
            lock (receivedLock)
            {
                received.AddRange(batch);
            }
        }

        var channel = new BatchChannel<int>(
            batchSize: 10,
            maxDelay: TimeSpan.FromMilliseconds(200),
            onBatchReady: OnBatchReady
        );

        int writersCount = 10;
        int itemsPerWriter = 1_000;
        int totalItems = writersCount * itemsPerWriter;

        // Act
        var tasks = Enumerable.Range(0, writersCount)
            .Select(w => Task.Run(async () =>
            {
                int start = w * itemsPerWriter;
                int end = start + itemsPerWriter;

                for (int i = start; i < end; i++)
                {
                    await channel.WriteAsync(i);
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        await channel.StopAsync();

        // Assert
        Assert.Equal(totalItems, received.Count);

        var expected = Enumerable.Range(0, totalItems).OrderBy(x => x).ToArray();
        var actual = received.OrderBy(x => x).ToArray();

        Assert.True(expected.SequenceEqual(actual), "Некоторые элементы потеряны или продублированы при многопоточной записи");
    }
}