using Microsoft.EntityFrameworkCore;
using UserIPTracker.Models;

namespace UserIPTracker;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Включаем расширение pg_trgm для ускорения поиска по LIKE/ILIKE
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.Entity<UserConnection>(entity =>
        {
            entity.ToTable("usersConnections");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId)
                .IsRequired();

            entity.Property(e => e.IpStr)
                .IsRequired();
            
            entity.Property(e => e.IpInet)
                .IsRequired();

            entity.Property(e => e.ConnectedAt)
                .IsRequired();

            // Индекс для поиска по UserId
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_usersconnections_user_id");
            
            // Индекс для поиска по IpInet
            entity.HasIndex(e => e.IpInet)
                .HasDatabaseName("idx_usersconnections_ipinet");
            
            // GIN-индекс с триграммами для быстрого поиска по частям IP
            entity.HasIndex(e => e.IpStr)
                .HasMethod("gin")
                .HasDatabaseName("idx_usersconnections_ip_trgm")
                .HasOperators("gin_trgm_ops");
        });
    }
    
    public DbSet<UserConnection> UsersConnections { get; set; } = null!;
}