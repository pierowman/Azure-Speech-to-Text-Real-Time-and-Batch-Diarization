# Test Validation Rules Endpoint
# This script tests the GetValidationRules endpoint to see what's being returned

Write-Host "================================" -ForegroundColor Cyan
Write-Host "Testing Validation Rules API" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Check if the app is running
$appUrl = "http://localhost:5000"
$appUrlHttps = "https://localhost:5001"

Write-Host "Checking if app is running..." -ForegroundColor Yellow

try {
    # Try HTTP first
    $healthCheck = Invoke-WebRequest -Uri $appUrl -TimeoutSec 2 -UseBasicParsing -ErrorAction SilentlyContinue
    $baseUrl = $appUrl
    Write-Host "? App is running on $baseUrl" -ForegroundColor Green
} catch {
    try {
        # Try HTTPS
        $healthCheck = Invoke-WebRequest -Uri $appUrlHttps -TimeoutSec 2 -UseBasicParsing -SkipCertificateCheck -ErrorAction SilentlyContinue
        $baseUrl = $appUrlHttps
        Write-Host "? App is running on $baseUrl" -ForegroundColor Green
    } catch {
        Write-Host "? App is not running. Please start the application first." -ForegroundColor Red
        Write-Host ""
        Write-Host "Run: dotnet run --project speechtotext" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host ""
Write-Host "Testing Real-Time Validation Rules..." -ForegroundColor Cyan
Write-Host "--------------------------------------" -ForegroundColor Gray

try {
    $rtResponse = Invoke-RestMethod -Uri "$baseUrl/Home/GetValidationRules?mode=RealTime" -Method Get -SkipCertificateCheck
    
    if ($rtResponse.success) {
        Write-Host "? Real-Time Rules Retrieved Successfully" -ForegroundColor Green
        Write-Host ""
        Write-Host "Rules Text:" -ForegroundColor Yellow
        Write-Host $rtResponse.rules -ForegroundColor White
        Write-Host ""
        
        # Parse extensions from the rules text
        if ($rtResponse.rules -match '([A-Z/]+),') {
            $extensions = $matches[1]
            Write-Host "Extracted Extensions: $extensions" -ForegroundColor Cyan
            
            # Count occurrences of each extension
            $extArray = $extensions -split '/'
            Write-Host "Extension Count: $($extArray.Length)" -ForegroundColor Cyan
            Write-Host "Extensions List:" -ForegroundColor Cyan
            $extArray | ForEach-Object { Write-Host "  - $_" -ForegroundColor White }
            
            # Check for duplicates
            $duplicates = $extArray | Group-Object | Where-Object { $_.Count -gt 1 }
            if ($duplicates) {
                Write-Host ""
                Write-Host "??  DUPLICATES FOUND!" -ForegroundColor Red
                $duplicates | ForEach-Object {
                    Write-Host "  - $($_.Name) appears $($_.Count) times" -ForegroundColor Red
                }
            } else {
                Write-Host ""
                Write-Host "? No duplicates found" -ForegroundColor Green
            }
        }
    } else {
        Write-Host "? Failed to retrieve rules" -ForegroundColor Red
    }
} catch {
    Write-Host "? Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Testing Batch Validation Rules..." -ForegroundColor Cyan
Write-Host "--------------------------------------" -ForegroundColor Gray

try {
    $batchResponse = Invoke-RestMethod -Uri "$baseUrl/Home/GetValidationRules?mode=Batch" -Method Get -SkipCertificateCheck
    
    if ($batchResponse.success) {
        Write-Host "? Batch Rules Retrieved Successfully" -ForegroundColor Green
        Write-Host ""
        Write-Host "Rules Text:" -ForegroundColor Yellow
        Write-Host $batchResponse.rules -ForegroundColor White
        Write-Host ""
        
        # Parse extensions from the rules text
        if ($batchResponse.rules -match 'files \(up to \d+\), ([A-Z/]+),') {
            $extensions = $matches[1]
            Write-Host "Extracted Extensions: $extensions" -ForegroundColor Cyan
            
            # Count occurrences of each extension
            $extArray = $extensions -split '/'
            Write-Host "Extension Count: $($extArray.Length)" -ForegroundColor Cyan
            Write-Host "Extensions List:" -ForegroundColor Cyan
            $extArray | ForEach-Object { Write-Host "  - $_" -ForegroundColor White }
            
            # Check for duplicates
            $duplicates = $extArray | Group-Object | Where-Object { $_.Count -gt 1 }
            if ($duplicates) {
                Write-Host ""
                Write-Host "??  DUPLICATES FOUND!" -ForegroundColor Red
                $duplicates | ForEach-Object {
                    Write-Host "  - $($_.Name) appears $($_.Count) times" -ForegroundColor Red
                }
            } else {
                Write-Host ""
                Write-Host "? No duplicates found" -ForegroundColor Green
            }
        }
    } else {
        Write-Host "? Failed to retrieve rules" -ForegroundColor Red
    }
} catch {
    Write-Host "? Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "================================" -ForegroundColor Cyan
Write-Host "Testing Complete" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "If duplicates were found, check the application logs for more details." -ForegroundColor Yellow
Write-Host "The fix has been applied to use .Distinct() to remove duplicates." -ForegroundColor Green
