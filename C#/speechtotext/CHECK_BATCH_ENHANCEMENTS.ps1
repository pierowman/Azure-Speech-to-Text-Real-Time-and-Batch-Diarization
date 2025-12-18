# Diagnostic Script - Check if Changes are Visible

Write-Host "?? Checking Batch Job Enhancement Files..." -ForegroundColor Cyan
Write-Host ""

# Check if JavaScript file has the changes
$jsFile = "C:\Users\cbo\source\repos\speechtotext\speechtotext\wwwroot\js\transcription-jobs.js"
$jsContent = Get-Content $jsFile -Raw

Write-Host "1. Checking JavaScript file..." -ForegroundColor Yellow
if ($jsContent -match "formattedDuration") {
    Write-Host "   ? Duration code found in JavaScript" -ForegroundColor Green
} else {
    Write-Host "   ? Duration code NOT found in JavaScript" -ForegroundColor Red
}

if ($jsContent -match "succeededCount") {
    Write-Host "   ? Success/failure code found in JavaScript" -ForegroundColor Green
} else {
    Write-Host "   ? Success/failure code NOT found in JavaScript" -ForegroundColor Red
}

if ($jsContent -match "data-bs-toggle.*collapse") {
    Write-Host "   ? Collapse toggle code found in JavaScript" -ForegroundColor Green
} else {
    Write-Host "   ? Collapse toggle code NOT found in JavaScript" -ForegroundColor Red
}

Write-Host ""

# Check if Model file has the changes
$modelFile = "C:\Users\cbo\source\repos\speechtotext\speechtotext\Models\TranscriptionJob.cs"
$modelContent = Get-Content $modelFile -Raw

Write-Host "2. Checking Model file..." -ForegroundColor Yellow
if ($modelContent -match "FormattedDuration") {
    Write-Host "   ? FormattedDuration property found in Model" -ForegroundColor Green
} else {
    Write-Host "   ? FormattedDuration property NOT found in Model" -ForegroundColor Red
}

Write-Host ""

# Check if CSS file has the changes
$cssFile = "C:\Users\cbo\source\repos\speechtotext\speechtotext\wwwroot\css\transcription.css"
$cssContent = Get-Content $cssFile -Raw

Write-Host "3. Checking CSS file..." -ForegroundColor Yellow
if ($cssContent -match "Transcription Jobs List Enhancements") {
    Write-Host "   ? Enhanced job list styles found in CSS" -ForegroundColor Green
} else {
    Write-Host "   ? Enhanced job list styles NOT found in CSS" -ForegroundColor Red
}

Write-Host ""
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Gray
Write-Host ""

# Check if app is running
$dotnetProcess = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like "*speechtotext*" -or $_.Path -like "*speechtotext*" }

Write-Host "4. Checking if application is running..." -ForegroundColor Yellow
if ($dotnetProcess) {
    Write-Host "   ??  Application appears to be running" -ForegroundColor Yellow
    Write-Host "   ??  You need to RESTART the app to see changes!" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   To restart:" -ForegroundColor Cyan
    Write-Host "   1. Press Ctrl+C in the terminal running the app" -ForegroundColor White
    Write-Host "   2. Run: dotnet run" -ForegroundColor White
} else {
    Write-Host "   ??  Application doesn't appear to be running" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "   To start:" -ForegroundColor Cyan
    Write-Host "   cd C:\Users\cbo\source\repos\speechtotext\speechtotext" -ForegroundColor White
    Write-Host "   dotnet run" -ForegroundColor White
}

Write-Host ""
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Gray
Write-Host ""

Write-Host "5. Browser Cache - IMPORTANT!" -ForegroundColor Yellow
Write-Host "   ??  Even after restarting, you MUST clear browser cache!" -ForegroundColor Red
Write-Host ""
Write-Host "   Option 1 - Hard Refresh (try this first):" -ForegroundColor Cyan
Write-Host "   • Press Ctrl+Shift+R (Chrome/Edge)" -ForegroundColor White
Write-Host "   • Or Ctrl+F5" -ForegroundColor White
Write-Host ""
Write-Host "   Option 2 - Clear Cache:" -ForegroundColor Cyan
Write-Host "   • Press Ctrl+Shift+Delete" -ForegroundColor White
Write-Host "   • Select 'Cached images and files'" -ForegroundColor White
Write-Host "   • Click 'Clear data'" -ForegroundColor White
Write-Host ""
Write-Host "   Option 3 - Developer Tools:" -ForegroundColor Cyan
Write-Host "   • Press F12" -ForegroundColor White
Write-Host "   • Right-click the refresh button" -ForegroundColor White
Write-Host "   • Select 'Empty Cache and Hard Reload'" -ForegroundColor White

Write-Host ""
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Gray
Write-Host ""

Write-Host "6. Quick Test After Restart + Cache Clear:" -ForegroundColor Yellow
Write-Host "   1. Navigate to Jobs tab" -ForegroundColor White
Write-Host "   2. Open browser DevTools (F12)" -ForegroundColor White
Write-Host "   3. Go to Console tab" -ForegroundColor White
Write-Host "   4. Look for any errors (red text)" -ForegroundColor White
Write-Host "   5. Check Network tab - verify JS/CSS files loaded" -ForegroundColor White

Write-Host ""
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Gray
Write-Host ""

Write-Host "?? MOST COMMON ISSUE: Browser Cache!" -ForegroundColor Magenta
Write-Host "   The JavaScript and CSS changes are there, but your browser" -ForegroundColor White
Write-Host "   is showing old cached versions. Clear cache and hard refresh!" -ForegroundColor White
Write-Host ""
