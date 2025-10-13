using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using UserIPTracker.Models;

namespace UserIPTracker.Services;

public partial class ConnectionsRepository(AppDbContext _dbContext)
{
    /// <summary>
    /// Найти пользователей по части IP
    /// </summary>
    public async Task<List<long>> FindUsersByIpPartAsync(string ipPart)
    {
        return await _dbContext.UsersConnections
            .Where(u => u.IpStr.StartsWith(ipPart))
            .Select(u => u.UserId)
            .Distinct()
            .ToListAsync();
        
        // TODO: UseInMemoryDatabase не поддерживает ILike
        return await _dbContext.UsersConnections
            .Where(u => EF.Functions.ILike(u.IpStr, $"{ipPart}%"))
            .Select(u => u.UserId)
            .Distinct()
            .ToListAsync();
    }
    
    /// <summary>
    /// Найти все IP пользователя
    /// </summary>
    public async Task<List<string>> GetUserIpsAsync(long userId)
    {
        return await _dbContext.UsersConnections
            .Where(u => u.UserId == userId)
            .Select(u => u.IpStr)
            .Distinct()
            .ToListAsync();
    }
    
    /// <summary>
    /// Найти последнее подключение пользователя
    /// </summary>
    public async Task<(DateTime? Time, string? Ip)> GetLastConnectionAsync(long userId)
    {
        var last = await _dbContext.UsersConnections
            .Where(u => u.UserId == userId)
            .OrderByDescending(u => u.ConnectedAt)
            .Select(u => new { u.ConnectedAt, Ip = u.IpStr })
            .FirstOrDefaultAsync();

        return last is null ? (null, null) : (last.ConnectedAt, last.Ip);
    }
    
    /// <summary>
    /// Найти последнее подключение пользователя по точному IP (inet)
    /// </summary>
    public async Task<(DateTime? Time, long? UserId)> GetLastConnectionByIpAsync(string ip)
    {
        var inet = new NpgsqlTypes.NpgsqlInet(ip);
        var last = await _dbContext.UsersConnections
            .Where(u => u.IpInet == inet)
            .OrderByDescending(u => u.ConnectedAt)
            .Select(u => new { u.ConnectedAt, u.UserId })
            .FirstOrDefaultAsync();
        return last is null ? (null, null) : (last.ConnectedAt, last.UserId);
    }
    
    /// <summary>
    /// Массовая вставка
    /// </summary>
    public async Task BulkInsertConnectionsAsync(IEnumerable<UserConnection> connections)
    {
        await _dbContext.BulkInsertAsync(connections);
    }
}

// Для теста
public partial class ConnectionsRepository
{
    public async Task<int> Count()
    {
        return await _dbContext.UsersConnections.CountAsync();
    }
    
    /// <summary>
    /// Быстрая очистка таблицы UsersConnections
    /// </summary>
    public async Task TruncateAsync()
    {
        await _dbContext.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"usersConnections\" RESTART IDENTITY CASCADE;");
    }
}
