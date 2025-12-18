# Batch Transcription Diagnostics Script
# Run this in PowerShell to check Azure Speech Service batch transcription API

param(
    [Parameter(Mandatory=$true)]
    [string]$SubscriptionKey,
    
    [Parameter(Mandatory=$true)]
    [string]$Region,
    
    [Parameter(Mandatory=$true)]
    [string]$JobId
)

Write-Host "=== Azure Speech Service Batch Transcription Diagnostics ===" -ForegroundColor Cyan
Write-Host ""

$baseUrl = "https://$Region.api.cognitive.microsoft.com/speechtotext/v3.1"
$headers = @{
    "Ocp-Apim-Subscription-Key" = $SubscriptionKey
}

# Step 1: Get Job Details
Write-Host "Step 1: Fetching job details..." -ForegroundColor Yellow
Write-Host "URL: $baseUrl/transcriptions/$JobId"
try {
    $jobResponse = Invoke-RestMethod -Uri "$baseUrl/transcriptions/$JobId" -Headers $headers -Method Get
    Write-Host "? Job found" -ForegroundColor Green
    Write-Host "  Display Name: $($jobResponse.displayName)" -ForegroundColor Gray
    Write-Host "  Status: $($jobResponse.status)" -ForegroundColor Gray
    Write-Host "  Created: $($jobResponse.createdDateTime)" -ForegroundColor Gray
    
    if ($jobResponse.status -ne "Succeeded") {
        Write-Host "? WARNING: Job status is '$($jobResponse.status)', not 'Succeeded'" -ForegroundColor Red
        Write-Host "  Transcription results may not be available yet." -ForegroundColor Red
    }
    
    if ($jobResponse.properties.error) {
        Write-Host "? ERROR in job properties:" -ForegroundColor Red
        Write-Host "  $($jobResponse.properties.error)" -ForegroundColor Red
    }
}
catch {
    Write-Host "? Failed to fetch job details" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Step 2: Get Files List
Write-Host "Step 2: Fetching files list..." -ForegroundColor Yellow
Write-Host "URL: $baseUrl/transcriptions/$JobId/files"
try {
    $filesResponse = Invoke-RestMethod -Uri "$baseUrl/transcriptions/$JobId/files" -Headers $headers -Method Get
    $fileCount = $filesResponse.values.Count
    Write-Host "? Found $fileCount file(s)" -ForegroundColor Green
    
    $transcriptionFile = $null
    foreach ($file in $filesResponse.values) {
        Write-Host "  File:" -ForegroundColor Gray
        Write-Host "    Kind: $($file.kind)" -ForegroundColor Gray
        Write-Host "    Name: $($file.name)" -ForegroundColor Gray
        
        if ($file.kind -eq "Transcription") {
            $transcriptionFile = $file
            Write-Host "    ? This is the transcription results file!" -ForegroundColor Green
        }
    }
    
    if (-not $transcriptionFile) {
        Write-Host "? No transcription file found (kind='Transcription')" -ForegroundColor Red
        Write-Host "  This job may have failed or not completed properly." -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host "? Failed to fetch files list" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Step 3: Download Transcription Results
Write-Host "Step 3: Downloading transcription results..." -ForegroundColor Yellow
$transcriptionUrl = $transcriptionFile.links.contentUrl
Write-Host "URL: $transcriptionUrl"
try {
    $transcriptionResponse = Invoke-RestMethod -Uri $transcriptionUrl -Method Get
    Write-Host "? Downloaded transcription JSON" -ForegroundColor Green
    
    $jsonSize = ($transcriptionResponse | ConvertTo-Json -Depth 100).Length
    Write-Host "  JSON Size: $jsonSize bytes" -ForegroundColor Gray
}
catch {
    Write-Host "? Failed to download transcription results" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Step 4: Analyze JSON Structure
Write-Host "Step 4: Analyzing JSON structure..." -ForegroundColor Yellow

$hasRecognizedPhrases = $null -ne $transcriptionResponse.recognizedPhrases
$hasCombinedPhrases = $null -ne $transcriptionResponse.combinedRecognizedPhrases

if ($hasRecognizedPhrases) {
    $phraseCount = $transcriptionResponse.recognizedPhrases.Count
    Write-Host "? Found 'recognizedPhrases' array with $phraseCount elements" -ForegroundColor Green
    
    if ($phraseCount -eq 0) {
        Write-Host "? WARNING: Array is empty - no speech was detected in audio" -ForegroundColor Red
    }
    else {
        # Analyze first phrase
        $firstPhrase = $transcriptionResponse.recognizedPhrases[0]
        Write-Host "  First phrase details:" -ForegroundColor Gray
        Write-Host "    Channel: $($firstPhrase.channel)" -ForegroundColor Gray
        Write-Host "    Offset: $($firstPhrase.offsetInTicks) ticks" -ForegroundColor Gray
        Write-Host "    Duration: $($firstPhrase.durationInTicks) ticks" -ForegroundColor Gray
        
        if ($firstPhrase.nBest -and $firstPhrase.nBest.Count -gt 0) {
            $text = $firstPhrase.nBest[0].display
            Write-Host "    Text: '$text'" -ForegroundColor Gray
            Write-Host "? Phrase has valid text content" -ForegroundColor Green
        }
        else {
            Write-Host "? WARNING: Phrase has no 'nBest' array or it's empty" -ForegroundColor Red
        }
        
        # Count unique channels
        $channels = $transcriptionResponse.recognizedPhrases | ForEach-Object { $_.channel } | Select-Object -Unique
        Write-Host "  Unique channels found: $($channels.Count)" -ForegroundColor Gray
        Write-Host "  (This typically represents the number of audio files)" -ForegroundColor Gray
    }
}
elseif ($hasCombinedPhrases) {
    $phraseCount = $transcriptionResponse.combinedRecognizedPhrases.Count
    Write-Host "? Found 'combinedRecognizedPhrases' array with $phraseCount elements" -ForegroundColor Green
    
    if ($phraseCount -eq 0) {
        Write-Host "? WARNING: Array is empty - no speech was detected in audio" -ForegroundColor Red
    }
    else {
        $firstPhrase = $transcriptionResponse.combinedRecognizedPhrases[0]
        Write-Host "  First phrase details:" -ForegroundColor Gray
        Write-Host "    Channel: $($firstPhrase.channel)" -ForegroundColor Gray
        $text = $firstPhrase.display
        Write-Host "    Text: '$text'" -ForegroundColor Gray
    }
}
else {
    Write-Host "? Neither 'recognizedPhrases' nor 'combinedRecognizedPhrases' found" -ForegroundColor Red
    Write-Host "  Available properties:" -ForegroundColor Gray
    $transcriptionResponse.PSObject.Properties | ForEach-Object {
        Write-Host "    - $($_.Name)" -ForegroundColor Gray
    }
}

Write-Host ""

# Summary
Write-Host "=== DIAGNOSIS SUMMARY ===" -ForegroundColor Cyan
Write-Host ""

if ($jobResponse.status -ne "Succeeded") {
    Write-Host "? Job has not succeeded yet (Status: $($jobResponse.status))" -ForegroundColor Red
    Write-Host "   ? Wait for job to complete before viewing results" -ForegroundColor Yellow
}
elseif (-not $hasRecognizedPhrases -and -not $hasCombinedPhrases) {
    Write-Host "? JSON structure is unexpected - missing recognized phrases" -ForegroundColor Red
    Write-Host "   ? This may be an API version mismatch or Azure issue" -ForegroundColor Yellow
}
elseif ($hasRecognizedPhrases -and $transcriptionResponse.recognizedPhrases.Count -eq 0) {
    Write-Host "? No speech detected in audio file" -ForegroundColor Red
    Write-Host "   ? Check audio file:" -ForegroundColor Yellow
    Write-Host "      - Does it have audible speech?" -ForegroundColor Yellow
    Write-Host "      - Is the language setting correct?" -ForegroundColor Yellow
    Write-Host "      - Is audio quality sufficient?" -ForegroundColor Yellow
}
elseif ($hasCombinedPhrases -and $transcriptionResponse.combinedRecognizedPhrases.Count -eq 0) {
    Write-Host "? No speech detected in audio file" -ForegroundColor Red
    Write-Host "   ? Check audio file (see above)" -ForegroundColor Yellow
}
else {
    Write-Host "? Transcription results look good!" -ForegroundColor Green
    $totalPhrases = if ($hasRecognizedPhrases) { $transcriptionResponse.recognizedPhrases.Count } else { $transcriptionResponse.combinedRecognizedPhrases.Count }
    Write-Host "   ? $totalPhrases phrases should appear in the application" -ForegroundColor Green
    Write-Host ""
    Write-Host "If you're seeing zero segments in the app:" -ForegroundColor Yellow
    Write-Host "  1. Check application logs for parsing errors" -ForegroundColor Yellow
    Write-Host "  2. Enable Debug logging in appsettings.json" -ForegroundColor Yellow
    Write-Host "  3. Review ZERO_SEGMENTS_TROUBLESHOOTING.md" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== END DIAGNOSTICS ===" -ForegroundColor Cyan

# Optionally save JSON to file
$saveJson = Read-Host "Save full transcription JSON to file? (y/n)"
if ($saveJson -eq 'y') {
    $outputPath = "transcription_$JobId.json"
    $transcriptionResponse | ConvertTo-Json -Depth 100 | Out-File -FilePath $outputPath -Encoding UTF8
    Write-Host "? Saved to: $outputPath" -ForegroundColor Green
}
