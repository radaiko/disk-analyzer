namespace DiskAnalyzer.Models;

public class ScanResult
{
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public ScanStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public int FoldersScanned { get; set; }
    public int FilesScanned { get; set; }
    public long TotalBytes { get; set; }
}

public enum ScanStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
