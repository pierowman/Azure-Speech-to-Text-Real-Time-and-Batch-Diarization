# Quick Restart and Test for Validation Rules Fix
# This script stops the app, rebuilds, restarts, and tests the validation rules

Write-Host "================================" -ForegroundColor Cyan
Write-Host "Validation Rules Fix - Restart" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Stop any running instances
Write-Host "Step 1: Stopping any running instances..." -ForegroundColor Yellow
Get-Process -Name "speechtotext" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process | Where-Object { $_.ProcessName -like "*dotnet*" -and $_.MainWindowTitle -like "*speechtotext*" } | Stop-Process -Force
Start-Sleep -Seconds 2
Write-Host "? Stopped" -ForegroundColor Green
Write-Host ""

# Step 2: Clean and rebuild
Write-Host "Step 2: Cleaning and rebuilding..." -ForegroundColor Yellow
Push-Location speechtotext
dotnet clean --verbosity quiet
dotnet build --verbosity quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host "? Build successful" -ForegroundColor Green
} else {
    Write-Host "? Build failed" -ForegroundColor Red
    Pop-Location
    exit 1
}
Pop-Location
Write-Host ""

# Step 3: Start the application in the background
Write-Host "Step 3: Starting application..." -ForegroundColor Yellow
$job = Start-Job -ScriptBlock {
    Set-Location $using:PWD
    Push-Location speechtotext
    dotnet run --no-build
    Pop-Location
}
Write-Host "? Application starting (Job ID: $($job.Id))" -ForegroundColor Green
Write-Host ""

# Step 4: Wait for app to be ready
Write-Host "Step 4: Waiting for application to be ready..." -ForegroundColor Yellow
$maxWaitSeconds = 30
$waitedSeconds = 0
$appReady = $false

while ($waitedSeconds -lt $maxWaitSeconds) {
    Start-Sleep -Seconds 1
    $waitedSeconds++
    Write-Host "." -NoNewline -ForegroundColor Gray
    
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5000" -TimeoutSec 1 -UseBasicParsing -ErrorAction SilentlyContinue
        $appReady = $true
        break
    } catch {
        # App not ready yet, continue waiting
    }
    
    # Check if job failed
    if ($job.State -eq "Failed" -or $job.State -eq "Stopped") {
        Write-Host ""
        Write-Host "? Application failed to start" -ForegroundColor Red
        Receive-Job -Job $job
        Remove-Job -Job $job
        exit 1
    }
}

Write-Host ""

if (-not $appReady) {
    Write-Host "? Application did not start within $maxWaitSeconds seconds" -ForegroundColor Red
    Stop-Job -Job $job
    Remove-Job -Job $job
    exit 1
}

Write-Host "? Application is ready!" -ForegroundColor Green
Write-Host ""

# Step 5: Run validation rules test
Write-Host "Step 5: Testing validation rules..." -ForegroundColor Yellow
Write-Host ""
& .\speechtotext\TEST_VALIDATION_RULES.ps1

Write-Host ""
Write-Host "================================" -ForegroundColor Cyan
Write-Host "Restart Complete" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "The application is now running." -ForegroundColor Green
Write-Host "  - HTTP:  http://localhost:5000" -ForegroundColor Cyan
Write-Host "  - HTTPS: https://localhost:5001" -ForegroundColor Cyan
Write-Host ""
Write-Host "To stop the application:" -ForegroundColor Yellow
Write-Host "  Stop-Job -Id $($job.Id); Remove-Job -Id $($job.Id)" -ForegroundColor White
Write-Host ""
Write-Host "To view application output:" -ForegroundColor Yellow
Write-Host "  Receive-Job -Id $($job.Id) -Keep" -ForegroundColor White
Write-Host ""
