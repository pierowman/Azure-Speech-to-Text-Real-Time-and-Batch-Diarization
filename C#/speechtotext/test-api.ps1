# Direct API Test Script
# Tests the transcription endpoint directly with katiesteve.wav

param(
    [string]$Port = "32769"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Direct API Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$audioFile = "katiesteve.wav"
$audioPath = Join-Path $PSScriptRoot $audioFile

# Check if file exists
if (-not (Test-Path $audioPath)) {
    Write-Host "? Audio file not found: $audioPath" -ForegroundColor Red
    exit
}

Write-Host "? Audio file found: $audioFile" -ForegroundColor Green
$fileInfo = Get-Item $audioPath
Write-Host "  Size: $([math]::Round($fileInfo.Length / 1KB, 2)) KB" -ForegroundColor Gray
Write-Host ""

# Disable certificate validation for localhost testing
Add-Type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPolicy : ICertificatePolicy {
    public bool CheckValidationResult(
        ServicePoint srvPoint, X509Certificate certificate,
        WebRequest request, int certificateProblem) {
        return true;
    }
}
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

$baseUrl = "https://localhost:$Port"
Write-Host "Testing against: $baseUrl" -ForegroundColor Cyan
Write-Host ""

try {
    Write-Host "Testing connection to application..." -ForegroundColor Yellow
    
    # Test if app is running
    try {
        $response = Invoke-WebRequest -Uri $baseUrl -UseBasicParsing -TimeoutSec 5
        Write-Host "? Application is running at $baseUrl" -ForegroundColor Green
    }
    catch {
        Write-Host "? Cannot connect to application at $baseUrl" -ForegroundColor Red
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
        Write-Host "Make sure the application is running and accessible at the correct port." -ForegroundColor Yellow
        throw
    }
    
    Write-Host ""
    Write-Host "Uploading audio file and requesting transcription..." -ForegroundColor Cyan
    Write-Host "This may take 30-60 seconds depending on audio length..." -ForegroundColor Yellow
    Write-Host ""
    
    # Create multipart form data
    $uri = "$baseUrl/Home/UploadAndTranscribe"
    $boundary = [System.Guid]::NewGuid().ToString()
    $LF = "`r`n"
    
    $fileBin = [System.IO.File]::ReadAllBytes($audioPath)
    $enc = [System.Text.Encoding]::GetEncoding("iso-8859-1")
    
    $bodyLines = (
        "--$boundary",
        "Content-Disposition: form-data; name=`"audioFile`"; filename=`"$audioFile`"",
        "Content-Type: audio/wav$LF",
        $enc.GetString($fileBin),
        "--$boundary--$LF"
    ) -join $LF
    
    $bodyBytes = $enc.GetBytes($bodyLines)
    
    # Create web request
    $webRequest = [System.Net.WebRequest]::Create($uri)
    $webRequest.Method = "POST"
    $webRequest.ContentType = "multipart/form-data; boundary=$boundary"
    $webRequest.ContentLength = $bodyBytes.Length
    $webRequest.Timeout = 300000 # 5 minutes
    
    Write-Host "Sending request to: $uri" -ForegroundColor Gray
    
    # Write request body
    $requestStream = $webRequest.GetRequestStream()
    $requestStream.Write($bodyBytes, 0, $bodyBytes.Length)
    $requestStream.Close()
    
    Write-Host "Waiting for response..." -ForegroundColor Yellow
    
    # Get response
    $response = $webRequest.GetResponse()
    $responseStream = $response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($responseStream)
    $responseText = $reader.ReadToEnd()
    $reader.Close()
    $responseStream.Close()
    $response.Close()
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "RESPONSE RECEIVED" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    # Parse JSON response
    $result = $responseText | ConvertFrom-Json
    
    Write-Host "Success: $($result.success)" -ForegroundColor $(if ($result.success) { "Green" } else { "Red" })
    Write-Host "Message: $($result.message)" -ForegroundColor White
    Write-Host ""
    
    if ($result.success) {
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "TRANSCRIPTION RESULTS" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host ""
        
        if ($result.segments -and $result.segments.Count -gt 0) {
            Write-Host "Total Segments: $($result.segments.Count)" -ForegroundColor Cyan
            Write-Host ""
            
            foreach ($segment in $result.segments) {
                Write-Host "[$($segment.speaker)]" -ForegroundColor Yellow -NoNewline
                Write-Host " @ $($segment.formattedStartTime)" -ForegroundColor Gray
                Write-Host "  $($segment.text)" -ForegroundColor White
                Write-Host ""
            }
            
            Write-Host "========================================" -ForegroundColor Cyan
            Write-Host "FULL TRANSCRIPT" -ForegroundColor Green
            Write-Host "========================================" -ForegroundColor Cyan
            Write-Host ""
            Write-Host $result.fullTranscript -ForegroundColor White
            
            Write-Host ""
            Write-Host "? TEST PASSED: Results received and displayed!" -ForegroundColor Green
            Write-Host ""
            Write-Host "Summary:" -ForegroundColor Cyan
            Write-Host "  - Speakers detected: $($result.segments.speaker | Select-Object -Unique | Measure-Object).Count" -ForegroundColor White
            Write-Host "  - Total segments: $($result.segments.Count)" -ForegroundColor White
            Write-Host "  - Audio file: $audioFile" -ForegroundColor White
            Write-Host "  - Endpoint: $baseUrl" -ForegroundColor White
        }
        else {
            Write-Host "? WARNING: No segments detected in audio" -ForegroundColor Yellow
            Write-Host "The transcription completed but no speech was recognized." -ForegroundColor Yellow
            Write-Host ""
            Write-Host "This could mean:" -ForegroundColor Yellow
            Write-Host "  - The audio is too short" -ForegroundColor Gray
            Write-Host "  - No clear speech was detected" -ForegroundColor Gray
            Write-Host "  - Audio quality issues" -ForegroundColor Gray
            Write-Host "  - Diarization didn't identify multiple speakers" -ForegroundColor Gray
        }
    }
    else {
        Write-Host "? TEST FAILED: $($result.message)" -ForegroundColor Red
        Write-Host ""
        Write-Host "Check the application logs for more details." -ForegroundColor Yellow
    }
}
catch {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "ERROR OCCURRED" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host ""
    Write-Host $_.Exception.Message -ForegroundColor Red
    
    if ($_.Exception.InnerException) {
        Write-Host ""
        Write-Host "Inner Exception:" -ForegroundColor Yellow
        Write-Host $_.Exception.InnerException.Message -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  1. Verify the application is running at $baseUrl" -ForegroundColor Gray
    Write-Host "  2. Check the console output for errors" -ForegroundColor Gray
    Write-Host "  3. Verify Azure credentials are correct" -ForegroundColor Gray
    Write-Host "  4. Ensure katiesteve.wav is a valid audio file" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Test completed." -ForegroundColor Cyan
Write-Host ""
Read-Host "Press Enter to exit"
