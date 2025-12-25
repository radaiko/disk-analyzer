using DiskAnalyzer.Data;
using DiskAnalyzer.Models;
using DiskAnalyzer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiskAnalyzer.Tests;

public class ScanHistoryIntegrationTests : IDisposable
{
    private readonly DbContextOptions<DiskAnalyzerContext> _options;
    private readonly IDbContextFactory<DiskAnalyzerContext> _contextFactory;

    public ScanHistoryIntegrationTests()
    {
        // Use in-memory database for testing
        _options = new DbContextOptionsBuilder<DiskAnalyzerContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var factory = new Mock<IDbContextFactory<DiskAnalyzerContext>>();
        factory.Setup(f => f.CreateDbContextAsync(default))
            .ReturnsAsync(() => new DiskAnalyzerContext(_options));

        _contextFactory = factory.Object;
    }

    public void Dispose()
    {
        using var context = new DiskAnalyzerContext(_options);
        context.Database.EnsureDeleted();
    }

    [Fact]
    public async Task MultipleScanHistory_MaintainsSeparateDataSets()
    {
        // Arrange
        var logger = new Mock<ILogger<FolderDataService>>();
        var service = new FolderDataService(_contextFactory, logger.Object);

        // Create first scan
        using (var context = new DiskAnalyzerContext(_options))
        {
            var scan1 = new ScanResult
            {
                StartTime = DateTime.UtcNow.AddDays(-2),
                EndTime = DateTime.UtcNow.AddDays(-2).AddMinutes(5),
                Status = ScanStatus.Completed,
                FoldersScanned = 2,
                FilesScanned = 2,
                TotalBytes = 1000
            };
            context.ScanResults.Add(scan1);
            await context.SaveChangesAsync();

            var root1 = new FolderNode
            {
                Path = "/test",
                Name = "test",
                ScanResultId = scan1.Id,
                ParentId = null,
                SizeBytes = 1000,
                FileCount = 2
            };
            context.FolderNodes.Add(root1);
            await context.SaveChangesAsync();

            var child1 = new FolderNode
            {
                Path = "/test/folder1",
                Name = "folder1",
                ScanResultId = scan1.Id,
                ParentId = root1.Id,
                SizeBytes = 500,
                FileCount = 1
            };
            context.FolderNodes.Add(child1);
            await context.SaveChangesAsync();
        }

        // Create second scan with different data
        using (var context = new DiskAnalyzerContext(_options))
        {
            var scan2 = new ScanResult
            {
                StartTime = DateTime.UtcNow.AddDays(-1),
                EndTime = DateTime.UtcNow.AddDays(-1).AddMinutes(5),
                Status = ScanStatus.Completed,
                FoldersScanned = 3,
                FilesScanned = 3,
                TotalBytes = 2000
            };
            context.ScanResults.Add(scan2);
            await context.SaveChangesAsync();

            var root2 = new FolderNode
            {
                Path = "/test",
                Name = "test",
                ScanResultId = scan2.Id,
                ParentId = null,
                SizeBytes = 2000,
                FileCount = 3
            };
            context.FolderNodes.Add(root2);
            await context.SaveChangesAsync();

            var child2a = new FolderNode
            {
                Path = "/test/folder1",
                Name = "folder1",
                ScanResultId = scan2.Id,
                ParentId = root2.Id,
                SizeBytes = 1000,
                FileCount = 2
            };
            var child2b = new FolderNode
            {
                Path = "/test/folder2",
                Name = "folder2",
                ScanResultId = scan2.Id,
                ParentId = root2.Id,
                SizeBytes = 1000,
                FileCount = 1
            };
            context.FolderNodes.AddRange(child2a, child2b);
            await context.SaveChangesAsync();
        }

        // Act & Assert - Get latest scan
        var latestRoot = await service.GetRootFolderAsync();
        Assert.NotNull(latestRoot);
        Assert.Equal(2000, latestRoot.SizeBytes);

        // Get first scan data
        int scan1Id;
        using (var context = new DiskAnalyzerContext(_options))
        {
            scan1Id = await context.ScanResults
                .Where(s => s.TotalBytes == 1000)
                .Select(s => s.Id)
                .FirstAsync();
        }
        var firstScanRoot = await service.GetRootFolderAsync(scan1Id);
        Assert.NotNull(firstScanRoot);
        Assert.Equal(1000, firstScanRoot.SizeBytes);

        // Verify children count for each scan
        var latestChildren = await service.GetChildrenAsync(latestRoot.Id);
        Assert.Equal(2, latestChildren.Count);

        var firstScanChildren = await service.GetChildrenAsync(firstScanRoot.Id);
        Assert.Single(firstScanChildren);

        // Verify scan history is maintained
        var scans = await service.GetRecentScansAsync(10);
        Assert.Equal(2, scans.Count);
        Assert.All(scans, s => Assert.Equal(ScanStatus.Completed, s.Status));
    }

    [Fact]
    public async Task RunningScan_ShowsLastCompletedScan()
    {
        // Arrange
        var logger = new Mock<ILogger<FolderDataService>>();
        var service = new FolderDataService(_contextFactory, logger.Object);

        // Create completed scan
        using (var context = new DiskAnalyzerContext(_options))
        {
            var completedScan = new ScanResult
            {
                StartTime = DateTime.UtcNow.AddHours(-1),
                EndTime = DateTime.UtcNow.AddHours(-1).AddMinutes(5),
                Status = ScanStatus.Completed,
                FoldersScanned = 10,
                FilesScanned = 50,
                TotalBytes = 5000
            };
            context.ScanResults.Add(completedScan);
            await context.SaveChangesAsync();

            var completedRoot = new FolderNode
            {
                Path = "/data",
                Name = "data",
                ScanResultId = completedScan.Id,
                ParentId = null,
                SizeBytes = 5000,
                FileCount = 50
            };
            context.FolderNodes.Add(completedRoot);
            await context.SaveChangesAsync();
        }

        // Create running scan
        using (var context = new DiskAnalyzerContext(_options))
        {
            var runningScan = new ScanResult
            {
                StartTime = DateTime.UtcNow,
                Status = ScanStatus.Running,
                FoldersScanned = 0,
                FilesScanned = 0,
                TotalBytes = 0
            };
            context.ScanResults.Add(runningScan);
            await context.SaveChangesAsync();
        }

        // Act
        var latestCompleted = await service.GetLatestCompletedScanAsync();
        var latestAny = await service.GetLatestScanAsync();

        // Assert
        Assert.NotNull(latestCompleted);
        Assert.Equal(ScanStatus.Completed, latestCompleted.Status);
        Assert.Equal(5000, latestCompleted.TotalBytes);

        Assert.NotNull(latestAny);
        Assert.Equal(ScanStatus.Running, latestAny.Status);

        // When a scan is running, the service should return the last completed scan's data
        var root = await service.GetRootFolderAsync(latestCompleted.Id);
        Assert.NotNull(root);
        Assert.Equal(5000, root.SizeBytes);
    }
}
