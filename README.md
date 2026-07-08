# SSHM - SSH Manager (.NET)

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Linux%20%7C%20macOS%20%7C%20Windows-lightgrey?style=for-the-badge)](https://github.com/Gu1llaum-3/sshm)
[![Upstream](https://img.shields.io/badge/upstream-sshm--go-00ADD8?style=for-the-badge)](https://github.com/Gu1llaum-3/sshm)

> **A modern, interactive SSH Manager for your terminal** — .NET port of [Gu1llaum-3/sshm](https://github.com/Gu1llaum-3/sshm)

This directory contains a **.NET 10** reimplementation of [sshm-go](https://github.com/Gu1llaum-3/sshm), focused on **feature parity with the original TUI**. It reads and writes the same `~/.ssh/config`, shares the same `sshm` application data directory, and behaves like the Go edition in interactive mode.

<p align="center">
    <a href="https://github.com/Gu1llaum-3/sshm/blob/main/images/sshm.gif" target="_blank">
        <img src="https://github.com/Gu1llaum-3/sshm/raw/main/images/sshm.gif" alt="Demo SSHM Terminal" width="800" />
    </a>
    <br>
    <em>Demo from the upstream Go project — .NET TUI aims for the same experience</em>
</p>

## Features

### Core Capabilities

- **Beautiful TUI Interface** — Navigate SSH hosts with an interactive terminal UI ([Terminal.Gui](https://github.com/gui-cs/Terminal.Gui))
- **Quick Connect** — Connect through the TUI or CLI with `sshm <host>`
- **Port Forwarding** — Local, Remote, and Dynamic (SOCKS) forwarding with history persistence
- **Easy Management** — Add, edit, move, and delete hosts from the TUI
- **Tag Support** — Organize hosts with custom tags; the `hidden` tag excludes hosts from the list while keeping them connectable
- **Smart Search** — Multi-word AND filtering across name, hostname, user, and tags
- **Real-time Status** — Asynchronous SSH connectivity checks with color-coded indicators
- **Smart Updates** — Background GitHub release check with in-TUI notification
- **Connection History** — Last login timestamps and port-forwarding presets per host

### Technical Features

- **Secure** — Works directly with your existing `~/.ssh/config`
- **Custom Config Support** — Any SSH config file via `-c` / `--config`
- **SSH Include Support** — Full recursive `Include` directive parsing
- **SSH Options Support** — Additional options via forms (`-o` format auto-converted)
- **Automatic Backups** — Config backups before every modification
- **Validation** — Built-in host/hostname/port/identity validation
- **ProxyJump / ProxyCommand** — Bastion and custom jump command support
- **Keyboard Shortcuts** — Vim-like navigation (`j`/`k`, `/`, `Tab`, etc.)
- **Cross-platform** — Linux, macOS, and Windows (.NET 10)
- **Data compatible** — Same `config.json` and `sshm_history.json` as the Go version

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- OpenSSH client (`ssh` on `PATH`)

### Build & Run

```bash
# From the sshm/ directory
dotnet build src/Sshm/Sshm.csproj

# Launch TUI
dotnet run --project src/Sshm/Sshm.csproj

# Publish a single-file executable (optional)
dotnet publish src/Sshm/Sshm.csproj -c Release -r win-x64 --self-contained
```

**Windows (PowerShell):**

```powershell
cd sshm
dotnet run --project src/Sshm/Sshm.csproj
```

**Terminal encoding (Windows):** Use **Windows Terminal** or **Cursor integrated terminal** for correct UTF-8 / emoji rendering. The app sets console code page 65001 at startup; legacy `cmd.exe` windows may still show replacement characters if the font lacks emoji glyphs.

## Usage

### Interactive Mode

Launch SSHM without arguments to enter the TUI:

```bash
sshm
# or: dotnet run --project src/Sshm/Sshm.csproj
```

**Navigation:**

| Key | Action |
|-----|--------|
| `↑`/`↓` or `j`/`k` | Navigate hosts |
| `Enter` | Connect to selected host |
| `a` | Add new host |
| `e` | Edit selected host |
| `d` | Delete selected host |
| `m` | Move host to another config file (requires `Include`) |
| `f` | Port forwarding setup |
| `i` | Host information |
| `H` | Toggle hidden hosts visibility |
| `h` | Help |
| `q` | Quit |
| `/` or `Ctrl+F` | Search / filter hosts |

**Real-time Status Indicators:**

| Indicator | Meaning |
|-----------|---------|
| 🟢 | Online — host reachable via SSH |
| 🟡 | Connecting — check in progress |
| 🔴 | Offline — unreachable or SSH failed |
| ⚫ | Unknown — not yet checked (`p` pings all) |

**Sorting & Filtering:**

| Key | Action |
|-----|--------|
| `s` | Cycle sort mode (name ↔ last login) |
| `n` | Sort by name (A–Z) |
| `r` | Sort by recent connection |
| `Tab` | Toggle focus: search bar ↔ host table |
| `/` | Enter search mode (multi-word AND filter) |

Interactive forms cover: Host name, Hostname/IP, User, Port, Identity file, ProxyJump, ProxyCommand, SSH options, Tags, RemoteCommand, RequestTTY.

### CLI Mode (current .NET build)

```bash
# TUI (default)
sshm

# Connect directly
sshm my-server

# Custom SSH config
sshm -c /path/to/ssh_config
sshm my-server -c /path/to/ssh_config

# Focus search at startup
sshm -s
sshm --search

# Disable update check
sshm --no-update-check

# Version & help
sshm --version
sshm --help
```

> **Note:** Standalone subcommands from the Go edition (`add`, `edit`, `move`, `search`, `info`, shell `completion`) are not exposed as separate CLI commands yet — use the **TUI** (`a`, `e`, `m`, `/`, `i`) for the same workflows.

### Port Forwarding

Press `f` on a selected host. Forward types:

- **Local (-L)** — `ssh -L [bind:]local:remote_host:remote_port host`
- **Remote (-R)** — `ssh -R [bind:]remote:local_host:local_port host`
- **Dynamic (-D)** — `ssh -D [bind:]port host` (SOCKS proxy)

Use `←`/`→` on the forward-type field to switch modes. Previous settings are restored from per-host history. See the [upstream README](https://github.com/Gu1llaum-3/sshm#port-forwarding) for remote forwarding / SOCKS troubleshooting.

### Backup Configuration

Backups are created automatically before any config change.

| Platform | Backup directory |
|----------|------------------|
| Linux / macOS | `~/.config/sshm/backups/` (or `$XDG_CONFIG_HOME/sshm/backups/`) |
| Windows | `%APPDATA%\sshm\backups\` |

**Quick recovery (Windows):**

```powershell
copy "$env:APPDATA\sshm\backups\config.backup" "$env:USERPROFILE\.ssh\config"
```

**Quick recovery (Unix):**

```bash
cp ~/.config/sshm/backups/config.backup ~/.ssh/config
```

Connection history and port-forwarding presets: `{SSHM config dir}/sshm_history.json`

## Configuration

SSHM uses your standard OpenSSH config and adds `# Tags:` comment lines for organization. Fully compatible with other SSH tools.

### SSH Include Support

```ssh
# ~/.ssh/config
Include ~/.ssh/config.d/*
Include work-servers.conf

Host personal-server
    HostName personal.example.com
    User myuser
```

### Tags & Hidden Hosts

```ssh
# Tags: production, web
Host web-prod-01
    HostName 192.168.1.10
    User deploy

# Tags: hidden, backup
Host secret-backup
    HostName 10.0.0.99
    User admin
```

Hosts tagged `hidden` are omitted from the TUI list unless you press `H`. They remain connectable via `sshm secret-backup`.

### Supported SSH Options (forms)

**Built-in fields:** `HostName`, `User`, `Port`, `IdentityFile`, `ProxyJump`, `ProxyCommand`, `Tags`, `RemoteCommand`, `RequestTTY`

**Additional options** — enter in the form as CLI style:

```
-o Compression=yes -o ServerAliveInterval=60
```

Stored in config as:

```ssh
    Compression yes
    ServerAliveInterval 60
```

### Application Configuration

| Platform | Path |
|----------|------|
| Linux / macOS | `~/.config/sshm/config.json` |
| Windows | `%APPDATA%\sshm\config.json` |

**Example:**

```json
{
  "check_for_updates": false,
  "key_bindings": {
    "quit_keys": ["q", "ctrl+c"],
    "disable_esc_quit": true
  }
}
```

| Option | Description |
|--------|-------------|
| `check_for_updates` | Check GitHub for new releases at TUI startup (default: `true`) |
| `quit_keys` | Keys that quit the app (default: `q`, `ctrl+c`) |
| `disable_esc_quit` | When `true`, `Esc` no longer quits (useful for Vim users) |

Disable once via CLI: `sshm --no-update-check`

## Development

### Prerequisites

- .NET 10 SDK
- Git
- OpenSSH client

### Build from Source

```bash
cd sshm
dotnet restore
dotnet build src/Sshm/Sshm.csproj
dotnet run --project src/Sshm/Sshm.csproj
```

### Project Structure

```
sshm/
├── Directory.Build.props     # Shared MSBuild properties (net10.0)
├── Sshm.slnx                 # Solution file
├── README.md
└── src/
    ├── Sshm/                 # Executable entry (Program.cs)
    ├── Sshm.Core/            # Models, enums, ConsoleEncoding
    ├── Sshm.Config/          # SSH config parse / CRUD / app config
    ├── Sshm.History/         # Connection & port-forward history
    ├── Sshm.Connectivity/    # Async SSH ping (SSH.NET + external ssh)
    ├── Sshm.Validation/      # Host field validation
    ├── Sshm.Version/           # GitHub release checker
    └── Sshm.UI/                # Terminal.Gui TUI
        ├── TuiApplication.cs   # Main shell & keyboard routing
        ├── HostTableBuilder.cs
        ├── HostFilter.cs / HostSorter.cs
        ├── AddHostFormView.cs
        ├── EditHostFormView.cs
        ├── MoveHostFormView.cs
        ├── InfoView.cs
        ├── PortForwardFormView.cs
        ├── HelpView.cs
        └── FileSelectorView.cs
```

### Dependencies

| Package | Role |
|---------|------|
| [Terminal.Gui](https://www.nuget.org/packages/Terminal.Gui) 2.4+ | TUI framework (≈ Bubble Tea + Lipgloss) |
| [SSH.NET](https://www.nuget.org/packages/SSH.NET) | TCP / SSH handshake for connectivity checks |
| System.Text.Json | `config.json` & history persistence |

### Module Map (Go → .NET)

| Go (`internal/`) | .NET |
|------------------|------|
| `config/` | `Sshm.Config` |
| `history/` | `Sshm.History` |
| `connectivity/` | `Sshm.Connectivity` |
| `validation/` | `Sshm.Validation` |
| `version/` | `Sshm.Version` |
| `ui/` | `Sshm.UI` |
| `cmd/` | `Sshm` (partial — TUI + basic CLI) |

## Platform Notes

**Windows**

- Uses built-in OpenSSH (Windows 10/11)
- SSH config: `%USERPROFILE%\.ssh\config`
- App data: `%APPDATA%\sshm\`
- Prefer Windows Terminal for UTF-8 and emoji

**Linux / macOS**

- SSH config: `~/.ssh/config`
- App data: `~/.config/sshm/` (or `$XDG_CONFIG_HOME/sshm/`)
- File permissions preserved on config writes (Unix)

## Relationship to Upstream

This project is a **.NET reimplementation** of [Gu1llaum-3/sshm](https://github.com/Gu1llaum-3/sshm). Feature design, keyboard layout, data formats, and SSH config conventions follow the original Go tool.

For release binaries, install scripts, and the full CLI command reference, see the **[upstream repository](https://github.com/Gu1llaum-3/sshm)**.

## License

Behavior and documentation are derived from the upstream MIT-licensed [sshm](https://github.com/Gu1llaum-3/sshm) project. See upstream [LICENSE](https://github.com/Gu1llaum-3/sshm/blob/main/LICENSE).

## Acknowledgments

- [Guillaume](https://github.com/Gu1llaum-3) — original **sshm** (Go)
- [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui) — .NET TUI toolkit
- [Charm](https://charm.sh/) — Bubble Tea / Lipgloss (upstream UI inspiration)
- SSH Include, multi-word search, and key-binding features — upstream contributors
