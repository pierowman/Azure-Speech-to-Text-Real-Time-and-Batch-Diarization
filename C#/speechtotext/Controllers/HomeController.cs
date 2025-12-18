using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using speechtotext.Models;
using speechtotext.Services;
using speechtotext.Exceptions;
using speechtotext.Validation;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace speechtotext.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ISpeechToTextService _speechToTextService;
        private readonly IWebHostEnvironment _environment;
        private readonly IAudioFileValidator _audioFileValidator;
        private readonly AudioUploadOptions _audioUploadOptions;
        private readonly ITranscriptionJobService _transcriptionJobService;
        private readonly IBatchTranscriptionService _batchTranscriptionService;

        public HomeController(
            ILogger<HomeController> logger, 
            ISpeechToTextService speechToTextService,
            IWebHostEnvironment environment,
            IAudioFileValidator audioFileValidator,
            IOptions<AudioUploadOptions> audioUploadOptions,
            ITranscriptionJobService transcriptionJobService,
            IBatchTranscriptionService batchTranscriptionService)
        {
            _logger = logger;
            _speechToTextService = speechToTextService;
            _environment = environment;
            _audioFileValidator = audioFileValidator;
            _audioUploadOptions = audioUploadOptions.Value;
            _transcriptionJobService = transcriptionJobService;
            _batchTranscriptionService = batchTranscriptionService;
        }

        public IActionResult Index()
        {
            // DIAGNOSTIC: Log configuration values to help debug duplicates
            _logger.LogInformation("=== CONFIGURATION DIAGNOSTIC ===");
            _logger.LogInformation($"RealTimeAllowedExtensions: [{string.Join(", ", _audioUploadOptions.RealTimeAllowedExtensions)}]");
            _logger.LogInformation($"RealTimeAllowedExtensions Count: {_audioUploadOptions.RealTimeAllowedExtensions.Length}");
            _logger.LogInformation($"BatchAllowedExtensions: [{string.Join(", ", _audioUploadOptions.BatchAllowedExtensions)}]");
            _logger.LogInformation($"BatchAllowedExtensions Count: {_audioUploadOptions.BatchAllowedExtensions.Length}");
            _logger.LogInformation($"DefaultMinSpeakers: {_audioUploadOptions.DefaultMinSpeakers}");
            _logger.LogInformation($"DefaultMaxSpeakers: {_audioUploadOptions.DefaultMaxSpeakers}");
            _logger.LogInformation($"DefaultLocale: {_audioUploadOptions.DefaultLocale}");
            
            // Check for duplicates in the arrays
            var batchUnique = _audioUploadOptions.BatchAllowedExtensions.Distinct().ToArray();
            if (batchUnique.Length != _audioUploadOptions.BatchAllowedExtensions.Length)
            {
                _logger.LogWarning("?? DUPLICATES DETECTED in BatchAllowedExtensions!");
                var duplicates = _audioUploadOptions.BatchAllowedExtensions
                    .GroupBy(x => x)
                    .Where(g => g.Count() > 1)
                    .Select(g => $"{g.Key} (appears {g.Count()} times)");
                _logger.LogWarning($"Duplicates: {string.Join(", ", duplicates)}");
            }
            else
            {
                _logger.LogInformation("? No duplicates in BatchAllowedExtensions");
            }
            
            ViewData["ShowTranscriptionJobsTab"] = _audioUploadOptions.ShowTranscriptionJobsTab;
            ViewData["EnableBatchTranscription"] = _audioUploadOptions.EnableBatchTranscription;
            ViewData["BatchJobAutoRefreshSeconds"] = _audioUploadOptions.BatchJobAutoRefreshSeconds;
            ViewData["RealTimeAllowedExtensions"] = _audioUploadOptions.RealTimeAllowedExtensions;
            ViewData["BatchAllowedExtensions"] = _audioUploadOptions.BatchAllowedExtensions;
            ViewData["DefaultMinSpeakers"] = _audioUploadOptions.DefaultMinSpeakers;
            ViewData["DefaultMaxSpeakers"] = _audioUploadOptions.DefaultMaxSpeakers;
            ViewData["DefaultLocale"] = _audioUploadOptions.DefaultLocale;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadAndTranscribe(IFormFile audioFile, string? locale, CancellationToken cancellationToken)
        {
            try
            {
                // Validate the uploaded file (using RealTime mode)
                _audioFileValidator.ValidateFile(audioFile, TranscriptionMode.RealTime);

                // Create uploads directory in wwwroot for serving audio files
                var uploadsFolder = Path.Combine(_environment.WebRootPath, _audioUploadOptions.UploadFolderPath);
                Directory.CreateDirectory(uploadsFolder);

                // Save the uploaded file with a unique name
                var extension = Path.GetExtension(audioFile.FileName).ToLowerInvariant();
                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await audioFile.CopyToAsync(stream, cancellationToken);
                }

                _logger.LogInformation($"File uploaded: {filePath}");

                // Use provided locale or fall back to configured default
                var selectedLocale = string.IsNullOrWhiteSpace(locale) 
                    ? _audioUploadOptions.DefaultLocale 
                    : locale;

                _logger.LogInformation($"Real-time transcription using locale: {selectedLocale}");

                // Process the audio file with Azure Speech Service
                var result = await _speechToTextService.TranscribeWithDiarizationAsync(filePath, selectedLocale, cancellationToken);

                // Add the audio file URL to the result
                result.AudioFileUrl = $"/{_audioUploadOptions.UploadFolderPath}/{uniqueFileName}";

                // Create original record (unmodified) before any modifications
                result.GoldenRecordJsonData = JsonSerializer.Serialize(result, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });

                // Serialize the current result to JSON (this will be the working copy)
                result.RawJsonData = JsonSerializer.Serialize(result, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });

                return Json(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Upload and transcribe operation was canceled by user");
                return Json(new { success = false, message = "Transcription was canceled" });
            }
            catch (InvalidAudioFileException ex)
            {
                _logger.LogWarning(ex, "Invalid audio file uploaded");
                return Json(new { success = false, message = $"Invalid audio file: {ex.Message}" });
            }
            catch (TranscriptionException ex)
            {
                _logger.LogError(ex, "Error during transcription");
                
                // Provide more specific error messages based on the exception
                var errorMessage = ex.Message;
                
                if (ex.Message.Contains("Subscription") || ex.Message.Contains("key") || ex.Message.Contains("credentials"))
                {
                    errorMessage = "Authentication failed: Your Azure Speech Service credentials may be invalid or expired. Please check your subscription key and region in appsettings.json.";
                }
                else if (ex.Message.Contains("audio") || ex.Message.Contains("format"))
                {
                    errorMessage = "Audio format error: The audio file may be in an unsupported format or corrupted. Please use WAV, MP3, OGG, or FLAC format.";
                }
                else if (ex.Message.Contains("timeout") || ex.Message.Contains("network"))
                {
                    errorMessage = "Network error: Unable to connect to Azure Speech Service. Please check your internet connection and firewall settings.";
                }
                else if (ex.Message.Contains("quota") || ex.Message.Contains("throttle"))
                {
                    errorMessage = "Quota exceeded: You have reached the limit of your Azure Speech Service tier. Please check your Azure Portal for usage details.";
                }
                
                return Json(new { success = false, message = errorMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing audio file");
                return Json(new { success = false, message = $"An unexpected error occurred: {ex.Message}. Please check application logs for details." });
            }
        }

        [HttpPost]
        public IActionResult DownloadRawJson([FromBody] DownloadRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.JsonData))
                {
                    return BadRequest("No data provided");
                }

                var bytes = Encoding.UTF8.GetBytes(request.JsonData);
                var fileName = $"transcription_raw_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                
                return File(bytes, "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating raw JSON download");
                return StatusCode(500, "Error generating download");
            }
        }

        [HttpPost]
        public IActionResult DownloadGoldenRecord([FromBody] DownloadRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.GoldenRecordJsonData))
                {
                    return BadRequest("No original record data provided");
                }

                var bytes = Encoding.UTF8.GetBytes(request.GoldenRecordJsonData);
                var fileName = $"transcription_original_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                
                return File(bytes, "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating original record download");
                return StatusCode(500, "Error generating download");
            }
        }

        [HttpPost]
        public IActionResult UpdateSpeakerNames([FromBody] UpdateSpeakersRequest request)
        {
            try
            {
                if (request.Segments == null || !request.Segments.Any())
                {
                    return BadRequest("No segments provided");
                }

                // Validate segments
                foreach (var segment in request.Segments)
                {
                    // Validate speaker name
                    if (string.IsNullOrWhiteSpace(segment.Speaker))
                    {
                        return BadRequest("Speaker name cannot be empty or whitespace");
                    }

                    if (segment.Speaker.Length > 100)
                    {
                        return BadRequest("Speaker name is too long (max 100 characters)");
                    }

                    // Prevent XSS attacks
                    if (segment.Speaker.Contains('<') || segment.Speaker.Contains('>'))
                    {
                        return BadRequest("Speaker name contains invalid characters");
                    }

                    // Validate transcript text
                    if (string.IsNullOrWhiteSpace(segment.Text))
                    {
                        return BadRequest("Transcript text cannot be empty or whitespace");
                    }

                    if (segment.Text.Length > 10000)
                    {
                        return BadRequest("Transcript text is too long (max 10000 characters)");
                    }

                    // Validate timing values
                    if (segment.OffsetInTicks < 0 || segment.DurationInTicks < 0)
                    {
                        return BadRequest("Invalid timing values");
                    }
                }

                // Assign line numbers to segments (centralized)
                for (int i = 0; i < request.Segments.Count; i++)
                {
                    request.Segments[i].LineNumber = i + 1;
                }

                // Rebuild full transcript with updated speaker names
                var sb = new StringBuilder();
                foreach (var segment in request.Segments)
                {
                    sb.AppendLine($"[{segment.Speaker}]: {segment.Text}");
                }

                var result = new TranscriptionResult
                {
                    Success = true,
                    Message = $"Updated speaker names for {request.Segments.Count} segments",
                    Segments = request.Segments,
                    FullTranscript = sb.ToString(),
                    AudioFileUrl = request.AudioFileUrl,
                    GoldenRecordJsonData = request.GoldenRecordJsonData,
                    AuditLog = request.AuditLog, // Pass through audit log from client
                    
                    // Recalculate available speakers (centralized)
                    AvailableSpeakers = request.Segments
                        .Select(s => s.Speaker)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct()
                        .OrderBy(s => s)
                        .ToList(),
                    
                    // Recalculate speaker statistics (centralized)
                    SpeakerStatistics = request.Segments
                        .GroupBy(s => s.Speaker)
                        .Select(g => new SpeakerInfo
                        {
                            Name = g.Key,
                            SegmentCount = g.Count(),
                            TotalSpeakTimeSeconds = g.Sum(s => s.EndTimeInSeconds - s.StartTimeInSeconds),
                            FirstAppearanceSeconds = g.Min(s => s.StartTimeInSeconds)
                        })
                        .OrderBy(s => s.FirstAppearanceSeconds)
                        .ToList()
                };

                // Serialize the updated result
                result.RawJsonData = JsonSerializer.Serialize(result, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating speaker names");
                return StatusCode(500, "Error updating speaker names");
            }
        }

        [HttpPost]
        public IActionResult DownloadReadableText([FromBody] DownloadRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.FullTranscript))
                {
                    return BadRequest("No transcript data provided");
                }

                // Create Word document in memory
                using var memoryStream = new MemoryStream();
                using (var wordDocument = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document))
                {
                    // Add main document part
                    var mainPart = wordDocument.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    var body = mainPart.Document.AppendChild(new Body());

                    // Add title
                    var titleParagraph = body.AppendChild(new Paragraph());
                    var titleRun = titleParagraph.AppendChild(new Run());
                    var titleRunProperties = titleRun.AppendChild(new RunProperties());
                    titleRunProperties.AppendChild(new Bold());
                    titleRunProperties.AppendChild(new FontSize { Val = "32" }); // 16pt
                    titleRun.AppendChild(new Text("Speech to Text Transcription with Diarization"));

                    // Add separator
                    var separatorParagraph = body.AppendChild(new Paragraph());
                    var separatorRun = separatorParagraph.AppendChild(new Run());
                    separatorRun.AppendChild(new Text("=".PadRight(50, '=')));

                    // Add empty line
                    body.AppendChild(new Paragraph());

                    // Add generated date
                    var dateParagraph = body.AppendChild(new Paragraph());
                    var dateRun = dateParagraph.AppendChild(new Run());
                    dateRun.AppendChild(new Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"));

                    // Add empty lines
                    body.AppendChild(new Paragraph());
                    body.AppendChild(new Paragraph());

                    // Add transcript header
                    var headerParagraph = body.AppendChild(new Paragraph());
                    var headerRun = headerParagraph.AppendChild(new Run());
                    var headerRunProperties = headerRun.AppendChild(new RunProperties());
                    headerRunProperties.AppendChild(new Bold());
                    headerRunProperties.AppendChild(new FontSize { Val = "28" }); // 14pt
                    headerRun.AppendChild(new Text("TRANSCRIPT:"));

                    // Add separator
                    var headerSeparatorParagraph = body.AppendChild(new Paragraph());
                    var headerSeparatorRun = headerSeparatorParagraph.AppendChild(new Run());
                    headerSeparatorRun.AppendChild(new Text("?".PadRight(50, '?')));

                    // Add empty line
                    body.AppendChild(new Paragraph());

                    // If segments are provided, format them nicely
                    if (request.Segments != null && request.Segments.Count > 0)
                    {
                        foreach (var segment in request.Segments)
                        {
                            // Use centralized line number from segment data
                            var lineNumber = segment.LineNumber;

                            // Add line number, timestamp and speaker (bold)
                            var speakerParagraph = body.AppendChild(new Paragraph());
                            
                            // Line number
                            var lineNumberRun = speakerParagraph.AppendChild(new Run());
                            var lineNumberRunProperties = lineNumberRun.AppendChild(new RunProperties());
                            lineNumberRunProperties.AppendChild(new Bold());
                            lineNumberRunProperties.AppendChild(new Color { Val = "808080" }); // Gray color
                            lineNumberRun.AppendChild(new Text($"#{lineNumber}"));
                            
                            // Add space between line number and timestamp
                            var spaceRun = speakerParagraph.AppendChild(new Run());
                            spaceRun.AppendChild(new Text(" "));
                            
                            // Timestamp and speaker
                            var timestampRun = speakerParagraph.AppendChild(new Run());
                            var timestampRunProperties = timestampRun.AppendChild(new RunProperties());
                            timestampRunProperties.AppendChild(new Bold());
                            timestampRun.AppendChild(new Text($"[{segment.UIFormattedStartTime}] {segment.Speaker}:"));

                            // Add transcript text (indented)
                            var textParagraph = body.AppendChild(new Paragraph());
                            var textParagraphProperties = textParagraph.AppendChild(new ParagraphProperties());
                            var indentation = textParagraphProperties.AppendChild(new Indentation());
                            indentation.Left = "720"; // 0.5 inch indent
                            var textRun = textParagraph.AppendChild(new Run());
                            textRun.AppendChild(new Text(segment.Text));

                            // Add empty line between segments
                            body.AppendChild(new Paragraph());
                        }
                    }
                    else
                    {
                        // Fallback to full transcript
                        var lines = request.FullTranscript.Split('\n');
                        foreach (var line in lines)
                        {
                            var paragraph = body.AppendChild(new Paragraph());
                            var run = paragraph.AppendChild(new Run());
                            run.AppendChild(new Text(line));
                        }
                    }

                    mainPart.Document.Save();
                }

                memoryStream.Position = 0;
                var fileBytes = memoryStream.ToArray();
                var fileName = $"transcription_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
                
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Word document download");
                return StatusCode(500, "Error generating download");
            }
        }

        [HttpPost]
        public IActionResult DownloadReadableTextWithTracking([FromBody] DownloadRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.FullTranscript))
                {
                    return BadRequest("No transcript data provided");
                }

                // Create Word document in memory
                using var memoryStream = new MemoryStream();
                using (var wordDocument = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document))
                {
                    // Add main document part
                    var mainPart = wordDocument.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    var body = mainPart.Document.AppendChild(new Body());

                    // Add title
                    var titleParagraph = body.AppendChild(new Paragraph());
                    var titleRun = titleParagraph.AppendChild(new Run());
                    var titleRunProperties = titleRun.AppendChild(new RunProperties());
                    titleRunProperties.AppendChild(new Bold());
                    titleRunProperties.AppendChild(new FontSize { Val = "32" }); // 16pt
                    titleRun.AppendChild(new Text("Speech to Text Transcription with Change Tracking"));

                    // Add separator
                    var separatorParagraph = body.AppendChild(new Paragraph());
                    var separatorRun = separatorParagraph.AppendChild(new Run());
                    separatorRun.AppendChild(new Text("=".PadRight(60, '=')));

                    // Add empty line
                    body.AppendChild(new Paragraph());

                    // Add generated date
                    var dateParagraph = body.AppendChild(new Paragraph());
                    var dateRun = dateParagraph.AppendChild(new Run());
                    dateRun.AppendChild(new Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"));

                    // Add legend
                    var legendParagraph = body.AppendChild(new Paragraph());
                    var legendRun = legendParagraph.AppendChild(new Run());
                    var legendRunProperties = legendRun.AppendChild(new RunProperties());
                    legendRunProperties.AppendChild(new Italic());
                    legendRunProperties.AppendChild(new Color { Val = "666666" }); // Dark gray
                    legendRun.AppendChild(new Text("Legend: "));
                    
                    var greenRun = legendParagraph.AppendChild(new Run());
                    var greenRunProperties = greenRun.AppendChild(new RunProperties());
                    greenRunProperties.AppendChild(new Color { Val = "70AD47" }); // Green
                    greenRun.AppendChild(new Text("[Current/Edited] "));
                    
                    var redRun = legendParagraph.AppendChild(new Run());
                    var redRunProperties = redRun.AppendChild(new RunProperties());
                    redRunProperties.AppendChild(new Color { Val = "C00000" }); // Red
                    redRun.AppendChild(new Text("[Original]"));

                    // Add empty lines
                    body.AppendChild(new Paragraph());
                    body.AppendChild(new Paragraph());

                    // Add transcript header
                    var headerParagraph = body.AppendChild(new Paragraph());
                    var headerRun = headerParagraph.AppendChild(new Run());
                    var headerRunProperties = headerRun.AppendChild(new RunProperties());
                    headerRunProperties.AppendChild(new Bold());
                    headerRunProperties.AppendChild(new FontSize { Val = "28" }); // 14pt
                    headerRun.AppendChild(new Text("TRANSCRIPT WITH CHANGE TRACKING:"));

                    // Add separator
                    var headerSeparatorParagraph = body.AppendChild(new Paragraph());
                    var headerSeparatorRun = headerSeparatorParagraph.AppendChild(new Run());
                    headerSeparatorRun.AppendChild(new Text("?".PadRight(60, '?')));

                    // Add empty line
                    body.AppendChild(new Paragraph());

                    // If segments are provided, format them with change tracking
                    if (request.Segments != null && request.Segments.Count > 0)
                    {
                        foreach (var segment in request.Segments)
                        {
                            // Use centralized line number from segment data
                            var lineNumber = segment.LineNumber;

                            // Add line number, timestamp and speaker (bold)
                            var speakerParagraph = body.AppendChild(new Paragraph());
                            var speakerParagraphProperties = speakerParagraph.AppendChild(new ParagraphProperties());
                            var speakerSpacing = speakerParagraphProperties.AppendChild(new SpacingBetweenLines());
                            speakerSpacing.After = "120"; // 6pt space after (120 twentieths of a point)
                            
                            // Line number
                            var lineNumberRun = speakerParagraph.AppendChild(new Run());
                            var lineNumberRunProperties = lineNumberRun.AppendChild(new RunProperties());
                            lineNumberRunProperties.AppendChild(new Bold());
                            lineNumberRunProperties.AppendChild(new Color { Val = "808080" }); // Gray color
                            lineNumberRun.AppendChild(new Text($"#{lineNumber}"));
                            
                            // Add space
                            speakerParagraph.AppendChild(new Run(new Text("  "))); // Two spaces for better separation
                            
                            // Timestamp
                            var timestampRun = speakerParagraph.AppendChild(new Run());
                            var timestampRunProperties = timestampRun.AppendChild(new RunProperties());
                            timestampRunProperties.AppendChild(new Bold());
                            timestampRun.AppendChild(new Text($"[{segment.UIFormattedStartTime}]  ")); // Two spaces after timestamp

                            // Current speaker (green if changed)
                            var speakerRun = speakerParagraph.AppendChild(new Run());
                            var speakerRunProperties = speakerRun.AppendChild(new RunProperties());
                            speakerRunProperties.AppendChild(new Bold());
                            if (segment.SpeakerWasChanged)
                            {
                                speakerRunProperties.AppendChild(new Color { Val = "70AD47" }); // Green for changed
                            }
                            speakerRun.AppendChild(new Text($"{segment.Speaker}:"));

                            // Show original speaker if changed
                            if (segment.SpeakerWasChanged && !string.IsNullOrEmpty(segment.OriginalSpeaker))
                            {
                                speakerParagraph.AppendChild(new Run(new Text("  "))); // Two spaces
                                var originalSpeakerRun = speakerParagraph.AppendChild(new Run());
                                var originalSpeakerRunProperties = originalSpeakerRun.AppendChild(new RunProperties());
                                originalSpeakerRunProperties.AppendChild(new Italic());
                                originalSpeakerRunProperties.AppendChild(new Color { Val = "C00000" }); // Red for original
                                originalSpeakerRun.AppendChild(new Text($"(Was: {segment.OriginalSpeaker})"));
                            }

                            // Add current transcript text (indented, green if changed)
                            var textParagraph = body.AppendChild(new Paragraph());
                            var textParagraphProperties = textParagraph.AppendChild(new ParagraphProperties());
                            var textIndentation = textParagraphProperties.AppendChild(new Indentation());
                            textIndentation.Left = "720"; // 0.5 inch indent
                            var textSpacing = textParagraphProperties.AppendChild(new SpacingBetweenLines());
                            textSpacing.After = "120"; // 6pt space after
                            textSpacing.Line = "276"; // 1.15 line spacing (276 twentieths of a point)
                            textSpacing.LineRule = LineSpacingRuleValues.Auto;
                            
                            var textRun = textParagraph.AppendChild(new Run());
                            var textRunProperties = textRun.AppendChild(new RunProperties());
                            if (segment.TextWasChanged)
                            {
                                textRunProperties.AppendChild(new Color { Val = "70AD47" }); // Green for changed
                            }
                            textRun.AppendChild(new Text(segment.Text));

                            // Show original text if changed
                            if (segment.TextWasChanged && !string.IsNullOrEmpty(segment.OriginalText))
                            {
                                var originalTextParagraph = body.AppendChild(new Paragraph());
                                var originalTextParagraphProperties = originalTextParagraph.AppendChild(new ParagraphProperties());
                                var originalTextIndentation = originalTextParagraphProperties.AppendChild(new Indentation());
                                originalTextIndentation.Left = "720"; // 0.5 inch indent
                                var originalTextSpacing = originalTextParagraphProperties.AppendChild(new SpacingBetweenLines());
                                originalTextSpacing.After = "240"; // 12pt space after (extra space after original text)
                                
                                var originalTextRun = originalTextParagraph.AppendChild(new Run());
                                var originalTextRunProperties = originalTextRun.AppendChild(new RunProperties());
                                originalTextRunProperties.AppendChild(new Italic());
                                originalTextRunProperties.AppendChild(new Color { Val = "C00000" }); // Red for original
                                originalTextRunProperties.AppendChild(new FontSize { Val = "20" }); // Slightly smaller
                                originalTextRun.AppendChild(new Text($"[Original: {segment.OriginalText}]"));
                            }

                            // Add empty line between segments (with spacing)
                            var spacerParagraph = body.AppendChild(new Paragraph());
                            var spacerProperties = spacerParagraph.AppendChild(new ParagraphProperties());
                            var spacerSpacing = spacerProperties.AppendChild(new SpacingBetweenLines());
                            spacerSpacing.After = "240"; // 12pt space after for visual separation
                        }
                    }
                    else
                    {
                        // Fallback to full transcript
                        var lines = request.FullTranscript.Split('\n');
                        foreach (var line in lines)
                        {
                            var paragraph = body.AppendChild(new Paragraph());
                            var run = paragraph.AppendChild(new Run());
                            run.AppendChild(new Text(line));
                        }
                    }

                    mainPart.Document.Save();
                }

                memoryStream.Position = 0;
                var fileBytes = memoryStream.ToArray();
                var fileName = $"transcription_with_tracking_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
                
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Word document with tracking");
                return StatusCode(500, "Error generating download");
            }
        }
        
        [HttpPost]
        public IActionResult DownloadAuditLog([FromBody] DownloadRequest request)
        {
            try
            {
                if (request.AuditLog == null || !request.AuditLog.Any())
                {
                    return BadRequest("No audit log data available");
                }

                // Create Word document in memory
                using var memoryStream = new MemoryStream();
                using (var wordDocument = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document))
                {
                    // Add main document part
                    var mainPart = wordDocument.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    var body = mainPart.Document.AppendChild(new Body());

                    // Add title
                    var titleParagraph = body.AppendChild(new Paragraph());
                    var titleRun = titleParagraph.AppendChild(new Run());
                    var titleRunProperties = titleRun.AppendChild(new RunProperties());
                    titleRunProperties.AppendChild(new Bold());
                    titleRunProperties.AppendChild(new FontSize { Val = "32" }); // 16pt
                    titleRun.AppendChild(new Text("Transcription Edit History - Audit Log"));

                    // Add separator
                    var separatorParagraph = body.AppendChild(new Paragraph());
                    var separatorRun = separatorParagraph.AppendChild(new Run());
                    separatorRun.AppendChild(new Text("=".PadRight(70, '=')));

                    // Add empty line
                    body.AppendChild(new Paragraph());

                    // Add generated date
                    var dateParagraph = body.AppendChild(new Paragraph());
                    var dateRun = dateParagraph.AppendChild(new Run());
                    dateRun.AppendChild(new Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"));

                    // Add summary stats
                    var statsParagraph = body.AppendChild(new Paragraph());
                    var statsRun = statsParagraph.AppendChild(new Run());
                    statsRun.AppendChild(new Text($"Total Changes: {request.AuditLog.Count}"));

                    // Add empty lines
                    body.AppendChild(new Paragraph());
                    body.AppendChild(new Paragraph());

                    // Add audit log header
                    var headerParagraph = body.AppendChild(new Paragraph());
                    var headerRun = headerParagraph.AppendChild(new Run());
                    var headerRunProperties = headerRun.AppendChild(new RunProperties());
                    headerRunProperties.AppendChild(new Bold());
                    headerRunProperties.AppendChild(new FontSize { Val = "28" }); // 14pt
                    headerRun.AppendChild(new Text("EDIT HISTORY:"));

                    // Add separator
                    var headerSeparatorParagraph = body.AppendChild(new Paragraph());
                    var headerSeparatorRun = headerSeparatorParagraph.AppendChild(new Run());
                    headerSeparatorRun.AppendChild(new Text("?".PadRight(70, '?')));

                    // Add empty line
                    body.AppendChild(new Paragraph());

                    // Group changes by type for better organization
                    var groupedChanges = request.AuditLog
                        .OrderBy(c => c.Timestamp)
                        .GroupBy(c => c.ChangeType);

                    foreach (var group in groupedChanges)
                    {
                        // Add section header for each change type
                        var sectionHeaderParagraph = body.AppendChild(new Paragraph());
                        var sectionHeaderRun = sectionHeaderParagraph.AppendChild(new Run());
                        var sectionHeaderRunProperties = sectionHeaderRun.AppendChild(new RunProperties());
                        sectionHeaderRunProperties.AppendChild(new Bold());
                        sectionHeaderRunProperties.AppendChild(new FontSize { Val = "24" }); // 12pt
                        sectionHeaderRunProperties.AppendChild(new Color { Val = "2E75B6" }); // Blue
                        
                        string sectionTitle = group.Key switch
                        {
                            "SpeakerEdit" => "SPEAKER NAME EDITS",
                            "TextEdit" => "TRANSCRIPT TEXT EDITS",
                            "SpeakerReassignment" => "SPEAKER REASSIGNMENTS",
                            _ => group.Key.ToUpper()
                        };
                        sectionHeaderRun.AppendChild(new Text(sectionTitle));

                        body.AppendChild(new Paragraph()); // Empty line

                        // Add each change in the group
                        foreach (var change in group)
                        {
                            // Change entry container
                            var changeParagraph = body.AppendChild(new Paragraph());
                            var changeParagraphProperties = changeParagraph.AppendChild(new ParagraphProperties());
                            var changeSpacing = changeParagraphProperties.AppendChild(new SpacingBetweenLines());
                            changeSpacing.After = "120"; // 6pt space after
                            
                            // Timestamp
                            var timestampRun = changeParagraph.AppendChild(new Run());
                            var timestampRunProperties = timestampRun.AppendChild(new RunProperties());
                            timestampRunProperties.AppendChild(new Bold());
                            timestampRunProperties.AppendChild(new Color { Val = "70AD47" }); // Green
                            timestampRun.AppendChild(new Text($"[{change.Timestamp:yyyy-MM-dd HH:mm:ss}]  ")); // Two spaces after

                            // Line number if available
                            if (change.LineNumber.HasValue)
                            {
                                var lineNumRun = changeParagraph.AppendChild(new Run());
                                var lineNumRunProperties = lineNumRun.AppendChild(new RunProperties());
                                lineNumRunProperties.AppendChild(new Color { Val = "808080" }); // Gray
                                lineNumRun.AppendChild(new Text($"Line #{change.LineNumber}:  ")); // Two spaces after
                            }

                            // Change description
                            var descRun = changeParagraph.AppendChild(new Run());
                            descRun.AppendChild(new Text(GetChangeDescription(change)));

                            // Details paragraph (indented)
                            var detailsParagraph = body.AppendChild(new Paragraph());
                            var detailsParagraphProperties = detailsParagraph.AppendChild(new ParagraphProperties());
                            var detailsIndentation = detailsParagraphProperties.AppendChild(new Indentation());
                            detailsIndentation.Left = "720"; // 0.5 inch indent
                            var detailsSpacing = detailsParagraphProperties.AppendChild(new SpacingBetweenLines());
                            detailsSpacing.After = "120"; // 6pt space after
                            detailsSpacing.Line = "276"; // 1.15 line spacing
                            detailsSpacing.LineRule = LineSpacingRuleValues.Auto;

                            // Old value
                            var oldValueRun = detailsParagraph.AppendChild(new Run());
                            var oldValueRunProperties = oldValueRun.AppendChild(new RunProperties());
                            oldValueRunProperties.AppendChild(new Color { Val = "C00000" }); // Red
                            oldValueRun.AppendChild(new Text($"FROM:  {change.OldValue ?? "N/A"}")); // Two spaces after FROM:

                            detailsParagraph.AppendChild(new Run(new Text("  ?  "))); // Spaces around arrow

                            // New value
                            var newValueRun = detailsParagraph.AppendChild(new Run());
                            var newValueRunProperties = newValueRun.AppendChild(new RunProperties());
                            newValueRunProperties.AppendChild(new Color { Val = "70AD47" }); // Green
                            newValueRun.AppendChild(new Text($"TO:  {change.NewValue ?? "N/A"}")); // Two spaces after TO:

                            // Additional info if available
                            if (!string.IsNullOrEmpty(change.AdditionalInfo))
                            {
                                var infoParagraph = body.AppendChild(new Paragraph());
                                var infoParagraphProperties = infoParagraph.AppendChild(new ParagraphProperties());
                                var infoIndentation = infoParagraphProperties.AppendChild(new Indentation());
                                infoIndentation.Left = "720"; // 0.5 inch indent
                                var infoSpacing = infoParagraphProperties.AppendChild(new SpacingBetweenLines());
                                infoSpacing.After = "240"; // 12pt space after (extra space for separation)

                                var infoRun = infoParagraph.AppendChild(new Run());
                                var infoRunProperties = infoRun.AppendChild(new RunProperties());
                                infoRunProperties.AppendChild(new Italic());
                                infoRunProperties.AppendChild(new Color { Val = "7F7F7F" }); // Gray
                                infoRun.AppendChild(new Text($"({change.AdditionalInfo})"));
                            }

                            // Add empty line between changes (with extra spacing)
                            var spacerParagraph = body.AppendChild(new Paragraph());
                            var spacerProperties = spacerParagraph.AppendChild(new ParagraphProperties());
                            var spacerSpacing = spacerProperties.AppendChild(new SpacingBetweenLines());
                            spacerSpacing.After = "240"; // 12pt space after for visual separation
                        }

                        // Add extra space between sections
                        body.AppendChild(new Paragraph());
                    }

                    // Add summary at the end
                    body.AppendChild(new Paragraph());
                    var summaryHeaderParagraph = body.AppendChild(new Paragraph());
                    var summaryHeaderRun = summaryHeaderParagraph.AppendChild(new Run());
                    var summaryHeaderRunProperties = summaryHeaderRun.AppendChild(new RunProperties());
                    summaryHeaderRunProperties.AppendChild(new Bold());
                    summaryHeaderRunProperties.AppendChild(new FontSize { Val = "24" }); // 12pt
                    summaryHeaderRun.AppendChild(new Text("SUMMARY"));

                    body.AppendChild(new Paragraph());

                    // Count by type
                    foreach (var group in groupedChanges)
                    {
                        var summaryParagraph = body.AppendChild(new Paragraph());
                        var summaryRun = summaryParagraph.AppendChild(new Run());
                        string typeName = group.Key switch
                        {
                            "SpeakerEdit" => "Speaker Name Edits",
                            "TextEdit" => "Transcript Text Edits",
                            "SpeakerReassignment" => "Speaker Reassignments",
                            _ => group.Key
                        };
                        summaryRun.AppendChild(new Text($"• {typeName}: {group.Count()}"));
                    }

                    mainPart.Document.Save();
                }

                memoryStream.Position = 0;
                var fileBytes = memoryStream.ToArray();
                var fileName = $"audit_log_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
                
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating audit log download");
                return StatusCode(500, "Error generating download");
            }
        }

        private string GetChangeDescription(AuditLogEntry change)
        {
            return change.ChangeType switch
            {
                "SpeakerEdit" => "Speaker name changed",
                "TextEdit" => "Transcript text edited",
                "SpeakerReassignment" => "Speaker reassigned",
                _ => "Change made"
            };
        }

        [HttpGet]
        public async Task<IActionResult> GetTranscriptionJob(string jobId, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(jobId))
                {
                    return BadRequest(new { success = false, message = "Job ID is required" });
                }

                _logger.LogInformation("Fetching transcription job details: {JobId}", jobId);
                
                var job = await _transcriptionJobService.GetTranscriptionJobAsync(jobId, cancellationToken);
                
                if (job == null)
                {
                    return Json(new { success = false, message = "Job not found" });
                }
                
                _logger.LogInformation("Successfully retrieved job details for {JobId}", jobId);
                
                return Json(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving transcription job {JobId}", jobId);
                return Json(new { success = false, message = $"Error retrieving job: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTranscriptionJobs(CancellationToken cancellationToken)
        {
            try
            {
                var jobs = await _transcriptionJobService.GetTranscriptionJobsAsync(cancellationToken);
                
                _logger.LogInformation($"Retrieved {jobs.Count} jobs from Azure Speech Service");
                
                // Fetch locale display names
                Dictionary<string, string> localeNameMap = new();
                try
                {
                    var locales = await _batchTranscriptionService.GetSupportedLocalesWithNamesAsync(cancellationToken);
                    // Use case-insensitive comparer to avoid case mismatch issues
                    localeNameMap = locales.ToDictionary(l => l.Code, l => l.Name, StringComparer.OrdinalIgnoreCase);
                    _logger.LogInformation($"Loaded {localeNameMap.Count} locale display names (case-insensitive)");
                    
                    // Log a few samples for debugging
                    if (localeNameMap.Count > 0)
                    {
                        var samples = localeNameMap.Take(3);
                        foreach (var kvp in samples)
                        {
                            _logger.LogInformation($"  Sample locale: {kvp.Key} = '{kvp.Value}'");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load locale display names, will use locale codes instead");
                }
                
                // Log details of first job for debugging
                if (jobs.Count > 0)
                {
                    var firstJob = jobs[0];
                    _logger.LogInformation($"First job sample:");
                    _logger.LogInformation($"  - ID: {firstJob.Id}");
                    _logger.LogInformation($"  - DisplayName: {firstJob.DisplayName}");
                    _logger.LogInformation($"  - Status: {firstJob.Status}");
                    _logger.LogInformation($"  - Locale: {firstJob.Locale}");
                    _logger.LogInformation($"  - Locale in map: {localeNameMap.ContainsKey(firstJob.Locale ?? "")}");
                    if (!string.IsNullOrEmpty(firstJob.Locale) && localeNameMap.ContainsKey(firstJob.Locale))
                    {
                        _logger.LogInformation($"  - Mapped name: '{localeNameMap[firstJob.Locale]}'");
                    }
                    _logger.LogInformation($"  - Files count: {firstJob.Files?.Count ?? 0}");
                    _logger.LogInformation($"  - Properties is null: {firstJob.Properties == null}");
                    if (firstJob.Properties != null)
                    {
                        _logger.LogInformation($"  - Duration: {firstJob.Properties.Duration}");
                        _logger.LogInformation($"  - SucceededCount: {firstJob.Properties.SucceededCount}");
                        _logger.LogInformation($"  - FailedCount: {firstJob.Properties.FailedCount}");
                    }
                    _logger.LogInformation($"  - FormattedDuration: {firstJob.FormattedDuration}");
                    _logger.LogInformation($"  - TotalFileCount: {firstJob.TotalFileCount}");
                }
                
                // Explicitly map jobs to include computed properties and locale display name
                var jobsWithComputedProps = jobs.Select(j => new
                {
                    j.Id,
                    j.DisplayName,
                    j.Status,
                    j.CreatedDateTime,
                    j.LastActionDateTime,
                    j.Error,
                    j.Files,
                    j.ResultsUrl,
                    j.Properties,
                    j.Locale,
                    // Add formatted locale display name
                    FormattedLocale = !string.IsNullOrEmpty(j.Locale) && localeNameMap.ContainsKey(j.Locale)
                        ? localeNameMap[j.Locale]
                        : j.Locale?.ToUpper() ?? "N/A",
                    // Explicitly include computed properties
                    FormattedDuration = j.FormattedDuration,
                    TotalFileCount = j.TotalFileCount
                }).ToList();
                
                // Log first job's formatted locale for debugging
                if (jobsWithComputedProps.Count > 0)
                {
                    _logger.LogInformation($"First job FormattedLocale: '{jobsWithComputedProps[0].FormattedLocale}'");
                }
                
                _logger.LogInformation($"Returning {jobsWithComputedProps.Count} jobs to client");
                
                return Json(new { success = true, jobs = jobsWithComputedProps });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving transcription jobs");
                return Json(new { success = false, message = $"Error retrieving jobs: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CancelTranscriptionJob([FromBody] CancelJobRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(request.JobId))
                {
                    return BadRequest(new { success = false, message = "Job ID is required" });
                }

                var result = await _transcriptionJobService.CancelTranscriptionJobAsync(request.JobId, cancellationToken);
                
                if (result)
                {
                    return Json(new { success = true, message = "Job canceled successfully" });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to cancel job" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error canceling transcription job {request.JobId}");
                return Json(new { success = false, message = $"Error canceling job: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBatchTranscriptionResults(string jobId, int? fileIndex, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(jobId))
                {
                    return BadRequest(new { success = false, message = "Job ID is required" });
                }

                _logger.LogInformation($"Fetching batch transcription results for job: {jobId}, fileIndex: {fileIndex}");

                BatchTranscriptionResult? result;
                
                // If fileIndex is specified, get results for that specific file
                if (fileIndex.HasValue)
                {
                    result = await _transcriptionJobService.GetTranscriptionResultsByFileAsync(jobId, fileIndex.Value, cancellationToken);
                }
                else
                {
                    // Otherwise get all results (combined)
                    result = await _transcriptionJobService.GetTranscriptionResultsAsync(jobId, cancellationToken);
                }
                
                if (result == null || !result.Success)
                {
                    return Json(new { 
                        success = false, 
                        message = result?.Message ?? "Failed to retrieve results" 
                    });
                }
                
                _logger.LogInformation($"Successfully retrieved results for job {jobId} with {result.Segments.Count} segments");
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving batch results for job {jobId}");
                return Json(new { 
                    success = false, 
                    message = $"Error: {ex.Message}" 
                });
            }
        }

        [HttpGet]
        public IActionResult GetValidationRules(TranscriptionMode mode)
        {
            try
            {
                var rules = _audioFileValidator.GetValidationRulesSummary(mode);
                return Json(new { success = true, rules });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting validation rules for mode {mode}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SubmitBatchTranscription(
            IFormFileCollection audioFiles,
            string? jobName,
            int minSpeakers,
            int maxSpeakers,
            string? locale,
            CancellationToken cancellationToken)
        {
            try
            {
                // Debug logging
                _logger.LogInformation("SubmitBatchTranscription called");
                _logger.LogInformation("audioFiles parameter is: " + (audioFiles == null ? "NULL" : "NOT NULL"));
                _logger.LogInformation("audioFiles count: " + (audioFiles?.Count ?? 0));
                _logger.LogInformation("Request.Form.Files count: " + (Request.Form.Files?.Count ?? 0));
                _logger.LogInformation($"Speaker range: {minSpeakers}-{maxSpeakers}");
                _logger.LogInformation($"Locale: {locale ?? _audioUploadOptions.DefaultLocale}");
                
                _logger.LogInformation("Batch transcription request received: " + (audioFiles?.Count ?? 0) + " files");
                
                // Try to use Request.Form.Files if audioFiles is null
                var filesToProcess = audioFiles ?? Request.Form.Files;
                
                if (filesToProcess == null || filesToProcess.Count == 0)
                {
                    _logger.LogWarning("No files uploaded - returning error response");
                    return Json(new BatchTranscriptionResponse 
                    { 
                        Success = false, 
                        Message = "No files uploaded." 
                    });
                }

                // Validate speaker counts
                if (minSpeakers < 1 || minSpeakers > 10)
                {
                    return Json(new BatchTranscriptionResponse
                    {
                        Success = false,
                        Message = "Minimum speakers must be between 1 and 10."
                    });
                }

                if (maxSpeakers < 1 || maxSpeakers > 10)
                {
                    return Json(new BatchTranscriptionResponse
                    {
                        Success = false,
                        Message = "Maximum speakers must be between 1 and 10."
                    });
                }

                if (minSpeakers > maxSpeakers)
                {
                    return Json(new BatchTranscriptionResponse
                    {
                        Success = false,
                        Message = "Minimum speakers cannot be greater than maximum speakers."
                    });
                }

                // Use configured default locale if not specified
                var selectedLocale = string.IsNullOrWhiteSpace(locale) 
                    ? _audioUploadOptions.DefaultLocale 
                    : locale;

                // Validate files
                _audioFileValidator.ValidateFiles(filesToProcess);

                // Create uploads directory
                var uploadsFolder = Path.Combine(_environment.WebRootPath, _audioUploadOptions.UploadFolderPath);
                Directory.CreateDirectory(uploadsFolder);

                // Save files and collect paths
                var filePaths = new List<string>();
                var savedFiles = new List<string>();
                
                try
                {
                    foreach (var file in filesToProcess)
                    {
                        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream, cancellationToken);
                        }

                        filePaths.Add(filePath);
                        savedFiles.Add(file.FileName);
                        _logger.LogInformation($"Batch file saved: {filePath}");
                    }

                    // Submit batch job to Azure Speech Service
                    var finalJobName = string.IsNullOrWhiteSpace(jobName)
                        ? $"Batch Transcription {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
                        : jobName;

                    var job = await _batchTranscriptionService.CreateBatchTranscriptionAsync(
                        filePaths,
                        finalJobName,
                        selectedLocale,
                        true,
                        minSpeakers,
                        maxSpeakers,
                        cancellationToken);

                    _logger.LogInformation($"Batch job created: {job.Id} with locale '{selectedLocale}' and speaker range {minSpeakers}-{maxSpeakers}");

                    return Json(new BatchTranscriptionResponse
                    {
                        Success = true,
                        Message = $"Batch job '{finalJobName}' submitted successfully with {savedFiles.Count} file(s). " +
                                 "Check the 'Transcription Jobs' tab to monitor progress.",
                        JobId = job.Id,
                        JobName = finalJobName,
                        FilesSubmitted = savedFiles.Count
                    });
                }
                catch
                {
                    // Clean up files if job creation fails
                    foreach (var path in filePaths)
                    {
                        if (System.IO.File.Exists(path))
                        {
                            try
                            {
                                System.IO.File.Delete(path);
                            }
                            catch (Exception deleteEx)
                            {
                                _logger.LogWarning(deleteEx, $"Failed to delete file: {path}");
                            }
                        }
                    }
                    throw;
                }
            }
            catch (InvalidAudioFileException ex)
            {
                _logger.LogWarning(ex, "Batch validation failed");
                return Json(new BatchTranscriptionResponse 
                { 
                    Success = false, 
                    Message = ex.Message 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting batch transcription");
                return Json(new BatchTranscriptionResponse 
                { 
                    Success = false, 
                    Message = $"Error submitting batch job: {ex.Message}" 
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSupportedLocales(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Fetching supported locales for batch transcription");
                
                var locales = await _batchTranscriptionService.GetSupportedLocalesAsync(cancellationToken);
                
                _logger.LogInformation($"Successfully retrieved {locales.Count()} supported locales");
                
                return Json(new 
                { 
                    success = true, 
                    locales = locales.ToList(),
                    count = locales.Count()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving supported locales");
                return Json(new 
                { 
                    success = false, 
                    message = $"Error retrieving supported locales: {ex.Message}" 
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSupportedLocalesWithNames(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Fetching supported locales with names for batch transcription");
                
                var locales = await _batchTranscriptionService.GetSupportedLocalesWithNamesAsync(cancellationToken);
                
                _logger.LogInformation($"Successfully retrieved {locales.Count()} supported locales with names");
                
                return Json(new 
                { 
                    success = true, 
                    locales = locales.ToList(),
                    count = locales.Count()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving supported locales with names");
                return Json(new 
                { 
                    success = false, 
                    message = $"Error retrieving supported locales: {ex.Message}" 
                });
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }
        
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
