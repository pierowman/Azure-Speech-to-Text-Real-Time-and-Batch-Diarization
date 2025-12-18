# Locale Display Name Diagnostic Script
# This script helps diagnose why locale display names aren't showing

Write-Host "=== LOCALE DISPLAY NAME DIAGNOSTIC ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check if app is running
Write-Host "Step 1: Checking if application is running..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "https://localhost:5001" -SkipCertificateCheck -TimeoutSec 5 -ErrorAction SilentlyContinue
    Write-Host "? Application is running" -ForegroundColor Green
} catch {
    Write-Host "? Application is NOT running!" -ForegroundColor Red
    Write-Host "   Start the application first (F5 in Visual Studio)" -ForegroundColor Yellow
    exit
}

Write-Host ""

# Step 2: Test locale API endpoint
Write-Host "Step 2: Testing locale API endpoint..." -ForegroundColor Yellow
try {
    $localesResponse = Invoke-RestMethod -Uri "https://localhost:5001/Home/GetSupportedLocalesWithNames" -SkipCertificateCheck
    
    if ($localesResponse.success) {
        Write-Host "? Locale API is working" -ForegroundColor Green
        Write-Host "   Locale count: $($localesResponse.count)" -ForegroundColor White
        
        if ($localesResponse.locales.Count -gt 0) {
            Write-Host "   Sample locales:" -ForegroundColor White
            $localesResponse.locales | Select-Object -First 5 | ForEach-Object {
                Write-Host "     - $($_.code): $($_.name)" -ForegroundColor Gray
            }
            
            # Check for en-US specifically
            $enUS = $localesResponse.locales | Where-Object { $_.code -eq "en-US" }
            if ($enUS) {
                Write-Host "   ? en-US found: '$($enUS.name)'" -ForegroundColor Green
            } else {
                Write-Host "   ?? en-US NOT found in locale list!" -ForegroundColor Yellow
            }
        } else {
            Write-Host "   ?? Locale list is empty!" -ForegroundColor Yellow
        }
    } else {
        Write-Host "? Locale API returned failure" -ForegroundColor Red
        Write-Host "   Message: $($localesResponse.message)" -ForegroundColor White
    }
} catch {
    Write-Host "? Failed to call locale API" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor White
}

Write-Host ""

# Step 3: Test jobs API endpoint
Write-Host "Step 3: Testing jobs API endpoint..." -ForegroundColor Yellow
try {
    $jobsResponse = Invoke-RestMethod -Uri "https://localhost:5001/Home/GetTranscriptionJobs" -SkipCertificateCheck
    
    if ($jobsResponse.success) {
        Write-Host "? Jobs API is working" -ForegroundColor Green
        Write-Host "   Job count: $($jobsResponse.jobs.Count)" -ForegroundColor White
        
        if ($jobsResponse.jobs.Count -gt 0) {
            $firstJob = $jobsResponse.jobs[0]
            Write-Host ""
            Write-Host "   First Job Details:" -ForegroundColor White
            Write-Host "     - Display Name: $($firstJob.displayName)" -ForegroundColor Gray
            Write-Host "     - Status: $($firstJob.status)" -ForegroundColor Gray
            Write-Host "     - Locale (raw): '$($firstJob.locale)'" -ForegroundColor Gray
            Write-Host "     - FormattedLocale: '$($firstJob.formattedLocale)'" -ForegroundColor Cyan
            
            # Check if formattedLocale is actually a display name
            if ($firstJob.formattedLocale -match "^[A-Z]{2}-[A-Z]{2}$") {
                Write-Host "     ? FormattedLocale is a CODE (uppercase), not a display name!" -ForegroundColor Red
                Write-Host "        This means the locale name lookup FAILED" -ForegroundColor Yellow
            } elseif ($firstJob.formattedLocale -eq $firstJob.locale) {
                Write-Host "     ? FormattedLocale same as Locale - not formatted!" -ForegroundColor Red
            } elseif ($firstJob.formattedLocale -like "*(*)*") {
                Write-Host "     ? FormattedLocale appears to be a proper display name!" -ForegroundColor Green
            } else {
                Write-Host "     ?? FormattedLocale format unclear" -ForegroundColor Yellow
            }
        } else {
            Write-Host "   ?? No jobs found" -ForegroundColor Cyan
            Write-Host "   Submit a batch job first to test locale display" -ForegroundColor Yellow
        }
    } else {
        Write-Host "? Jobs API returned failure" -ForegroundColor Red
        Write-Host "   Message: $($jobsResponse.message)" -ForegroundColor White
    }
} catch {
    Write-Host "? Failed to call jobs API" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor White
}

Write-Host ""
Write-Host "=== RECOMMENDATIONS ===" -ForegroundColor Cyan

# Analyze results and provide recommendations
if ($localesResponse.count -eq 0 -or $null -eq $localesResponse.count) {
    Write-Host "?? Locale list is empty or failed to load" -ForegroundColor Yellow
    Write-Host "   Possible causes:" -ForegroundColor White
    Write-Host "   1. Azure Speech Service credentials invalid" -ForegroundColor Gray
    Write-Host "   2. Network connectivity issues to Azure" -ForegroundColor Gray
    Write-Host "   3. Cache is corrupted" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   Solutions:" -ForegroundColor White
    Write-Host "   1. Check appsettings.json Azure credentials" -ForegroundColor Gray
    Write-Host "   2. Restart the application to clear cache" -ForegroundColor Gray
    Write-Host "   3. Check Visual Studio Output window for errors" -ForegroundColor Gray
}

if ($jobsResponse.jobs.Count -gt 0 -and $jobsResponse.jobs[0].formattedLocale -match "^[A-Z]{2}-[A-Z]{2}$") {
    Write-Host "?? Locale display names are NOT working" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   Quick Fix:" -ForegroundColor White
    Write-Host "   1. Stop the application (Shift+F5)" -ForegroundColor Gray
    Write-Host "   2. Start the application (F5)" -ForegroundColor Gray
    Write-Host "   3. Hard refresh browser (Ctrl+Shift+F5)" -ForegroundColor Gray
    Write-Host "   4. Click 'Refresh' in Jobs tab" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   Check Output Window:" -ForegroundColor White
    Write-Host "   Look for these lines after clicking Refresh:" -ForegroundColor Gray
    Write-Host "   - 'Loaded X locale display names' (X should be > 0)" -ForegroundColor Gray
    Write-Host "   - 'Sample locale: en-US = [display name]'" -ForegroundColor Gray
    Write-Host "   - 'First job FormattedLocale: [should be display name]'" -ForegroundColor Gray
}

if ($jobsResponse.jobs.Count -gt 0 -and $jobsResponse.jobs[0].formattedLocale -like "*(*)*") {
    Write-Host "? Locale display names ARE working correctly!" -ForegroundColor Green
    Write-Host ""
    Write-Host "   If browser still shows codes:" -ForegroundColor White
    Write-Host "   - Hard refresh browser: Ctrl+Shift+F5" -ForegroundColor Gray
    Write-Host "   - Clear browser cache" -ForegroundColor Gray
    Write-Host "   - Try incognito/private window" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=== CHECK APPLICATION LOGS ===" -ForegroundColor Cyan
Write-Host "Open Visual Studio Output window and look for:" -ForegroundColor White
Write-Host "  - 'Fetching supported locales with names from Azure Speech Service'" -ForegroundColor Gray
Write-Host "  - 'Successfully fetched X supported locales with names from Azure'" -ForegroundColor Gray
Write-Host "  - 'Loaded X locale display names'" -ForegroundColor Gray
Write-Host "  - 'Sample locale: en-US = ...'" -ForegroundColor Gray
Write-Host "  - 'First job FormattedLocale: ...'" -ForegroundColor Gray
Write-Host ""
Write-Host "If you see errors in the logs, that's your clue to what's wrong!" -ForegroundColor Yellow
Write-Host ""
Write-Host "=== DIAGNOSTIC COMPLETE ===" -ForegroundColor Cyan
