using DiskAnalyzer.Data;
using DiskAnalyzer.Models;
using Microsoft.EntityFrameworkCore;

namespace DiskAnalyzer.Services;

public class FolderDataService
{
    private readonly IDbContextFactory<DiskAnalyzerContext> _contextFactory;
    private readonly ILogger<FolderDataService> _logger;

    public FolderDataService(
        IDbContextFactory<DiskAnalyzerContext> contextFactory,
        ILogger<FolderDataService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<FolderNode?> GetRootFolderAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.FolderNodes
            .Include(f => f.Children)
            .FirstOrDefaultAsync(f => f.ParentId == null);
    }

    public async Task<FolderNode?> GetFolderByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.FolderNodes
            .Include(f => f.Parent)
            .Include(f => f.Children)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<FolderNode?> GetFolderByPathAsync(string path)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.FolderNodes
            .Include(f => f.Parent)
            .Include(f => f.Children)
            .FirstOrDefaultAsync(f => f.Path == path);
    }

    public async Task<List<FolderNode>> GetChildrenAsync(int parentId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.FolderNodes
            .Where(f => f.ParentId == parentId)
            .OrderByDescending(f => f.SizeBytes)
            .ToListAsync();
    }

    public async Task<List<FolderNode>> GetFolderTreeAsync(int? parentId = null)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.FolderNodes
            .Where(f => f.ParentId == parentId)
            .Include(f => f.Children)
            .OrderByDescending(f => f.SizeBytes)
            .ToListAsync();
    }

    public async Task<List<ScanResult>> GetRecentScansAsync(int count = 10)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ScanResults
            .OrderByDescending(s => s.StartTime)
            .Take(count)
            .ToListAsync();
    }

    public async Task<ScanResult?> GetLatestScanAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ScanResults
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync();
    }

    public async Task<AppSettings?> GetSettingsAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AppSettings.FirstOrDefaultAsync();
    }

    public async Task UpdateSettingsAsync(AppSettings settings)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.AppSettings.Update(settings);
        await context.SaveChangesAsync();
    }
}
