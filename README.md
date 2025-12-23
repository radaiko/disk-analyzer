# Disk Space Analyzer

![Docker Build](https://github.com/radaiko/disk-analyzer/actions/workflows/docker-build-push.yml/badge.svg)

A self-hosted disk space analyzer that runs as a Docker container and provides a modern dark-themed web interface for visualizing disk usage across mounted directories.

## Features

- 🌙 **Dark Mode UI** - Easy on the eyes with a modern, clean interface
- 📊 **Interactive Pie Charts** - Visual representation of disk usage with ApexCharts
- 🌲 **Folder Tree Navigation** - Drill down through directories with an expandable tree view
- ⏰ **Scheduled Scanning** - Automatic periodic scans with configurable intervals
- 🔄 **Background Processing** - Non-blocking scans that run in the background
- 💾 **SQLite Storage** - Fast, embedded database for scan results
- 🐳 **Docker Ready** - Easy deployment with Docker and Docker Compose
- 🖥️ **NAS Friendly** - Perfect for TrueNAS, Unraid, and other NAS systems

## Technology Stack

- **Backend**: C# .NET 10, ASP.NET Core
- **Frontend**: Blazor Server with Interactive Server Components
- **Database**: SQLite
- **Charts**: ApexCharts for Blazor
- **UI**: Bootstrap 5 with custom dark theme

## Quick Start

### Using Pre-built Docker Image (Easiest)

Pull and run the latest pre-built image from GitHub Container Registry:

```bash
docker run -d \
  --name disk-analyzer \
  -p 8080:8080 \
  -v /your/data:/mnt/data:ro \
  -v /your/media:/mnt/media:ro \
  -v ./config:/app/config \
  ghcr.io/radaiko/disk-analyzer:latest
```

Or use Docker Compose with the pre-built image:

```yaml
version: "3.9"
services:
  disk-analyzer:
    image: ghcr.io/radaiko/disk-analyzer:latest
    container_name: disk-analyzer
    ports:
      - "8080:8080"
    volumes:
      - /your/data:/mnt/data:ro
      - /your/media:/mnt/media:ro
      - ./config:/app/config
    restart: unless-stopped
```

Then open your browser to `http://localhost:8080`

### Using Docker Compose (Build from Source)

1. Clone the repository:
   ```bash
   git clone https://github.com/radaiko/disk-analyzer.git
   cd disk-analyzer
   ```

2. Update `docker-compose.yml` to mount your directories:
   ```yaml
   volumes:
     - /your/data/path:/mnt/data:ro
     - /your/media/path:/mnt/media:ro
     - ./config:/app/config
   ```

3. Build and run:
   ```bash
   docker-compose up -d
   ```

4. Open your browser to `http://localhost:8080`

### Using Docker (Build from Source)

```bash
docker build -t disk-analyzer .

docker run -d \
  --name disk-analyzer \
  -p 8080:8080 \
  -v /your/data:/mnt/data:ro \
  -v /your/media:/mnt/media:ro \
  -v ./config:/app/config \
  disk-analyzer
```

### Manual Build

Requirements:
- .NET 10 SDK
- SQLite

```bash
cd src/DiskAnalyzer
dotnet build
dotnet run
```

## Usage

### Dashboard

The dashboard is the main view where you can:

1. **Start a Scan** - Click "Start Scan" to begin analyzing your mounted directories
2. **View Pie Chart** - See the size distribution of the current folder's subfolders
3. **Navigate Tree** - Click on folders in the tree view to drill down
4. **Monitor Progress** - Watch real-time progress during scans

### Settings

Configure the analyzer through the Settings page:

- **Scan Root Path**: The root directory to scan (default: `/mnt`)
- **Scan Interval**: How often to automatically scan (in hours)
- **Auto Scan**: Enable/disable automatic scheduled scanning

## Configuration

### Environment Variables

- `ASPNETCORE_URLS`: HTTP endpoint (default: `http://+:8080`)
- `ASPNETCORE_ENVIRONMENT`: Environment setting (default: `Production`)

### Volume Mounts

- `/mnt/*`: Mount your data directories here (read-only recommended)
- `/app/config`: Persistent storage for database and settings

## Docker Compose Examples

### TrueNAS Scale

```yaml
version: "3.9"
services:
  disk-analyzer:
    image: ghcr.io/radaiko/disk-analyzer:latest
    container_name: disk-analyzer
    ports:
      - "8080:8080"
    volumes:
      - /mnt/pool1/data:/mnt/data:ro
      - /mnt/pool1/media:/mnt/media:ro
      - /mnt/pool1/apps/disk-analyzer/config:/app/config
    restart: unless-stopped
```

### Unraid

```yaml
version: "3.9"
services:
  disk-analyzer:
    image: ghcr.io/radaiko/disk-analyzer:latest
    container_name: disk-analyzer
    ports:
      - "8080:8080"
    volumes:
      - /mnt/user/data:/mnt/data:ro
      - /mnt/user/media:/mnt/media:ro
      - /mnt/user/appdata/disk-analyzer:/app/config
    restart: unless-stopped
```

## Architecture

```
┌──────────────────────────┐
│    Web Browser           │
│  (Blazor Dark UI)        │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│  ASP.NET Core (.NET 10)  │
│  - Scan Scheduler        │
│  - Folder Analyzer       │
│  - Blazor Pages          │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│   SQLite Database        │
│  - Folder tree           │
│  - Size metrics          │
│  - Scan timestamps       │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│  Mounted Volumes (/mnt)  │
│  - User-defined folders  │
└──────────────────────────┘
```

## Development

### Project Structure

```
disk-analyzer/
├── src/
│   └── DiskAnalyzer/
│       ├── Components/       # Blazor components
│       ├── Data/            # Database context
│       ├── Models/          # Data models
│       ├── Services/        # Business logic
│       └── wwwroot/         # Static files
├── tests/
│   └── DiskAnalyzer.Tests/  # Unit tests
├── Dockerfile
├── docker-compose.yml
└── README.md
```

### Building

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Development Mode

```bash
cd src/DiskAnalyzer
dotnet run
```

## Performance Considerations

- **Large Directories**: Scanning very large directories (millions of files) may take time
- **Permissions**: The container needs read access to mounted directories
- **Storage**: SQLite database grows with the number of folders scanned
- **Memory**: Memory usage scales with the size of the directory tree

## Troubleshooting

### Scan Not Starting

- Verify volume mounts are correct
- Check container has read permissions
- View logs: `docker logs disk-analyzer`

### Database Issues

- Delete `config/diskanalyzer.db` to reset
- Ensure config directory is writable

### Performance Issues

- Reduce scan frequency in settings
- Exclude large, unnecessary directories
- Increase container resources

## Future Enhancements

- File-type breakdown (videos, backups, etc.)
- Cleanup recommendations
- Export reports (CSV/JSON)
- Authentication
- Multiple scan profiles
- Duplicate file detection
- Historical usage trends

## License

See LICENSE file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Support

For issues and questions, please use the GitHub issue tracker.
