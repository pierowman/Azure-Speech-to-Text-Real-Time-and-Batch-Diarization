# EMOJI FIX - FORCE COMPLETE RESTART
# This script helps ensure the emoji fix is properly applied

Write-Host "================================================================" -ForegroundColor Red
Write-Host "     EMOJI FIX REQUIRES COMPLETE RESTART" -ForegroundColor Red
Write-Host "================================================================" -ForegroundColor Red
Write-Host ""

Write-Host "YOU ARE SEEING: ???? English (US)" -ForegroundColor Yellow
Write-Host "YOU SHOULD SEE: ?? English (US) (with flag emoji)" -ForegroundColor Green
Write-Host ""

Write-Host "THE FIX IS IN THE CODE, BUT NOT APPLIED YET!" -ForegroundColor Yellow
Write-Host ""

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "     STEP-BY-STEP RESTART PROCEDURE" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "STEP 1: STOP THE APPLICATION" -ForegroundColor White
Write-Host ""
Write-Host "  In Visual Studio:" -ForegroundColor Gray
Write-Host "  ? Press Shift+F5" -ForegroundColor Cyan
Write-Host "  ? Or click the red square 'Stop' button" -ForegroundColor Cyan
Write-Host ""
Write-Host "  OR if running in terminal:" -ForegroundColor Gray
Write-Host "  ? Press Ctrl+C" -ForegroundColor Cyan
Write-Host "  ? Close the terminal window" -ForegroundColor Cyan
Write-Host ""

Write-Host "STEP 2: CLEAN THE BUILD" -ForegroundColor White
Write-Host ""
Write-Host "  In Visual Studio:" -ForegroundColor Gray
Write-Host "  ? Go to Build menu" -ForegroundColor Cyan
Write-Host "  ? Click 'Clean Solution'" -ForegroundColor Cyan
Write-Host "  ? Wait for it to complete" -ForegroundColor Cyan
Write-Host ""
Write-Host "  OR run this command:" -ForegroundColor Gray
Write-Host "  dotnet clean speechtotext/speechtotext.csproj" -ForegroundColor Cyan
Write-Host ""

Write-Host "STEP 3: REBUILD THE APPLICATION" -ForegroundColor White
Write-Host ""
Write-Host "  In Visual Studio:" -ForegroundColor Gray
Write-Host "  ? Go to Build menu" -ForegroundColor Cyan
Write-Host "  ? Click 'Rebuild Solution'" -ForegroundColor Cyan
Write-Host "  ? Wait for rebuild to complete" -ForegroundColor Cyan
Write-Host ""
Write-Host "  OR run this command:" -ForegroundColor Gray
Write-Host "  dotnet build speechtotext/speechtotext.csproj" -ForegroundColor Cyan
Write-Host ""

Write-Host "STEP 4: START THE APPLICATION" -ForegroundColor White
Write-Host ""
Write-Host "  In Visual Studio:" -ForegroundColor Gray
Write-Host "  ? Press F5" -ForegroundColor Cyan
Write-Host "  ? Or click the green 'Start' button" -ForegroundColor Cyan
Write-Host ""
Write-Host "  OR run this command:" -ForegroundColor Gray
Write-Host "  dotnet run --project speechtotext" -ForegroundColor Cyan
Write-Host ""

Write-Host "STEP 5: CLEAR BROWSER CACHE COMPLETELY" -ForegroundColor White
Write-Host ""
Write-Host "  Option A - Hard Refresh:" -ForegroundColor Gray
Write-Host "  ? Press Ctrl+Shift+F5" -ForegroundColor Cyan
Write-Host "  ? Or Ctrl+Shift+R" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Option B - Clear Cache:" -ForegroundColor Gray
Write-Host "  ? Press Ctrl+Shift+Delete" -ForegroundColor Cyan
Write-Host "  ? Select 'Cached images and files'" -ForegroundColor Cyan
Write-Host "  ? Click 'Clear data'" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Option C - Developer Tools:" -ForegroundColor Gray
Write-Host "  ? Press F12 to open DevTools" -ForegroundColor Cyan
Write-Host "  ? Right-click the refresh button" -ForegroundColor Cyan
Write-Host "  ? Select 'Empty Cache and Hard Reload'" -ForegroundColor Cyan
Write-Host ""

Write-Host "STEP 6: NAVIGATE TO JOBS TAB" -ForegroundColor White
Write-Host ""
Write-Host "  ? Click 'Batch' mode" -ForegroundColor Cyan
Write-Host "  ? Go to 'Transcription Jobs' tab" -ForegroundColor Cyan
Write-Host "  ? Click 'Refresh' button" -ForegroundColor Cyan
Write-Host ""

Write-Host "================================================================" -ForegroundColor Green
Write-Host "     EXPECTED RESULT" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""

Write-Host "In Browser Console (F12):" -ForegroundColor White
Write-Host "  - formattedLocale: ?? English (US)" -ForegroundColor Green
Write-Host "                     ^^ Flag emoji here" -ForegroundColor Gray
Write-Host ""

Write-Host "In UI (Jobs Tab):" -ForegroundColor White
Write-Host "  ?? Language: ?? English (US)" -ForegroundColor Green
Write-Host "  ^^ Icon   ^^ Flag emoji" -ForegroundColor Gray
Write-Host ""

Write-Host "================================================================" -ForegroundColor Red
Write-Host "     IF STILL SEEING ????" -ForegroundColor Red
Write-Host "================================================================" -ForegroundColor Red
Write-Host ""

Write-Host "If you still see ???? after following all steps:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. VERIFY PROGRAM.CS HAS THE FIX:" -ForegroundColor White
Write-Host "   Open: speechtotext/Program.cs" -ForegroundColor Gray
Write-Host "   Look for: JavaScriptEncoder.UnsafeRelaxedJsonEscaping" -ForegroundColor Gray
Write-Host "   Should be on line ~14" -ForegroundColor Gray
Write-Host ""

Write-Host "2. CHECK BUILD OUTPUT:" -ForegroundColor White
Write-Host "   Look in Output window for:" -ForegroundColor Gray
Write-Host "   'Build succeeded'" -ForegroundColor Gray
Write-Host "   No errors or warnings" -ForegroundColor Gray
Write-Host ""

Write-Host "3. CHECK NETWORK TAB:" -ForegroundColor White
Write-Host "   F12 -> Network tab" -ForegroundColor Gray
Write-Host "   Filter: GetTranscriptionJobs" -ForegroundColor Gray
Write-Host "   Click the request" -ForegroundColor Gray
Write-Host "   Look at Response" -ForegroundColor Gray
Write-Host "   Should see actual emoji, not \\uD83C\\uDDFA" -ForegroundColor Gray
Write-Host ""

Write-Host "4. TRY DIFFERENT BROWSER:" -ForegroundColor White
Write-Host "   ? Chrome" -ForegroundColor Cyan
Write-Host "   ? Edge" -ForegroundColor Cyan
Write-Host "   ? Firefox" -ForegroundColor Cyan
Write-Host "   This tests if it's a browser-specific issue" -ForegroundColor Gray
Write-Host ""

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "     QUICK TEST COMMANDS" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Run these to verify the fix is in place:" -ForegroundColor White
Write-Host ""

Write-Host "# Check if Program.cs has the fix:" -ForegroundColor Gray
Write-Host "Get-Content speechtotext/Program.cs | Select-String 'UnsafeRelaxedJsonEscaping'" -ForegroundColor Cyan
Write-Host ""

Write-Host "# This should return a line with the encoder configuration" -ForegroundColor Gray
Write-Host ""

Write-Host "================================================================" -ForegroundColor Yellow
Write-Host "     DEBUGGING - INSPECT JSON RESPONSE" -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Yellow
Write-Host ""

Write-Host "To see what the server is actually sending:" -ForegroundColor White
Write-Host ""
Write-Host "1. Open browser DevTools (F12)" -ForegroundColor Gray
Write-Host "2. Go to Network tab" -ForegroundColor Gray
Write-Host "3. Click 'Batch' mode" -ForegroundColor Gray
Write-Host "4. Find 'GetTranscriptionJobs' request" -ForegroundColor Gray
Write-Host "5. Click on it" -ForegroundColor Gray
Write-Host "6. Go to 'Response' or 'Preview' tab" -ForegroundColor Gray
Write-Host "7. Look for 'formattedLocale' field" -ForegroundColor Gray
Write-Host ""

Write-Host "BEFORE FIX (escaped):" -ForegroundColor Red
Write-Host '  "formattedLocale": "\\uD83C\\uDDFA\\uD83C\\uDDF8 English (US)"' -ForegroundColor Red
Write-Host ""

Write-Host "AFTER FIX (raw emoji):" -ForegroundColor Green
Write-Host '  "formattedLocale": "?? English (US)"' -ForegroundColor Green
Write-Host "                      ^^ Actual emoji character" -ForegroundColor Gray
Write-Host ""

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "     WHAT THE FIX DOES" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "In Program.cs, this line:" -ForegroundColor White
Write-Host "  options.JsonSerializerOptions.Encoder = " -ForegroundColor Gray
Write-Host "      JavaScriptEncoder.UnsafeRelaxedJsonEscaping;" -ForegroundColor Cyan
Write-Host ""
Write-Host "Tells ASP.NET Core to:" -ForegroundColor White
Write-Host "  ? NOT escape Unicode characters (like emojis)" -ForegroundColor Green
Write-Host "  ? Send emojis as-is in JSON" -ForegroundColor Green
Write-Host "  ? Let the browser render them natively" -ForegroundColor Green
Write-Host ""

Write-Host "================================================================" -ForegroundColor Magenta
Write-Host "     PRESS ANY KEY TO SEE VERIFICATION CHECKLIST" -ForegroundColor Magenta
Write-Host "================================================================" -ForegroundColor Magenta
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "     VERIFICATION CHECKLIST" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

$checklist = @(
    "? Application stopped (Shift+F5)",
    "? Solution cleaned (Build -> Clean Solution)",
    "? Solution rebuilt (Build -> Rebuild Solution)",
    "? Build succeeded (check Output window)",
    "? Application restarted (F5)",
    "? Browser cache cleared (Ctrl+Shift+Delete)",
    "? Page hard-refreshed (Ctrl+Shift+F5)",
    "? Navigated to Batch mode",
    "? Opened Jobs tab",
    "? Clicked Refresh",
    "? Checked console for emojis",
    "? Checked UI for emojis",
    "? Verified Network response has emojis"
)

foreach ($item in $checklist) {
    Write-Host "  $item" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host "     IF ALL STEPS DONE, EMOJIS SHOULD WORK!" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""

Write-Host "Still not working? Check:" -ForegroundColor Yellow
Write-Host "  ? EMOJI_UI_FIX.md for detailed explanation" -ForegroundColor Cyan
Write-Host "  ? Server logs for any errors" -ForegroundColor Cyan
Write-Host "  ? Browser console for JavaScript errors" -ForegroundColor Cyan
Write-Host ""

Write-Host "Press any key to exit..." -ForegroundColor Cyan
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
