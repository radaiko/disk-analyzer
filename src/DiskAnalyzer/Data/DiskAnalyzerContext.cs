using Microsoft.EntityFrameworkCore;
using DiskAnalyzer.Models;

namespace DiskAnalyzer.Data;

public class DiskAnalyzerContext : DbContext
{
    public DiskAnalyzerContext(DbContextOptions<DiskAnalyzerContext> options)
        : base(options)
    {
    }

    public DbSet<FolderNode> FolderNodes => Set<FolderNode>();
    public DbSet<ScanResult> ScanResults => Set<ScanResult>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FolderNode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Path, e.ScanResultId }).IsUnique();
            entity.HasIndex(e => e.ParentId);
            entity.HasIndex(e => e.ScanResultId);
            
            entity.HasOne(e => e.Parent)
                .WithMany(e => e.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.ScanResult)
                .WithMany()
                .HasForeignKey(e => e.ScanResultId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScanResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.StartTime);
        });

        modelBuilder.Entity<AppSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
        });
    }
}
