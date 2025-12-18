# COMPLETE APP RESTART SCRIPT
# This will force a complete clean restart

Write-Host "`n==================================================" -ForegroundColor Cyan
Write-Host "  COMPLETE APP RESTART - Fix Cached Config" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# Step 1: Kill ALL running instances
Write-Host "`n[Step 1/6] Stopping all .NET processes..." -ForegroundColor Yellow
$processes = @('dotnet', 'w3wp', 'iisexpress', 'VBCSCompiler')
foreach ($proc in $processes) {
    Get-Process -Name $proc -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "  Killing $($_.Name) (PID: $($_.Id))" -ForegroundColor Gray
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
}
Write-Host "  Done!" -ForegroundColor Green

# Step 2: Navigate to solution directory
Write-Host "`n[Step 2/6] Navigating to solution directory..." -ForegroundColor Yellow
$solutionDir = "C:\Users\cbo\source\repos\speechtotext"
Set-Location $solutionDir
Write-Host "  Current directory: $(Get-Location)" -ForegroundColor Green

# Step 3: Delete bin and obj folders
Write-Host "`n[Step 3/6] Deleting bin and obj folders..." -ForegroundColor Yellow
$foldersToDelete = Get-ChildItem -Path . -Recurse -Include bin,obj -Directory -ErrorAction SilentlyContinue
foreach ($folder in $foldersToDelete) {
    Write-Host "  Deleting: $($folder.FullName)" -ForegroundColor Gray
    Remove-Item -Path $folder.FullName -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "  Done!" -ForegroundColor Green

# Step 4: Clean solution
Write-Host "`n[Step 4/6] Running dotnet clean..." -ForegroundColor Yellow
dotnet clean --verbosity quiet
Write-Host "  Done!" -ForegroundColor Green

# Step 5: Rebuild solution
Write-Host "`n[Step 5/6] Rebuilding solution..." -ForegroundColor Yellow
dotnet build --no-incremental
if ($LASTEXITCODE -ne 0) {
    Write-Host "  Build failed! Check errors above." -ForegroundColor Red
    exit 1
}
Write-Host "  Build successful!" -ForegroundColor Green

# Step 6: Show what to do next
Write-Host "`n[Step 6/6] Ready to start!" -ForegroundColor Yellow
Write-Host "`n==================================================" -ForegroundColor Cyan
Write-Host "  NEXT STEPS:" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "`n1. START THE APP:" -ForegroundColor White
Write-Host "   dotnet run --project speechtotext/speechtotext.csproj" -ForegroundColor Green
Write-Host "`n2. CLEAR BROWSER CACHE:" -ForegroundColor White
Write-Host "   Press: Ctrl + Shift + Delete" -ForegroundColor Green
Write-Host "   Select: 'Cached images and files'" -ForegroundColor Green
Write-Host "   Click: 'Clear data'" -ForegroundColor Green
Write-Host "`n   OR just press: Ctrl + Shift + R (hard refresh)" -ForegroundColor Green
Write-Host "`n3. TEST IN BROWSER:" -ForegroundColor White
Write-Host "   Navigate to: http://localhost:5000 (or your port)" -ForegroundColor Green
Write-Host "   Check helper text under file inputs" -ForegroundColor Green
Write-Host "`n==================================================" -ForegroundColor Cyan

# Optional: Start the app automatically
Write-Host "`nDo you want to start the app now? (Y/N): " -ForegroundColor Yellow -NoNewline
$response = Read-Host
if ($response -eq 'Y' -or $response -eq 'y') {
    Write-Host "`nStarting application..." -ForegroundColor Green
    Write-Host "Press Ctrl+C to stop`n" -ForegroundColor Gray
    dotnet run --project speechtotext/speechtotext.csproj
} else {
    Write-Host "`nRun this command when ready:" -ForegroundColor Yellow
    Write-Host "dotnet run --project speechtotext/speechtotext.csproj" -ForegroundColor Green
}
