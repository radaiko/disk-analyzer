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
    await context.Database.EnsureCreatedAsync();
}

app.Run();
