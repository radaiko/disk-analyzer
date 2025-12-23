namespace DiskAnalyzer.Models;

public class AppSettings
{
    public int Id { get; set; }
    public int ScanIntervalHours { get; set; } = 24;
    public bool AutoScanEnabled { get; set; } = true;
    public string ScanRootPath { get; set; } = "/mnt";
    public DateTime? LastScanTime { get; set; }
    public DateTime? NextScanTime { get; set; }
}
