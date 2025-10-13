using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using UserIPTracker;
using UserIPTracker.Models;
using UserIPTracker.Services;

public class ConnectionsRepositoryTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private async Task SeedData(AppDbContext context)
    {
        var data = new List<UserConnection>
        {
            new() { UserId = 1234567, IpStr = "31.214.157.141", IpInet = new NpgsqlInet("31.214.157.141"), ConnectedAt = DateTime.UtcNow.AddHours(-5) },
            new() { UserId = 1234567, IpStr = "62.4.36.194", IpInet = new NpgsqlInet("62.4.36.194"), ConnectedAt = DateTime.UtcNow.AddHours(-3) },
            new() { UserId = 7654321, IpStr = "31.214.22.5", IpInet = new NpgsqlInet("31.214.22.5"), ConnectedAt = DateTime.UtcNow.AddHours(-2) },
            new() { UserId = 9999999, IpStr = "10.0.0.1", IpInet = new NpgsqlInet("10.0.0.1"), ConnectedAt = DateTime.UtcNow.AddHours(-1) }
        };
        await context.UsersConnections.AddRangeAsync(data);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task FindUsersByIpPartAsync_ShouldReturnUsersMatchingIpPart()
    {
        await using var context = CreateDbContext();
        await SeedData(context);
        var repo = new ConnectionsRepository(context);

        var users = await repo.FindUsersByIpPartAsync("31.214");

        Assert.Contains(1234567, users);
        Assert.Contains(7654321, users);
        Assert.DoesNotContain(9999999, users);
        Assert.Equal(2, users.Count);
    }

    [Fact]
    public async Task GetUserIpsAsync_ShouldReturnAllIpsForUser()
    {
        await using var context = CreateDbContext();
        await SeedData(context);
        var repo = new ConnectionsRepository(context);

        var ips = await repo.GetUserIpsAsync(1234567);

        Assert.Contains("31.214.157.141", ips);
        Assert.Contains("62.4.36.194", ips);
        Assert.Equal(2, ips.Count);
    }

    [Fact]
    public async Task GetLastConnectionAsync_ShouldReturnLatestConnectionForUser()
    {
        await using var context = CreateDbContext();
        await SeedData(context);
        var repo = new ConnectionsRepository(context);

        var (time, ip) = await repo.GetLastConnectionAsync(1234567);

        Assert.Equal("62.4.36.194", ip);
        Assert.True(time > DateTime.UtcNow.AddHours(-4));
    }

    [Fact]
    public async Task GetLastConnectionByIpAsync_ShouldReturnLatestConnectionByIp()
    {
        await using var context = CreateDbContext();
        await SeedData(context);
        var repo = new ConnectionsRepository(context);

        var (time, userId) = await repo.GetLastConnectionByIpAsync("31.214.157.141");

        Assert.Equal(1234567, userId);
        Assert.True(time < DateTime.UtcNow.AddHours(-4)); // проверка что время соответствует вставленной записи
    }
}
