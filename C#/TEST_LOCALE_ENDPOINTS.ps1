# Test Azure Speech Service Locale Endpoint
# This script tests the corrected locale endpoint directly

param(
    [Parameter(Mandatory=$true)]
    [string]$Region,
    
    [Parameter(Mandatory=$true)]
    [string]$SubscriptionKey
)

$baseUrlV31 = "https://$Region.api.cognitive.microsoft.com/speechtotext/v3.1"
$baseUrlV32 = "https://$Region.api.cognitive.microsoft.com/speechtotext/v3.2"

$headers = @{
    "Ocp-Apim-Subscription-Key" = $SubscriptionKey
    "Accept" = "application/json"
}

Write-Host "================================" -ForegroundColor Cyan
Write-Host "Testing Azure Speech API Endpoints" -ForegroundColor Cyan
Write-Host "Region: $Region" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Old endpoint (v3.1 transcriptions/locales)
Write-Host "Test 1: OLD endpoint (v3.1/transcriptions/locales)" -ForegroundColor Yellow
Write-Host "URL: $baseUrlV31/transcriptions/locales" -ForegroundColor Gray
try {
    $response = Invoke-RestMethod -Uri "$baseUrlV31/transcriptions/locales" -Headers $headers -Method Get
    Write-Host "? Response received (might be empty or error)" -ForegroundColor Green
    $json = $response | ConvertTo-Json -Depth 5 -Compress
    if ($json.Length -lt 500) {
        Write-Host "Response: $json" -ForegroundColor White
    } else {
        Write-Host "Response (first 500 chars): $($json.Substring(0, 500))..." -ForegroundColor White
    }
} catch {
    Write-Host "? Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "Status Code: $statusCode" -ForegroundColor Red
    }
}
Write-Host ""

# Test 2: New endpoint (v3.2 models/base/locales)
Write-Host "Test 2: NEW endpoint (v3.2/models/base/locales) ?" -ForegroundColor Yellow
Write-Host "URL: $baseUrlV32/models/base/locales" -ForegroundColor Gray
try {
    $response = Invoke-RestMethod -Uri "$baseUrlV32/models/base/locales" -Headers $headers -Method Get
    Write-Host "? Response received successfully!" -ForegroundColor Green
    
    if ($response -is [System.Array]) {
        Write-Host "Response format: Array" -ForegroundColor Cyan
        Write-Host "Number of locales: $($response.Count)" -ForegroundColor Cyan
        Write-Host "First 10 locales: $($response[0..9] -join ', ')" -ForegroundColor White
    } elseif ($response -is [PSCustomObject]) {
        Write-Host "Response format: Object" -ForegroundColor Cyan
        $locales = $response.PSObject.Properties.Name
        Write-Host "Number of locales: $($locales.Count)" -ForegroundColor Cyan
        Write-Host "First 10 locales: $($locales[0..9] -join ', ')" -ForegroundColor White
    } else {
        $json = $response | ConvertTo-Json -Depth 5 -Compress
        if ($json.Length -lt 500) {
            Write-Host "Response: $json" -ForegroundColor White
        } else {
            Write-Host "Response (first 500 chars): $($json.Substring(0, 500))..." -ForegroundColor White
        }
    }
} catch {
    Write-Host "? Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "Status Code: $statusCode" -ForegroundColor Red
    }
}
Write-Host ""

# Test 3: Alternative endpoint (v3.2 models)
Write-Host "Test 3: Alternative endpoint (v3.2/models)" -ForegroundColor Yellow
Write-Host "URL: $baseUrlV32/models" -ForegroundColor Gray
try {
    $response = Invoke-RestMethod -Uri "$baseUrlV32/models" -Headers $headers -Method Get
    Write-Host "? Response received" -ForegroundColor Green
    
    if ($response.values) {
        Write-Host "Number of models: $($response.values.Count)" -ForegroundColor Cyan
        
        # Extract unique locales from models
        $locales = $response.values | Select-Object -ExpandProperty locale -Unique | Sort-Object
        Write-Host "Unique locales from models: $($locales.Count)" -ForegroundColor Cyan
        Write-Host "First 10 locales: $($locales[0..9] -join ', ')" -ForegroundColor White
    } else {
        $json = $response | ConvertTo-Json -Depth 2 -Compress
        if ($json.Length -lt 500) {
            Write-Host "Response: $json" -ForegroundColor White
        } else {
            Write-Host "Response (first 500 chars): $($json.Substring(0, 500))..." -ForegroundColor White
        }
    }
} catch {
    Write-Host "? Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "Status Code: $statusCode" -ForegroundColor Red
    }
}
Write-Host ""

Write-Host "================================" -ForegroundColor Cyan
Write-Host "Test Complete" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? Check which endpoint returned valid locale data above." -ForegroundColor Yellow
Write-Host "?? The application now uses: v3.2/models/base/locales" -ForegroundColor Green
