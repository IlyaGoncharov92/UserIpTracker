using NpgsqlTypes;

namespace UserIPTracker.Models;

public class UserConnection
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string IpStr { get; set; } = null!;
    public NpgsqlInet IpInet { get; set; }
    public DateTime ConnectedAt { get; set; }
}