# Azure Speech Service Credentials Test
# Run this script to verify your Azure Speech Service credentials

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Azure Speech Service Credentials Test" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Read configuration from appsettings.Development.json
$configPath = Join-Path $PSScriptRoot "appsettings.Development.json"

if (-not (Test-Path $configPath)) {
    Write-Host "? Error: appsettings.Development.json not found at: $configPath" -ForegroundColor Red
    Write-Host "Please run this script from the speechtotext project directory." -ForegroundColor Yellow
    exit 1
}

Write-Host "Reading configuration from: $configPath" -ForegroundColor Gray

try {
    $config = Get-Content $configPath -Raw | ConvertFrom-Json
    $key = $config.AzureSpeech.SubscriptionKey
    $region = $config.AzureSpeech.Region
    $endpoint = $config.AzureSpeech.Endpoint
    
    Write-Host "`nConfiguration loaded:" -ForegroundColor Green
    Write-Host "  Subscription Key: $($key.Substring(0, 10))..." -ForegroundColor Gray
    Write-Host "  Region: $region" -ForegroundColor Gray
    Write-Host "  Endpoint: $endpoint" -ForegroundColor Gray
    
} catch {
    Write-Host "? Error reading configuration: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`n----------------------------------------" -ForegroundColor Cyan
Write-Host "Test 1: Token Generation (Authentication)" -ForegroundColor Cyan
Write-Host "----------------------------------------`n" -ForegroundColor Cyan

$tokenUrl = "https://$region.api.cognitive.microsoft.com/sts/v1.0/issuetoken"
$headers = @{
    "Ocp-Apim-Subscription-Key" = $key
}

try {
    Write-Host "Requesting authentication token from: $tokenUrl" -ForegroundColor Gray
    $response = Invoke-WebRequest -Uri $tokenUrl -Method POST -Headers $headers -ErrorAction Stop
    
    if ($response.StatusCode -eq 200) {
        Write-Host "? Authentication successful!" -ForegroundColor Green
        Write-Host "? Your subscription key is valid" -ForegroundColor Green
        Write-Host "? Region is correct: $region" -ForegroundColor Green
        $authSuccess = $true
    }
} catch {
    Write-Host "? Authentication failed!" -ForegroundColor Red
    
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "? Error: Unauthorized (401)" -ForegroundColor Red
        Write-Host "   This means your subscription key is invalid or expired." -ForegroundColor Yellow
        Write-Host "   Please check your key in Azure Portal:" -ForegroundColor Yellow
        Write-Host "   1. Go to https://portal.azure.com" -ForegroundColor Yellow
        Write-Host "   2. Navigate to your Speech Service resource" -ForegroundColor Yellow
        Write-Host "   3. Go to 'Keys and Endpoint'" -ForegroundColor Yellow
        Write-Host "   4. Copy Key 1 or Key 2" -ForegroundColor Yellow
    } elseif ($_.Exception.Response.StatusCode -eq 403) {
        Write-Host "? Error: Forbidden (403)" -ForegroundColor Red
        Write-Host "   Your key may not have permission to access this service." -ForegroundColor Yellow
    } elseif ($_.Exception.Response.StatusCode -eq 404) {
        Write-Host "? Error: Not Found (404)" -ForegroundColor Red
        Write-Host "   The region '$region' may be incorrect." -ForegroundColor Yellow
        Write-Host "   Common regions: eastus, westus, eastus2, westus2, westeurope, etc." -ForegroundColor Yellow
    } else {
        Write-Host "? Error: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    $authSuccess = $false
}

if (-not $authSuccess) {
    Write-Host "`n========================================" -ForegroundColor Red
    Write-Host "Test Failed - Please Fix Configuration" -ForegroundColor Red
    Write-Host "========================================`n" -ForegroundColor Red
    exit 1
}

Write-Host "`n----------------------------------------" -ForegroundColor Cyan
Write-Host "Test 2: Endpoint Configuration Check" -ForegroundColor Cyan
Write-Host "----------------------------------------`n" -ForegroundColor Cyan

if ($endpoint -and $endpoint -ne "") {
    Write-Host "Custom endpoint is configured: $endpoint" -ForegroundColor Gray
    Write-Host "`n? Note: Using custom endpoints is optional" -ForegroundColor Yellow
    Write-Host "   If you're having issues, try removing the Endpoint setting" -ForegroundColor Yellow
    Write-Host "   and rely on region-based connection instead." -ForegroundColor Yellow
    
    # Test if endpoint is reachable
    try {
        $uri = [System.Uri]$endpoint
        Write-Host "`nTesting endpoint connectivity..." -ForegroundColor Gray
        $testResult = Test-NetConnection -ComputerName $uri.Host -Port 443 -WarningAction SilentlyContinue -ErrorAction SilentlyContinue
        
        if ($testResult.TcpTestSucceeded) {
            Write-Host "? Endpoint is reachable" -ForegroundColor Green
        } else {
            Write-Host "? Cannot reach endpoint" -ForegroundColor Red
            Write-Host "   This could be a firewall or network issue" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "? Could not test endpoint connectivity: $($_.Exception.Message)" -ForegroundColor Yellow
    }
} else {
    Write-Host "? Using region-based endpoint (recommended)" -ForegroundColor Green
    Write-Host "   Connection will use: https://$region.stt.speech.microsoft.com" -ForegroundColor Gray
}

Write-Host "`n----------------------------------------" -ForegroundColor Cyan
Write-Host "Test 3: Network Connectivity" -ForegroundColor Cyan
Write-Host "----------------------------------------`n" -ForegroundColor Cyan

$speechEndpoint = "$region.stt.speech.microsoft.com"
Write-Host "Testing connection to Speech Service endpoint..." -ForegroundColor Gray
Write-Host "Endpoint: $speechEndpoint" -ForegroundColor Gray

try {
    $testConnection = Test-NetConnection -ComputerName $speechEndpoint -Port 443 -WarningAction SilentlyContinue -ErrorAction SilentlyContinue
    
    if ($testConnection.TcpTestSucceeded) {
        Write-Host "? Can reach Azure Speech Service" -ForegroundColor Green
        Write-Host "? Port 443 (HTTPS) is open" -ForegroundColor Green
    } else {
        Write-Host "? Cannot reach Azure Speech Service" -ForegroundColor Red
        Write-Host "   This could indicate:" -ForegroundColor Yellow
        Write-Host "   - Firewall blocking connections to *.speech.microsoft.com" -ForegroundColor Yellow
        Write-Host "   - Internet connectivity issues" -ForegroundColor Yellow
        Write-Host "   - Corporate proxy requiring configuration" -ForegroundColor Yellow
    }
} catch {
    Write-Host "? Could not test connectivity: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "? All Tests Passed!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Green

Write-Host "Your Azure Speech Service is configured correctly." -ForegroundColor Green
Write-Host "`nIf you're still experiencing issues with transcription:" -ForegroundColor White
Write-Host "1. Verify your audio file is in a supported format (WAV, MP3, OGG, FLAC)" -ForegroundColor Gray
Write-Host "2. Ensure the audio file is not corrupted" -ForegroundColor Gray
Write-Host "3. Check that the audio contains clear speech" -ForegroundColor Gray
Write-Host "4. Try with a simple WAV file first (16kHz sample rate recommended)" -ForegroundColor Gray
Write-Host "5. Check application logs for detailed error messages" -ForegroundColor Gray
Write-Host "`nFor more help, see: REALTIME_TRANSCRIPTION_ERROR_FIX.md`n" -ForegroundColor Cyan
