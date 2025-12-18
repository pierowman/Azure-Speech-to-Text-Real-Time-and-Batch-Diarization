using Microsoft.Extensions.Options;
using speechtotext.Models;
using speechtotext.Services;
using System.Text.Encodings.Web;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container with JSON configuration for Unicode support
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // Ensure emojis and Unicode characters are not escaped
        options.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        // Use camelCase for property names (default, but explicit)
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// Configure options
builder.Services.Configure<AudioUploadOptions>(
    builder.Configuration.GetSection("AudioUpload"));

builder.Services.Configure<AzureStorageOptions>(
    builder.Configuration.GetSection("AzureStorage"));

// Register services
builder.Services.AddScoped<ISpeechToTextService, SpeechToTextService>();
builder.Services.AddScoped<ITranscriptionJobService, TranscriptionJobService>();
builder.Services.AddScoped<IBatchTranscriptionService, BatchTranscriptionService>();
builder.Services.AddScoped<IFileCleanupService, FileCleanupService>();
builder.Services.AddScoped<speechtotext.Validation.IAudioFileValidator, speechtotext.Validation.AudioFileValidator>();

// Register HttpClientFactory for TranscriptionJobService and BatchTranscriptionService
builder.Services.AddHttpClient();

// Register background service for file cleanup
builder.Services.AddHostedService<FileCleanupBackgroundService>();

var app = builder.Build();

// Log Azure Storage configuration status
var storageOptions = app.Services.GetRequiredService<IOptions<AzureStorageOptions>>().Value;
var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Log environment
var environment = app.Environment;
logger.LogInformation("=== ENVIRONMENT CONFIGURATION ===");
logger.LogInformation("Environment: {EnvironmentName}", environment.EnvironmentName);
logger.LogInformation("Is Development: {IsDevelopment}", environment.IsDevelopment());
logger.LogInformation("Is Production: {IsProduction}", environment.IsProduction());

logger.LogInformation("=== AZURE STORAGE CONFIGURATION ===");
logger.LogInformation("EnableBlobStorage: {EnableBlobStorage}", storageOptions.EnableBlobStorage);
logger.LogInformation("StorageAccountName: {StorageAccountName}", 
    string.IsNullOrEmpty(storageOptions.StorageAccountName) ? "<EMPTY>" : storageOptions.StorageAccountName);
logger.LogInformation("ContainerName: {ContainerName}", storageOptions.ContainerName);

if (storageOptions.IsConfigured)
{
    logger.LogInformation("? Azure Blob Storage is configured and enabled for batch transcription");
}
else
{
    logger.LogWarning("?? Azure Blob Storage is NOT configured. Batch transcription will use placeholder mode. " +
                     "To enable full batch functionality, configure AzureStorage section in appsettings.json");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
