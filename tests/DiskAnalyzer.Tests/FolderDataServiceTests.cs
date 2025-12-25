using DiskAnalyzer.Data;
using DiskAnalyzer.Models;
using DiskAnalyzer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiskAnalyzer.Tests;

public class FolderDataServiceTests : IDisposable
{
    private readonly DbContextOptions<DiskAnalyzerContext> _options;
    private readonly IDbContextFactory<DiskAnalyzerContext> _contextFactory;
    private readonly FolderDataService _service;

    public FolderDataServiceTests()
    {
        // Use in-memory database for testing
        _options = new DbContextOptionsBuilder<DiskAnalyzerContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var factory = new Mock<IDbContextFactory<DiskAnalyzerContext>>();
        factory.Setup(f => f.CreateDbContextAsync(default))
            .ReturnsAsync(() => new DiskAnalyzerContext(_options));

        _contextFactory = factory.Object;

        var logger = new Mock<ILogger<FolderDataService>>();
        _service = new FolderDataService(_contextFactory, logger.Object);
    }

    public void Dispose()
    {
        using var context = new DiskAnalyzerContext(_options);
        context.Database.EnsureDeleted();
    }

    [Fact]
    public async Task GetRootFolderAsync_ReturnsRootFromLatestCompletedScan()
    {
        // Arrange
        using (var context = new DiskAnalyzerContext(_options))
        {
            var scan1 = new ScanResult
            {
                StartTime = DateTime.UtcNow.AddHours(-2),
                Status = ScanStatus.Completed
            };
            var scan2 = new ScanResult
            {
                StartTime = DateTime.UtcNow.AddHours(-1),
                Status = ScanStatus.Completed
            };
            context.ScanResults.AddRange(scan1, scan2);
            await context.SaveChangesAsync();

            var folder1 = new FolderNode
            {
                Path = "/mnt",
                Name = "mnt",
                ScanResultId = scan1.Id,
                ParentId = null
            };
            var folder2 = new FolderNode
            {
                Path = "/mnt",
                Name = "mnt",
                ScanResultId = scan2.Id,
                ParentId = null
            };
            context.FolderNodes.AddRange(folder1, folder2);
            await context.SaveChangesAsync();
        }

        // Act
        var result = await _service.GetRootFolderAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/mnt", result.Path);
        // Should return the folder from the most recent scan
        using (var context = new DiskAnalyzerContext(_options))
        {
            var latestScan = await context.ScanResults
                .Where(s => s.Status == ScanStatus.Completed)
                .OrderByDescending(s => s.StartTime)
                .FirstAsync();
            Assert.Equal(latestScan.Id, result.ScanResultId);
        }
    }

    [Fact]
    public async Task GetRootFolderAsync_WithScanResultId_ReturnsCorrectRoot()
    {
        // Arrange
        using (var context = new DiskAnalyzerContext(_options))
        {
            var scan1 = new ScanResult
            {
                StartTime = DateTime.UtcNow.AddHours(-2),
                Status = ScanStatus.Completed
            };
            var scan2 = new ScanResult
            {
                StartTime = DateTime.UtcNow.AddHours(-1),
                Status = ScanStatus.Completed
            };
            context.ScanResults.AddRange(scan1, scan2);
            await context.SaveChangesAsync();

            var folder1 = new FolderNode
            {
                Path = "/mnt",
                Name = "mnt",
                ScanResultId = scan1.Id,
                SizeBytes = 1000,
                ParentId = null
            };
            var folder2 = new FolderNode
            {
                Path = "/mnt",
                Name = "mnt",
                ScanResultId = scan2.Id,
                SizeBytes = 2000,
                ParentId = null
            };
            context.FolderNodes.AddRange(folder1, folder2);
            await context.SaveChangesAsync();
        }

        // Act
        ScanResult targetScan;
        using (var context = new DiskAnalyzerContext(_options))
        {
            targetScan = await context.ScanResults.FirstAsync();
        }
        var result = await _service.GetRootFolderAsync(targetScan.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(targetScan.Id, result.ScanResultId);
        Assert.Equal(1000, result.SizeBytes);
    }

    [Fact]
    public async Task GetLatestCompletedScanAsync_ReturnsOnlyCompletedScans()
    {
        // Arrange
        using (var context = new DiskAnalyzerContext(_options))
        {
            var scan1 = new ScanResult
            {
                StartTime = DateTime.UtcNow.AddHours(-3),
                Status = ScanStatus.Completed
            };
            var scan2 = new ScanResult
            {
                StartTime = DateTime.UtcNow.AddHours(-2),
                Status = ScanStatus.Failed
            };
            var scan3 = new ScanResult
            {
                StartTime = DateTime.UtcNow.AddHours(-1),
                Status = ScanStatus.Running
            };
            context.ScanResults.AddRange(scan1, scan2, scan3);
            await context.SaveChangesAsync();
        }

        // Act
        var result = await _service.GetLatestCompletedScanAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ScanStatus.Completed, result.Status);
    }

    [Fact]
    public async Task GetRecentScansAsync_ReturnsScansInDescendingOrder()
    {
        // Arrange
        using (var context = new DiskAnalyzerContext(_options))
        {
            for (int i = 0; i < 15; i++)
            {
                context.ScanResults.Add(new ScanResult
                {
                    StartTime = DateTime.UtcNow.AddHours(-i),
                    Status = ScanStatus.Completed
                });
            }
            await context.SaveChangesAsync();
        }

        // Act
        var result = await _service.GetRecentScansAsync(10);

        // Assert
        Assert.Equal(10, result.Count);
        for (int i = 0; i < result.Count - 1; i++)
        {
            Assert.True(result[i].StartTime >= result[i + 1].StartTime);
        }
    }

    [Fact]
    public async Task GetFolderTreeAsync_WithScanResultId_FiltersCorrectly()
    {
        // Arrange
        using (var context = new DiskAnalyzerContext(_options))
        {
            var scan1 = new ScanResult
            {
                StartTime = DateTime.UtcNow.AddHours(-2),
                Status = ScanStatus.Completed
            };
            var scan2 = new ScanResult
            {
                StartTime = DateTime.UtcNow.AddHours(-1),
                Status = ScanStatus.Completed
            };
            context.ScanResults.AddRange(scan1, scan2);
            await context.SaveChangesAsync();

            var root1 = new FolderNode
            {
                Path = "/mnt",
                Name = "mnt",
                ScanResultId = scan1.Id,
                ParentId = null
            };
            var root2 = new FolderNode
            {
                Path = "/mnt",
                Name = "mnt",
                ScanResultId = scan2.Id,
                ParentId = null
            };
            context.FolderNodes.AddRange(root1, root2);
            await context.SaveChangesAsync();

            var child1 = new FolderNode
            {
                Path = "/mnt/data",
                Name = "data",
                ScanResultId = scan1.Id,
                ParentId = root1.Id
            };
            var child2 = new FolderNode
            {
                Path = "/mnt/media",
                Name = "media",
                ScanResultId = scan2.Id,
                ParentId = root2.Id
            };
            context.FolderNodes.AddRange(child1, child2);
            await context.SaveChangesAsync();
        }

        // Act
        int scan1Id;
        using (var context = new DiskAnalyzerContext(_options))
        {
            scan1Id = await context.ScanResults.Where(s => s.StartTime < DateTime.UtcNow.AddHours(-1.5)).Select(s => s.Id).FirstAsync();
        }
        var result = await _service.GetFolderTreeAsync(null, scan1Id);

        // Assert
        Assert.Single(result);
        Assert.Equal(scan1Id, result[0].ScanResultId);
    }
}
