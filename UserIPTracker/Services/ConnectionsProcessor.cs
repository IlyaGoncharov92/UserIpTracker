using NpgsqlTypes;
using UserIPTracker.Models;
using UserIPTracker.Shared;

namespace UserIPTracker.Services;

public class ConnectionsProcessor
{
    private static int _instanceCreated = 0;

    private readonly BatchChannel<ConnectionDto> _connectionsChannel;
    private readonly IServiceProvider _serviceProvider;
    
    public ConnectionsProcessor(IServiceProvider serviceProvider)
    {
        if (Interlocked.Increment(ref _instanceCreated) > 1)
            throw new InvalidOperationException("An instance of this class has already been created. Use the DI container.");

        _serviceProvider = serviceProvider;

        _connectionsChannel = new BatchChannel<ConnectionDto>(
            batchSize: 10000,
            maxDelay: TimeSpan.FromMilliseconds(200),
            onBatchReady: OnBatchReady
        );
    }

    public ValueTask WriteAsync(ConnectionDto item)
    {
        return _connectionsChannel.WriteAsync(item);
    }
    
    private async Task OnBatchReady(List<ConnectionDto> batch)
    {
        using var scope = _serviceProvider.CreateScope();
        var connectionsRepository = scope.ServiceProvider.GetRequiredService<ConnectionsRepository>();
        
        var data = batch.Select(x => new UserConnection
        {
            UserId = x.UserId,
            IpStr = x.Ip,
            IpInet = new NpgsqlInet(x.Ip),
            ConnectedAt = x.ConnectedAt
        });
        
        await connectionsRepository.BulkInsertConnectionsAsync(data);
    }
}
