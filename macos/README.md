# Greenshot for macOS

## Setup

### Prerequisites

- macOS on Apple Silicon (`osx-arm64`)
- .NET SDK 9
- JetBrains Rider (optional, for IDE-based development)

### 1. Configure the .NET SDK in Rider

Open `Greenshot.Mac.sln` in Rider and go to **Rider > Settings/Preferences > Build, Execution, Deployment > Toolset and Build**. Ensure the .NET CLI path points to the installed `dotnet` executable, for example:

- `/usr/local/share/dotnet/dotnet`
- `$HOME/.dotnet/dotnet`

### 2. Install the macOS workload

The projects target `net9.0-macos15.0` and require the .NET macOS workload. From the project root, run:

```bash
dotnet workload restore
```

If the workload is not available yet, install it explicitly:

```bash
sudo dotnet workload install macos --skip-manifest-update
```

### 3. Restore and build

```bash
dotnet restore Greenshot.Mac.sln
dotnet build Greenshot.Mac.sln --no-restore
```

A successful build produces the application under:

```text
Greenshot.Mac/bin/Debug/net9.0-macos15.0/osx-arm64/
```

### 4. Run the application

Run the `Greenshot.Mac` project from Rider, or launch the generated executable from the project root:

```bash
./Greenshot.Mac/bin/Debug/net9.0-macos15.0/osx-arm64/Greenshot.Mac.app/Contents/MacOS/Greenshot.Mac
```

### Project configuration notes

- Target framework: `net9.0-macos15.0`
- Minimum supported macOS version: `14.0`
- Runtime identifier: `osx-arm64`
- Application ID: `org.greenshot.mac`

### Rider source mapping

The macOS project may require access to the original Greenshot source files. In Rider, add the original source directory as an additional source or content root:

```text
$PROJECT_DIR$/../src
```

This is a local Rider workspace setting and should not be committed with the project files.
