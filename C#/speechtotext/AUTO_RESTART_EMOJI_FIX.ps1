# AUTOMATED EMOJI FIX RESTART
# This script automates the restart process

param(
    [switch]$SkipBrowserInstructions
)

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "     AUTOMATED EMOJI FIX RESTART" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Stop all running instances
Write-Host "[1/5] Stopping all IIS Express and dotnet processes..." -ForegroundColor Yellow
$stopped = 0
Get-Process -Name "iisexpress","dotnet" -ErrorAction SilentlyContinue | ForEach-Object {
    $_ | Stop-Process -Force
    $stopped++
}

if ($stopped -gt 0) {
    Write-Host "    ? Stopped $stopped process(es)" -ForegroundColor Green
} else {
    Write-Host "    ? No processes running" -ForegroundColor Gray
}

Start-Sleep -Seconds 2

# Step 2: Clean solution
Write-Host ""
Write-Host "[2/5] Cleaning solution..." -ForegroundColor Yellow
$cleanOutput = dotnet clean speechtotext/speechtotext.csproj 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "    ? Clean succeeded" -ForegroundColor Green
} else {
    Write-Host "    ? Clean failed" -ForegroundColor Red
    Write-Host $cleanOutput
    exit 1
}

# Step 3: Rebuild solution
Write-Host ""
Write-Host "[3/5] Rebuilding solution..." -ForegroundColor Yellow
$buildOutput = dotnet build speechtotext/speechtotext.csproj 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "    ? Build succeeded" -ForegroundColor Green
} else {
    Write-Host "    ? Build failed" -ForegroundColor Red
    Write-Host $buildOutput
    exit 1
}

# Step 4: Verify Program.cs has the fix
Write-Host ""
Write-Host "[4/5] Verifying emoji fix is in code..." -ForegroundColor Yellow
$programCs = Get-Content "speechtotext/Program.cs" -Raw
if ($programCs -match "UnsafeRelaxedJsonEscaping") {
    Write-Host "    ? Emoji fix confirmed in Program.cs" -ForegroundColor Green
} else {
    Write-Host "    ? Emoji fix NOT FOUND in Program.cs!" -ForegroundColor Red
    Write-Host "    The fix may have been lost. Please re-apply." -ForegroundColor Yellow
    exit 1
}

# Step 5: Start application
Write-Host ""
Write-Host "[5/5] Starting application..." -ForegroundColor Yellow
Write-Host "    Starting in background..." -ForegroundColor Gray

# Start the app in a new window
Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run --project speechtotext" -WindowStyle Minimized

Write-Host "    ? Application starting..." -ForegroundColor Green
Write-Host "    Waiting 5 seconds for startup..." -ForegroundColor Gray
Start-Sleep -Seconds 5

# Check if app started
try {
    $response = Invoke-WebRequest -Uri "https://localhost:7139" -Method GET -TimeoutSec 3 -UseBasicParsing -ErrorAction Stop
    Write-Host "    ? Application is running!" -ForegroundColor Green
} catch {
    Write-Host "    ? Application may still be starting..." -ForegroundColor Yellow
    Write-Host "    Give it a few more seconds and try accessing it." -ForegroundColor Gray
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host "     RESTART COMPLETE!" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""

if (-not $SkipBrowserInstructions) {
    Write-Host "?? CRITICAL: YOU MUST CLEAR BROWSER CACHE! ??" -ForegroundColor Red
    Write-Host ""
    Write-Host "The server has restarted, but your browser still has OLD files cached." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "OPTION 1 - Hard Refresh (Easiest):" -ForegroundColor White
    Write-Host "  1. Open the app in your browser" -ForegroundColor Gray
    Write-Host "  2. Press Ctrl+Shift+F5" -ForegroundColor Cyan
    Write-Host "     OR Ctrl+Shift+R" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "OPTION 2 - DevTools (Most Reliable):" -ForegroundColor White
    Write-Host "  1. Open the app in your browser" -ForegroundColor Gray
    Write-Host "  2. Press F12 to open DevTools" -ForegroundColor Gray
    Write-Host "  3. RIGHT-CLICK the refresh button (?)" -ForegroundColor Cyan
    Write-Host "  4. Select 'Empty Cache and Hard Reload'" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "OPTION 3 - Clear All Cache:" -ForegroundColor White
    Write-Host "  1. Press Ctrl+Shift+Delete" -ForegroundColor Cyan
    Write-Host "  2. Select 'Cached images and files'" -ForegroundColor Gray
    Write-Host "  3. Click 'Clear data'" -ForegroundColor Gray
    Write-Host ""
}

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "     VERIFICATION STEPS" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "After clearing cache:" -ForegroundColor White
Write-Host "  1. Navigate to https://localhost:7139" -ForegroundColor Gray
Write-Host "  2. Click 'Batch' mode" -ForegroundColor Gray
Write-Host "  3. Go to 'Transcription Jobs' tab" -ForegroundColor Gray
Write-Host "  4. Press F12 and check console" -ForegroundColor Gray
Write-Host ""
Write-Host "EXPECTED:" -ForegroundColor Green
Write-Host "  - formattedLocale: ???? English (US)" -ForegroundColor Green
Write-Host "    ^^ FLAG EMOJI should display" -ForegroundColor Gray
Write-Host ""
Write-Host "NOT THIS:" -ForegroundColor Red
Write-Host "  - formattedLocale: ???? English (US)" -ForegroundColor Red
Write-Host "    ^^ Question marks" -ForegroundColor Gray
Write-Host ""

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Run this test to verify:" -ForegroundColor White
Write-Host "  .\TEST_EMOJI_JSON.ps1" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
