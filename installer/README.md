# DataChat installers

Builds one-click installers for end users. Both installers ship a self-contained .NET 8 publish
(no .NET runtime required on the target machine) and register DataChat as a background service.

| Platform | Output | Builder tool |
|----------|--------|--------------|
| Windows  | `DataChat-Setup-<version>-x64.exe` | [Inno Setup 6](https://jrsoftware.org/isinfo.php) |
| macOS    | `DataChat-Installer-<version>.pkg` | `pkgbuild` + `productbuild` (Xcode CLT) |

Signing is out of scope for the current pass — users will see SmartScreen / Gatekeeper warnings
and must click through. Hooks are in place to add Authenticode / Developer ID later.

## Prerequisites

- .NET 8 SDK on the build machine.
- **Windows:** Inno Setup 6 installed (`iscc` on PATH).
- **macOS:** Xcode Command Line Tools (`xcode-select --install`).

## Build the Windows installer

Run on Windows (or any machine with `dotnet`, then move the publish output to a Windows box with
Inno Setup):

```powershell
pwsh build/publish.ps1                      # produces build/out/win-x64/
iscc installer/windows/DataChat.iss         # produces build/installers/DataChat-Setup-1.0.0-x64.exe
```

What the `.exe` does on a user's machine:

1. EULA + destination folder picker.
2. Custom page asking **bundled SQL Express** vs **existing SQL Server**.
3. Copies the self-contained publish to `C:\Program Files\DataChat`.
4. Runs `postinstall.ps1` to:
   - Download `eng.traineddata` (~4 MB, `tessdata_fast`) into `<InstallDir>\tessdata\` for OCR. Native Tesseract DLLs are already bundled by the Tesseract NuGet package via self-contained publish.
   - Write `appsettings.Production.json` (connection string depends on the SQL choice).
5. Registers a `DataChat` Windows Service with `sc.exe` and starts it.
6. Optionally opens TCP 5159 in Windows Firewall.
7. Opens the browser to `http://localhost:5159` → the existing Setup Wizard takes over.

Optional branding: drop `assets/datachat.ico` into `installer/windows/assets/` and uncomment
`SetupIconFile` in `DataChat.iss`.

## Build the macOS installer

Run on macOS:

```bash
./build/publish.sh osx-arm64 osx-x64        # produces build/out/osx-arm64 and osx-x64
./installer/macos/build-pkg.sh              # produces build/installers/DataChat-Installer-1.0.0.pkg
```

What the `.pkg` does on a user's machine:

1. Welcome + MIT license + conclusion screens (HTML in `resources/`).
2. Copies both arch binaries to `/Applications/DataChat/bin/{osx-arm64,osx-x64}`.
3. `postinstall` picks the right arch, symlinks it to `/Applications/DataChat/DataChat.Web`.
4. Installs Tesseract OCR — `brew install tesseract` if Homebrew is present (warns the user otherwise) — and downloads `eng.traineddata` to `/Applications/DataChat/tessdata/`. The LaunchAgent sets `DYLD_FALLBACK_LIBRARY_PATH` to `/opt/homebrew/lib:/usr/local/lib` so the .NET Tesseract wrapper can find `libtesseract.dylib` on both Apple Silicon and Intel.
5. Prompts (via `osascript`) for **local Docker SQL Server** or **existing server**.
   - Local: writes a `docker-compose.yml` + random SA password; brings it up with `docker compose up -d`.
   - Existing: leaves connection string empty — the Setup Wizard prompts on first launch.
6. Loads the LaunchAgent (`/Library/LaunchAgents/com.datachat.app.plist`) so the app starts on login.
7. Opens the browser to `http://localhost:5159`.

Uninstall via `/Applications/DataChat/uninstall.command` (double-click).

## File layout

```
build/
  publish.sh                      # produces build/out/<rid>/ self-contained bundles
  publish.ps1
  installers/                     # .exe / .pkg land here

installer/
  README.md                       # this file
  windows/
    DataChat.iss                  # Inno Setup script
    assets/README.md              # drop icon here
    scripts/postinstall.ps1       # called during [Run]
    scripts/uninstall.ps1
  macos/
    build-pkg.sh                  # driver
    distribution.xml              # productbuild distribution
    uninstall.command
    scripts/preinstall
    scripts/postinstall
    LaunchAgent/com.datachat.app.plist
    resources/{welcome,license,conclusion}.html
```

## Adding signing later

**Windows:** add `SignTool=` directive in `[Setup]` pointing at a timestamped `signtool.exe`
invocation referencing your EV/OV code-signing cert.

**macOS:** `pkgbuild --sign "Developer ID Installer: ..."`, then `productbuild --sign ...`,
then `xcrun notarytool submit ... --wait` before shipping.
