<#
.SYNOPSIS
    Builds the Jukebox-Visualizations wrapper and stages a self-contained
    drop-in layout suitable for placing next to Jukebox.exe.

.DESCRIPTION
    Produces a single staged folder at `./publish/stage/` containing:

        publish/stage/
        ├── lib/                              (flat — all drop-in files)
        │   ├── JukeboxVisualizations.dll     (managed wrapper)
        │   ├── JukeboxVisualizations.deps.json
        │   ├── libprojectM.dll               (Windows — from source lib/)
        │   ├── libprojectM.so.4              (Linux   — from source lib/)
        │   └── glew32.dll                    (Windows — from source lib/)
        └── ProjectM/                         (preset data only)
            ├── presets/
            └── textures/

    The user drops this entire `publish/stage/` content next to Jukebox.exe
    to enable visualizations. The native lib/ folder is NOT shipped in this
    repo — the developer populates `lib/` manually by downloading from
    the GitHub release produced by the build-natives.yml CI workflow
    (see lib/README.md for instructions).

    The managed JukeboxVisualizations.dll is pure IL — identical for
    every platform. We run `dotnet publish` for both win-x64 and
    linux-x64 RIDs (so the RID-specific .deps.json is generated), but
    only stage the managed DLL from one of them. The native binaries
    are copied verbatim from the source `lib/` folder.

.NOTES
    Run from the Jukebox-Visualizations project root (next to
    JukeboxVisualizations.csproj).
#>

Set-Location $PSScriptRoot

Write-Host "Building Jukebox-Visualizations" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host ""

# ── Validate source layout ──────────────────────────────────────────────
if (-not (Test-Path "JukeboxVisualizations.csproj")) {
    Write-Host "ERROR: JukeboxVisualizations.csproj not found in current directory." -ForegroundColor Red
    Write-Host "       Run this script from the Jukebox-Visualizations project root." -ForegroundColor Red
    exit 1
}



$sourceLib = ".\lib"

# ── Verify lib/ is populated ────────────────────────────────────────────
# The user must manually download the libprojectM binary from the GitHub
# release (see lib/README.md for instructions). We don't auto-fetch —
# PowerShell execution policies and script dependencies make that fragile.
# Just check that lib/ has something in it and warn if not.
$libPopulated = (Test-Path $sourceLib) -and ((Get-ChildItem $sourceLib -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -notin @("README.md", ".gitignore") }).Count -gt 0)
if (-not $libPopulated) {
    Write-Host "WARNING: lib/ is empty or missing." -ForegroundColor Yellow
    Write-Host "         The build will succeed but native libprojectM binaries" -ForegroundColor Yellow
    Write-Host "         will not be staged in the drop-in zip." -ForegroundColor Yellow
    Write-Host "" -ForegroundColor Yellow
    Write-Host "         To populate lib/:" -ForegroundColor Yellow
    Write-Host "           1. Go to https://github.com/RobG66/Jukebox-Visualizations/releases" -ForegroundColor Yellow
    Write-Host "           2. Download libprojectM-win-x64.zip (or libprojectM-linux-x64.tar.gz)" -ForegroundColor Yellow
    Write-Host "           3. Extract into lib/" -ForegroundColor Yellow
    Write-Host "         See lib/README.md for details." -ForegroundColor Yellow
    Write-Host ""
}

# ── Clean previous builds ───────────────────────────────────────────────
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean "JukeboxVisualizations.csproj" --configuration Release 2>$null | Out-Null
if (Test-Path "./publish") {
    Remove-Item -Recurse -Force "./publish"
}
New-Item -ItemType Directory -Force -Path "./publish/stage" | Out-Null
Write-Host ""

# ── Windows x64 build ──────────────────────────────────────────────────
Write-Host "Building Windows x64..." -ForegroundColor Cyan
Write-Host "-----------------------" -ForegroundColor Cyan

dotnet publish "JukeboxVisualizations.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --output "./publish/win-x64" `
    -p:DebugType=None `
    -p:DebugSymbols=false

$winSuccess = ($LASTEXITCODE -eq 0)
Write-Host ""

# ── Linux x64 build ────────────────────────────────────────────────────
Write-Host "Building Linux x64..." -ForegroundColor Cyan
Write-Host "---------------------" -ForegroundColor Cyan

dotnet publish "JukeboxVisualizations.csproj" `
    --configuration Release `
    --runtime linux-x64 `
    --self-contained false `
    --output "./publish/linux-x64" `
    -p:DebugType=None `
    -p:DebugSymbols=false

$linuxSuccess = ($LASTEXITCODE -eq 0)
Write-Host ""

if (-not $winSuccess -or -not $linuxSuccess) {
    Write-Host "================================" -ForegroundColor Red
    Write-Host "Build FAILED - staging skipped." -ForegroundColor Red
    if (-not $winSuccess)   { Write-Host "  Windows x64 : FAILED" -ForegroundColor Red }
    if (-not $linuxSuccess) { Write-Host "  Linux x64   : FAILED" -ForegroundColor Red }
    exit 1
}

# ── Stage into publish/stage/ ──────────────────────────────────────────
Write-Host "Staging into ./publish/stage/..." -ForegroundColor Cyan
Write-Host "--------------------------------" -ForegroundColor Cyan

$dest = "./publish/stage"
$dstLib = Join-Path $dest "lib"
New-Item -ItemType Directory -Force -Path $dstLib | Out-Null

# 1) Native libraries from the source lib/ folder — flat, all platforms
#    coexist by extension. Goes into <dest>/lib/.
if ($libPopulated) {
    Copy-Item (Join-Path $sourceLib "*") -Destination $dstLib -Recurse -Force
    Write-Host "  Copied native libs: lib/" -ForegroundColor Gray
}
else {
    Write-Host "  Skipped native libs: source lib/ is empty" -ForegroundColor Yellow
}

# 2) Managed wrapper — same DLL for both platforms; copy from the win-x64
#    build (arbitrary choice). Also copy deps.json so the runtime can
#    resolve its Avalonia + Silk.NET dependencies when loaded via
#    Assembly.LoadFrom. Lives in <dest>/lib/ alongside the native
#    libprojectM binary — all optional drop-in files in one place.
foreach ($file in @(
    "JukeboxVisualizations.dll",
    "JukeboxVisualizations.deps.json"
)) {
    $src = Join-Path "./publish/win-x64" $file
    if (Test-Path $src) {
        Copy-Item $src -Destination $dstLib -Force
        Write-Host "  Copied: lib/$file" -ForegroundColor Gray
    }
}

# 3) ProjectM preset data copy has been disabled to prevent long build times and large ZIP sizes.

Write-Host ""

# ── Summary ───────────────────────────────────────────────────────────
Write-Host "================================" -ForegroundColor Green
Write-Host "Stage Complete" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host ""
Write-Host "Drop-in layout staged at:" -ForegroundColor Green
Write-Host "  $((Resolve-Path $dest).Path)" -ForegroundColor Cyan
Write-Host ""
Write-Host "Layout:" -ForegroundColor Gray
Get-ChildItem $dest -Recurse -Depth 1 | ForEach-Object {
    $rel = $_.FullName.Substring((Resolve-Path $dest).Path.Length).TrimStart('\','/')
    if ($_) {
        $type = if ($_.PSIsContainer) { "[DIR] " } else { "      " }
        Write-Host ("  {0}{1}" -f $type, $rel) -ForegroundColor Gray
    }
}
Write-Host ""

# ── Zip for distribution ───────────────────────────────────────────────
$zipPath = "./publish/Jukebox-Visualizations-dropin.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $dest "*") -DestinationPath $zipPath -Force
Write-Host "Zip created:" -ForegroundColor Green
Write-Host "  $((Resolve-Path $zipPath).Path)" -ForegroundColor Cyan
$zipSize = (Get-Item $zipPath).Length / 1MB
Write-Host ("  Size: {0:N2} MB" -f $zipSize) -ForegroundColor Cyan
Write-Host ""
Write-Host "To enable visualizations in Jukebox:" -ForegroundColor Yellow
Write-Host "  1. Unzip the archive into your Jukebox build output directory" -ForegroundColor Yellow
Write-Host "     (next to Jukebox.exe). The result is:" -ForegroundColor Yellow
Write-Host "         lib/                          (wrapper + native runtimes)" -ForegroundColor Yellow
Write-Host "  2. Also drop Jukebox's own native runtimes (bass.dll, libmpv-2.dll" -ForegroundColor Yellow
Write-Host "     or libbass.so, libmpv.so.2) into the same lib/ folder." -ForegroundColor Yellow
Write-Host "  3. Restart Jukebox. The visualizer button appears in the transport" -ForegroundColor Yellow
Write-Host "     bar automatically." -ForegroundColor Yellow
Write-Host ""

exit 0
