# Quick Test: Verify Emoji JSON Encoding

Write-Host "Testing JSON Encoder Configuration..." -ForegroundColor Cyan
Write-Host ""

# Check if Program.cs has the fix
Write-Host "[1] Checking Program.cs for the fix..." -ForegroundColor Yellow
$programCs = Get-Content "speechtotext/Program.cs" -Raw

if ($programCs -match "UnsafeRelaxedJsonEscaping") {
    Write-Host "? FIX FOUND in Program.cs" -ForegroundColor Green
    Write-Host "   Line: options.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;" -ForegroundColor Gray
} else {
    Write-Host "? FIX NOT FOUND in Program.cs" -ForegroundColor Red
    Write-Host "   The emoji fix is missing!" -ForegroundColor Red
    Write-Host ""
    Write-Host "   Run this to add the fix:" -ForegroundColor Yellow
    Write-Host "   # Contact support or check EMOJI_UI_FIX.md" -ForegroundColor Gray
    exit
}

Write-Host ""

# Check if app is running
Write-Host "[2] Checking if application is running..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "https://localhost:7139/Home/GetTranscriptionJobs" `
        -Method GET `
        -TimeoutSec 5 `
        -UseBasicParsing `
        -ErrorAction Stop
    
    Write-Host "? Application is RUNNING" -ForegroundColor Green
    
    Write-Host ""
    Write-Host "[3] Testing JSON response for emojis..." -ForegroundColor Yellow
    
    # Check if response contains escaped Unicode or actual emoji
    $content = $response.Content
    
    # Look for escaped Unicode pattern for flag emojis
    if ($content -match '\\u[0-9A-Fa-f]{4}') {
        Write-Host "? EMOJIS ARE STILL ESCAPED!" -ForegroundColor Red
        Write-Host "   Found: \\uXXXX pattern in JSON" -ForegroundColor Red
        Write-Host ""
        Write-Host "   This means:" -ForegroundColor Yellow
        Write-Host "   1. The fix is in the code, but..." -ForegroundColor Yellow
        Write-Host "   2. The app was NOT restarted after adding it" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "   SOLUTION:" -ForegroundColor Green
        Write-Host "   ? Stop the app (Shift+F5)" -ForegroundColor Cyan
        Write-Host "   ? Clean solution (Build -> Clean Solution)" -ForegroundColor Cyan
        Write-Host "   ? Rebuild (Build -> Rebuild Solution)" -ForegroundColor Cyan
        Write-Host "   ? Start the app (F5)" -ForegroundColor Cyan
        Write-Host "   ? Hard refresh browser (Ctrl+Shift+F5)" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "   Then run this test again." -ForegroundColor Gray
    } else {
        Write-Host "? EMOJIS ARE NOT ESCAPED!" -ForegroundColor Green
        Write-Host "   Server is sending raw emoji characters" -ForegroundColor Green
        Write-Host ""
        Write-Host "   If you're still seeing ???? in the browser:" -ForegroundColor Yellow
        Write-Host "   1. Hard refresh (Ctrl+Shift+F5)" -ForegroundColor Cyan
        Write-Host "   2. Clear browser cache (Ctrl+Shift+Delete)" -ForegroundColor Cyan
        Write-Host "   3. Try a different browser" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "   Check the Network tab in DevTools:" -ForegroundColor Gray
        Write-Host "   F12 -> Network -> GetTranscriptionJobs -> Response" -ForegroundColor Gray
        Write-Host "   Look for 'formattedLocale' - should show flag emoji" -ForegroundColor Gray
    }
    
} catch {
    Write-Host "? Application is NOT RUNNING" -ForegroundColor Red
    Write-Host ""
    Write-Host "   START THE APP FIRST:" -ForegroundColor Yellow
    Write-Host "   ? Press F5 in Visual Studio" -ForegroundColor Cyan
    Write-Host "   ? Or run: dotnet run --project speechtotext" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "   Then run this test again." -ForegroundColor Gray
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "Test complete. Press any key to exit..." -ForegroundColor Cyan
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
