<div align="center">
  <img src="Icons/main_128.png" width="96" alt="SolidWorks Slicer Bridge icon">

# SolidWorks Slicer Bridge

**One-click 3MF export from SOLIDWORKS 2026 to your slicer.**

[![SOLIDWORKS](https://img.shields.io/badge/SOLIDWORKS-2026-E2231A)](https://www.solidworks.com/)
[![Windows](https://img.shields.io/badge/Windows-x64-0078D4)](https://www.microsoft.com/windows)
[![Slicers](https://img.shields.io/badge/Slicers-Orca%20%7C%20Bambu%20%7C%20Prusa-2F7D32)](#supported-slicers)
[![Release](https://img.shields.io/github/v/release/garik816/SolidWorksSlicerBridge?display_name=tag&sort=semver)](https://github.com/garik816/SolidWorksSlicerBridge/releases/latest)
[![Build release installer](https://github.com/garik816/SolidWorksSlicerBridge/actions/workflows/release.yml/badge.svg)](https://github.com/garik816/SolidWorksSlicerBridge/actions/workflows/release.yml)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

**OrcaSlicer · Bambu Studio · PrusaSlicer**

[Download latest release](https://github.com/garik816/SolidWorksSlicerBridge/releases/latest) · [Русская инструкция](README_RU.md)
</div>

---

## What it does

SolidWorks Slicer Bridge is a COM Add-In for **SOLIDWORKS 2026 x64**. It adds a **3D Print** CommandManager tab and sends the active part or assembly straight to a slicer.

| Command | Action |
|---|---|
| **OrcaSlicer** | Export to 3MF and open in OrcaSlicer |
| **Bambu Studio** | Export to 3MF and open in Bambu Studio |
| **PrusaSlicer** | Export to 3MF and open in PrusaSlicer |
| **Slicer Settings** | Change slicer executable paths |

```text
SOLIDWORKS part / assembly
          ↓
   one toolbar click
          ↓
     silent 3MF export
          ↓
 Orca / Bambu / Prusa
```

## Features

- One-click SOLIDWORKS → 3MF → slicer workflow.
- Supports **parts and assemblies**.
- Exports the **entire active model**, not an accidentally selected face/body.
- Uses SOLIDWORKS' native **3MF** exporter and keeps your configured mesh quality/units.
- Automatically detects common slicer installations and Windows **App Paths**.
- Prompts for the slicer `.exe` only if auto-detection fails, then remembers it.
- Stores slicer paths per user in `HKCU\Software\SolidWorksSlicerBridge`.
- Temporarily suppresses 3MF info/preview dialogs for a true one-click flow, then restores the user's preferences.
- Cleans temporary exports older than 7 days.
- No Visual Studio required on the target PC.

## Install — recommended

1. Open [Releases](https://github.com/garik816/SolidWorksSlicerBridge/releases/latest).
2. Download **`SolidWorksSlicerBridge-<version>-Setup-x64.exe`**.
3. Close SOLIDWORKS.
4. Run Setup and accept the UAC prompt.
5. Start SOLIDWORKS 2026 and open a part or assembly.
6. Use the new **3D Print** tab.

The release installer puts the Add-In in `Program Files`, builds it against the **SOLIDWORKS API files installed on your own machine**, registers the 64-bit COM Add-In, and enables startup loading.

> Why build during install? SOLIDWORKS interop assemblies come from the local SOLIDWORKS installation. The release therefore does not redistribute Dassault Systèmes API binaries.

If the Add-In is not enabled automatically:

`Tools → Add-Ins → SolidWorks Slicer Bridge`

## Portable / developer install

Clone or download the source into a permanent folder and run:

```text
INSTALL.cmd
```

This uses the same local build and registration flow without the packaged Setup UI.

To remove a portable install:

```text
UNINSTALL.cmd
```

## Supported slicers

The Add-In recognizes these executables:

- `orca-slicer.exe`
- `bambu-studio.exe`
- `prusa-slicer.exe`

If auto-detection does not find yours, click the slicer button once and select the executable, or use **3D Print → Slicer Settings**.

## 3MF export settings

Mesh quality and units stay under SOLIDWORKS control:

`Tools → Options → System Options → Export → 3MF`

The Add-In automates export and launch; it does not replace your tessellation settings.

## Release installer

Release packaging is defined in:

```text
installer/SolidWorksSlicerBridge.iss
.github/workflows/release.yml
```

A tag such as `v1.0.0` builds `SolidWorksSlicerBridge-1.0.0-Setup-x64.exe` on GitHub Actions and attaches it to a GitHub Release. The workflow can also be started manually.

## Project layout

```text
SolidWorksSlicerBridge/
├── src/
│   ├── SwAddin.cs
│   └── SettingsForm.cs
├── Icons/
├── installer/
│   └── SolidWorksSlicerBridge.iss
├── .github/workflows/
│   └── release.yml
├── Build.ps1
├── Install.ps1
├── Uninstall.ps1
├── INSTALL.cmd
├── UNINSTALL.cmd
├── README_RU.md
└── LICENSE
```

## Build manually

```powershell
powershell -ExecutionPolicy Bypass -File .\Build.ps1
```

Output:

```text
build\SolidWorksSlicerBridge.dll
```

## Compatibility

| Component | Target |
|---|---|
| SOLIDWORKS | 2026 |
| Windows | x64 |
| .NET | .NET Framework 4.x |
| OrcaSlicer | desktop `.exe` |
| Bambu Studio | desktop `.exe` |
| PrusaSlicer | desktop `.exe` |

## License

MIT License. See [LICENSE](LICENSE).
