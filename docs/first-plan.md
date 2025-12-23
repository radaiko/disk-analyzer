# Disk Space Analyzer – Project Plan

## 1. Project Overview

### Goal
Develop a **self-hosted disk space analyzer** that runs as a **Docker container** and provides a **modern web interface** for visualizing disk usage across mounted directories.

### Target Environment
- **Host systems**: TrueNAS, Unraid, Linux servers, NAS devices
- **Deployment**: Docker / Docker Compose
- **Technology stack**:
  - Backend: **C# .NET 10**
  - Frontend: **Blazor UI**
  - UI Theme: **Dark Mode (default)**
  - Charts: Blazor-compatible charting library
  - Utilities: **CommunityToolkit**
  - Storage: **SQLite**

---

## 2. High-Level Architecture

```

+---------------------------+
|       Web Browser         |
|  (Blazor Dark UI)         |
+-------------+-------------+
|
v
+---------------------------+
|   ASP.NET Core (.NET 10)  |
|   - Scan Scheduler        |
|   - Folder Analyzer       |
|   - Blazor Pages          |
+-------------+-------------+
|
v
+---------------------------+
|   SQLite Database         |
|   - Folder tree           |
|   - Size metrics          |
|   - Scan timestamps      |
+-------------+-------------+
|
v
+---------------------------+
|   Mounted Volumes (/mnt)  |
|   - User-defined folders  |
+---------------------------+

```

---

## 3. Core Features

### 3.1 Folder Scanning Engine

**Responsibilities**
- Recursively scan directories mounted under `/mnt`
- Calculate:
  - Total folder size
  - Subfolder sizes
  - File counts
- Handle very large directories efficiently

**Design Considerations**
- Asynchronous scanning
- Cancellation support
- Error handling for permissions and missing mounts
- Persist results to avoid rescans during navigation

**Key Components**
- `FolderScannerService`
- `FolderNode`
- `ScanResult`

---

### 3.2 Scan Interval & Scheduler

**Functionality**
- User-defined scan interval (hours or days)
- Interval starts **after a scan finishes**
- Prevent overlapping scans
- Manual scan trigger

**Implementation**
- ASP.NET Core `BackgroundService`
- Persistent settings (SQLite or JSON)
- Scan locking mechanism

**CommunityToolkit Usage**
- `ObservableObject` for scan state
- `WeakReferenceMessenger` for progress updates

---

### 3.3 Data Storage

**Technology**
- SQLite (embedded, Docker-friendly)

**Stored Data**
- Folder path
- Parent-child relationships
- Aggregated folder size
- Percentage of parent
- Last scanned timestamp

**Benefits**
- Fast UI load times
- No rescanning required for browsing results

---

## 4. Web Interface (Blazor UI)

### 4.1 UI Design Principles
- Dark theme by default
- Clean NAS-style dashboard
- Responsive layout
- Fast drill-down navigation

---

### 4.2 Dashboard Layout

```

+--------------------------------------------------+
| Header (Scan status, Last scan, Settings)        |
+------------------------+-------------------------+
| Pie Chart              | Folder Tree View        |
| (Current Level)        | (Expandable)            |
|                        |                         |
+------------------------+-------------------------+

````

---

### 4.3 Pie Chart Visualization

**Behavior**
- Displays size distribution of the selected folder’s immediate subfolders
- Percentages relative to the selected folder
- Clicking a slice:
  - Selects folder
  - Updates tree view
  - Refreshes pie chart

**Displayed Data**
- Folder name
- Size (GB)
- Percentage

---

### 4.4 Folder Tree View

**Features**
- Hierarchical tree starting at `/mnt`
- Expand/collapse nodes
- Shows folder sizes
- Highlights selected folder

**Interaction**
- Selecting a folder updates:
  - Pie chart
  - Tree selection
  - Breadcrumb navigation

---

### 4.5 Settings Page

**Options**
- Scan interval
- Enable / disable automatic scanning
- Manual scan button
- (Optional) Excluded folders

**UX**
- Instant save
- Validation feedback
- Clear warnings for long scans

---

## 5. Docker & Deployment

### 5.1 Docker Image

**Base Image**
- `mcr.microsoft.com/dotnet/aspnet:10.0`

**Behavior**
- Runs ASP.NET Core app
- Exposes configurable port (default 8080)
- Uses `/mnt` as scan root

---

### 5.2 Docker Compose Example

```yaml
version: "3.9"
services:
  disk-analyzer:
    image: disk-space-analyzer
    container_name: disk-analyzer
    ports:
      - "8080:8080"
    volumes:
      - /data:/mnt/data
      - /media:/mnt/media
      - ./config:/app/config
    restart: unless-stopped
````

---

## 6. Development Phases

### Phase 1 – Foundation

* Create .NET 10 solution
* Setup Blazor UI
* Implement dark theme
* Dockerfile and Docker Compose
* Base layout and routing

---

### Phase 2 – Scanning Engine

* Recursive folder scanning
* Async execution
* SQLite persistence
* Progress reporting

---

### Phase 3 – Scheduler & Settings

* Background scan service
* Configurable scan interval
* Manual scan trigger
* Persistent settings

---

### Phase 4 – UI Visualization

* Folder tree component
* Pie chart integration
* Drill-down navigation
* UI refresh after scan completion

---

### Phase 5 – Optimization & Polish

* Performance improvements
* Error handling
* Loading states
* UI animations

---

### Phase 6 – Packaging & Documentation

* Docker image build
* TrueNAS deployment guide
* README with screenshots
* Configuration examples

---

## 7. Optional Future Enhancements

* File-type breakdown (videos, backups, etc.)
* Cleanup recommendations
* Export reports (CSV / JSON)
* Authentication
* Multiple scan profiles

---

## 8. Final Outcome

A **self-hosted, Docker-based disk space analyzer** with:

* Dark-mode Blazor web UI
* Efficient scanning of large directories
* Interactive pie charts and folder tree
* Easy deployment on TrueNAS and similar systems
