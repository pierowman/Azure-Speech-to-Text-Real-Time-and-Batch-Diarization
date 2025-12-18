# Script Loading Order Fix - Test Script
# This script helps verify that the JavaScript loading order fix works correctly

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "JavaScript Loading Order Fix - Test" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "? ISSUE FIXED: Script loading order" -ForegroundColor Green
Write-Host "  - transcription-jobs.js now loads BEFORE transcription-mode-selector.js" -ForegroundColor Green
Write-Host "  - This ensures loadTranscriptionJobs() is defined before it's called" -ForegroundColor Green
Write-Host ""

# Check if app is running
Write-Host "[Step 1] Checking if application is running..." -ForegroundColor Yellow
$appRunning = $false
try {
    $response = Invoke-WebRequest -Uri "https://localhost:7139" -Method GET -TimeoutSec 3 -UseBasicParsing -ErrorAction SilentlyContinue
    $appRunning = $true
    Write-Host "? Application is running" -ForegroundColor Green
} catch {
    Write-Host "? Application is NOT running" -ForegroundColor Red
}

Write-Host ""

if ($appRunning) {
    Write-Host "=====================================" -ForegroundColor Yellow
    Write-Host "RESTART REQUIRED" -ForegroundColor Yellow
    Write-Host "=====================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "The application is currently running, but you need to:" -ForegroundColor Yellow
    Write-Host "1. STOP the application (Shift+F5 in Visual Studio)" -ForegroundColor White
    Write-Host "2. START the application (F5 in Visual Studio)" -ForegroundColor White
    Write-Host "3. CLEAR browser cache (Ctrl+Shift+Delete or Ctrl+F5)" -ForegroundColor White
    Write-Host ""
    Write-Host "This ensures the new script loading order takes effect." -ForegroundColor Yellow
} else {
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host "READY TO START" -ForegroundColor Green
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Application is not running. You're ready to:" -ForegroundColor Green
    Write-Host "1. START the application (F5 in Visual Studio)" -ForegroundColor White
    Write-Host "   OR run: dotnet run --project speechtotext" -ForegroundColor White
    Write-Host ""
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "HOW TO TEST THE FIX" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Step 1: Clear Browser Cache" -ForegroundColor White
Write-Host "  - Press Ctrl+Shift+Delete" -ForegroundColor Gray
Write-Host "  - OR do a hard refresh: Ctrl+F5" -ForegroundColor Gray
Write-Host ""

Write-Host "Step 2: Open Browser Console" -ForegroundColor White
Write-Host "  - Press F12" -ForegroundColor Gray
Write-Host "  - Go to Console tab" -ForegroundColor Gray
Write-Host ""

Write-Host "Step 3: Click 'Batch' Mode Button" -ForegroundColor White
Write-Host "  - Click the 'Batch' radio button" -ForegroundColor Gray
Write-Host ""

Write-Host "Step 4: Verify - Should See" -ForegroundColor White
Write-Host "  ? transcription-jobs.js loaded" -ForegroundColor Green
Write-Host "  ? loadTranscriptionJobs exposed to window" -ForegroundColor Green
Write-Host "  ? Batch mode selected - switching to Jobs tab" -ForegroundColor Green
Write-Host "  ? Jobs tab shown" -ForegroundColor Green
Write-Host ""

Write-Host "Step 5: Verify - Should NOT See" -ForegroundColor White
Write-Host "  ? loadTranscriptionJobs function not found!" -ForegroundColor Red
Write-Host "  ? Uncaught ReferenceError" -ForegroundColor Red
Write-Host ""

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "SCRIPT LOADING ORDER (CORRECT)" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. transcription.js          (base functionality)" -ForegroundColor White
Write-Host "2. transcription-progress.js (progress tracking)" -ForegroundColor White
Write-Host "3. transcription-jobs.js     (defines loadTranscriptionJobs) ? MOVED UP" -ForegroundColor Green
Write-Host "4. transcription-mode-selector.js (calls loadTranscriptionJobs)" -ForegroundColor White
Write-Host ""

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "WHAT WAS WRONG BEFORE" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. transcription.js" -ForegroundColor White
Write-Host "2. transcription-jobs.js" -ForegroundColor White
Write-Host "3. transcription-mode-selector.js ? LOADED TOO EARLY" -ForegroundColor Red
Write-Host "4. transcription-progress.js" -ForegroundColor White
Write-Host ""
Write-Host "Problem: Step 3 tried to call loadTranscriptionJobs()" -ForegroundColor Red
Write-Host "         but step 2 hadn't defined it yet!" -ForegroundColor Red
Write-Host ""

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "VERIFICATION CHECKLIST" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

$checklist = @(
    "? Clear browser cache (Ctrl+F5)",
    "? Open browser console (F12)",
    "? Navigate to application",
    "? Click 'Batch' mode button",
    "? Verify no JavaScript errors",
    "? Verify jobs tab appears",
    "? Verify jobs load (or show 'no jobs')"
)

foreach ($item in $checklist) {
    Write-Host "  $item" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Green
Write-Host "FIX SUMMARY" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""
Write-Host "File Changed:" -ForegroundColor White
Write-Host "  speechtotext/Views/Home/Index.cshtml" -ForegroundColor Gray
Write-Host ""
Write-Host "Change Made:" -ForegroundColor White
Write-Host "  Reordered <script> tags in @section Scripts" -ForegroundColor Gray
Write-Host ""
Write-Host "Result:" -ForegroundColor White
Write-Host "  ? transcription-jobs.js loads before transcription-mode-selector.js" -ForegroundColor Green
Write-Host "  ? loadTranscriptionJobs() is defined before it's called" -ForegroundColor Green
Write-Host "  ? No more 'function not found' errors" -ForegroundColor Green
Write-Host ""

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "DOCUMENTATION" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "For more details, see:" -ForegroundColor White
Write-Host "  SCRIPT_LOADING_ORDER_FIX.md" -ForegroundColor Gray
Write-Host ""

Write-Host "Press any key to exit..." -ForegroundColor Cyan
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
