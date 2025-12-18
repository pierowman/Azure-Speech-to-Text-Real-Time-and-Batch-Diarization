# Test Script for Speech to Text Application
# This script runs the application and provides test instructions

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Speech to Text Application Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if katiesteve.wav exists
$audioFile = "katiesteve.wav"
$audioPath = Join-Path $PSScriptRoot $audioFile

if (Test-Path $audioPath) {
    Write-Host "? Test audio file found: $audioFile" -ForegroundColor Green
    
    # Get file info
    $fileInfo = Get-Item $audioPath
    Write-Host "  Size: $([math]::Round($fileInfo.Length / 1KB, 2)) KB" -ForegroundColor Gray
    Write-Host "  Path: $audioPath" -ForegroundColor Gray
} else {
    Write-Host "? Test audio file NOT found: $audioFile" -ForegroundColor Red
    Write-Host "  Expected location: $audioPath" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please ensure katiesteve.wav is in the project root directory." -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit
}

Write-Host ""

# Check configuration
Write-Host "Checking Azure Speech Service configuration..." -ForegroundColor Cyan
$configPath = Join-Path $PSScriptRoot "appsettings.json"

if (Test-Path $configPath) {
    $config = Get-Content $configPath | ConvertFrom-Json
    
    if ($config.AzureSpeech.SubscriptionKey -and $config.AzureSpeech.SubscriptionKey -ne "YOUR_AZURE_SPEECH_KEY_HERE") {
        Write-Host "? Subscription Key is configured" -ForegroundColor Green
    } else {
        Write-Host "? Subscription Key is NOT configured" -ForegroundColor Red
        Write-Host "  Please update appsettings.json with your Azure credentials" -ForegroundColor Yellow
        Write-Host ""
        Read-Host "Press Enter to exit"
        exit
    }
    
    if ($config.AzureSpeech.Region) {
        Write-Host "? Region is set to: $($config.AzureSpeech.Region)" -ForegroundColor Green
    }
    
    if ($config.AzureSpeech.Endpoint) {
        Write-Host "? Custom Endpoint: $($config.AzureSpeech.Endpoint)" -ForegroundColor Green
    } else {
        Write-Host "  Using default regional endpoint" -ForegroundColor Gray
    }
} else {
    Write-Host "? Configuration file not found: $configPath" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Starting Application..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "The application will start in a moment." -ForegroundColor Yellow
Write-Host "Once it starts, your browser will open automatically." -ForegroundColor Yellow
Write-Host ""
Write-Host "TEST STEPS:" -ForegroundColor Cyan
Write-Host "1. Click 'Choose File' button" -ForegroundColor White
Write-Host "2. Navigate to: $audioPath" -ForegroundColor White
Write-Host "3. Select 'katiesteve.wav' and click Open" -ForegroundColor White
Write-Host "4. Click 'Transcribe' button" -ForegroundColor White
Write-Host "5. Wait for the transcription to complete" -ForegroundColor White
Write-Host "6. Verify results are displayed in both tabs:" -ForegroundColor White
Write-Host "   - Speaker Segments tab" -ForegroundColor Gray
Write-Host "   - Full Transcript tab" -ForegroundColor Gray
Write-Host ""
Write-Host "Press Ctrl+C in this window to stop the application" -ForegroundColor Yellow
Write-Host ""

# Wait a moment before starting
Start-Sleep -Seconds 2

# Start the application
try {
    # Run dotnet run and capture the output to get the URL
    $process = Start-Process -FilePath "dotnet" -ArgumentList "run" -WorkingDirectory $PSScriptRoot -PassThru
    
    Write-Host "Application starting (Process ID: $($process.Id))..." -ForegroundColor Green
    Write-Host ""
    Write-Host "Waiting for application to be ready..." -ForegroundColor Yellow
    
    # Wait a few seconds for the app to start
    Start-Sleep -Seconds 5
    
    # Open browser to the default URL
    $url = "https://localhost:5001"
    Write-Host "Opening browser to: $url" -ForegroundColor Green
    Start-Process $url
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Application is running!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "To stop: Press Ctrl+C or close this window" -ForegroundColor Yellow
    
    # Wait for the process to exit
    $process.WaitForExit()
}
catch {
    Write-Host ""
    Write-Host "Error starting application: $_" -ForegroundColor Red
    Write-Host ""
}

Write-Host ""
Write-Host "Application stopped." -ForegroundColor Yellow
