using DiskAnalyzer.Components;
using DiskAnalyzer.Data;
using DiskAnalyzer.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure SQLite database
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "config", "diskanalyzer.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContextFactory<DiskAnalyzerContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Add application services
builder.Services.AddSingleton<FolderScannerService>();
builder.Services.AddScoped<FolderDataService>();
builder.Services.AddHostedService<ScanSchedulerService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DiskAnalyzerContext>>();
    using var context = await contextFactory.CreateDbContextAsync();
    
    // Try to migrate the database if it exists
    try
    {
        // Check if database exists and needs migration
        var dbExists = await context.Database.CanConnectAsync();
        if (dbExists)
        {
            // Check if ScanResultId column exists in FolderNodes
            var hasScanResultIdColumn = false;
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    "SELECT ScanResultId FROM FolderNodes LIMIT 1");
                hasScanResultIdColumn = true;
            }
            catch
            {
                // Column doesn't exist, need to add it
            }
            
            if (!hasScanResultIdColumn)
            {
                // Add the ScanResultId column and set a default value
                // First, ensure there's at least one scan result
                var hasScans = await context.ScanResults.AnyAsync();
                if (!hasScans)
                {
                    // Create a default scan result for existing data
                    var defaultScan = new DiskAnalyzer.Models.ScanResult
                    {
                        StartTime = DateTime.UtcNow,
                        EndTime = DateTime.UtcNow,
                        Status = DiskAnalyzer.Models.ScanStatus.Completed,
                        FoldersScanned = await context.FolderNodes.CountAsync(),
                        FilesScanned = 0,
                        TotalBytes = await context.FolderNodes.SumAsync(f => (long?)f.SizeBytes) ?? 0
                    };
                    context.ScanResults.Add(defaultScan);
                    await context.SaveChangesAsync();
                }
                
                var firstScanId = await context.ScanResults
                    .OrderBy(s => s.Id)
                    .Select(s => s.Id)
                    .FirstOrDefaultAsync();
                
                // Add the column with a default value
                // Note: firstScanId is an integer from the database, not user input - safe from SQL injection
                await context.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE FolderNodes ADD COLUMN ScanResultId INTEGER NOT NULL DEFAULT " + firstScanId.ToString());
                    
                // Drop the old unique index on Path and create new composite one
                await context.Database.ExecuteSqlRawAsync(
                    "DROP INDEX IF EXISTS IX_FolderNodes_Path");
                await context.Database.ExecuteSqlRawAsync(
                    "CREATE UNIQUE INDEX IX_FolderNodes_Path_ScanResultId ON FolderNodes (Path, ScanResultId)");
                await context.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX IX_FolderNodes_ScanResultId ON FolderNodes (ScanResultId)");
            }
        }
        else
        {
            // Database doesn't exist, create it
            await context.Database.EnsureCreatedAsync();
        }
    }
    catch
    {
        // If migration fails, try to create from scratch
        await context.Database.EnsureCreatedAsync();
    }
}

app.Run();
