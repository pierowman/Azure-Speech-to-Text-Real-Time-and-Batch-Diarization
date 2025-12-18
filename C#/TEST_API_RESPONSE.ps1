# Quick API Test - Check Current Response
# Run this AFTER restarting the application

Write-Host "=== TESTING API RESPONSE ===" -ForegroundColor Cyan
Write-Host ""

try {
    Write-Host "Calling /Home/GetTranscriptionJobs..." -ForegroundColor Yellow
    $jobs = Invoke-RestMethod -Uri "https://localhost:5001/Home/GetTranscriptionJobs" -SkipCertificateCheck
    
    if ($jobs.success -and $jobs.jobs.Count -gt 0) {
        $firstJob = $jobs.jobs[0]
        
        Write-Host "? API Call Successful" -ForegroundColor Green
        Write-Host ""
        Write-Host "First Job Data:" -ForegroundColor White
        Write-Host "  Locale (raw):        '$($firstJob.locale)'" -ForegroundColor Gray
        Write-Host "  FormattedLocale:     '$($firstJob.formattedLocale)'" -ForegroundColor Cyan
        Write-Host ""
        
        # Check if it's working
        if ($firstJob.formattedLocale -match "^[A-Z]{2}-[A-Z]{2}$") {
            Write-Host "? STILL BROKEN - FormattedLocale is uppercase code!" -ForegroundColor Red
            Write-Host ""
            Write-Host "This means one of:" -ForegroundColor Yellow
            Write-Host "  1. Application wasn't restarted after code change" -ForegroundColor Gray
            Write-Host "  2. Locale name map is empty (fetch failed)" -ForegroundColor Gray
            Write-Host "  3. Case mismatch still occurring" -ForegroundColor Gray
            Write-Host ""
            Write-Host "ACTION REQUIRED:" -ForegroundColor Red
            Write-Host "  1. STOP the application (Shift+F5 in Visual Studio)" -ForegroundColor White
            Write-Host "  2. START the application (F5)" -ForegroundColor White
            Write-Host "  3. Wait for 'Now listening on' message" -ForegroundColor White
            Write-Host "  4. Run this script again" -ForegroundColor White
        }
        elseif ($firstJob.formattedLocale -like "*(*)*") {
            Write-Host "? FIXED! FormattedLocale is now a display name!" -ForegroundColor Green
            Write-Host ""
            Write-Host "Now do this:" -ForegroundColor Yellow
            Write-Host "  1. Hard refresh browser: Ctrl+Shift+F5" -ForegroundColor White
            Write-Host "  2. Go to Jobs tab" -ForegroundColor White
            Write-Host "  3. Click Refresh" -ForegroundColor White
            Write-Host "  4. Language should show display name!" -ForegroundColor White
        }
        else {
            Write-Host "??  UNEXPECTED VALUE" -ForegroundColor Yellow
            Write-Host "The formattedLocale doesn't match expected patterns" -ForegroundColor Gray
        }
    }
    else {
        Write-Host "??  No jobs found or API call failed" -ForegroundColor Yellow
        Write-Host "Response: $($jobs | ConvertTo-Json -Depth 3)" -ForegroundColor Gray
    }
}
catch {
    Write-Host "? ERROR calling API" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Make sure:" -ForegroundColor Yellow
    Write-Host "  - Application is running" -ForegroundColor Gray
    Write-Host "  - HTTPS certificate is trusted" -ForegroundColor Gray
    Write-Host "  - Port 5001 is correct" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=== TEST COMPLETE ===" -ForegroundColor Cyan
