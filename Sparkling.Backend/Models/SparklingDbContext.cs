using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Sparkling.Backend.Models;

public class SparklingDbContext : IdentityDbContext<User>
{
    public DbSet<Node> Nodes { get; set; } = null!;

    public DbSet<Container> Containers { get; set; } = null!;
    
    public DbSet<WorkSession> WorkSessions { get; set; } = null!;

    public SparklingDbContext(DbContextOptions<SparklingDbContext> options) :
        base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        builder
            .Entity<Container>()
            .Property(c => c.Type)
            .HasConversion<string>();
        
        builder
            .Entity<WorkSession>()
            .Property(c => c.Status)
            .HasConversion<string>();
        
        builder.Entity<Container>().Property(e => e.Id).ValueGeneratedNever();
    }
}
