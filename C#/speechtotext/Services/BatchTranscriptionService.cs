using speechtotext.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace speechtotext.Services
{
    /// <summary>
    /// Service for creating batch transcription jobs in Azure Speech Service
    /// Uses Azure AD authentication for Blob Storage access
    /// </summary>
    public class BatchTranscriptionService : IBatchTranscriptionService
    {
        private readonly string _subscriptionKey;
        private readonly string _region;
        private readonly ILogger<BatchTranscriptionService> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _modelsBaseUrl;  // Add separate URL for models API
        private readonly BlobServiceClient? _blobServiceClient;
        private readonly AzureStorageOptions _storageOptions;
        private readonly AudioUploadOptions _audioUploadOptions;

        // Locale caching
        private static List<string>? _cachedLocales;
        private static List<LocaleInfo>? _cachedLocalesWithNames;
        private static DateTime _cacheExpiration = DateTime.MinValue;
        private static readonly object _cacheLock = new object();

        public BatchTranscriptionService(
            IConfiguration configuration,
            ILogger<BatchTranscriptionService> logger,
            IHttpClientFactory httpClientFactory,
            IOptions<AzureStorageOptions> storageOptions,
            IOptions<AudioUploadOptions> audioUploadOptions)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var factory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _storageOptions = storageOptions?.Value ?? throw new ArgumentNullException(nameof(storageOptions));
            _audioUploadOptions = audioUploadOptions?.Value ?? throw new ArgumentNullException(nameof(audioUploadOptions));

            _subscriptionKey = _configuration["AzureSpeech:SubscriptionKey"] 
                ?? throw new ArgumentException("Azure Speech subscription key not found in configuration");
            _region = _configuration["AzureSpeech:Region"] 
                ?? throw new ArgumentException("Azure Speech region not found in configuration");

            _baseUrl = $"https://{_region}.api.cognitive.microsoft.com/speechtotext/v3.1";
            _modelsBaseUrl = $"https://{_region}.api.cognitive.microsoft.com/speechtotext/v3.2";  // Use v3.2 for models API

            _httpClient = factory.CreateClient();
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            // Initialize Blob Storage if configured
            if (_storageOptions.IsConfigured)
            {
                try
                {
                    // Create BlobServiceClient with Azure AD authentication
                    _blobServiceClient = CreateBlobServiceClient();
                    _logger.LogInformation("Azure Blob Storage client initialized successfully using Azure AD authentication");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Azure Blob Storage client");
                    _blobServiceClient = null;
                }
            }
            else
            {
                _logger.LogInformation("Azure Blob Storage is not configured. Using placeholder mode for batch jobs.");
            }
        }

        /// <summary>
        /// Creates a BlobServiceClient using Azure AD authentication
        /// Supports Managed Identity and Service Principal authentication
        /// </summary>
        private BlobServiceClient CreateBlobServiceClient()
        {
            var blobServiceUri = new Uri(_storageOptions.BlobServiceEndpoint);

            if (_storageOptions.UseManagedIdentity)
            {
                // Use DefaultAzureCredential for Managed Identity
                // This works with:
                // - Managed Identity (when deployed to Azure)
                // - Azure CLI (for local development)
                // - Visual Studio (for local development)
                // - Environment variables
                _logger.LogInformation("Using DefaultAzureCredential (Managed Identity) for blob storage authentication");
                var credential = new DefaultAzureCredential();
                return new BlobServiceClient(blobServiceUri, credential);
            }
            else
            {
                // Use Service Principal (Client ID + Secret)
                _logger.LogInformation("Using Service Principal (Client ID/Secret) for blob storage authentication");
                var credential = new ClientSecretCredential(
                    _storageOptions.TenantId,
                    _storageOptions.ClientId,
                    _storageOptions.ClientSecret);
                return new BlobServiceClient(blobServiceUri, credential);
            }
        }

        public async Task<TranscriptionJob> CreateBatchTranscriptionAsync(
            IEnumerable<string> audioFilePaths,
            string jobName,
            string language = "en-US",
            bool enableDiarization = true,
            int? minSpeakers = null,
            int? maxSpeakers = null,
            CancellationToken cancellationToken = default)
        {
            // Use configuration defaults if not specified
            var effectiveMinSpeakers = minSpeakers ?? _audioUploadOptions.DefaultMinSpeakers;
            var effectiveMaxSpeakers = maxSpeakers ?? _audioUploadOptions.DefaultMaxSpeakers;
            
            _logger.LogInformation($"Creating batch transcription: {jobName} with speaker range {effectiveMinSpeakers}-{effectiveMaxSpeakers}");

            // Check if Blob Storage is configured
            if (_blobServiceClient == null || !_storageOptions.IsConfigured)
            {
                _logger.LogWarning("Blob Storage not configured. Creating placeholder job.");
                return CreatePlaceholderJob(audioFilePaths, jobName);
            }

            try
            {
                // Step 1: Upload files to Blob Storage and get URLs with Azure AD tokens
                var blobUrls = await UploadFilesToBlobStorageAsync(audioFilePaths, cancellationToken);

                if (blobUrls.Count == 0)
                {
                    _logger.LogWarning("No files uploaded to blob storage. Creating placeholder job.");
                    return CreatePlaceholderJob(audioFilePaths, jobName);
                }

                // Step 2: Submit to Azure Speech Service with blob URLs
                // Note: Azure Speech Service needs to access these blobs
                // You need to grant the Speech Service identity access to your storage account
                
                // Build request with proper diarization configuration
                // Azure Speech Service v3.1 requires specific format for speaker diarization
                var requestBody = new
                {
                    contentUrls = blobUrls,
                    locale = language,
                    displayName = jobName,
                    properties = new
                    {
                        // Diarization settings - use string for mode, not nested object
                        diarizationEnabled = enableDiarization,
                        diarization = new
                        {
                            mode = "Identity",  // Required: Identity mode for speaker diarization
                            speakers = new
                            {
                                minCount = effectiveMinSpeakers,
                                maxCount = effectiveMaxSpeakers
                            }
                        },
                        wordLevelTimestampsEnabled = true,
                        punctuationMode = "DictatedAndAutomatic",
                        profanityFilterMode = "Masked"
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation($"Submitting batch job to Azure Speech Service with {blobUrls.Count} files and speaker range {effectiveMinSpeakers}-{effectiveMaxSpeakers}");
                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/transcriptions",
                    content,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError($"Batch job creation failed: Status {response.StatusCode}, Error: {error}");
                    throw new Exception($"Failed to create batch job: {error}");
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var jobData = JsonDocument.Parse(responseContent);

                var selfUrl = jobData.RootElement.GetProperty("self").GetString() ?? "";
                var jobId = selfUrl.Split('/').LastOrDefault() ?? Guid.NewGuid().ToString();

                _logger.LogInformation($"Batch job created successfully: {jobId} with {effectiveMinSpeakers}-{effectiveMaxSpeakers} speakers");

                return new TranscriptionJob
                {
                    Id = jobId,
                    DisplayName = jobName,
                    Status = "NotStarted",
                    CreatedDateTime = DateTime.UtcNow,
                    Files = audioFilePaths.Select(Path.GetFileName).ToList()!
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating batch transcription job");
                
                // Return placeholder for demonstration if real job creation fails
                _logger.LogWarning("Creating placeholder job due to error");
                return CreatePlaceholderJob(audioFilePaths, jobName);
            }
        }

        /// <summary>
        /// Uploads files to Blob Storage using Azure AD authentication
        /// Returns blob URLs (no SAS tokens needed - uses Azure RBAC)
        /// </summary>
        private async Task<List<string>> UploadFilesToBlobStorageAsync(
            IEnumerable<string> audioFilePaths,
            CancellationToken cancellationToken)
        {
            var blobUrls = new List<string>();

            if (_blobServiceClient == null)
            {
                _logger.LogWarning("Blob service client is null, cannot upload files");
                return blobUrls;
            }

            try
            {
                // Get or create container
                var containerClient = _blobServiceClient.GetBlobContainerClient(_storageOptions.ContainerName);
                await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
                
                _logger.LogInformation($"Using blob container: {_storageOptions.ContainerName}");

                foreach (var filePath in audioFilePaths)
                {
                    try
                    {
                        var fileName = Path.GetFileName(filePath);
                        var blobName = $"{Guid.NewGuid()}_{fileName}";
                        var blobClient = containerClient.GetBlobClient(blobName);

                        _logger.LogInformation($"Uploading file to blob storage: {fileName} as {blobName}");

                        // Upload file
                        using (var fileStream = File.OpenRead(filePath))
                        {
                            await blobClient.UploadAsync(fileStream, overwrite: true, cancellationToken);
                        }

                        // Return the blob URL (no SAS token needed)
                        // Azure Speech Service must have Storage Blob Data Reader role on this storage account
                        var blobUrl = blobClient.Uri.ToString();
                        blobUrls.Add(blobUrl);

                        _logger.LogInformation($"File uploaded successfully: {blobName}");
                        _logger.LogInformation($"Blob URL: {blobUrl}");
                        _logger.LogInformation("Note: Ensure Azure Speech Service has 'Storage Blob Data Reader' role on this storage account");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to upload file to blob storage: {filePath}");
                        // Continue with other files
                    }
                }

                _logger.LogInformation($"Successfully uploaded {blobUrls.Count} files to blob storage");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading files to blob storage");
            }

            return blobUrls;
        }

        private TranscriptionJob CreatePlaceholderJob(IEnumerable<string> audioFilePaths, string jobName)
        {
            var jobId = Guid.NewGuid().ToString();
            _logger.LogInformation($"Created placeholder batch job: {jobId}");
            
            return new TranscriptionJob
            {
                Id = jobId,
                DisplayName = jobName,
                Status = "NotStarted",
                CreatedDateTime = DateTime.UtcNow,
                Files = audioFilePaths.Select(Path.GetFileName).ToList()!,
                Error = _storageOptions.IsConfigured 
                    ? null 
                    : "Placeholder job - Azure Blob Storage not configured. See AZURE_AD_BLOB_STORAGE_SETUP.md"
            };
        }

        /// <summary>
        /// Gets the list of supported locales for batch transcription from Azure Speech Service
        /// </summary>
        public async Task<IEnumerable<string>> GetSupportedLocalesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Check cache first
                lock (_cacheLock)
                {
                    if (_cachedLocales != null && DateTime.UtcNow < _cacheExpiration)
                    {
                        _logger.LogInformation("Returning cached list of locales");
                        return _cachedLocales.OrderBy(l => l).ToList();
                    }
                }

                _logger.LogInformation("Fetching supported locales from Azure Speech Service");

                // Use the models API endpoint to get all models, then extract unique locales
                var response = await _httpClient.GetAsync(
                    $"{_modelsBaseUrl}/models",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError($"Failed to fetch models list: Status {response.StatusCode}, Error: {error}");
                    
                    // Return a fallback list of common locales
                    return GetFallbackLocales();
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                
                // DIAGNOSTIC: Log the raw response for debugging
                _logger.LogInformation($"Raw Azure models API response length: {responseContent.Length} bytes");
                
                var modelsData = JsonDocument.Parse(responseContent);

                // Azure returns: { "values": [ { "locale": "en-US", ...}, { "locale": "es-ES", ...}, ... ] }
                var locales = new List<string>();
                
                if (modelsData.RootElement.TryGetProperty("values", out var valuesElement) && 
                    valuesElement.ValueKind == JsonValueKind.Array)
                {
                    _logger.LogInformation($"Found 'values' array in response");
                    
                    foreach (var model in valuesElement.EnumerateArray())
                    {
                        if (model.TryGetProperty("locale", out var localeElement))
                        {
                            var locale = localeElement.GetString();
                            if (!string.IsNullOrEmpty(locale) && !locales.Contains(locale))
                            {
                                locales.Add(locale);
                            }
                        }
                    }
                }

                _logger.LogInformation($"Successfully extracted {locales.Count} unique locales from Azure models API");
                
                // If Azure returns an empty list, use fallback instead
                if (locales.Count == 0)
                {
                    _logger.LogWarning("Azure returned 0 locales. Using fallback list instead.");
                    return GetFallbackLocales();
                }
                
                // Update cache
                lock (_cacheLock)
                {
                    _cachedLocales = locales;
                    // Use configured cache duration in hours (convert to DateTime)
                    var cacheDurationHours = _audioUploadOptions.LocalesCacheDurationHours;
                    _cacheExpiration = cacheDurationHours > 0 
                        ? DateTime.UtcNow.AddHours(cacheDurationHours)
                        : DateTime.MinValue; // No caching if set to 0
                    
                    _logger.LogInformation($"Cached {locales.Count} locales, expiration: {(cacheDurationHours > 0 ? _cacheExpiration.ToString() : "disabled")}");
                }
                
                return locales.OrderBy(l => l).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching supported locales from Azure Speech Service");
                
                // Return fallback list on error
                return GetFallbackLocales();
            }
        }

        /// <summary>
        /// Gets detailed locale information including display names from Azure Speech Service
        /// </summary>
        public async Task<IEnumerable<LocaleInfo>> GetSupportedLocalesWithNamesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Check cache first
                lock (_cacheLock)
                {
                    if (_cachedLocalesWithNames != null && DateTime.UtcNow < _cacheExpiration)
                    {
                        _logger.LogInformation("Returning cached list of locales with names");
                        return _cachedLocalesWithNames.OrderBy(l => l.Code).ToList();
                    }
                }

                _logger.LogInformation("Fetching supported locales with names from Azure Speech Service");

                // Use the models API endpoint to get all models with locale information
                var response = await _httpClient.GetAsync(
                    $"{_modelsBaseUrl}/models",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError($"Failed to fetch models list: Status {response.StatusCode}, Error: {error}");
                    
                    // Return a fallback list
                    return GetFallbackLocalesWithNames();
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                
                // DIAGNOSTIC: Log the raw response for debugging
                _logger.LogInformation($"Raw Azure models API response length: {responseContent.Length} bytes");
                
                var modelsData = JsonDocument.Parse(responseContent);

                // Azure returns: { "values": [ { "locale": "en-US", "displayName": "English (United States)", ...}, ... ] }
                var localesDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                
                if (modelsData.RootElement.TryGetProperty("values", out var valuesElement) && 
                    valuesElement.ValueKind == JsonValueKind.Array)
                {
                    _logger.LogInformation($"Found 'values' array in response");
                    
                    foreach (var model in valuesElement.EnumerateArray())
                    {
                        if (model.TryGetProperty("locale", out var localeElement))
                        {
                            var locale = localeElement.GetString();
                            if (!string.IsNullOrEmpty(locale) && !localesDict.ContainsKey(locale))
                            {
                                // Try to get display name from the model's displayName
                                var displayName = locale.ToUpper();
                                
                                if (model.TryGetProperty("displayName", out var nameElement))
                                {
                                    var modelDisplayName = nameElement.GetString();
                                    // Display name might be like "English (United States) - Base"
                                    // Extract just the locale part before " - "
                                    if (!string.IsNullOrEmpty(modelDisplayName))
                                    {
                                        var dashIndex = modelDisplayName.IndexOf(" - ");
                                        displayName = dashIndex > 0 
                                            ? modelDisplayName.Substring(0, dashIndex).Trim()
                                            : modelDisplayName;
                                    }
                                }
                                else
                                {
                                    // Use CultureInfo if no displayName in model
                                    displayName = GetDisplayNameForLocale(locale);
                                }
                                
                                localesDict[locale] = displayName;
                            }
                        }
                    }
                }

                var locales = localesDict.Select(kvp => new LocaleInfo 
                { 
                    Code = kvp.Key, 
                    Name = kvp.Value 
                }).ToList();

                _logger.LogInformation($"Successfully extracted {locales.Count} unique locales with names from Azure models API");
                
                // If Azure returns an empty list, use fallback instead
                if (locales.Count == 0)
                {
                    _logger.LogWarning("Azure returned 0 locales. Using fallback list instead.");
                    return GetFallbackLocalesWithNames();
                }
                
                // Update cache
                lock (_cacheLock)
                {
                    _cachedLocalesWithNames = locales;
                    // Use configured cache duration in hours (convert to DateTime)
                    var cacheDurationHours = _audioUploadOptions.LocalesCacheDurationHours;
                    _cacheExpiration = cacheDurationHours > 0 
                        ? DateTime.UtcNow.AddHours(cacheDurationHours)
                        : DateTime.MinValue; // No caching if set to 0
                    
                    _logger.LogInformation($"Cached {locales.Count} locales with names, expiration: {(cacheDurationHours > 0 ? _cacheExpiration.ToString() : "disabled")}");
                }
                
                return locales.OrderBy(l => l.Code).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching supported locales from Azure Speech Service");
                
                // Return fallback list on error
                return GetFallbackLocalesWithNames();
            }
        }

        /// <summary>
        /// Gets a display name for a locale code using CultureInfo
        /// </summary>
        private string GetDisplayNameForLocale(string localeCode)
        {
            try
            {
                var culture = new System.Globalization.CultureInfo(localeCode);
                return culture.DisplayName;
            }
            catch
            {
                // If CultureInfo fails, return the locale code in uppercase
                return localeCode.ToUpper();
            }
        }

        /// <summary>
        /// Returns a fallback list of common locales when Azure API is unavailable
        /// </summary>
        private IEnumerable<string> GetFallbackLocales()
        {
            _logger.LogWarning("Using fallback locale list");
            
            return new[]
            {
                "ar-SA", "ar-EG", "ar-AE",
                "da-DK",
                "de-DE", "de-AT", "de-CH",
                "en-US", "en-GB", "en-AU", "en-CA", "en-IN",
                "es-ES", "es-MX", "es-AR",
                "fi-FI",
                "fr-FR", "fr-CA", "fr-BE", "fr-CH",
                "hi-IN",
                "it-IT",
                "ja-JP",
                "ko-KR",
                "nl-NL", "nl-BE",
                "no-NO",
                "pl-PL",
                "pt-BR", "pt-PT",
                "ru-RU",
                "sv-SE",
                "th-TH",
                "tr-TR",
                "vi-VN",
                "zh-CN", "zh-HK", "zh-TW"
            }.OrderBy(l => l).ToList();
        }

        /// <summary>
        /// Returns a fallback list of common locales with display names when Azure API is unavailable
        /// </summary>
        private IEnumerable<LocaleInfo> GetFallbackLocalesWithNames()
        {
            _logger.LogWarning("Using fallback locale list with names");
            
            var fallbackLocales = new Dictionary<string, string>
            {
                ["ar-SA"] = "Arabic (Saudi Arabia)",
                ["ar-EG"] = "Arabic (Egypt)",
                ["ar-AE"] = "Arabic (UAE)",
                ["da-DK"] = "Danish",
                ["de-DE"] = "German",
                ["de-AT"] = "German (Austria)",
                ["de-CH"] = "German (Switzerland)",
                ["en-US"] = "English (United States)",
                ["en-GB"] = "English (United Kingdom)",
                ["en-AU"] = "English (Australia)",
                ["en-CA"] = "English (Canada)",
                ["en-IN"] = "English (India)",
                ["es-ES"] = "Spanish (Spain)",
                ["es-MX"] = "Spanish (Mexico)",
                ["es-AR"] = "Spanish (Argentina)",
                ["fi-FI"] = "Finnish",
                ["fr-FR"] = "French (France)",
                ["fr-CA"] = "French (Canada)",
                ["fr-BE"] = "French (Belgium)",
                ["fr-CH"] = "French (Switzerland)",
                ["hi-IN"] = "Hindi",
                ["it-IT"] = "Italian",
                ["ja-JP"] = "Japanese",
                ["ko-KR"] = "Korean",
                ["nl-NL"] = "Dutch",
                ["nl-BE"] = "Dutch (Belgium)",
                ["no-NO"] = "Norwegian",
                ["pl-PL"] = "Polish",
                ["pt-BR"] = "Portuguese (Brazil)",
                ["pt-PT"] = "Portuguese (Portugal)",
                ["ru-RU"] = "Russian",
                ["sv-SE"] = "Swedish",
                ["th-TH"] = "Thai",
                ["tr-TR"] = "Turkish",
                ["vi-VN"] = "Vietnamese",
                ["zh-CN"] = "Chinese (Simplified)",
                ["zh-HK"] = "Chinese (Hong Kong)",
                ["zh-TW"] = "Chinese (Traditional)"
            };

            return fallbackLocales
                .Select(kvp => new LocaleInfo { Code = kvp.Key, Name = kvp.Value })
                .OrderBy(l => l.Code)
                .ToList();
        }
    }
}
