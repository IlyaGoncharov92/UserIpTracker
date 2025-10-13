namespace UserIPTracker.Models;

public record ConnectionDto
{
    public long UserId { get; set; }
    public string Ip { get; set; }
    public DateTime ConnectedAt { get; } = DateTime.UtcNow;
}
