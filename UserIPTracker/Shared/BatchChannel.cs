using System.Threading.Channels;

namespace UserIPTracker.Shared;

// TODO: Упрощенный вариант без использования IDisposable или доп проверок на start/stop/write и тп.

/// <summary>
/// Асинхронный канал, который собирает элементы в батчи.
/// Передаёт батчи обработчику при достижении _batchSize или _maxDelay
/// </summary>
public class BatchChannel<T>
{
    private readonly Channel<T> _channel;
    private readonly Func<List<T>, Task> _onBatchReady;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _processingTask;

    private List<T> _currentBatch;
    private readonly ObjectPool<List<T>> _pool;

    private readonly int _batchSize;
    private readonly TimeSpan _maxDelay;

    private readonly SemaphoreSlim _flushLock = new(1, 1);

    public BatchChannel(int batchSize, TimeSpan maxDelay, Func<List<T>, Task> onBatchReady)
    {
        _batchSize = batchSize;
        _maxDelay = maxDelay;
        _onBatchReady = onBatchReady;

        _channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _pool = new ObjectPool<List<T>>(() => new List<T>(_batchSize), l => l.Clear());
        _currentBatch = _pool.Get();

        _processingTask = Task.Run(ProcessChannelAsync);
    }

    public ValueTask WriteAsync(T item)
    {
        return _channel.Writer.WriteAsync(item);
    }

    public async Task StopAsync()
    {
        _channel.Writer.Complete();
        await _cts.CancelAsync();
        await _processingTask;
    }

    private async Task ProcessChannelAsync()
    {
        var timerTask = TimerLoopAsync(_cts.Token);

        try
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                _currentBatch.Add(item);

                if (_currentBatch.Count >= _batchSize)
                {
                    await FlushBatchAsync();
                }
            }
        }
        catch (OperationCanceledException) { }

        // финальный flush перед завершением
        if (_currentBatch.Count > 0)
        {
            await FlushBatchAsync();
        }

        // Для корректного завершения
        await timerTask;
    }

    // таймер, который проверяет батч каждые _maxDelay
    private async Task TimerLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(_maxDelay, token);

                if (_currentBatch.Count > 0)
                {
                    await FlushBatchAsync();
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task FlushBatchAsync()
    {
        await _flushLock.WaitAsync();
        try
        {
            if (_currentBatch.Count == 0)
                return;

            var batchToSend = _currentBatch;
            _currentBatch = _pool.Get();

            await _onBatchReady(batchToSend);

            _pool.Return(batchToSend);
        }
        finally
        {
            _flushLock.Release();
        }
    }

    // простой Object Pool для списков
    private class ObjectPool<TItem>(Func<TItem> factory, Action<TItem> reset)
        where TItem : class
    {
        private readonly Stack<TItem> _items = new();

        public TItem Get() => _items.Count > 0 ? _items.Pop() : factory();

        public void Return(TItem item)
        {
            reset(item);
            _items.Push(item);
        }
    }
}
