namespace DiskAnalyzer.Models;

public class FolderNode
{
    public int Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int FileCount { get; set; }
    public int? ParentId { get; set; }
    public DateTime LastScanned { get; set; }
    public int ScanResultId { get; set; }
    
    public FolderNode? Parent { get; set; }
    public List<FolderNode> Children { get; set; } = new();
    public ScanResult? ScanResult { get; set; }
    
    public double SizeGB => SizeBytes / (1024.0 * 1024.0 * 1024.0);
    
    public double GetPercentageOfParent()
    {
        if (Parent == null || Parent.SizeBytes == 0)
            return 100.0;
        
        return (SizeBytes * 100.0) / Parent.SizeBytes;
    }
}
