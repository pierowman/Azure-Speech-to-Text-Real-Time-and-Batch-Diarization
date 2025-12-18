using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using speechtotext.Services;

namespace speechtotext.Testing
{
    public class SpeechServiceTest
    {
        public static async Task TestTranscription()
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            var logger = loggerFactory.CreateLogger<SpeechToTextService>();

            // Create service
            var speechService = new SpeechToTextService(configuration, logger);

            // Test with katiesteve.wav
            var audioFilePath = Path.Combine(Directory.GetCurrentDirectory(), "katiesteve.wav");

            if (!File.Exists(audioFilePath))
            {
                Console.WriteLine($"ERROR: Audio file not found at: {audioFilePath}");
                return;
            }

            Console.WriteLine($"Testing transcription with: {audioFilePath}");
            Console.WriteLine("Starting transcription...");
            Console.WriteLine("This may take a few moments...");
            Console.WriteLine();

            var result = await speechService.TranscribeWithDiarizationAsync(audioFilePath);

            // Display results
            Console.WriteLine("=== TRANSCRIPTION RESULTS ===");
            Console.WriteLine();
            Console.WriteLine($"Success: {result.Success}");
            Console.WriteLine($"Message: {result.Message}");
            Console.WriteLine();

            if (result.Success && result.Segments.Any())
            {
                Console.WriteLine($"Total Segments: {result.Segments.Count}");
                Console.WriteLine();
                Console.WriteLine("=== SPEAKER SEGMENTS ===");
                Console.WriteLine();

                foreach (var segment in result.Segments)
                {
                    Console.WriteLine($"[{segment.Speaker}] @ {segment.FormattedStartTime}");
                    Console.WriteLine($"  {segment.Text}");
                    Console.WriteLine();
                }

                Console.WriteLine("=== FULL TRANSCRIPT ===");
                Console.WriteLine();
                Console.WriteLine(result.FullTranscript);
            }
            else if (result.Success && !result.Segments.Any())
            {
                Console.WriteLine("WARNING: Transcription succeeded but no segments were detected.");
                Console.WriteLine("This could mean:");
                Console.WriteLine("- The audio is too short");
                Console.WriteLine("- No speech was detected");
                Console.WriteLine("- Audio quality issues");
            }
            else
            {
                Console.WriteLine($"ERROR: {result.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("Test completed.");
        }
    }
}
