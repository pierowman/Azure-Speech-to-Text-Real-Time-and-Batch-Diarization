using Microsoft.Extensions.Options;
using speechtotext.Exceptions;
using speechtotext.Models;

namespace speechtotext.Validation
{
    public class AudioFileValidator : IAudioFileValidator
    {
        private readonly AudioUploadOptions _options;
        private readonly ILogger<AudioFileValidator> _logger;

        public AudioFileValidator(
            IOptions<AudioUploadOptions> options,
            ILogger<AudioFileValidator> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public void ValidateFile(IFormFile file, TranscriptionMode mode = TranscriptionMode.RealTime)
        {
            if (file == null || file.Length == 0)
            {
                throw new InvalidAudioFileException("No file uploaded or file is empty.");
            }

            // Get mode-specific settings
            var (allowedExtensions, maxSize, maxDuration) = GetModeSettings(mode);
            
            // Validate file size
            if (file.Length > maxSize)
            {
                var maxSizeMB = maxSize / (1024.0 * 1024.0);
                var modeName = mode == TranscriptionMode.RealTime ? "Real-Time" : "Batch";
                throw new InvalidAudioFileException(
                    $"File size exceeds {modeName} mode maximum of {maxSizeMB:F2} MB. " +
                    (mode == TranscriptionMode.RealTime 
                        ? "Consider using Batch mode for larger files." 
                        : "Please reduce file size or split into smaller files."));
            }

            // Validate file extension
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                var modeName = mode == TranscriptionMode.RealTime ? "Real-Time" : "Batch";
                throw new InvalidAudioFileException(
                    $"Invalid file type for {modeName} mode. " +
                    $"Allowed types: {string.Join(", ", allowedExtensions)}" +
                    (mode == TranscriptionMode.RealTime && extension != ".wav"
                        ? " (Tip: Batch mode supports more formats)"
                        : ""));
            }

            _logger.LogInformation(
                $"File validation passed ({mode} mode): {file.FileName}, " +
                $"Size: {file.Length / 1024.0:F2} KB");
        }

        public void ValidateFiles(IFormFileCollection files)
        {
            if (files == null || files.Count == 0)
            {
                throw new InvalidAudioFileException("No files uploaded.");
            }

            // Check file count
            if (files.Count > _options.BatchMaxFiles)
            {
                throw new InvalidAudioFileException(
                    $"Too many files. Batch mode supports up to {_options.BatchMaxFiles} files per job. " +
                    $"You uploaded {files.Count} files. Please remove {files.Count - _options.BatchMaxFiles} files.");
            }

            // Validate each file
            var errors = new List<string>();
            foreach (var file in files)
            {
                try
                {
                    ValidateFile(file, TranscriptionMode.Batch);
                }
                catch (InvalidAudioFileException ex)
                {
                    errors.Add($"{file.FileName}: {ex.Message}");
                }
            }

            if (errors.Any())
            {
                throw new InvalidAudioFileException(
                    $"Validation failed for {errors.Count} file(s):\n" + 
                    string.Join("\n", errors));
            }

            _logger.LogInformation(
                $"Batch file validation passed: {files.Count} files, " +
                $"Total size: {files.Sum(f => f.Length) / (1024.0 * 1024.0):F2} MB");
        }

        public string GetValidationRulesSummary(TranscriptionMode mode)
        {
            var (extensions, maxSize, maxDuration) = GetModeSettings(mode);
            var maxSizeMB = maxSize / (1024.0 * 1024.0);
            
            // DIAGNOSTIC: Log what we're actually using
            _logger.LogInformation($"GetValidationRulesSummary called for {mode} mode");
            _logger.LogInformation($"  Extensions array length: {extensions.Length}");
            _logger.LogInformation($"  Extensions: [{string.Join(", ", extensions)}]");
            
            // Check for duplicates and remove them
            var uniqueExtensions = extensions.Distinct().ToArray();
            if (uniqueExtensions.Length != extensions.Length)
            {
                _logger.LogWarning($"  ?? DUPLICATES DETECTED! Unique count: {uniqueExtensions.Length}, Actual count: {extensions.Length}");
                var duplicates = extensions.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => $"{g.Key} ({g.Count()}x)");
                _logger.LogWarning($"  Duplicates: {string.Join(", ", duplicates)}");
            }
            else
            {
                _logger.LogInformation($"  ? No duplicates detected");
            }
            
            // Use uniqueExtensions to ensure no duplicates in display
            var displayExtensions = uniqueExtensions.Select(e => e.TrimStart('.')).ToArray();
            var extensionsDisplay = string.Join("/", displayExtensions).ToUpper();
            
            _logger.LogInformation($"  Display string: {extensionsDisplay}");
            
            if (mode == TranscriptionMode.RealTime)
            {
                return $"Real-Time: Single file, {extensionsDisplay}, " +
                       $"max {maxSizeMB:F0} MB, up to {maxDuration} minutes. Results appear immediately.";
            }
            else
            {
                return $"Batch: Multiple files (up to {_options.BatchMaxFiles}), " +
                       $"{extensionsDisplay}, " +
                       $"max {maxSizeMB:F0} MB per file, up to {maxDuration} minutes. " +
                       $"Check 'Transcription Jobs' tab for results.";
            }
        }

        private (string[] extensions, long maxSize, int maxDuration) GetModeSettings(TranscriptionMode mode)
        {
            return mode == TranscriptionMode.RealTime
                ? (_options.RealTimeAllowedExtensions, _options.RealTimeMaxFileSizeInBytes, _options.RealTimeMaxDurationInMinutes)
                : (_options.BatchAllowedExtensions, _options.BatchMaxFileSizeInBytes, _options.BatchMaxDurationInMinutes);
        }
    }
}
