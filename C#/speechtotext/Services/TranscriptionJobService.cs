using speechtotext.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace speechtotext.Services
{
    /// <summary>
    /// Service for managing Azure Speech Service batch transcription jobs via REST API
    /// </summary>
    public class TranscriptionJobService : ITranscriptionJobService
    {
        // Constants for magic numbers
        private const int LogPreviewMaxLength = 500;
        private const int JsonPreviewMaxLength = 2000;
        private const long TicksToSecondsConversionFactor = 10000000;
        
        private readonly string _subscriptionKey;
        private readonly string _region;
        private readonly ILogger<TranscriptionJobService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _baseUrl;

        public TranscriptionJobService(
            IConfiguration configuration, 
            ILogger<TranscriptionJobService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _subscriptionKey = configuration["AzureSpeech:SubscriptionKey"] 
                ?? throw new ArgumentException("Azure Speech subscription key not found in configuration");
            _region = configuration["AzureSpeech:Region"] 
                ?? throw new ArgumentException("Azure Speech region not found in configuration");
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _httpClient = httpClientFactory.CreateClient();
            _baseUrl = $"https://{_region}.api.cognitive.microsoft.com/speechtotext/v3.1";
            
            // Configure HTTP client
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<List<TranscriptionJob>> GetTranscriptionJobsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Fetching transcription jobs from Azure Speech Service");
                
                var response = await _httpClient.GetAsync($"{_baseUrl}/transcriptions", cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Failed to fetch transcription jobs. Status: {StatusCode}", response.StatusCode);
                    return new List<TranscriptionJob>();
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                using var jsonDoc = JsonDocument.Parse(content);
                
                var jobs = new List<TranscriptionJob>();
                
                if (jsonDoc.RootElement.TryGetProperty("values", out var valuesElement))
                {
                    _logger.LogInformation("Found {JobCount} jobs in list response", valuesElement.GetArrayLength());
                    
                    foreach (var jobElement in valuesElement.EnumerateArray())
                    {
                        var job = ParseTranscriptionJob(jobElement);
                        if (job != null)
                        {
                            jobs.Add(job);
                            
                            // NOTE: List endpoint often returns incomplete data
                            // Properties like contentUrls, duration, etc. may be null/empty
                            // To get complete job details, call GetTranscriptionJobAsync(jobId)
                            if (job.Status == "Succeeded" && job.TotalFileCount == 0)
                            {
                                _logger.LogDebug("Job {JobId} succeeded but has no file info in list response. " +
                                               "This is normal - file details are only in the detail/files endpoints.", 
                                               job.Id);
                            }
                        }
                    }
                }
                
                _logger.LogInformation("Retrieved {JobCount} transcription jobs from list endpoint", jobs.Count);
                _logger.LogDebug("Note: Job list may have incomplete data. Use GetTranscriptionJobAsync() or " +
                               "GetTranscriptionResultsAsync() for full job details including files and duration.");
                
                return jobs.OrderByDescending(j => j.CreatedDateTime).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching transcription jobs");
                return new List<TranscriptionJob>();
            }
        }

        public async Task<TranscriptionJob?> GetTranscriptionJobAsync(string jobId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Fetching transcription job: {JobId}", jobId);
                
                var response = await _httpClient.GetAsync($"{_baseUrl}/transcriptions/{jobId}", cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch transcription job {JobId}. Status: {StatusCode}", jobId, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                using var jobDoc = JsonDocument.Parse(content);
                
                return ParseTranscriptionJob(jobDoc.RootElement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching transcription job {JobId}", jobId);
                return null;
            }
        }

        public async Task<bool> CancelTranscriptionJobAsync(string jobId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Canceling transcription job: {JobId}", jobId);
                
                // To cancel, we delete the job
                var response = await _httpClient.DeleteAsync($"{_baseUrl}/transcriptions/{jobId}", cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully canceled/deleted transcription job {JobId}", jobId);
                    return true;
                }
                
                _logger.LogWarning("Failed to cancel transcription job {JobId}. Status: {StatusCode}", jobId, response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling transcription job {JobId}", jobId);
                return false;
            }
        }

        public async Task<bool> DeleteTranscriptionJobAsync(string jobId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Deleting transcription job: {JobId}", jobId);
                
                var response = await _httpClient.DeleteAsync($"{_baseUrl}/transcriptions/{jobId}", cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully deleted transcription job {JobId}", jobId);
                    return true;
                }
                
                _logger.LogWarning("Failed to delete transcription job {JobId}. Status: {StatusCode}", jobId, response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting transcription job {JobId}", jobId);
                return false;
            }
        }

        private TranscriptionJob? ParseTranscriptionJob(JsonElement jobElement)
        {
            try
            {
                var job = new TranscriptionJob();
                
                // Log the raw JSON for debugging (only if debug logging is enabled)
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    var rawJson = jobElement.GetRawText();
                    var preview = rawJson.Length > LogPreviewMaxLength 
                        ? rawJson[..LogPreviewMaxLength] + "..." 
                        : rawJson;
                    _logger.LogDebug("Parsing job JSON: {JsonPreview}", preview);
                }
                
                // Safely get string properties
                job.Id = GetStringProperty(jobElement, "self")?.Split('/').LastOrDefault() ?? "";
                job.DisplayName = GetStringProperty(jobElement, "displayName") ?? "Unnamed Job";
                job.Status = GetStringProperty(jobElement, "status") ?? "Unknown";
                job.Locale = GetStringProperty(jobElement, "locale") ?? "en-US";  // Default to en-US if not specified
                
                // Parse dates
                if (DateTime.TryParse(GetStringProperty(jobElement, "createdDateTime"), out var createdDate))
                {
                    job.CreatedDateTime = createdDate;
                }
                
                if (DateTime.TryParse(GetStringProperty(jobElement, "lastActionDateTime"), out var lastActionDate))
                {
                    job.LastActionDateTime = lastActionDate;
                }
                
                // Parse links - check if it's an object first
                if (TryGetObjectProperty(jobElement, "links", out var linksElement))
                {
                    if (linksElement.TryGetProperty("files", out var filesLinkElement) && 
                        filesLinkElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        job.ResultsUrl = filesLinkElement.GetString();
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("Job {JobId} has files URL: {ResultsUrl}", job.Id, job.ResultsUrl);
                        }
                    }
                }
                
                // Parse properties - with comprehensive defensive handling
                if (TryGetObjectProperty(jobElement, "properties", out var propsElement))
                {
                    var props = new TranscriptionProperties();
                    
                    // Duration
                    if (TryGetNumberProperty(propsElement, "durationInTicks", out var durationVal))
                    {
                        props.Duration = durationVal;
                    }
                    
                    // Succeeded count
                    if (TryGetNumberProperty(propsElement, "succeededTranscriptionsCount", out var succeededVal))
                    {
                        props.SucceededCount = (int?)succeededVal;
                    }
                    
                    // Failed count
                    if (TryGetNumberProperty(propsElement, "failedTranscriptionsCount", out var failedVal))
                    {
                        props.FailedCount = (int?)failedVal;
                    }
                    
                    // Error - handle all types
                    job.Error = ParseErrorProperty(propsElement, job.Id);
                    if (!string.IsNullOrEmpty(job.Error))
                    {
                        props.ErrorMessage = job.Error;
                    }
                    
                    job.Properties = props;
                }
                
                // Content URLs
                if (TryGetArrayProperty(jobElement, "contentUrls", out var contentUrlsElement))
                {
                    foreach (var urlElement in contentUrlsElement.EnumerateArray())
                    {
                        var url = urlElement.GetString();
                        if (!string.IsNullOrEmpty(url))
                        {
                            try
                            {
                                var uri = new Uri(url);
                                var fileName = uri.AbsolutePath.Split('/').LastOrDefault();
                                if (!string.IsNullOrEmpty(fileName))
                                {
                                    fileName = Uri.UnescapeDataString(fileName);
                                    job.Files.Add(fileName);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to parse content URL");
                                job.Files.Add(url.Split('/').LastOrDefault() ?? url);
                            }
                        }
                    }
                }
                
                // Try alternate file sources
                if (job.Files.Count == 0)
                {
                    if (TryGetObjectProperty(jobElement, "properties", out var propsElem2))
                    {
                        if (TryGetArrayProperty(propsElem2, "channels", out var channelsElement))
                        {
                            foreach (var channel in channelsElement.EnumerateArray())
                            {
                                var label = GetStringProperty(channel, "label");
                                if (!string.IsNullOrEmpty(label))
                                {
                                    job.Files.Add(label);
                                }
                            }
                        }
                    }
                }

                if (job.Files.Count == 0 && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("No file information available for job {JobId}", job.Id);
                }
                
                return job;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing transcription job");
                return null;
            }
        }
        
        /// <summary>
        /// Safely get a string property from a JSON element
        /// </summary>
        private string? GetStringProperty(JsonElement element, string propertyName)
        {
            try
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }
                
                if (element.TryGetProperty(propertyName, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.String)
                    {
                        return prop.GetString();
                    }
                    else if (prop.ValueKind == JsonValueKind.Number)
                    {
                        return prop.GetRawText();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get string property '{propertyName}'");
            }
            return null;
        }
        
        /// <summary>
        /// Safely try to get an object property
        /// </summary>
        private bool TryGetObjectProperty(JsonElement element, string propertyName, out JsonElement result)
        {
            result = default;
            try
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }
                
                if (element.TryGetProperty(propertyName, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.Object)
                    {
                        result = prop;
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning($"Property '{propertyName}' is {prop.ValueKind}, expected Object");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get object property '{propertyName}'");
            }
            return false;
        }
        
        /// <summary>
        /// Safely try to get an array property
        /// </summary>
        private bool TryGetArrayProperty(JsonElement element, string propertyName, out JsonElement result)
        {
            result = default;
            try
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }
                
                if (element.TryGetProperty(propertyName, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.Array)
                    {
                        result = prop;
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning($"Property '{propertyName}' is {prop.ValueKind}, expected Array");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get array property '{propertyName}'");
            }
            return false;
        }
        
        /// <summary>
        /// Safely try to get a number property - handles both camelCase and PascalCase
        /// </summary>
        private bool TryGetNumberProperty(JsonElement element, string propertyName, out long? result)
        {
            result = null;
            try
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }
                
                // Try all case variations - don't stop on first match with wrong type
                var alternateNames = new[]
                {
                    propertyName,                                          // offsetInTicks
                    char.ToUpper(propertyName[0]) + propertyName.Substring(1),  // OffsetInTicks
                    char.ToLower(propertyName[0]) + propertyName.Substring(1),  // (back to original if was camelCase)
                    propertyName.ToLower(),                                // offsetinticks
                    propertyName.ToUpper()                                 // OFFSETINTICKS
                };
                
                foreach (var altName in alternateNames.Distinct())
                {
                    if (element.TryGetProperty(altName, out var prop))
                    {
                        if (prop.ValueKind == JsonValueKind.Number)
                        {
                            // Try Int64 first
                            if (prop.TryGetInt64(out var longVal))
                            {
                                result = longVal;
                                if (altName != propertyName)
                                {
                                    _logger.LogDebug($"Found property '{altName}' (looking for '{propertyName}'), value={longVal}");
                                }
                                return true;
                            }
                            // Try Int32
                            else if (prop.TryGetInt32(out var intVal))
                            {
                                result = intVal;
                                if (altName != propertyName)
                                {
                                    _logger.LogDebug($"Found property '{altName}' (looking for '{propertyName}'), value={intVal}");
                                }
                                return true;
                            }
                            // NEW: Try Double (for values like 800000.0)
                            else if (prop.TryGetDouble(out var doubleVal))
                            {
                                result = (long)doubleVal;  // Convert to long
                                if (altName != propertyName)
                                {
                                    _logger.LogDebug($"Found property '{altName}' (looking for '{propertyName}'), value={doubleVal} (converted to {result})");
                                }
                                else
                                {
                                    _logger.LogDebug($"Found property '{propertyName}' as double: {doubleVal}, converted to long: {result}");
                                }
                                return true;
                            }
                            // NEW: Try Decimal as last resort
                            else if (prop.TryGetDecimal(out var decimalVal))
                            {
                                result = (long)decimalVal;  // Convert to long
                                if (altName != propertyName)
                                {
                                    _logger.LogDebug($"Found property '{altName}' (looking for '{propertyName}'), value={decimalVal} (converted to {result})");
                                }
                                else
                                {
                                    _logger.LogDebug($"Found property '{propertyName}' as decimal: {decimalVal}, converted to long: {result}");
                                }
                                return true;
                            }
                        }
                        // If found but wrong type, continue trying other case variations
                    }
                }
                
                // None of the variations worked
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get number property '{propertyName}'");
                return false;
            }
        }
        
        /// <summary>
        /// Parse error property which can be Object, String, Number, or Null
        /// </summary>
        private string? ParseErrorProperty(JsonElement propsElement, string jobId)
        {
            try
            {
                if (propsElement.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }
                
                if (propsElement.TryGetProperty("error", out var errorElement))
                {
                    switch (errorElement.ValueKind)
                    {
                        case JsonValueKind.Object:
                            // Error object with message and code
                            var message = GetStringProperty(errorElement, "message");
                            var code = GetStringProperty(errorElement, "code");
                            if (!string.IsNullOrEmpty(message))
                            {
                                return message;
                            }
                            else if (!string.IsNullOrEmpty(code))
                            {
                                return $"Error code: {code}";
                            }
                            break;
                            
                        case JsonValueKind.String:
                            return errorElement.GetString();
                            
                        case JsonValueKind.Number:
                            return $"Error code: {errorElement.GetRawText()}";
                            
                        case JsonValueKind.Null:
                        case JsonValueKind.Undefined:
                            return null;
                            
                        default:
                            _logger.LogWarning($"Unexpected error format for job {jobId}: {errorElement.ValueKind}");
                            return $"Error: {errorElement.GetRawText()}";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to parse error property for job {jobId}");
            }
            return null;
        }
        
        public async Task<BatchTranscriptionResult?> GetTranscriptionResultsAsync(string jobId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Fetching transcription results for job: {JobId}", jobId);
                
                // First get the job to get the files URL
                var job = await GetTranscriptionJobAsync(jobId, cancellationToken);
                if (job == null)
                {
                    _logger.LogWarning("Job not found: {JobId}", jobId);
                    return new BatchTranscriptionResult
                    {
                        Success = false,
                        Message = "Job not found",
                        JobId = jobId
                    };
                }
                
                if (job.Status != "Succeeded")
                {
                    _logger.LogWarning("Job {JobId} is not completed. Status: {JobStatus}", jobId, job.Status);
                    return new BatchTranscriptionResult
                    {
                        Success = false,
                        Message = $"Job is not completed. Current status: {job.Status}",
                        JobId = jobId,
                        DisplayName = job.DisplayName
                    };
                }
                
                // Get the files URL from the job
                var response = await _httpClient.GetAsync($"{_baseUrl}/transcriptions/{jobId}/files", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Failed to fetch files for job {JobId}. Status: {StatusCode}, Content: {ErrorContent}", jobId, response.StatusCode, errorContent);
                    return new BatchTranscriptionResult
                    {
                        Success = false,
                        Message = "Failed to fetch transcription files",
                        JobId = jobId,
                        DisplayName = job.DisplayName
                    };
                }
                
                var filesContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Files response for job {JobId}: {FilesContent}", jobId, filesContent);
                
                using var filesDoc = JsonDocument.Parse(filesContent);
                
                // Find ALL transcription files (may be multiple - one per audio file)
                var transcriptionUrls = new List<(string url, string name)>();
                var allFiles = new List<(string kind, string name, string? url)>();
                
                if (filesDoc.RootElement.TryGetProperty("values", out var filesArray))
                {
                    _logger.LogInformation("Found {FileCount} files for job {JobId}", filesArray.GetArrayLength(), jobId);
                    
                    foreach (var file in filesArray.EnumerateArray())
                    {
                        var kind = GetStringProperty(file, "kind");
                        var name = GetStringProperty(file, "name");
                        string? contentUrl = null;
                        
                        if (TryGetObjectProperty(file, "links", out var links))
                        {
                            contentUrl = GetStringProperty(links, "contentUrl");
                        }
                        
                        allFiles.Add((kind ?? "Unknown", name ?? "Unknown", contentUrl));
                        var hasUrl = !string.IsNullOrEmpty(contentUrl);
                        _logger.LogInformation("File {FileIndex}: Kind='{Kind}', Name='{Name}', HasURL={HasURL}", allFiles.Count, kind, name, hasUrl);
                        
                        // Collect ALL Transcription files (not just one!)
                        if (kind == "Transcription" && !string.IsNullOrEmpty(contentUrl))
                        {
                            transcriptionUrls.Add((contentUrl, name ?? $"File {transcriptionUrls.Count + 1}"));
                            _logger.LogInformation("? Found transcription file #{TranscriptionFileCount}: '{Name}'", transcriptionUrls.Count, name);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("No 'values' array found in files response for job {JobId}", jobId);
                }
                
                // Log analysis of what we found
                _logger.LogInformation("?? TRANSCRIPTION FILES ANALYSIS:");
                _logger.LogInformation("   Total files from Azure: {TotalFiles}", allFiles.Count);
                _logger.LogInformation("   Transcription files found: {TranscriptionFileCount}", transcriptionUrls.Count);
                
                // If no transcription files found, log all files we did find
                if (transcriptionUrls.Count == 0)
                {
                    _logger.LogWarning("No files with kind='Transcription' found for job {JobId}", jobId);
                    _logger.LogWarning("Available files:");
                    foreach (var (kind, name, url) in allFiles)
                    {
                        var urlStatus = string.IsNullOrEmpty(url) ? "NONE" : "present";
                        _logger.LogWarning("  - Kind: '{Kind}', Name: '{Name}', URL: {UrlStatus}", kind, name, urlStatus);
                    }
                    
                    // Check if there's an error or report file that might explain why
                    foreach (var (kind, name, url) in allFiles)
                    {
                        if ((kind == "Report" || kind == "Error" || kind == "TranscriptionReport") && !string.IsNullOrEmpty(url))
                        {
                            _logger.LogInformation("Found potential error/report file: {Kind}, attempting to download for details...", kind);
                            try
                            {
                                // Create a new HttpClient without subscription key for SAS URL
                                var sasClient = _httpClientFactory.CreateClient();
                                var errorFileResponse = await sasClient.GetAsync(url, cancellationToken);
                                if (errorFileResponse.IsSuccessStatusCode)
                                {
                                    var errorContent = await errorFileResponse.Content.ReadAsStringAsync(cancellationToken);
                                    _logger.LogWarning("Content of {Kind} file '{Name}': {ErrorContent}", kind, name, errorContent);
                                }
                                else
                                {
                                    _logger.LogWarning("Failed to download {Kind} file. Status: {StatusCode}", kind, errorFileResponse.StatusCode);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to download {Kind} file", kind);
                            }
                        }
                    }
                    
                    return new BatchTranscriptionResult
                    {
                        Success = false,
                        Message = $"No transcription results available. Found {allFiles.Count} files but none with kind='Transcription'. Check logs for details.",
                        JobId = jobId,
                        DisplayName = job.DisplayName
                    };
                }
                
                // Download and parse ALL transcription files
                var allSegments = new List<SpeakerSegment>();
                var fullTranscriptBuilder = new System.Text.StringBuilder();
                var allSpeakers = new HashSet<string>();
                var fileResults = new List<FileTranscriptionInfo>();
                string? combinedRawJsonData = null;
                int globalLineNumber = 1;
                
                _logger.LogInformation("Downloading and parsing {TranscriptionFileCount} transcription file(s)...", transcriptionUrls.Count);
                
                for (int fileIndex = 0; fileIndex < transcriptionUrls.Count; fileIndex++)
                {
                    var (transcriptionUrl, fileName) = transcriptionUrls[fileIndex];
                    
                    _logger.LogInformation("?? Downloading transcription file {FileIndex}/{TranscriptionFileCount}: {FileName}", fileIndex + 1, transcriptionUrls.Count, fileName);
                    _logger.LogDebug("   URL: {TranscriptionUrl}", transcriptionUrl);
                    
                    // Download the transcription file using SAS URL
                    // Create a client without the subscription key header for SAS URLs
                    var sasClient = _httpClientFactory.CreateClient();
                    var transcriptionResponse = await sasClient.GetAsync(transcriptionUrl, cancellationToken);
                    
                    if (!transcriptionResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to download transcription file {FileIndex}. Status: {StatusCode}", fileIndex + 1, transcriptionResponse.StatusCode);
                        var errorContent = await transcriptionResponse.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogDebug("Download error content: {ErrorContent}", errorContent);
                        continue; // Skip this file, try next one
                    }
                    
                    var transcriptionContent = await transcriptionResponse.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogInformation("? Downloaded file {FileIndex + 1}, size: {TranscriptionContentLength} bytes", fileIndex, transcriptionContent?.Length ?? 0);
                    
                    // Check if the content is empty or too small
                    if (string.IsNullOrWhiteSpace(transcriptionContent) || transcriptionContent.Length < 10)
                    {
                        _logger.LogWarning("Transcription file {FileIndex} is empty or too small ({TranscriptionContentLength} bytes)", fileIndex + 1, transcriptionContent?.Length ?? 0);
                        continue;
                    }
                    
                    // Store the first file's JSON as the raw data (for backward compatibility)
                    if (fileIndex == 0)
                    {
                        combinedRawJsonData = transcriptionContent;
                    }
                    
                    // Parse this file's JSON and extract segments
                    var parsedFiles = ParseSingleTranscriptionFile(transcriptionContent, fileName, fileIndex, ref globalLineNumber);
                    
                    if (parsedFiles != null && parsedFiles.Count > 0)
                    {
                        _logger.LogInformation("? Parsed file {FileIndex + 1}: {FileCount} file(s) with {SegmentCount} total segments", 
                            fileIndex, parsedFiles.Count, parsedFiles.Sum(f => f.Segments.Count));
                        
                        // Add all files from this transcription (might be multiple if multi-channel)
                        foreach (var parsedFile in parsedFiles)
                        {
                            allSegments.AddRange(parsedFile.Segments);
                            fullTranscriptBuilder.AppendLine(parsedFile.FullTranscript);
                            foreach (var speaker in parsedFile.AvailableSpeakers)
                            {
                                allSpeakers.Add(speaker);
                            }
                            fileResults.Add(parsedFile);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Failed to parse transcription file {FileIndex + 1}", fileIndex);
                    }
                }
                
                _logger.LogInformation("?? FINAL RESULTS SUMMARY:");
                _logger.LogInformation("   Files processed: {ProcessedFileCount}/{TotalTranscriptionFiles}", fileResults.Count, transcriptionUrls.Count);
                _logger.LogInformation("   Total segments: {TotalSegments}", allSegments.Count);
                _logger.LogInformation("   Total speakers: {TotalSpeakers}", allSpeakers.Count);
                
                if (allSegments.Count == 0)
                {
                    _logger.LogWarning("No segments found across {TranscriptionFileCount} transcription file(s)", transcriptionUrls.Count);
                    return new BatchTranscriptionResult
                    {
                        Success = false,
                        Message = $"Downloaded {transcriptionUrls.Count} transcription file(s) but found no segments. Check logs for details.",
                        JobId = jobId,
                        DisplayName = job.DisplayName
                    };
                }
                
                // Calculate overall speaker statistics
                var speakerStats = allSegments
                    .GroupBy(s => s.Speaker)
                    .Select(g => new SpeakerInfo
                    {
                        Name = g.Key,
                        SegmentCount = g.Count(),
                        TotalSpeakTimeSeconds = g.Sum(s => s.EndTimeInSeconds - s.StartTimeInSeconds),
                        FirstAppearanceSeconds = g.Min(s => s.StartTimeInSeconds)
                    })
                    .OrderBy(s => s.FirstAppearanceSeconds)
                    .ToList();
                
                var result = new BatchTranscriptionResult
                {
                    Success = true,
                    Message = $"Transcription results retrieved successfully from {fileResults.Count} file(s)",
                    JobId = jobId,
                    DisplayName = job.DisplayName,
                    Segments = allSegments,
                    FullTranscript = fullTranscriptBuilder.ToString(),
                    AvailableSpeakers = allSpeakers.OrderBy(s => s).ToList(),
                    SpeakerStatistics = speakerStats,
                    RawJsonData = combinedRawJsonData,
                    FileResults = fileResults,
                    TotalFiles = fileResults.Count
                };
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching transcription results for job {JobId}", jobId);
                return new BatchTranscriptionResult
                {
                    Success = false,
                    Message = $"Error: {ex.Message}",
                    JobId = jobId
                };
            }
        }

        public async Task<BatchTranscriptionResult?> GetTranscriptionResultsByFileAsync(string jobId, int fileIndex, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Fetching transcription results for job {JobId}, file index {FileIndex}", jobId, fileIndex);
                
                // First get the full results
                var fullResults = await GetTranscriptionResultsAsync(jobId, cancellationToken);
                
                if (fullResults == null || !fullResults.Success)
                {
                    return fullResults;
                }
                
                // Check if file index is valid
                if (fileIndex < 0 || fileIndex >= fullResults.FileResults.Count)
                {
                    _logger.LogWarning("Invalid file index {FileIndex} for job {JobId}. Total files: {TotalFiles}", fileIndex, jobId, fullResults.FileResults.Count);
                    return new BatchTranscriptionResult
                    {
                        Success = false,
                        Message = $"Invalid file index. Job has {fullResults.FileResults.Count} file(s).",
                        JobId = jobId,
                        DisplayName = fullResults.DisplayName
                    };
                }
                
                // Get the specific file
                var fileInfo = fullResults.FileResults[fileIndex];
                
                // Return results for just this file
                return new BatchTranscriptionResult
                {
                    Success = true,
                    Message = $"Transcription results for {fileInfo.FileName}",
                    JobId = jobId,
                    DisplayName = $"{fullResults.DisplayName} - {fileInfo.FileName}",
                    Segments = fileInfo.Segments,
                    FullTranscript = fileInfo.FullTranscript,
                    AvailableSpeakers = fileInfo.AvailableSpeakers,
                    SpeakerStatistics = fileInfo.SpeakerStatistics,
                    RawJsonData = fullResults.RawJsonData,
                    FileResults = new List<FileTranscriptionInfo> { fileInfo },
                    TotalFiles = fullResults.TotalFiles
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching transcription results for job {JobId}, file {FileIndex}", jobId, fileIndex);
                return new BatchTranscriptionResult
                {
                    Success = false,
                    Message = $"Error: {ex.Message}",
                    JobId = jobId
                };
            }
        }
        
        private BatchTranscriptionResult ParseBatchTranscriptionJson(string jsonContent, string jobId, string displayName)
        {
            try
            {
                _logger.LogInformation("Parsing batch transcription JSON for job {JobId}", jobId);
                _logger.LogDebug("JSON content length: {JsonContentLength} bytes", jsonContent?.Length ?? 0);
                
                // Validate input
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    _logger.LogWarning("Empty or null JSON content for job {JobId}", jobId);
                    return new BatchTranscriptionResult
                    {
                        Success = false,
                        Message = "No transcription data available",
                        JobId = jobId,
                        DisplayName = displayName
                    };
                }
                
                // LOG: First 2000 characters of JSON for debugging
                var preview = jsonContent.Length > JsonPreviewMaxLength ? jsonContent.Substring(0, JsonPreviewMaxLength) : jsonContent;
                _logger.LogInformation("JSON preview (first 2000 chars): {JsonPreview}", preview);
                
                var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;
                
                // LOG: What properties exist in the root
                var rootProperties = new List<string>();
                if (root.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in root.EnumerateObject())
                    {
                        rootProperties.Add(prop.Name);
                    }
                    _logger.LogInformation("Root JSON properties found: {RootProperties}", string.Join(", ", rootProperties));
                }
                
                var allSegments = new List<SpeakerSegment>();
                var fullTranscriptBuilder = new System.Text.StringBuilder();
                var allSpeakers = new HashSet<string>();
                var fileResults = new List<FileTranscriptionInfo>();
                
                // Parse recognizedPhrases for detailed segments with file/channel information
                if (TryGetArrayProperty(root, "recognizedPhrases", out var recognizedArray))
                {
                    _logger.LogInformation("Found 'recognizedPhrases' array with {ElementCount} elements", recognizedArray.GetArrayLength());
                    
                    // LOG: First phrase for debugging
                    if (recognizedArray.GetArrayLength() > 0)
                    {
                        var firstPhrase = recognizedArray[0];
                        _logger.LogInformation("First phrase sample: {FirstPhraseJson}", firstPhrase.GetRawText());
                        _logger.LogInformation("First phrase properties: {FirstPhraseProperties}", string.Join(", ", firstPhrase.EnumerateObject().Select(p => p.Name)));
                    }
                    
                    // NEW: Log channel distribution for multi-file diagnostics
                    var channelDistribution = new Dictionary<int, int>();
                    foreach (var phrase in recognizedArray.EnumerateArray())
                    {
                        var channelStr = GetStringProperty(phrase, "channel") ?? "0";
                        if (int.TryParse(channelStr, out var channel))
                        {
                            if (!channelDistribution.ContainsKey(channel))
                            {
                                channelDistribution[channel] = 0;
                            }
                            channelDistribution[channel]++;
                        }
                    }
                    
                    _logger.LogInformation("?? CHANNEL DISTRIBUTION ANALYSIS:");
                    _logger.LogInformation("   Total phrases: {TotalPhrases}", recognizedArray.GetArrayLength());
                    _logger.LogInformation("   Unique channels found: {UniqueChannelCount}", channelDistribution.Count);
                    foreach (var kvp in channelDistribution.OrderBy(k => k.Key))
                    {
                        _logger.LogInformation("   - Channel {Channel}: {PhraseCount} phrases ({Percentage:F1}%)", kvp.Key, kvp.Value, kvp.Value * 100.0 / recognizedArray.GetArrayLength());
                    }
                    
                    if (channelDistribution.Count == 1 && channelDistribution.ContainsKey(0))
                    {
                        _logger.LogWarning("?? ALL PHRASES ARE IN CHANNEL 0!");
                        _logger.LogWarning("   This means Azure Speech Service returned all audio in a single channel.");
                        _logger.LogWarning("   If you submitted multiple files, they were not separated by Azure.");
                        _logger.LogWarning("   See MULTI_FILE_CHANNEL_DIAGNOSTIC.md for solutions.");
                    }
                    else if (channelDistribution.Count > 1)
                    {
                        _logger.LogInformation("? Multiple channels detected - files are properly separated!");
                    }
                    
                    // Group by channel (each channel typically represents a different audio file)
                    var phrasesByChannel = new Dictionary<int, List<JsonElement>>();
                    
                    foreach (var phrase in recognizedArray.EnumerateArray())
                    {
                        var channelStr = GetStringProperty(phrase, "channel") ?? "0";
                        if (int.TryParse(channelStr, out var channel))
                        {
                            if (!phrasesByChannel.ContainsKey(channel))
                            {
                                phrasesByChannel[channel] = new List<JsonElement>();
                            }
                            phrasesByChannel[channel].Add(phrase);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to parse channel number: '{ChannelStr}'", channelStr);
                        }
                    }
                    
                    _logger.LogInformation("Grouped phrases into {GroupCount} channel(s)", phrasesByChannel.Count);
                    
                    // Process each channel/file
                    int lineNumber = 1;
                    foreach (var channelGroup in phrasesByChannel.OrderBy(kvp => kvp.Key))
                    {
                        var channel = channelGroup.Key;
                        var phrases = channelGroup.Value;
                        
                        _logger.LogInformation("Processing channel {Channel} with {PhraseCount} phrases", channel, phrases.Count);
                        
                        var fileSegments = new List<SpeakerSegment>();
                        var fileSpeakers = new HashSet<string>();
                        long fileDuration = 0;
                        
                        // Estimate capacity for StringBuilder
                        var estimatedCapacity = phrases.Count * 100;
                        var fileTranscriptBuilder = new System.Text.StringBuilder(estimatedCapacity);
                        
                        foreach (var phrase in phrases)
                        {
                            var segment = ParsePhraseToSegment(phrase, channel, ref lineNumber);
                            
                            if (segment != null)
                            {
                                fileSegments.Add(segment);
                                allSegments.Add(segment);
                                fileSpeakers.Add(segment.Speaker);
                                allSpeakers.Add(segment.Speaker);
                                
                                fileTranscriptBuilder.AppendLine($"[{segment.UIFormattedStartTime}] {segment.Speaker}: {segment.Text}");
                                fullTranscriptBuilder.AppendLine($"[{segment.UIFormattedStartTime}] {segment.Speaker}: {segment.Text}");
                                
                                var endTicks = segment.OffsetInTicks + segment.DurationInTicks;
                                if (endTicks > fileDuration)
                                {
                                    fileDuration = endTicks;
                                }
                            }
                        }
                        
                        _logger.LogInformation("Channel {Channel} produced {SegmentCount} segments", channel, fileSegments.Count);
                        
                        // Create file info using the helper method
                        var fileInfo = CreateFileTranscriptionInfo(
                            $"File {channel + 1}", 
                            channel, 
                            fileSegments, 
                            fileSpeakers,
                            fileTranscriptBuilder.ToString(), 
                            fileDuration);
                        
                        fileResults.Add(fileInfo);
                    }
                }
                // Fallback to combinedRecognizedPhrases if recognizedPhrases not available
                else if (TryGetArrayProperty(root, "combinedRecognizedPhrases", out var phrasesArray))
                {
                    _logger.LogInformation("Found 'combinedRecognizedPhrases' array with {ElementCount} elements", phrasesArray.GetArrayLength());
                    
                    // Parse using helper method
                    int lineNumber = 1;
                    var fileInfo = ParseCombinedPhrasesAsFile(phrasesArray, "Combined Results", 0, ref lineNumber);
                    
                    if (fileInfo != null && fileInfo.Segments.Count > 0)
                    {
                        allSegments.AddRange(fileInfo.Segments);
                        foreach (var speaker in fileInfo.AvailableSpeakers)
                        {
                            allSpeakers.Add(speaker);
                        }
                        fullTranscriptBuilder.Append(fileInfo.FullTranscript);
                        fileResults.Add(fileInfo);
                        
                        _logger.LogInformation("Created {SegmentCount} segments from combinedRecognizedPhrases", allSegments.Count);
                    }
                }
                else
                {
                    _logger.LogWarning("No 'recognizedPhrases' or 'combinedRecognizedPhrases' found in JSON for job {JobId}", jobId);
                    _logger.LogWarning("Available root properties: {RootProperties}", string.Join(", ", rootProperties));
                    
                    // Try to log some sample of the JSON structure
                    if (rootProperties.Count > 0)
                    {
                        _logger.LogInformation("Attempting to inspect JSON structure...");
                        foreach (var propName in rootProperties.Take(5))
                        {
                            if (root.TryGetProperty(propName, out var prop))
                            {
                                _logger.LogInformation("  Property '{PropName}': ValueKind={ValueKind}", propName, prop.ValueKind);
                                if (prop.ValueKind == JsonValueKind.Array)
                                {
                                    _logger.LogInformation("    Array length: {ArrayLength}", prop.GetArrayLength());
                                }
                            }
                        }
                    }
                    
                    _logger.LogDebug("Full JSON content for job {JobId}: {JsonContent}", jobId, jsonContent);
                }
                
                _logger.LogInformation("Parsing complete for job {JobId}: {SegmentCount} total segments, {FileCount} files", jobId, allSegments.Count, fileResults.Count);
                
                // Calculate overall speaker statistics
                var speakerStats = allSegments
                    .GroupBy(s => s.Speaker)
                    .Select(g => new SpeakerInfo
                    {
                        Name = g.Key,
                        SegmentCount = g.Count(),
                        TotalSpeakTimeSeconds = g.Sum(s => s.EndTimeInSeconds - s.StartTimeInSeconds),
                        FirstAppearanceSeconds = g.Min(s => s.StartTimeInSeconds)
                    })
                    .OrderBy(s => s.FirstAppearanceSeconds)
                    .ToList();
                
                var result = new BatchTranscriptionResult
                {
                    Success = true,
                    Message = allSegments.Count > 0 
                        ? "Transcription results retrieved successfully" 
                        : "Job completed but no transcription segments found",
                    JobId = jobId,
                    DisplayName = displayName,
                    Segments = allSegments,
                    FullTranscript = fullTranscriptBuilder.ToString(),
                    AvailableSpeakers = allSpeakers.OrderBy(s => s).ToList(),
                    SpeakerStatistics = speakerStats,
                    RawJsonData = jsonContent,
                    FileResults = fileResults,
                    TotalFiles = fileResults.Count
                };
                
                if (allSegments.Count == 0)
                {
                    _logger.LogWarning("?? Zero segments returned for job {JobId}. Check if audio had speech or if transcription succeeded.", jobId);
                    _logger.LogWarning("JSON structure summary:");
                    _logger.LogWarning("  - Root properties: {RootProperties}", string.Join(", ", rootProperties));
                    _logger.LogWarning("  - Has recognizedPhrases: {HasRecognizedPhrases}", root.TryGetProperty("recognizedPhrases", out _));
                    _logger.LogWarning("  - Has combinedRecognizedPhrases: {HasCombinedRecognizedPhrases}", root.TryGetProperty("combinedRecognizedPhrases", out _));
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing batch transcription JSON for job {JobId}", jobId);
                return new BatchTranscriptionResult
                {
                    Success = false,
                    Message = $"Error parsing transcription: {ex.Message}",
                    JobId = jobId,
                    DisplayName = displayName
                };
            }
        }
        
        private string FormatTimeForUI(double seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return $"{ts:hh\\:mm\\:ss}";
        }
        
        /// <summary>
        /// Parse a single transcription file JSON and extract segments.
        /// Handles both single-channel and multi-channel JSON files.
        /// </summary>
        private List<FileTranscriptionInfo> ParseSingleTranscriptionFile(
            string jsonContent, 
            string fileName, 
            int fileIndex, 
            ref int globalLineNumber)
        {
            try
            {
                _logger.LogInformation("Parsing transcription file: {FileName}", fileName);
                
                using var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;
                
                // Try to parse as multi-channel first (handles both single and multi-channel)
                if (TryGetArrayProperty(root, "recognizedPhrases", out var recognizedArray))
                {
                    // Check if this has multiple channels
                    var channels = GetUniqueChannels(recognizedArray);
                    
                    if (channels.Count > 1)
                    {
                        // Multi-channel JSON - parse each channel as a separate file
                        _logger.LogInformation("Multi-channel JSON detected with {ChannelCount} channels", channels.Count);
                        return ParseMultiChannelRecognizedPhrases(recognizedArray, fileName, ref globalLineNumber);
                    }
                    
                    // Single channel - parse normally
                    var singleFile = ParseRecognizedPhrasesAsFile(recognizedArray, fileName, fileIndex, ref globalLineNumber);
                    return new List<FileTranscriptionInfo> { singleFile };
                }
                
                // Fallback to combinedRecognizedPhrases
                if (TryGetArrayProperty(root, "combinedRecognizedPhrases", out var phrasesArray))
                {
                    var singleFile = ParseCombinedPhrasesAsFile(phrasesArray, fileName, fileIndex, ref globalLineNumber);
                    return new List<FileTranscriptionInfo> { singleFile };
                }
                
                _logger.LogWarning("No recognizedPhrases or combinedRecognizedPhrases found in file {FileName}", fileName);
                return new List<FileTranscriptionInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing transcription file: {FileName}", fileName);
                return new List<FileTranscriptionInfo>();
            }
        }
        
        /// <summary>
        /// Parse multi-channel recognizedPhrases array into separate FileTranscriptionInfo objects
        /// </summary>
        private List<FileTranscriptionInfo> ParseMultiChannelRecognizedPhrases(
            JsonElement recognizedArray,
            string baseFileName,
            ref int globalLineNumber)
        {
            var results = new List<FileTranscriptionInfo>();
            
            // Group by channel
            var phrasesByChannel = new Dictionary<int, List<JsonElement>>();
            
            foreach (var phrase in recognizedArray.EnumerateArray())
            {
                var channelStr = GetStringProperty(phrase, "channel") ?? "0";
                if (int.TryParse(channelStr, out var channel))
                {
                    if (!phrasesByChannel.ContainsKey(channel))
                    {
                        phrasesByChannel[channel] = new List<JsonElement>();
                    }
                    phrasesByChannel[channel].Add(phrase);
                }
            }
            
            _logger.LogInformation("Grouped {PhraseCount} phrases into {ChannelCount} channel(s)", 
                recognizedArray.GetArrayLength(), phrasesByChannel.Count);
            
            // Process each channel as a separate file
            foreach (var channelGroup in phrasesByChannel.OrderBy(kvp => kvp.Key))
            {
                var channel = channelGroup.Key;
                var phrases = channelGroup.Value;
                
                _logger.LogInformation("Processing channel {Channel} with {PhraseCount} phrases", channel, phrases.Count);
                
                var fileSegments = new List<SpeakerSegment>();
                var fileSpeakers = new HashSet<string>();
                long fileDuration = 0;
                
                var estimatedCapacity = phrases.Count * 100;
                var fileTranscriptBuilder = new System.Text.StringBuilder(estimatedCapacity);
                
                foreach (var phrase in phrases)
                {
                    var segment = ParsePhraseToSegment(phrase, channel, ref globalLineNumber);
                    
                    if (segment != null)
                    {
                        fileSegments.Add(segment);
                        fileSpeakers.Add(segment.Speaker);
                        fileTranscriptBuilder.AppendLine($"[{segment.UIFormattedStartTime}] {segment.Speaker}: {segment.Text}");
                        
                        var endTicks = segment.OffsetInTicks + segment.DurationInTicks;
                        if (endTicks > fileDuration)
                        {
                            fileDuration = endTicks;
                        }
                    }
                }
                
                _logger.LogInformation("Channel {Channel} produced {SegmentCount} segments", channel, fileSegments.Count);
                
                // Create file info with channel-specific name
                var fileName = $"File {channel + 1}";
                var fileInfo = CreateFileTranscriptionInfo(
                    fileName, 
                    channel, 
                    fileSegments, 
                    fileSpeakers,
                    fileTranscriptBuilder.ToString(), 
                    fileDuration);
                
                results.Add(fileInfo);
            }
            
            return results;
        }
        
        /// <summary>
        /// Get unique channel numbers from phrases array
        /// </summary>
        private HashSet<int> GetUniqueChannels(JsonElement phrasesArray)
        {
            var channels = new HashSet<int>();
            
            foreach (var phrase in phrasesArray.EnumerateArray())
            {
                var channelStr = GetStringProperty(phrase, "channel") ?? "0";
                if (int.TryParse(channelStr, out var channel))
                {
                    channels.Add(channel);
                }
            }
            
            return channels;
        }
        
        /// <summary>
        /// Parse recognizedPhrases array as a single file
        /// </summary>
        private FileTranscriptionInfo ParseRecognizedPhrasesAsFile(
            JsonElement recognizedArray,
            string fileName,
            int fileIndex,
            ref int globalLineNumber)
        {
            _logger.LogInformation("Found {PhraseCount} recognized phrases", recognizedArray.GetArrayLength());
            
            var fileSegments = new List<SpeakerSegment>();
            var fileSpeakers = new HashSet<string>();
            long fileDuration = 0;
            
            var estimatedCapacity = recognizedArray.GetArrayLength() * 100;
            var fileTranscriptBuilder = new System.Text.StringBuilder(estimatedCapacity);
            
            foreach (var phrase in recognizedArray.EnumerateArray())
            {
                var segment = ParsePhraseToSegment(phrase, fileIndex, ref globalLineNumber);
                
                if (segment != null)
                {
                    fileSegments.Add(segment);
                    fileSpeakers.Add(segment.Speaker);
                    fileTranscriptBuilder.AppendLine($"[{segment.UIFormattedStartTime}] {segment.Speaker}: {segment.Text}");
                    
                    var endTicks = segment.OffsetInTicks + segment.DurationInTicks;
                    if (endTicks > fileDuration)
                    {
                        fileDuration = endTicks;
                    }
                }
            }
            
            return CreateFileTranscriptionInfo(fileName, fileIndex, fileSegments, fileSpeakers, 
                fileTranscriptBuilder.ToString(), fileDuration);
        }
        
        /// <summary>
        /// Parse combinedRecognizedPhrases array as a single file
        /// </summary>
        private FileTranscriptionInfo ParseCombinedPhrasesAsFile(
            JsonElement phrasesArray,
            string fileName,
            int fileIndex,
            ref int globalLineNumber)
        {
            _logger.LogInformation("Found {PhraseCount} combined phrases", phrasesArray.GetArrayLength());
            
            var fileSegments = new List<SpeakerSegment>();
            var fileSpeakers = new HashSet<string>();
            
            var estimatedCapacity = phrasesArray.GetArrayLength() * 100;
            var fileTranscriptBuilder = new System.Text.StringBuilder(estimatedCapacity);
            
            foreach (var phrase in phrasesArray.EnumerateArray())
            {
                var text = GetStringProperty(phrase, "display");
                var speakerFromJson = GetStringProperty(phrase, "speaker");
                var speaker = !string.IsNullOrEmpty(speakerFromJson) 
                    ? speakerFromJson 
                    : $"Speaker {fileIndex + 1}";
                
                if (!string.IsNullOrEmpty(text))
                {
                    var segment = new SpeakerSegment
                    {
                        LineNumber = globalLineNumber++,
                        Speaker = speaker,
                        Text = text,
                        OffsetInTicks = 0,
                        DurationInTicks = 0
                    };
                    
                    fileSegments.Add(segment);
                    fileSpeakers.Add(speaker);
                    fileTranscriptBuilder.AppendLine($"[{speaker}]: {text}");
                }
            }
            
            return CreateFileTranscriptionInfo(fileName, fileIndex, fileSegments, fileSpeakers,
                fileTranscriptBuilder.ToString(), 0);
        }
        
        /// <summary>
        /// Parse a single phrase element into a SpeakerSegment
        /// </summary>
        private SpeakerSegment? ParsePhraseToSegment(JsonElement phrase, int channelOrFileIndex, ref int lineNumber)
        {
            // Get speaker
            var speakerFromJson = GetStringProperty(phrase, "speaker");
            var speaker = !string.IsNullOrEmpty(speakerFromJson) 
                ? speakerFromJson 
                : $"Speaker {channelOrFileIndex + 1}";
            
            // Get timing
            TryGetNumberProperty(phrase, "offsetInTicks", out var offset);
            TryGetNumberProperty(phrase, "durationInTicks", out var duration);
            
            // Get text from nBest array
            if (TryGetArrayProperty(phrase, "nBest", out var nBestArray))
            {
                foreach (var nBest in nBestArray.EnumerateArray())
                {
                    var text = GetStringProperty(nBest, "display");
                    if (!string.IsNullOrEmpty(text))
                    {
                        return new SpeakerSegment
                        {
                            LineNumber = lineNumber++,
                            Speaker = speaker,
                            Text = text,
                            OffsetInTicks = offset ?? 0,
                            DurationInTicks = duration ?? 0
                        };
                    }
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Create FileTranscriptionInfo with calculated statistics
        /// </summary>
        private FileTranscriptionInfo CreateFileTranscriptionInfo(
            string fileName,
            int channelOrFileIndex,
            List<SpeakerSegment> segments,
            HashSet<string> speakers,
            string fullTranscript,
            long durationInTicks)
        {
            if (segments.Count == 0)
            {
                return new FileTranscriptionInfo
                {
                    FileName = fileName,
                    Channel = channelOrFileIndex,
                    Segments = segments,
                    FullTranscript = fullTranscript,
                    AvailableSpeakers = new List<string>(),
                    SpeakerStatistics = new List<SpeakerInfo>(),
                    DurationInTicks = durationInTicks
                };
            }
            
            var speakerStats = segments
                .GroupBy(s => s.Speaker)
                .Select(g => new SpeakerInfo
                {
                    Name = g.Key,
                    SegmentCount = g.Count(),
                    TotalSpeakTimeSeconds = g.Sum(s => s.EndTimeInSeconds - s.StartTimeInSeconds),
                    FirstAppearanceSeconds = g.Min(s => s.StartTimeInSeconds)
                })
                .OrderBy(s => s.FirstAppearanceSeconds)
                .ToList();
            
            return new FileTranscriptionInfo
            {
                FileName = fileName,
                Channel = channelOrFileIndex,
                Segments = segments,
                FullTranscript = fullTranscript,
                AvailableSpeakers = speakers.OrderBy(s => s).ToList(),
                SpeakerStatistics = speakerStats,
                DurationInTicks = durationInTicks
            };
        }
    }
}
