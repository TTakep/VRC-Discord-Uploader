# PowerShell script to build Release artifacts (Portable ZIP & Inno Setup Installer)
param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Building VRChat Discord Uploader v$Version Release Package" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Locate dotnet executable
$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnetExe = if ($dotnetCmd) { $dotnetCmd.Source } else { $null }
if (-not $dotnetExe) {
    $possibleDotnetPaths = @(
        "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe",
        "$env:ProgramFiles\dotnet\dotnet.exe"
    )
    foreach ($path in $possibleDotnetPaths) {
        if (Test-Path $path) {
            $dotnetExe = $path
            break
        }
    }
}

if (-not $dotnetExe) {
    Write-Error "Could not find dotnet.exe. Please install the .NET SDK."
    exit 1
}
Write-Host "[1/5] Using dotnet: $dotnetExe" -ForegroundColor Green

# 2. Locate Inno Setup compiler (ISCC.exe)
$isccCmd = Get-Command iscc -ErrorAction SilentlyContinue
$isccExe = if ($isccCmd) { $isccCmd.Source } else { $null }
if (-not $isccExe) {
    $possibleIsccPaths = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles (x86)\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    foreach ($path in $possibleIsccPaths) {
        if (Test-Path $path) {
            $isccExe = $path
            break
        }
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $projectRoot

# 3. Run unit tests
Write-Host "[2/5] Running tests..." -ForegroundColor Green
& $dotnetExe test VRChatDiscordUploader.sln --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Tests failed!"
    exit $LASTEXITCODE
}

# 4. Publish Self-Contained Win-x64 binary
Write-Host "[3/5] Publishing win-x64 self-contained build..." -ForegroundColor Green
$distAppDir = Join-Path $projectRoot "dist\app"
if (Test-Path $distAppDir) {
    Remove-Item -Recurse -Force $distAppDir
}

& $dotnetExe publish src/VRChatDiscordUploader/VRChatDiscordUploader.csproj -c Release -r win-x64 --self-contained true -o "dist/app"
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed!"
    exit $LASTEXITCODE
}

# 5. Create Portable ZIP
Write-Host "[4/5] Creating Portable ZIP..." -ForegroundColor Green
$zipPath = Join-Path $projectRoot "dist\VRChatDiscordUploader-v$Version-win-x64.zip"
if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}
Compress-Archive -Path "dist\app\*" -DestinationPath $zipPath -Force
Write-Host "  -> Created: $zipPath" -ForegroundColor Yellow

# 6. Build Inno Setup Installer
if ($isccExe -and (Test-Path $isccExe)) {
    Write-Host "[5/5] Building Installer with Inno Setup ($isccExe)..." -ForegroundColor Green
    & $isccExe "/DMyAppVersion=$Version" "installer\installer.iss"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Inno Setup compilation failed!"
        exit $LASTEXITCODE
    }
    $installerPath = Join-Path $projectRoot "dist\VRChatDiscordUploader-Setup-$Version.exe"
    Write-Host "  -> Created: $installerPath" -ForegroundColor Yellow
} else {
    Write-Warning "ISCC.exe not found. Installer was not built. (Portable ZIP was created)"
}

Write-Host "==========================================================" -ForegroundColor Green
Write-Host " Build & Packaging completed successfully!" -ForegroundColor Green
Write-Host " Artifacts are in: $(Join-Path $projectRoot 'dist')" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
