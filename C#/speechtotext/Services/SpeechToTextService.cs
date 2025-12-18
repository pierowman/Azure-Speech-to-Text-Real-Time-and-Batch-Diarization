using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Transcription;
using speechtotext.Models;
using speechtotext.Exceptions;

namespace speechtotext.Services
{
    public class SpeechToTextService : ISpeechToTextService
    {
        private readonly string _subscriptionKey;
        private readonly string _region;
        private readonly string? _endpoint;
        private readonly ILogger<SpeechToTextService> _logger;
        private readonly string _defaultLocale;

        public SpeechToTextService(IConfiguration configuration, ILogger<SpeechToTextService> logger)
        {
            _subscriptionKey = configuration["AzureSpeech:SubscriptionKey"] 
                ?? throw new ArgumentException("Azure Speech subscription key not found in configuration");
            _region = configuration["AzureSpeech:Region"] 
                ?? throw new ArgumentException("Azure Speech region not found in configuration");
            _endpoint = configuration["AzureSpeech:Endpoint"];
            _logger = logger;
            _defaultLocale = configuration["AudioUpload:DefaultLocale"] ?? "en-US";
        }

        public async Task<TranscriptionResult> TranscribeWithDiarizationAsync(
            string audioFilePath, 
            string? locale = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(audioFilePath))
            {
                throw new ArgumentException("Audio file path cannot be null or empty", nameof(audioFilePath));
            }

            if (!File.Exists(audioFilePath))
            {
                throw new FileNotFoundException($"Audio file not found: {audioFilePath}");
            }

            // NOTE: Real-time transcription with ConversationTranscriber currently only supports English (en-US)
            // This is a limitation of Azure Speech SDK's ConversationTranscriber API for diarization
            // For multi-language support, consider using batch transcription mode
            var selectedLocale = "en-US";

            // Check for cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();

            var result = new TranscriptionResult();
            var segments = new List<SpeakerSegment>();

            try
            {
                SpeechConfig speechConfig = CreateSpeechConfig();
                
                speechConfig.SpeechRecognitionLanguage = selectedLocale;
                _logger.LogInformation($"Using locale for real-time transcription: {selectedLocale}");
                
                // Enable diarization through request properties
                speechConfig.SetProperty(PropertyId.SpeechServiceResponse_DiarizeIntermediateResults, "true");
                speechConfig.SetProperty("DiarizeIntermediateResults", "true");

                var audioConfig = AudioConfig.FromWavFileInput(audioFilePath);

                _logger.LogInformation($"Starting transcription for file: {audioFilePath} with locale: {selectedLocale}");

                using var conversationTranscriber = new ConversationTranscriber(speechConfig, audioConfig);

                var transcriptionCompletionSource = new TaskCompletionSource<bool>();
                var fullTranscriptBuilder = new System.Text.StringBuilder();
                bool hasError = false;
                string? errorMessage = null;

                // Register cancellation callback
                using var registration = cancellationToken.Register(() =>
                {
                    _logger.LogWarning("Cancellation requested by user");
                    transcriptionCompletionSource.TrySetCanceled();
                });

                conversationTranscriber.Transcribed += (s, e) =>
                {
                    if (e.Result.Reason == ResultReason.RecognizedSpeech)
                    {
                        var speaker = e.Result.SpeakerId ?? "Unknown";
                        var text = e.Result.Text;

                        _logger.LogInformation($"TRANSCRIBED - Speaker: {speaker}, Text: {text}");

                        segments.Add(new SpeakerSegment
                        {
                            Speaker = speaker,
                            OriginalSpeaker = speaker,
                            Text = text,
                            OriginalText = text,
                            OffsetInTicks = e.Result.OffsetInTicks,
                            DurationInTicks = e.Result.Duration.Ticks
                        });

                        fullTranscriptBuilder.AppendLine($"[{speaker}]: {text}");
                    }
                    else if (e.Result.Reason == ResultReason.NoMatch)
                    {
                        var noMatchDetails = NoMatchDetails.FromResult(e.Result);
                        _logger.LogWarning($"NOMATCH: Speech could not be recognized. Reason: {noMatchDetails.Reason}");
                    }
                };

                conversationTranscriber.Transcribing += (s, e) =>
                {
                    _logger.LogDebug($"TRANSCRIBING: {e.Result.Text}");
                };

                conversationTranscriber.Canceled += (s, e) =>
                {
                    _logger.LogError($"CANCELED: Reason={e.Reason}");

                    if (e.Reason == CancellationReason.Error)
                    {
                        _logger.LogError($"CANCELED: ErrorCode={e.ErrorCode}");
                        _logger.LogError($"CANCELED: ErrorDetails={e.ErrorDetails}");
                        _logger.LogError($"CANCELED: Did you set the speech resource key and region values?");
                        hasError = true;
                        
                        // Create more specific error messages based on error code
                        errorMessage = e.ErrorCode switch
                        {
                            CancellationErrorCode.AuthenticationFailure => 
                                $"Authentication failed: Invalid subscription key or region. ErrorDetails: {e.ErrorDetails}",
                            CancellationErrorCode.BadRequest => 
                                $"Bad request: The audio format may not be supported or the file is corrupted. ErrorDetails: {e.ErrorDetails}",
                            CancellationErrorCode.ConnectionFailure => 
                                $"Connection failed: Unable to connect to Azure Speech Service. Check your internet connection and firewall. ErrorDetails: {e.ErrorDetails}",
                            CancellationErrorCode.ServiceTimeout => 
                                $"Service timeout: The request took too long. Try a smaller file or check your connection. ErrorDetails: {e.ErrorDetails}",
                            CancellationErrorCode.TooManyRequests => 
                                $"Too many requests: You have exceeded your quota or rate limit. ErrorDetails: {e.ErrorDetails}",
                            CancellationErrorCode.Forbidden => 
                                $"Forbidden: Access denied. Check your subscription permissions. ErrorDetails: {e.ErrorDetails}",
                            CancellationErrorCode.ServiceUnavailable => 
                                $"Service unavailable: Azure Speech Service is temporarily unavailable. Try again later. ErrorDetails: {e.ErrorDetails}",
                            _ => 
                                $"Error during transcription (Code: {e.ErrorCode}): {e.ErrorDetails}"
                        };
                    }
                    else
                    {
                        // User canceled or other non-error cancellation
                        errorMessage = $"Transcription canceled: {e.Reason}";
                    }

                    transcriptionCompletionSource.TrySetResult(false);
                };

                conversationTranscriber.SessionStarted += (s, e) =>
                {
                    _logger.LogInformation($"Session started: {e.SessionId}");
                };

                conversationTranscriber.SessionStopped += (s, e) =>
                {
                    _logger.LogInformation($"Session stopped: {e.SessionId}");
                    transcriptionCompletionSource.TrySetResult(true);
                };

                await conversationTranscriber.StartTranscribingAsync();
                _logger.LogInformation("Transcription started, waiting for completion...");

                bool completedSuccessfully;
                try
                {
                    completedSuccessfully = await transcriptionCompletionSource.Task;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Transcription was canceled by user");
                    await conversationTranscriber.StopTranscribingAsync();
                    // Re-throw OperationCanceledException directly instead of wrapping
                    throw;
                }

                await conversationTranscriber.StopTranscribingAsync();
                _logger.LogInformation("Transcription stopped.");

                // Check if cancellation was requested
                cancellationToken.ThrowIfCancellationRequested();

                // Filter out segments where speaker is "Unknown" and text is empty
                var filteredSegments = segments
                    .Where(s => !(s.Speaker.Equals("Unknown", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(s.Text)))
                    .ToList();

                // Assign line numbers to segments (centralized)
                for (int i = 0; i < filteredSegments.Count; i++)
                {
                    filteredSegments[i].LineNumber = i + 1;
                }

                // Rebuild the full transcript with filtered segments
                fullTranscriptBuilder.Clear();
                foreach (var segment in filteredSegments)
                {
                    fullTranscriptBuilder.AppendLine($"[{segment.Speaker}]: {segment.Text}");
                }

                _logger.LogInformation($"Filtered out {segments.Count - filteredSegments.Count} segments with Unknown speaker and empty text.");

                if (hasError)
                {
                    result.Success = false;
                    result.Message = errorMessage ?? "Unknown error occurred";
                    throw new TranscriptionException(result.Message);
                }
                else
                {
                    result.Success = true;
                    result.Segments = filteredSegments;
                    result.FullTranscript = fullTranscriptBuilder.ToString();
                    
                    // Calculate available speakers (centralized)
                    result.AvailableSpeakers = filteredSegments
                        .Select(s => s.Speaker)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct()
                        .OrderBy(s => s)
                        .ToList();
                    
                    // Calculate speaker statistics (centralized)
                    result.SpeakerStatistics = filteredSegments
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
                    
                    if (filteredSegments.Count == 0)
                    {
                        result.Message = "Transcription completed but no speech segments were detected. This could mean the audio has no speech, is too short, or diarization couldn't identify distinct speakers.";
                        _logger.LogWarning("No segments detected in transcription.");
                    }
                    else
                    {
                        result.Message = $"Transcription completed successfully with {filteredSegments.Count} segment(s)";
                        _logger.LogInformation($"Transcription completed with {filteredSegments.Count} segments.");
                    }
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Transcription operation was canceled");
                // Re-throw to let controller handle it
                throw;
            }
            catch (TranscriptionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during transcription");
                throw new TranscriptionException($"Transcription failed: {ex.Message}", ex);
            }
        }

        private SpeechConfig CreateSpeechConfig()
        {
            if (!string.IsNullOrWhiteSpace(_endpoint))
            {
                _logger.LogInformation($"Using custom endpoint: {_endpoint}");
                return SpeechConfig.FromEndpoint(new Uri(_endpoint), _subscriptionKey);
            }
            else
            {
                _logger.LogInformation($"Using region-based endpoint for region: {_region}");
                return SpeechConfig.FromSubscription(_subscriptionKey, _region);
            }
        }
    }
}
