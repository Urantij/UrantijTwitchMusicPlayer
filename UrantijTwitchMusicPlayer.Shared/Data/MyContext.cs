using Microsoft.EntityFrameworkCore;
using UrantijTwitchMusicPlayer.Shared.Data.Models;

namespace UrantijTwitchMusicPlayer.Shared.Data;

public class MyContext : DbContext
{
    public DbSet<TwitchUserModel> TwitchUsers { get; set; }
    public DbSet<RequestModel> Requests { get; set; }
    public DbSet<TrackCachedData> TrackCache { get; set; }

    public MyContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=db.sqlite;");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RequestTwitchData>()
            .HasKey(d => d.RequestId);
        modelBuilder.Entity<RequestApiData>()
            .HasKey(d => d.RequestId);

        modelBuilder.Entity<RequestModel>()
            .HasOne<RequestTwitchData>(req => req.TwitchData)
            .WithOne(da => da.Request)
            .HasForeignKey<RequestTwitchData>(da => da.RequestId);

        modelBuilder.Entity<RequestModel>()
            .HasOne<RequestApiData>(req => req.ApiData)
            .WithOne(da => da.Request)
            .HasForeignKey<RequestApiData>(da => da.RequestId);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Conventions.Add(_ => new DateTimeOffsetAotConvention());
    }
}