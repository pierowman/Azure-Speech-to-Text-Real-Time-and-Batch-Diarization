# Test Batch Jobs Additional Information Display
# This script helps verify what data is being returned for batch transcription jobs

Write-Host "=================================" -ForegroundColor Cyan
Write-Host "Batch Jobs Info Display Test" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "This test will help diagnose why additional job information" -ForegroundColor Yellow
Write-Host "may not be showing in the Transcription Jobs list." -ForegroundColor Yellow
Write-Host ""

# Step 1: Check if app is running
Write-Host "[Step 1] Checking if application is running..." -ForegroundColor Green
$response = $null
try {
    $response = Invoke-WebRequest -Uri "https://localhost:7139" -Method GET -TimeoutSec 3 -UseBasicParsing -ErrorAction SilentlyContinue
    Write-Host "? Application is running" -ForegroundColor Green
} catch {
    Write-Host "? Application is not running" -ForegroundColor Red
    Write-Host "  Please start the application first:" -ForegroundColor Yellow
    Write-Host "  - Press F5 in Visual Studio, or" -ForegroundColor Yellow
    Write-Host "  - Run: dotnet run --project speechtotext" -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit
}

Write-Host ""

# Step 2: Fetch transcription jobs
Write-Host "[Step 2] Fetching transcription jobs from API..." -ForegroundColor Green
try {
    $jobsResponse = Invoke-RestMethod -Uri "https://localhost:7139/Home/GetTranscriptionJobs" -Method GET
    
    if ($jobsResponse.success) {
        $jobCount = $jobsResponse.jobs.Count
        Write-Host "? Successfully retrieved $jobCount job(s)" -ForegroundColor Green
        
        if ($jobCount -eq 0) {
            Write-Host ""
            Write-Host "? No jobs found in the system" -ForegroundColor Yellow
            Write-Host "  To test, please:" -ForegroundColor Yellow
            Write-Host "  1. Go to the application in your browser" -ForegroundColor Yellow
            Write-Host "  2. Switch to 'Batch' mode" -ForegroundColor Yellow
            Write-Host "  3. Upload one or more audio files" -ForegroundColor Yellow
            Write-Host "  4. Submit the batch job" -ForegroundColor Yellow
            Write-Host "  5. Re-run this test script" -ForegroundColor Yellow
            Write-Host ""
            Read-Host "Press Enter to exit"
            exit
        }
        
        # Analyze first job
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "ANALYZING FIRST JOB" -ForegroundColor Cyan
        Write-Host "========================================" -ForegroundColor Cyan
        
        $firstJob = $jobsResponse.jobs[0]
        
        Write-Host ""
        Write-Host "Basic Info:" -ForegroundColor White
        Write-Host "  Job ID: $($firstJob.id)" -ForegroundColor Gray
        Write-Host "  Display Name: $($firstJob.displayName)" -ForegroundColor Gray
        Write-Host "  Status: $($firstJob.status)" -ForegroundColor Gray
        Write-Host "  Created: $($firstJob.createdDateTime)" -ForegroundColor Gray
        
        Write-Host ""
        Write-Host "Additional Information Check:" -ForegroundColor White
        
        # Check formattedDuration
        if ($null -ne $firstJob.formattedDuration -and $firstJob.formattedDuration -ne "N/A") {
            Write-Host "  ? Duration: $($firstJob.formattedDuration)" -ForegroundColor Green
        } else {
            Write-Host "  ? Duration: NOT AVAILABLE" -ForegroundColor Red
            Write-Host "    (formattedDuration is null or 'N/A')" -ForegroundColor DarkGray
        }
        
        # Check locale
        if ($null -ne $firstJob.formattedLocale -and $firstJob.formattedLocale -ne "N/A") {
            Write-Host "  ? Language: $($firstJob.formattedLocale)" -ForegroundColor Green
        } elseif ($null -ne $firstJob.locale) {
            Write-Host "  ? Language: $($firstJob.locale)" -ForegroundColor Green
        } else {
            Write-Host "  ? Language: NOT AVAILABLE" -ForegroundColor Red
            Write-Host "    (locale is null)" -ForegroundColor DarkGray
        }
        
        # Check totalFileCount
        if ($null -ne $firstJob.totalFileCount -and $firstJob.totalFileCount -gt 0) {
            Write-Host "  ? Total Files: $($firstJob.totalFileCount)" -ForegroundColor Green
        } else {
            Write-Host "  ? Total Files: NOT AVAILABLE" -ForegroundColor Red
            Write-Host "    (totalFileCount is null or 0)" -ForegroundColor DarkGray
        }
        
        # Check files array
        if ($null -ne $firstJob.files -and $firstJob.files.Count -gt 0) {
            Write-Host "  ? Files Array: $($firstJob.files.Count) file(s)" -ForegroundColor Green
            foreach ($file in $firstJob.files) {
                Write-Host "    - $file" -ForegroundColor DarkGray
            }
        } else {
            Write-Host "  ? Files Array: EMPTY OR NULL" -ForegroundColor Red
            Write-Host "    (files array is null or has 0 items)" -ForegroundColor DarkGray
        }
        
        # Check properties object
        Write-Host ""
        Write-Host "Properties Object:" -ForegroundColor White
        if ($null -ne $firstJob.properties) {
            Write-Host "  ? Properties object exists" -ForegroundColor Green
            
            if ($null -ne $firstJob.properties.duration) {
                Write-Host "    ? Duration (ticks): $($firstJob.properties.duration)" -ForegroundColor Green
            } else {
                Write-Host "    ? Duration: NULL" -ForegroundColor Red
            }
            
            if ($null -ne $firstJob.properties.succeededCount) {
                Write-Host "    ? Succeeded Count: $($firstJob.properties.succeededCount)" -ForegroundColor Green
            } else {
                Write-Host "    ? Succeeded Count: NULL" -ForegroundColor Red
            }
            
            if ($null -ne $firstJob.properties.failedCount) {
                Write-Host "    ? Failed Count: $($firstJob.properties.failedCount)" -ForegroundColor Green
            } else {
                Write-Host "    ? Failed Count: NULL" -ForegroundColor Red
            }
            
        } else {
            Write-Host "  ? Properties object is NULL" -ForegroundColor Red
            Write-Host "    This means Azure didn't return properties data" -ForegroundColor DarkGray
        }
        
        # Overall assessment
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "ASSESSMENT" -ForegroundColor Cyan
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host ""
        
        $hasFormattedDuration = ($null -ne $firstJob.formattedDuration -and $firstJob.formattedDuration -ne "N/A")
        $hasFiles = ($null -ne $firstJob.files -and $firstJob.files.Count -gt 0)
        $hasProperties = ($null -ne $firstJob.properties)
        $hasCounts = $hasProperties -and ($null -ne $firstJob.properties.succeededCount -or $null -ne $firstJob.properties.failedCount)
        
        if ($hasFormattedDuration -and $hasFiles -and $hasCounts) {
            Write-Host "? ALL ADDITIONAL INFORMATION IS PRESENT" -ForegroundColor Green
            Write-Host "  The UI should display all job details correctly." -ForegroundColor Green
            Write-Host ""
            Write-Host "  If you're not seeing this in the browser:" -ForegroundColor Yellow
            Write-Host "  1. Clear browser cache (Ctrl+Shift+Delete)" -ForegroundColor Yellow
            Write-Host "  2. Hard refresh the page (Ctrl+F5)" -ForegroundColor Yellow
            Write-Host "  3. Check browser console (F12) for JavaScript errors" -ForegroundColor Yellow
        } elseif ($firstJob.status -ne "Succeeded") {
            Write-Host "? JOB IS NOT COMPLETED YET" -ForegroundColor Yellow
            Write-Host "  Status: $($firstJob.status)" -ForegroundColor Yellow
            Write-Host ""
            Write-Host "  Additional information is only available for completed jobs." -ForegroundColor Yellow
            Write-Host "  Please wait for the job to finish, then:" -ForegroundColor Yellow
            Write-Host "  1. Click 'Refresh' in the Transcription Jobs tab" -ForegroundColor Yellow
            Write-Host "  2. Or re-run this test script" -ForegroundColor Yellow
        } else {
            Write-Host "? SOME INFORMATION IS MISSING" -ForegroundColor Red
            Write-Host ""
            Write-Host "  Missing items:" -ForegroundColor Yellow
            if (-not $hasFormattedDuration) {
                Write-Host "    - Duration" -ForegroundColor Yellow
            }
            if (-not $hasFiles) {
                Write-Host "    - Files list" -ForegroundColor Yellow
            }
            if (-not $hasCounts) {
                Write-Host "    - Success/Failed counts" -ForegroundColor Yellow
            }
            Write-Host ""
            Write-Host "  This could mean:" -ForegroundColor Yellow
            Write-Host "  1. Azure Speech Service API doesn't return this data for your region/tier" -ForegroundColor Yellow
            Write-Host "  2. The job was created before these fields were added" -ForegroundColor Yellow
            Write-Host "  3. There's a parsing issue in TranscriptionJobService.cs" -ForegroundColor Yellow
            Write-Host ""
            Write-Host "  Next steps:" -ForegroundColor Cyan
            Write-Host "  1. Check application logs for parsing warnings" -ForegroundColor Cyan
            Write-Host "  2. Submit a new test job to see if it has the data" -ForegroundColor Cyan
            Write-Host "  3. See BATCH_JOBS_INFO_DIAGNOSTIC.md for more debugging steps" -ForegroundColor Cyan
        }
        
        # Show raw JSON
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "RAW JSON DATA" -ForegroundColor Cyan
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Full first job as JSON:" -ForegroundColor White
        $firstJob | ConvertTo-Json -Depth 5 | Write-Host -ForegroundColor DarkGray
        
    } else {
        Write-Host "? API returned success=false" -ForegroundColor Red
        Write-Host "  Message: $($jobsResponse.message)" -ForegroundColor Red
    }
    
} catch {
    Write-Host "? Failed to fetch jobs" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "  This could mean:" -ForegroundColor Yellow
    Write-Host "  - Azure Speech Service credentials are not configured" -ForegroundColor Yellow
    Write-Host "  - Network connectivity issues" -ForegroundColor Yellow
    Write-Host "  - The API endpoint changed" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "TEST COMPLETE" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "For more information, see:" -ForegroundColor Cyan
Write-Host "  - BATCH_JOBS_INFO_DIAGNOSTIC.md" -ForegroundColor Cyan
Write-Host "  - Application logs in Visual Studio Output window" -ForegroundColor Cyan
Write-Host ""
Read-Host "Press Enter to exit"
