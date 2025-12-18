# Speech to Text Web Application

A .NET 9 Razor Pages web application that provides real-time and batch speech-to-text transcription using Azure Cognitive Services.

## Features

- **Real-Time Transcription**: Upload single audio files for immediate transcription
- **Batch Transcription**: Process multiple large audio files asynchronously
- **Diarization**: Identify different speakers in audio recordings
- **Multiple Audio Formats**: Support for WAV, MP3, OGG, FLAC, M4A, AAC
- **Azure Blob Storage Integration**: Secure storage for batch processing
- **Job Management**: Track and manage transcription jobs
- **Configurable Validation**: File size, duration, and format limits

## Prerequisites

- .NET 9 SDK
- Azure Subscription
- Azure Cognitive Services Speech Service
- Azure Blob Storage (for batch transcription)

## Getting Started

### 1. Clone the Repository

```bash
git clone <your-repo-url>
cd speechtotext
```

### 2. Configure Azure Services

#### Create Azure Speech Service

1. Go to [Azure Portal](https://portal.azure.com)
2. Create a new **Cognitive Services** resource (Speech)
3. Note the following:
   - Subscription Key
   - Region (e.g., `eastus2`)
   - Endpoint URL

#### Create Azure Storage Account (for batch transcription)

1. Create a **Storage Account**
2. Create a container named `audio-uploads`
3. Set up authentication (see [Azure AD Setup Guide](speechtotext/AZURE_AD_BLOB_STORAGE_SETUP.md))

### 3. Configure Application Secrets

**IMPORTANT**: Never commit `appsettings.Development.json` to Git!

#### Option A: Use appsettings.Development.json (Local Development)

Create `speechtotext/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AzureSpeech": {
    "SubscriptionKey": "your-subscription-key-here",
    "Region": "your-region-here",
    "Endpoint": "https://your-endpoint.cognitiveservices.azure.com/"
  },
  "AzureStorage": {
    "StorageAccountName": "your-storage-account-name",
    "ContainerName": "audio-uploads",
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "EnableBlobStorage": true,
    "UseManagedIdentity": false
  }
}
```

#### Option B: Use .NET User Secrets (Recommended)

```bash
cd speechtotext
dotnet user-secrets init
dotnet user-secrets set "AzureSpeech:SubscriptionKey" "your-key"
dotnet user-secrets set "AzureSpeech:Region" "eastus2"
dotnet user-secrets set "AzureSpeech:Endpoint" "https://your-endpoint.cognitiveservices.azure.com/"
dotnet user-secrets set "AzureStorage:StorageAccountName" "yourstorageaccount"
dotnet user-secrets set "AzureStorage:TenantId" "your-tenant-id"
dotnet user-secrets set "AzureStorage:ClientId" "your-client-id"
dotnet user-secrets set "AzureStorage:ClientSecret" "your-client-secret"
dotnet user-secrets set "AzureStorage:EnableBlobStorage" "true"
```

### 4. Run the Application

```bash
cd speechtotext
dotnet restore
dotnet build
dotnet run
```

Navigate to `https://localhost:5001` (or the port shown in the console)

## Configuration

### Application Settings

All configuration is in `appsettings.json` (template with empty values) and `appsettings.Development.json` (your local secrets).

Key configuration sections:

#### Azure Speech Service

```json
{
  "AzureSpeech": {
    "SubscriptionKey": "your-key",
    "Region": "eastus2",
    "Endpoint": "https://your-service.cognitiveservices.azure.com/"
  }
}
```

#### Azure Blob Storage

```json
{
  "AzureStorage": {
    "StorageAccountName": "yourstorageaccount",
    "ContainerName": "audio-uploads",
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "EnableBlobStorage": true,
    "UseManagedIdentity": false
  }
}
```

#### Audio Upload Limits

```json
{
  "AudioUpload": {
    "RealTimeAllowedExtensions": [".wav", ".mp3", ".ogg", ".flac"],
    "RealTimeMaxFileSizeInBytes": 26214400,
    "RealTimeMaxDurationInMinutes": 60,
    "BatchAllowedExtensions": [".wav", ".mp3", ".ogg", ".flac", ".m4a", ".aac"],
    "BatchMaxFileSizeInBytes": 1073741824,
    "BatchMaxDurationInMinutes": 480,
    "BatchMaxFiles": 20
  }
}
```

## Project Structure

```
speechtotext/
??? Controllers/          # MVC Controllers
??? Models/              # Data models and view models
??? Services/            # Business logic services
?   ??? ISpeechToTextService.cs
?   ??? SpeechToTextService.cs
?   ??? IBatchTranscriptionService.cs
?   ??? BatchTranscriptionService.cs
?   ??? TranscriptionJobService.cs
??? Validation/          # Audio file validation
??? Views/               # Razor views
?   ??? Home/
?   ??? Shared/
??? wwwroot/             # Static files
?   ??? css/
?   ??? js/
?   ??? lib/
??? appsettings.json     # Configuration template (safe to commit)
??? appsettings.Development.json  # Your secrets (DO NOT COMMIT)
```

## Usage

### Real-Time Transcription

1. Select "Real-Time" mode
2. Upload a single audio file (max 25 MB, up to 60 minutes)
3. Configure speaker settings (optional)
4. Click "Transcribe"
5. Results appear immediately

### Batch Transcription

1. Select "Batch" mode
2. Upload multiple audio files (max 20 files, 1 GB each, up to 8 hours)
3. Configure speaker settings and locale
4. Click "Submit Batch Job"
5. Monitor progress in "Transcription Jobs" tab
6. Download results when complete

## Testing

```bash
cd speechtotext.Tests
dotnet test
```

## Documentation

- [Azure AD Blob Storage Setup](speechtotext/AZURE_AD_BLOB_STORAGE_SETUP.md) - Complete guide for Azure authentication
- [Security Checklist](SECURITY_CHECKLIST.md) - Security best practices and Git setup
- [Transcription Jobs Feature](speechtotext/TRANSCRIPTION_JOBS_FEATURE.md) - Job management documentation

## Security

### Important Security Notes

?? **DO NOT COMMIT SECRETS**
- `appsettings.Development.json` is in `.gitignore` - it contains your secrets
- Use `.gitignore` to prevent committing sensitive files
- Use User Secrets or Azure Key Vault for secrets management

? **Safe to Commit**
- `appsettings.json` - Template with empty values
- All source code files
- Documentation

See [SECURITY_CHECKLIST.md](SECURITY_CHECKLIST.md) for complete security guidelines.

## Deployment

### Azure App Service

1. Enable Managed Identity on your App Service
2. Grant Storage Blob Data Contributor role to the Managed Identity
3. Set application settings in Azure Portal:
   ```
   AzureSpeech__SubscriptionKey = <your-key>
   AzureSpeech__Region = eastus2
   AzureStorage__StorageAccountName = yourstorageaccount
   AzureStorage__UseManagedIdentity = true
   AzureStorage__EnableBlobStorage = true
   ```

### Docker

```bash
# Build
docker build -t speechtotext .

# Run
docker run -p 8080:80 \
  -e AzureSpeech__SubscriptionKey="your-key" \
  -e AzureSpeech__Region="eastus2" \
  speechtotext
```

## Troubleshooting

### "Azure Blob Storage is NOT configured"

- Check that `EnableBlobStorage` is `true`
- Verify `StorageAccountName` is set
- Ensure authentication credentials are correct

### "Authentication failed"

- Verify Azure AD credentials (TenantId, ClientId, ClientSecret)
- Check role assignments in Azure Portal
- For Managed Identity: ensure it's enabled and has proper roles

### "Transcription failed"

- Check Azure Speech Service quota
- Verify subscription key is valid
- Ensure audio file meets format requirements

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Run tests: `dotnet test`
5. Submit a pull request

## License

[Your License Here]

## Support

For issues and questions:
- Check the [Documentation](speechtotext/) folder
- Review [Azure Speech Service Docs](https://docs.microsoft.com/azure/cognitive-services/speech-service/)
- Open an issue in this repository

---

**Built with**: .NET 9, ASP.NET Core Razor Pages, Azure Cognitive Services, Azure Blob Storage
