# Azure Speech-to-Text with Diarization

A Flask-based web application for transcribing audio files with speaker diarization using Azure Cognitive Services.

## ?? Security Notice

**?? IMPORTANT: Before deploying or committing code:**

1. **NEVER commit `.env` file** - It contains sensitive credentials
2. **Rotate all exposed credentials** if they were committed to Git
3. **Use Azure Key Vault** for production deployments
4. **Enable HTTPS** for all production environments
5. **Set strong `FLASK_SECRET_KEY`** in production

## ?? Features

- **Real-time Transcription**: Upload audio files for immediate transcription with speaker diarization
- **Batch Transcription**: Process multiple audio files asynchronously
- **Speaker Management**: Edit speaker names and segment text
- **Audit Logging**: Track all edits with comprehensive audit trails
- **Export Options**: Download transcripts as Word documents or JSON
- **Multi-language Support**: Support for 50+ languages (batch mode)
- **Rate Limiting**: Built-in protection against abuse
- **Caching**: Optimized performance with intelligent caching

## ?? Prerequisites

- Python 3.9 or higher
- Azure Cognitive Services Speech API subscription
- Azure Blob Storage account (optional, for batch transcription)

## ??? Installation

### 1. Clone the repository

```bash
git clone <repository-url>
cd python_version
```

### 2. Create virtual environment

```bash
python -m venv venv

# Windows
venv\Scripts\activate

# Linux/Mac
source venv/bin/activate
```

### 3. Install dependencies

```bash
pip install -r requirements.txt
```

### 4. Configure environment variables

```bash
# Copy the example file
cp .env.example .env

# Edit .env with your credentials
# IMPORTANT: Use a text editor to fill in your actual Azure credentials
```

**Required environment variables:**

```env
# Flask
FLASK_SECRET_KEY=<generate-a-strong-random-key>
FLASK_DEBUG=false  # Set to true only for development

# Azure Speech Service
AZURE_SPEECH_KEY=<your-azure-speech-key>
AZURE_SPEECH_REGION=<your-region>  # e.g., eastus2
AZURE_SPEECH_ENDPOINT=<your-custom-endpoint>  # Optional

# Azure Blob Storage (for batch transcription)
AZURE_STORAGE_ACCOUNT_NAME=<your-storage-account>
AZURE_STORAGE_CONTAINER_NAME=speech-transcriptions
ENABLE_BLOB_STORAGE=true

# Authentication
USE_MANAGED_IDENTITY=false  # Set to true when running in Azure
AZURE_TENANT_ID=<your-tenant-id>
AZURE_CLIENT_ID=<your-client-id>
AZURE_CLIENT_SECRET=<your-client-secret>
```

### 5. Generate a secure secret key

```python
python -c "import secrets; print(secrets.token_hex(32))"
```

Copy the output to `FLASK_SECRET_KEY` in your `.env` file.

## ?? Running the Application

### Development Mode

```bash
python app.py
```

The application will be available at `http://localhost:5000`

### Production Mode

**Important**: Never use Flask's built-in server in production!

```bash
# Install production server
pip install gunicorn

# Run with Gunicorn
gunicorn --bind 0.0.0.0:5000 --workers 4 --timeout 120 app:app
```

## ?? Configuration

All configuration is done through environment variables. See `.env.example` for all available options.

### Key Configuration Options

| Variable | Default | Description |
|----------|---------|-------------|
| `RATE_LIMIT_PER_MINUTE` | 10 | API rate limit per IP address |
| `REALTIME_MAX_FILE_SIZE` | 104857600 | Max file size for real-time (100MB) |
| `BATCH_MAX_FILE_SIZE` | 1073741824 | Max file size for batch (1GB) |
| `BATCH_MAX_FILES` | 100 | Max files per batch job |
| `LOCALES_CACHE_DURATION_HOURS` | 24 | Cache duration for locale list |

## ?? API Endpoints

### Transcription

- `POST /upload-and-transcribe` - Upload audio for real-time transcription
- `POST /create-batch-transcription` - Create batch transcription job
- `GET /batch-jobs` - List all batch jobs
- `GET /batch-job/<id>` - Get job status
- `GET /batch-job/<id>/results` - Get transcription results
- `DELETE /batch-job/<id>` - Delete a job

### Editing

- `POST /update-speaker-names` - Update speaker names in bulk
- `POST /update-segment-text` - Update individual segment

### Downloads

- `POST /download-audit-log` - Download edit audit log (DOCX)
- `POST /download-readable-text` - Download formatted transcript (DOCX)
- `POST /download-raw-json` - Download raw JSON data
- `POST /download-golden-record` - Download original transcription

### Utilities

- `GET /supported-locales` - List supported languages
- `GET /validation-rules/<mode>` - Get validation rules

## ?? Security Features

### Implemented

- ? CSRF Protection
- ? Rate Limiting
- ? Input Validation
- ? File Content Validation
- ? Secure Headers (XSS, Frame, Content-Type)
- ? Request ID Tracking
- ? Comprehensive Error Handling
- ? Secure Temporary File Handling

### Recommended for Production

- ?? Enable HTTPS/TLS
- ?? Use Azure Key Vault for secrets
- ?? Enable Azure Monitor for logging
- ?? Implement WAF (Web Application Firewall)
- ?? Use Azure Managed Identity
- ?? Enable Azure DDoS Protection
- ?? Implement CORS policies

## ?? Testing

```bash
# Install dev dependencies
pip install pytest pytest-cov pytest-mock

# Run tests
pytest

# Run with coverage
pytest --cov=. --cov-report=html
```

## ?? Monitoring

The application includes:

- Request ID tracking for all requests
- Structured logging with context
- Error tracking with stack traces
- Performance metrics

Consider integrating with:
- Azure Application Insights
- Sentry for error tracking
- ELK stack for log aggregation

## ?? Troubleshooting

### Common Issues

**1. Configuration validation errors on startup**

Make sure all required environment variables are set in `.env`. Check the error message for which variable is missing.

**2. Azure authentication errors**

- Verify your Azure credentials are correct
- Check if the Speech API key has the correct permissions
- For Managed Identity: ensure the identity has "Cognitive Services User" role

**3. File upload failures**

- Check file size limits
- Verify file format is supported
- Ensure upload folder exists and is writable

**4. Batch transcription not working**

- Verify `ENABLE_BLOB_STORAGE=true` in `.env`
- Check Azure Storage account credentials
- Ensure container exists or app has permission to create it

## ?? Documentation

- [Azure Speech Service Docs](https://docs.microsoft.com/azure/cognitive-services/speech-service/)
- [Flask Documentation](https://flask.palletsprojects.com/)
- [Azure Storage Python SDK](https://docs.microsoft.com/python/api/overview/azure/storage)

## ?? Support

For issues and questions:
- Open an issue in GitHub
- Check the [Troubleshooting](#troubleshooting) section
- Review Azure Speech Service documentation

## ?? Changelog

### Version 2.0.0 (Latest)

**Security Enhancements:**
- Added CSRF protection
- Implemented rate limiting
- Enhanced input validation
- Added file content validation
- Secure headers for all responses

**Performance Improvements:**
- Added caching for locale lookups
- Optimized file handling
- Improved async operation handling
- Reduced redundant API calls

**Code Quality:**
- Refactored duplicate code
- Added comprehensive error handling
- Improved logging with request tracking
- Enhanced documentation

**Configuration:**
- Removed hardcoded secrets
- Added environment-based configuration
- Created configuration validation
- Added .env.example template

## ?? Production Checklist

Before deploying to production:

- [ ] Rotated all Azure credentials
- [ ] Set strong `FLASK_SECRET_KEY`
- [ ] Set `FLASK_DEBUG=false`
- [ ] Enabled HTTPS
- [ ] Configured Azure Key Vault
- [ ] Set up monitoring and logging
- [ ] Configured backup strategy
- [ ] Tested rate limiting
- [ ] Reviewed security headers
- [ ] Set up CI/CD pipeline
- [ ] Configured environment variables in Azure App Service
- [ ] Enabled Azure Managed Identity
- [ ] Set up Application Insights
- [ ] Configured auto-scaling
- [ ] Tested disaster recovery

---

**Built with ?? using Azure Cognitive Services**
