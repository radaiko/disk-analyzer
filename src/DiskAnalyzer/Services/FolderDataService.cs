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

    public async Task<FolderNode?> GetRootFolderAsync(int? scanResultId = null)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        if (scanResultId.HasValue)
        {
            return await context.FolderNodes
                .Include(f => f.Children)
                .FirstOrDefaultAsync(f => f.ParentId == null && f.ScanResultId == scanResultId.Value);
        }
        
        // Get root folder from the latest completed scan
        var latestScan = await context.ScanResults
            .Where(s => s.Status == ScanStatus.Completed)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync();
            
        if (latestScan == null)
            return null;
            
        return await context.FolderNodes
            .Include(f => f.Children)
            .FirstOrDefaultAsync(f => f.ParentId == null && f.ScanResultId == latestScan.Id);
    }

    public async Task<FolderNode?> GetFolderByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.FolderNodes
            .Include(f => f.Parent)
            .Include(f => f.Children)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<FolderNode?> GetFolderByPathAsync(string path, int? scanResultId = null)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        if (scanResultId.HasValue)
        {
            return await context.FolderNodes
                .Include(f => f.Parent)
                .Include(f => f.Children)
                .FirstOrDefaultAsync(f => f.Path == path && f.ScanResultId == scanResultId.Value);
        }
        
        // Get from the latest completed scan
        var latestScan = await context.ScanResults
            .Where(s => s.Status == ScanStatus.Completed)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync();
            
        if (latestScan == null)
            return null;
            
        return await context.FolderNodes
            .Include(f => f.Parent)
            .Include(f => f.Children)
            .FirstOrDefaultAsync(f => f.Path == path && f.ScanResultId == latestScan.Id);
    }

    public async Task<List<FolderNode>> GetChildrenAsync(int parentId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.FolderNodes
            .Where(f => f.ParentId == parentId)
            .OrderByDescending(f => f.SizeBytes)
            .ToListAsync();
    }

    public async Task<List<FolderNode>> GetFolderTreeAsync(int? parentId = null, int? scanResultId = null)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var query = context.FolderNodes.Where(f => f.ParentId == parentId);
        
        if (scanResultId.HasValue)
        {
            query = query.Where(f => f.ScanResultId == scanResultId.Value);
        }
        else
        {
            // Get from the latest completed scan
            var latestScan = await context.ScanResults
                .Where(s => s.Status == ScanStatus.Completed)
                .OrderByDescending(s => s.StartTime)
                .FirstOrDefaultAsync();
                
            if (latestScan != null)
            {
                query = query.Where(f => f.ScanResultId == latestScan.Id);
            }
        }
        
        return await query
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
    
    public async Task<ScanResult?> GetLatestCompletedScanAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ScanResults
            .Where(s => s.Status == ScanStatus.Completed)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync();
    }
    
    public async Task<ScanResult?> GetScanByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ScanResults.FirstOrDefaultAsync(s => s.Id == id);
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
