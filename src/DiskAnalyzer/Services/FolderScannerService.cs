using DiskAnalyzer.Data;
using DiskAnalyzer.Models;
using Microsoft.EntityFrameworkCore;

namespace DiskAnalyzer.Services;

public class FolderScannerService
{
    private readonly IDbContextFactory<DiskAnalyzerContext> _contextFactory;
    private readonly ILogger<FolderScannerService> _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isScanning = false;

    public event EventHandler<ScanProgressEventArgs>? ProgressUpdated;
    public event EventHandler<ScanResult>? ScanCompleted;

    public bool IsScanning => _isScanning;

    public FolderScannerService(
        IDbContextFactory<DiskAnalyzerContext> contextFactory,
        ILogger<FolderScannerService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<ScanResult> StartScanAsync(string rootPath)
    {
        if (_isScanning)
        {
            throw new InvalidOperationException("A scan is already in progress");
        }

        _isScanning = true;
        _cancellationTokenSource = new CancellationTokenSource();

        var scanResult = new ScanResult
        {
            StartTime = DateTime.UtcNow,
            Status = ScanStatus.Running
        };

        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.ScanResults.Add(scanResult);
            await context.SaveChangesAsync();

            // Start scanning (no longer deleting previous folder nodes)
            await ScanDirectoryRecursiveAsync(rootPath, null, scanResult, context, _cancellationTokenSource.Token);

            scanResult.EndTime = DateTime.UtcNow;
            scanResult.Status = ScanStatus.Completed;
            await context.SaveChangesAsync();

            ScanCompleted?.Invoke(this, scanResult);

            _logger.LogInformation("Scan completed: {FoldersScanned} folders, {FilesScanned} files, {TotalGB:F2} GB",
                scanResult.FoldersScanned, scanResult.FilesScanned, scanResult.TotalBytes / (1024.0 * 1024.0 * 1024.0));
        }
        catch (OperationCanceledException)
        {
            scanResult.Status = ScanStatus.Cancelled;
            scanResult.EndTime = DateTime.UtcNow;
            
            using var context = await _contextFactory.CreateDbContextAsync();
            context.ScanResults.Update(scanResult);
            await context.SaveChangesAsync();
            
            _logger.LogInformation("Scan cancelled");
        }
        catch (Exception ex)
        {
            scanResult.Status = ScanStatus.Failed;
            scanResult.ErrorMessage = ex.Message;
            scanResult.EndTime = DateTime.UtcNow;
            
            using var context = await _contextFactory.CreateDbContextAsync();
            context.ScanResults.Update(scanResult);
            await context.SaveChangesAsync();
            
            _logger.LogError(ex, "Scan failed");
        }
        finally
        {
            _isScanning = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        return scanResult;
    }

    public void CancelScan()
    {
        _cancellationTokenSource?.Cancel();
    }

    private async Task<FolderNode> ScanDirectoryRecursiveAsync(
        string path,
        int? parentId,
        ScanResult scanResult,
        DiskAnalyzerContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var folderNode = new FolderNode
        {
            Path = path,
            Name = Path.GetFileName(path) == "" ? path : Path.GetFileName(path),
            ParentId = parentId,
            LastScanned = DateTime.UtcNow,
            ScanResultId = scanResult.Id
        };

        long totalSize = 0;
        int fileCount = 0;

        try
        {
            // Count files in current directory
            var files = Directory.GetFiles(path);
            foreach (var file in files)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    totalSize += fileInfo.Length;
                    fileCount++;
                    scanResult.FilesScanned++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error accessing file: {File}", file);
                }
            }

            folderNode.FileCount = fileCount;
            folderNode.SizeBytes = totalSize;

            // Save current folder node
            context.FolderNodes.Add(folderNode);
            await context.SaveChangesAsync(cancellationToken);

            scanResult.FoldersScanned++;
            scanResult.TotalBytes += totalSize;

            // Report progress
            ProgressUpdated?.Invoke(this, new ScanProgressEventArgs
            {
                CurrentPath = path,
                FoldersScanned = scanResult.FoldersScanned,
                FilesScanned = scanResult.FilesScanned,
                TotalBytes = scanResult.TotalBytes
            });

            // Scan subdirectories
            var directories = Directory.GetDirectories(path);
            foreach (var directory in directories)
            {
                try
                {
                    var childNode = await ScanDirectoryRecursiveAsync(
                        directory,
                        folderNode.Id,
                        scanResult,
                        context,
                        cancellationToken);

                    folderNode.SizeBytes += childNode.SizeBytes;
                    totalSize += childNode.SizeBytes;
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogWarning(ex, "Access denied to directory: {Directory}", directory);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error scanning directory: {Directory}", directory);
                }
            }

            // Update folder node with aggregated size
            folderNode.SizeBytes = totalSize;
            context.FolderNodes.Update(folderNode);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied to directory: {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning directory: {Path}", path);
        }

        return folderNode;
    }
}

public class ScanProgressEventArgs : EventArgs
{
    public string CurrentPath { get; set; } = string.Empty;
    public int FoldersScanned { get; set; }
    public int FilesScanned { get; set; }
    public long TotalBytes { get; set; }
}
