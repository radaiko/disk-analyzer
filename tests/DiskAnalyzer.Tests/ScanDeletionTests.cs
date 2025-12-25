using DiskAnalyzer.Data;
using DiskAnalyzer.Models;
using DiskAnalyzer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiskAnalyzer.Tests;

public class ScanDeletionTests : IDisposable
{
    private readonly DbContextOptions<DiskAnalyzerContext> _options;
    private readonly IDbContextFactory<DiskAnalyzerContext> _contextFactory;

    public ScanDeletionTests()
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
    public async Task DeleteScan_RemovesScanAndRelatedFolderNodes()
    {
        // Arrange
        var logger = new Mock<ILogger<FolderDataService>>();
        var service = new FolderDataService(_contextFactory, logger.Object);

        int scanId;
        using (var context = new DiskAnalyzerContext(_options))
        {
            var scan = new ScanResult
            {
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(5),
                Status = ScanStatus.Completed,
                FoldersScanned = 2,
                FilesScanned = 5,
                TotalBytes = 1000
            };
            context.ScanResults.Add(scan);
            await context.SaveChangesAsync();
            scanId = scan.Id;

            var root = new FolderNode
            {
                Path = "/test",
                Name = "test",
                ScanResultId = scan.Id,
                ParentId = null,
                SizeBytes = 1000,
                FileCount = 5
            };
            context.FolderNodes.Add(root);
            await context.SaveChangesAsync();

            var child = new FolderNode
            {
                Path = "/test/child",
                Name = "child",
                ScanResultId = scan.Id,
                ParentId = root.Id,
                SizeBytes = 500,
                FileCount = 2
            };
            context.FolderNodes.Add(child);
            await context.SaveChangesAsync();
        }

        // Act
        var result = await service.DeleteScanAsync(scanId);

        // Assert
        Assert.True(result);

        using (var context = new DiskAnalyzerContext(_options))
        {
            var scan = await context.ScanResults.FirstOrDefaultAsync(s => s.Id == scanId);
            Assert.Null(scan);

            var folderNodes = await context.FolderNodes
                .Where(f => f.ScanResultId == scanId)
                .ToListAsync();
            Assert.Empty(folderNodes);
        }
    }

    [Fact]
    public async Task DeleteScan_NonExistentScan_ReturnsFalse()
    {
        // Arrange
        var logger = new Mock<ILogger<FolderDataService>>();
        var service = new FolderDataService(_contextFactory, logger.Object);

        // Act
        var result = await service.DeleteScanAsync(999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteScan_KeepsOtherScans()
    {
        // Arrange
        var logger = new Mock<ILogger<FolderDataService>>();
        var service = new FolderDataService(_contextFactory, logger.Object);

        int scan1Id, scan2Id;
        using (var context = new DiskAnalyzerContext(_options))
        {
            var scan1 = new ScanResult
            {
                StartTime = DateTime.UtcNow.AddDays(-1),
                EndTime = DateTime.UtcNow.AddDays(-1).AddMinutes(5),
                Status = ScanStatus.Completed,
                FoldersScanned = 1,
                FilesScanned = 1,
                TotalBytes = 500
            };
            var scan2 = new ScanResult
            {
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(5),
                Status = ScanStatus.Completed,
                FoldersScanned = 2,
                FilesScanned = 2,
                TotalBytes = 1000
            };
            context.ScanResults.AddRange(scan1, scan2);
            await context.SaveChangesAsync();
            scan1Id = scan1.Id;
            scan2Id = scan2.Id;

            var root1 = new FolderNode
            {
                Path = "/test1",
                Name = "test1",
                ScanResultId = scan1.Id,
                ParentId = null,
                SizeBytes = 500,
                FileCount = 1
            };
            var root2 = new FolderNode
            {
                Path = "/test2",
                Name = "test2",
                ScanResultId = scan2.Id,
                ParentId = null,
                SizeBytes = 1000,
                FileCount = 2
            };
            context.FolderNodes.AddRange(root1, root2);
            await context.SaveChangesAsync();
        }

        // Act - Delete scan1
        var result = await service.DeleteScanAsync(scan1Id);

        // Assert
        Assert.True(result);

        using (var context = new DiskAnalyzerContext(_options))
        {
            var deletedScan = await context.ScanResults.FirstOrDefaultAsync(s => s.Id == scan1Id);
            Assert.Null(deletedScan);

            var remainingScan = await context.ScanResults.FirstOrDefaultAsync(s => s.Id == scan2Id);
            Assert.NotNull(remainingScan);

            var scan1Nodes = await context.FolderNodes
                .Where(f => f.ScanResultId == scan1Id)
                .ToListAsync();
            Assert.Empty(scan1Nodes);

            var scan2Nodes = await context.FolderNodes
                .Where(f => f.ScanResultId == scan2Id)
                .ToListAsync();
            Assert.Single(scan2Nodes);
        }
    }

    [Fact]
    public async Task StaleScanDetection_MarksRunningScansAsCancelled()
    {
        // Arrange & Act - Simulate app restart scenario
        using (var context = new DiskAnalyzerContext(_options))
        {
            var runningScan = new ScanResult
            {
                StartTime = DateTime.UtcNow.AddMinutes(-10),
                Status = ScanStatus.Running,
                FoldersScanned = 5,
                FilesScanned = 10,
                TotalBytes = 500
            };
            var pendingScan = new ScanResult
            {
                StartTime = DateTime.UtcNow.AddMinutes(-5),
                Status = ScanStatus.Pending,
                FoldersScanned = 0,
                FilesScanned = 0,
                TotalBytes = 0
            };
            var completedScan = new ScanResult
            {
                StartTime = DateTime.UtcNow.AddHours(-1),
                EndTime = DateTime.UtcNow.AddHours(-1).AddMinutes(5),
                Status = ScanStatus.Completed,
                FoldersScanned = 100,
                FilesScanned = 200,
                TotalBytes = 10000
            };
            context.ScanResults.AddRange(runningScan, pendingScan, completedScan);
            await context.SaveChangesAsync();
        }

        // Simulate the startup code that marks stale scans as cancelled
        using (var context = new DiskAnalyzerContext(_options))
        {
            var staleScans = await context.ScanResults
                .Where(s => s.Status == ScanStatus.Running || s.Status == ScanStatus.Pending)
                .ToListAsync();

            foreach (var scan in staleScans)
            {
                scan.Status = ScanStatus.Cancelled;
                scan.EndTime ??= DateTime.UtcNow;
            }
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new DiskAnalyzerContext(_options))
        {
            var allScans = await context.ScanResults.ToListAsync();
            Assert.Equal(3, allScans.Count);

            var cancelledScans = allScans.Where(s => s.Status == ScanStatus.Cancelled).ToList();
            Assert.Equal(2, cancelledScans.Count);
            Assert.All(cancelledScans, s => Assert.NotNull(s.EndTime));

            var completedScans = allScans.Where(s => s.Status == ScanStatus.Completed).ToList();
            Assert.Single(completedScans);
        }
    }
}
