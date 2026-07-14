# WhatSprintIsItWidget (Windows 11)

A Windows 11 widget that displays the **current Azure DevOps sprint and week**, matching
[whatsprintis.it](https://whatsprintis.it/).

The sprint/week is **computed locally** (no network call) using the same algorithm the
site uses, so the widget keeps working even if the site is unreachable:

```
epoch          = 2010-07-24 (UTC)
weeksPerSprint = 3
weeksSinceEpoch = floor((now - epoch) / 1 week)
sprint          = floor(weeksSinceEpoch / weeksPerSprint)
week            = (weeksSinceEpoch % weeksPerSprint) + 1
```

## Project layout

| File | Purpose |
| ---- | ------- |
| `src/SprintCalculator.cs` | Offline sprint/week calculation (verified against the site). |
| `src/WidgetProvider.cs` | `IWidgetProvider` implementation — builds the Adaptive Card + data. |
| `src/Program.cs` | Out-of-process COM server host (class factory + message loop). |
| `src/Package.appxmanifest` | MSIX manifest: `windows.comServer` + `windows.widgetProvider` extensions. |
| `src/WhatSprintIsItWidget.csproj` | Windows App SDK (packaged, full-trust) project. |
| `test/` | Console test that verifies the calculation against known golden values. |

## Prerequisites

- Windows 11 (widgets require Win11).
- Visual Studio 2022 with the **Windows App SDK** / .NET desktop workload, **or**
  the .NET 8 SDK plus the Windows App SDK build tools.
- Developer Mode enabled (Settings → Privacy & security → For developers) to sideload MSIX.

## Icon & screenshot assets

The asset set under `src\Assets\` is **generated** by `tools\make_icons.py`
(a checkered "sprint" finish-flag badge on an Azure-blue → teal gradient):

| Asset | Size | Used for |
| ----- | ---- | -------- |
| `Square44x44Logo.png` | 44×44 | App list / taskbar |
| `Square71x71Logo.png` | 71×71 | Small tile |
| `Square150x150Logo.png` | 150×150 | Medium tile |
| `Square310x310Logo.png` | 310×310 | Large tile |
| `Wide310x150Logo.png` | 310×150 | Wide tile (badge + wordmark) |
| `StoreLogo.png` | 50×50 | Store / provider icon |
| `WhatSprintIsItWidgetScreenshot.png` | 320×176 | Widget picker preview |
| `WhatSprintIsItWidget.png` | 256×256 | Master badge |
| `WhatSprintIsItWidget.ico` | 16–256 | General app icon |

Regenerate them any time with:

```powershell
py tools\make_icons.py   # requires Pillow: py -m pip install pillow
```

The generator also emits the **scale-qualified** variants MSIX packaging expects
(`*.scale-100/125/150/200/400.png`) plus target-size app-list icons
(`Square44x44Logo.targetsize-16/24/32/48/256.png` and `.altform-unplated`), so a
plain `dotnet build` / VS **Deploy** won't fail on missing scaled assets.

## Verify the calculation

```powershell
cd test
dotnet run -c Release
```

Expected: all checks pass (e.g. `2026-07-14 => sprint 277 week 3`).

## Build & install the widget

### One-command script (recommended)

```powershell
# From the project root. Builds and registers the widget for the current user.
.\build-deploy.ps1                     # Release, auto-detects x64/arm64
.\build-deploy.ps1 -Configuration Debug -Platform arm64
.\build-deploy.ps1 -Package            # build + install a sideloadable MSIX instead
.\build-deploy.ps1 -Unregister         # remove the installed widget
```

The script finds MSBuild via `vswhere`, builds the packaged project, and
registers the loose AppX layout with `Add-AppxPackage -Register` (no code-signing
needed for the dev inner loop). Then open **Win + W** -> **Add widgets** ->
**Azure DevOps Sprint**.

### Visual Studio

1. Open `src/WhatSprintIsItWidget.csproj` in Visual Studio 2022.
2. Set the platform to match your machine (x64/arm64) and build.
3. Right-click the project → **Deploy** (or **Package and Publish → Create App Packages**
   to produce a sideloadable MSIX).
4. After deploy, open the Widgets board (**Win + W**) → **+ Add widgets** → pick
   **Azure DevOps Sprint**.

## How updates work

- On `CreateWidget` / `Activate` / `OnWidgetContextChanged`, the provider recomputes the
  sprint and calls `WidgetManager.UpdateWidget` with the Adaptive Card template + data.
- To refresh on a schedule (e.g. daily so the week rolls over), add a background trigger
  or a timer that iterates the active widgets and calls the same update path. Sprints only
  change weekly, so a once-per-day refresh is plenty.

## Notes

- The CLSID `6E3E1B58-1D8C-4F2A-9C4E-6A5B2F0E9D11` appears in three places and must stay
  in sync: `WidgetProvider.ClsidString`, the `com:Class Id` and the `CreateInstance ClassId`
  in `Package.appxmanifest`.
- The widget definition id `Sprint_Widget` in the manifest is the id passed to
  `CreateWidget` as `widgetContext.DefinitionId`.
