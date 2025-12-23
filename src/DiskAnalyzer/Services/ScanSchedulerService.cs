using DiskAnalyzer.Data;
using DiskAnalyzer.Models;
using Microsoft.EntityFrameworkCore;

namespace DiskAnalyzer.Services;

public class ScanSchedulerService : BackgroundService
{
    private readonly IDbContextFactory<DiskAnalyzerContext> _contextFactory;
    private readonly FolderScannerService _scannerService;
    private readonly ILogger<ScanSchedulerService> _logger;

    public ScanSchedulerService(
        IDbContextFactory<DiskAnalyzerContext> contextFactory,
        FolderScannerService scannerService,
        ILogger<ScanSchedulerService> logger)
    {
        _contextFactory = contextFactory;
        _scannerService = scannerService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scan Scheduler Service started");

        // Initialize settings if they don't exist
        await InitializeSettingsAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndScheduleScanAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in scan scheduler");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Scan Scheduler Service stopped");
    }

    private async Task InitializeSettingsAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        if (!await context.AppSettings.AnyAsync())
        {
            var settings = new AppSettings
            {
                ScanIntervalHours = 24,
                AutoScanEnabled = true,
                ScanRootPath = "/mnt"
            };
            
            context.AppSettings.Add(settings);
            await context.SaveChangesAsync();
            
            _logger.LogInformation("Initialized default settings");
        }
    }

    private async Task CheckAndScheduleScanAsync(CancellationToken cancellationToken)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var settings = await context.AppSettings.FirstOrDefaultAsync(cancellationToken);

        if (settings == null || !settings.AutoScanEnabled)
        {
            return;
        }

        // Check if it's time for a scan
        var shouldScan = false;
        
        if (settings.LastScanTime == null)
        {
            // Never scanned before
            shouldScan = true;
        }
        else if (settings.NextScanTime != null && DateTime.UtcNow >= settings.NextScanTime)
        {
            // Scheduled scan time has arrived
            shouldScan = true;
        }

        if (shouldScan && !_scannerService.IsScanning)
        {
            _logger.LogInformation("Starting scheduled scan");
            
            try
            {
                var scanResult = await _scannerService.StartScanAsync(settings.ScanRootPath);
                
                // Update settings with scan completion time
                settings.LastScanTime = DateTime.UtcNow;
                settings.NextScanTime = DateTime.UtcNow.AddHours(settings.ScanIntervalHours);
                
                context.AppSettings.Update(settings);
                await context.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("Scheduled scan completed. Next scan at: {NextScanTime}", settings.NextScanTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled scan failed");
            }
        }
    }
}
