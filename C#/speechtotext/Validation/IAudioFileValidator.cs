using Microsoft.AspNetCore.Http;
using speechtotext.Models;

namespace speechtotext.Validation
{
    public interface IAudioFileValidator
    {
        /// <summary>
        /// Validates a single file for the specified transcription mode
        /// </summary>
        void ValidateFile(IFormFile file, TranscriptionMode mode = TranscriptionMode.RealTime);
        
        /// <summary>
        /// Validates multiple files for batch transcription
        /// </summary>
        void ValidateFiles(IFormFileCollection files);
        
        /// <summary>
        /// Gets validation rules summary for a mode
        /// </summary>
        string GetValidationRulesSummary(TranscriptionMode mode);
    }
}
