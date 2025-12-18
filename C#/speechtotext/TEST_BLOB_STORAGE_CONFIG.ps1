# Test Blob Storage Configuration

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "     BLOB STORAGE CONFIGURATION TEST" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Check which configuration will be used
Write-Host "[1] Checking environment configuration..." -ForegroundColor Yellow
Write-Host ""

$devConfig = Get-Content "speechtotext/appsettings.Development.json" | ConvertFrom-Json
$prodConfig = Get-Content "speechtotext/appsettings.json" | ConvertFrom-Json

Write-Host "Development Config (appsettings.Development.json):" -ForegroundColor White
Write-Host "  EnableBlobStorage: " -NoNewline -ForegroundColor Gray
if ($devConfig.AzureStorage.EnableBlobStorage) {
    Write-Host "TRUE" -ForegroundColor Green
} else {
    Write-Host "FALSE" -ForegroundColor Red
}
Write-Host "  StorageAccountName: " -NoNewline -ForegroundColor Gray
if ($devConfig.AzureStorage.StorageAccountName) {
    Write-Host "$($devConfig.AzureStorage.StorageAccountName)" -ForegroundColor Green
} else {
    Write-Host "<EMPTY>" -ForegroundColor Red
}
Write-Host "  TenantId: " -NoNewline -ForegroundColor Gray
if ($devConfig.AzureStorage.TenantId) {
    Write-Host "$($devConfig.AzureStorage.TenantId.Substring(0, 8))..." -ForegroundColor Green
} else {
    Write-Host "<EMPTY>" -ForegroundColor Red
}
Write-Host "  ClientId: " -NoNewline -ForegroundColor Gray
if ($devConfig.AzureStorage.ClientId) {
    Write-Host "$($devConfig.AzureStorage.ClientId.Substring(0, 8))..." -ForegroundColor Green
} else {
    Write-Host "<EMPTY>" -ForegroundColor Red
}

Write-Host ""
Write-Host "Production Config (appsettings.json):" -ForegroundColor White
Write-Host "  EnableBlobStorage: " -NoNewline -ForegroundColor Gray
if ($prodConfig.AzureStorage.EnableBlobStorage) {
    Write-Host "TRUE" -ForegroundColor Green
} else {
    Write-Host "FALSE" -ForegroundColor Red
}
Write-Host "  StorageAccountName: " -NoNewline -ForegroundColor Gray
if ($prodConfig.AzureStorage.StorageAccountName) {
    Write-Host "$($prodConfig.AzureStorage.StorageAccountName)" -ForegroundColor Green
} else {
    Write-Host "<EMPTY>" -ForegroundColor Red
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

if ($devConfig.AzureStorage.EnableBlobStorage -and $devConfig.AzureStorage.StorageAccountName) {
    Write-Host "? DEVELOPMENT CONFIG IS CORRECT!" -ForegroundColor Green
    Write-Host "   Blob Storage is enabled and configured" -ForegroundColor Green
    Write-Host ""
    Write-Host "IMPORTANT:" -ForegroundColor Yellow
    Write-Host "  Old jobs (created before Blob Storage was configured)" -ForegroundColor Yellow
    Write-Host "  will ALWAYS show empty properties." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Create a NEW test job to verify Blob Storage is working!" -ForegroundColor Yellow
} else {
    Write-Host "? DEVELOPMENT CONFIG HAS ISSUES!" -ForegroundColor Red
    Write-Host "   Blob Storage is not properly configured" -ForegroundColor Red
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "     NEXT STEPS" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "1. Restart the application:" -ForegroundColor White
Write-Host "   - Ensure it's running in DEVELOPMENT mode" -ForegroundColor Gray
Write-Host "   - Press Shift+F5 then F5 in Visual Studio" -ForegroundColor Gray
Write-Host ""

Write-Host "2. Check startup logs for:" -ForegroundColor White
Write-Host "   Environment: Development" -ForegroundColor Gray
Write-Host "   EnableBlobStorage: True" -ForegroundColor Gray
Write-Host "   StorageAccountName: doctranslationstoragecbo" -ForegroundColor Gray
Write-Host ""

Write-Host "3. Create a NEW batch job:" -ForegroundColor White
Write-Host "   - Switch to Batch mode" -ForegroundColor Gray
Write-Host "   - Upload 1-2 small audio files" -ForegroundColor Gray
Write-Host "   - Submit the job" -ForegroundColor Gray
Write-Host "   - Wait for completion" -ForegroundColor Gray
Write-Host ""

Write-Host "4. Refresh jobs list and check NEW job:" -ForegroundColor White
Write-Host "   - Should have file names" -ForegroundColor Gray
Write-Host "   - Should have duration (when completed)" -ForegroundColor Gray
Write-Host "   - Should have success/failure counts" -ForegroundColor Gray
Write-Host ""

Write-Host "5. If NEW job still has empty properties:" -ForegroundColor White
Write-Host "   - Check Azure Portal permissions" -ForegroundColor Gray
Write-Host "   - Speech Service needs 'Storage Blob Data Reader' role" -ForegroundColor Gray
Write-Host "   - On storage account: doctranslationstoragecbo" -ForegroundColor Gray
Write-Host ""

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Note: OLD jobs cannot be fixed retroactively." -ForegroundColor Yellow
Write-Host "      They were created without Blob Storage and will" -ForegroundColor Yellow
Write-Host "      always show empty properties." -ForegroundColor Yellow
Write-Host ""

Write-Host "Press any key to continue..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
