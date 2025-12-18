# Batch Transcription Storage Diagnostic

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "     BATCH TRANSCRIPTION STORAGE REQUIREMENTS" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Check configuration
$devConfig = Get-Content "speechtotext/appsettings.Development.json" | ConvertFrom-Json

Write-Host "[1] Your Configuration:" -ForegroundColor Yellow
Write-Host ""
Write-Host "  EnableBlobStorage: " -NoNewline
if ($devConfig.AzureStorage.EnableBlobStorage) {
    Write-Host "TRUE ?" -ForegroundColor Green
    $storageEnabled = $true
} else {
    Write-Host "FALSE ?" -ForegroundColor Red
    $storageEnabled = $false
}

Write-Host "  StorageAccountName: " -NoNewline
$storageAccount = $devConfig.AzureStorage.StorageAccountName
if ($storageAccount) {
    Write-Host "$storageAccount ?" -ForegroundColor Green
} else {
    Write-Host "<EMPTY> ?" -ForegroundColor Red
}

Write-Host "  Has Credentials: " -NoNewline
$hasCreds = $devConfig.AzureStorage.TenantId -and $devConfig.AzureStorage.ClientId -and $devConfig.AzureStorage.ClientSecret
if ($hasCreds) {
    Write-Host "YES ?" -ForegroundColor Green
} else {
    Write-Host "NO ?" -ForegroundColor Red
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "[2] What This Means:" -ForegroundColor Yellow
Write-Host ""

if ($storageEnabled -and $storageAccount -and $hasCreds) {
    Write-Host "? BLOB STORAGE IS CONFIGURED" -ForegroundColor Green
    Write-Host ""
    Write-Host "Your app CAN:" -ForegroundColor White
    Write-Host "  ? Upload files to Blob Storage" -ForegroundColor Green
    Write-Host "  ? Submit real batch transcription jobs" -ForegroundColor Green
    Write-Host "  ? Get actual transcription results" -ForegroundColor Green
    Write-Host ""
    Write-Host "But you STILL NEED:" -ForegroundColor Yellow
    Write-Host "  ??  Azure Speech Service must have 'Storage Blob Data Reader' role" -ForegroundColor Yellow
    Write-Host "      on storage account: $storageAccount" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Without this permission:" -ForegroundColor Yellow
    Write-Host "  - Jobs will be created" -ForegroundColor Gray
    Write-Host "  - Files will be uploaded" -ForegroundColor Gray
    Write-Host "  - But jobs will FAIL with 'Access Denied' error" -ForegroundColor Gray
    
} else {
    Write-Host "? BLOB STORAGE NOT CONFIGURED" -ForegroundColor Red
    Write-Host ""
    Write-Host "Your app WILL:" -ForegroundColor White
    Write-Host "  ??  Create PLACEHOLDER jobs only" -ForegroundColor Yellow
    Write-Host "  ??  NOT upload files to Azure" -ForegroundColor Yellow
    Write-Host "  ??  NOT call Azure Speech Service" -ForegroundColor Yellow
    Write-Host "  ??  NOT perform actual transcription" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Jobs will show:" -ForegroundColor White
    Write-Host '  Error: "Placeholder job - Azure Blob Storage not configured"' -ForegroundColor Gray
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "     BATCH TRANSCRIPTION FLOW" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Required Flow for Batch Transcription:" -ForegroundColor White
Write-Host ""
Write-Host "  1. Your App uploads files to Blob Storage" -ForegroundColor Gray
Write-Host "     ?? Requires: Service Principal with Storage Blob Data Contributor" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  2. Your App sends Blob URLs to Azure Speech Service" -ForegroundColor Gray
Write-Host "     ?? Via: REST API (v3.1)" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  3. Azure Speech Service reads files from Blob Storage" -ForegroundColor Gray
Write-Host "     ?? Requires: Speech Service with Storage Blob Data Reader" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  4. Azure Speech Service processes files" -ForegroundColor Gray
Write-Host "     ?? Diarization, transcription, etc." -ForegroundColor DarkGray
Write-Host ""
Write-Host "  5. Azure Speech Service writes results to Blob Storage" -ForegroundColor Gray
Write-Host "     ?? Creates transcription JSON files" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  6. Your App retrieves results from Speech Service" -ForegroundColor Gray
Write-Host "     ?? Gets SAS URLs to download transcription files" -ForegroundColor DarkGray
Write-Host ""

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "     ALTERNATIVE: REAL-TIME TRANSCRIPTION" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Real-Time Transcription DOES NOT require Blob Storage:" -ForegroundColor Green
Write-Host ""
Write-Host "  ? Uses Speech SDK (not REST API)" -ForegroundColor Green
Write-Host "  ? Streams audio directly to Speech Service" -ForegroundColor Green
Write-Host "  ? Returns results immediately" -ForegroundColor Green
Write-Host "  ? No blob storage needed" -ForegroundColor Green
Write-Host ""

Write-Host "This is why Real-Time mode works even without Blob Storage!" -ForegroundColor Cyan

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "     VERIFICATION STEPS" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "To verify Batch Transcription is working:" -ForegroundColor White
Write-Host ""

if ($storageEnabled -and $storageAccount -and $hasCreds) {
    Write-Host "1. Check app startup logs for:" -ForegroundColor White
    Write-Host "   '? Azure Blob Storage is configured and enabled'" -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. Submit a batch job and watch logs for:" -ForegroundColor White
    Write-Host "   'Uploading file to blob storage...'" -ForegroundColor Gray
    Write-Host "   'File uploaded successfully...'" -ForegroundColor Gray
    Write-Host "   'Batch job created successfully...'" -ForegroundColor Gray
    Write-Host ""
    Write-Host "3. If job fails with 'Access Denied':" -ForegroundColor White
    Write-Host "   - Go to Azure Portal" -ForegroundColor Gray
    Write-Host "   - Navigate to Storage Account: $storageAccount" -ForegroundColor Gray
    Write-Host "   - Go to Access Control (IAM)" -ForegroundColor Gray
    Write-Host "   - Add Role Assignment:" -ForegroundColor Gray
    Write-Host "     Role: Storage Blob Data Reader" -ForegroundColor Gray
    Write-Host "     Assign to: Your Azure Speech Service resource" -ForegroundColor Gray
    
} else {
    Write-Host "1. Configure Blob Storage in appsettings.Development.json:" -ForegroundColor White
    Write-Host "   EnableBlobStorage: true" -ForegroundColor Gray
    Write-Host "   StorageAccountName: <your-storage-account>" -ForegroundColor Gray
    Write-Host "   TenantId: <your-tenant-id>" -ForegroundColor Gray
    Write-Host "   ClientId: <your-app-registration-id>" -ForegroundColor Gray
    Write-Host "   ClientSecret: <your-client-secret>" -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. See: AZURE_BLOB_STORAGE_SETUP.md for detailed steps" -ForegroundColor White
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press any key to continue..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
