"""
Azure Batch Transcription Service
"""
import logging
import json
import asyncio
import requests
import aiohttp
from typing import List, Optional
from datetime import datetime, timedelta
from urllib.parse import urlparse, parse_qs
from azure.storage.blob import BlobServiceClient
from azure.identity import DefaultAzureCredential, ClientSecretCredential
from models import TranscriptionJob, LocaleInfo, TranscriptionProperties
from config import config


# Custom logging filter to provide default request_id
class RequestIdFilter(logging.Filter):
    """Add request_id to log records, using 'async' as default for batch operations"""
    def filter(self, record) -> bool:
        try:
            from flask import g
            record.request_id = getattr(g, 'request_id', 'async')
        except (RuntimeError, LookupError):
            # Outside of application context (async operations)
            record.request_id = 'async'
        return True


logger = logging.getLogger(__name__)
logger.addFilter(RequestIdFilter())


class BatchTranscriptionService:
    """Service for creating batch transcription jobs in Azure Speech Service"""
    
    # Locale caching
    _cached_locales = None
    _cached_locales_with_names = None
    _cache_expiration = None
    
    def __init__(self):
        self.subscription_key = config.AZURE_SPEECH_KEY
        self.region = config.AZURE_SPEECH_REGION
        self.base_url = f"https://{self.region}.api.cognitive.microsoft.com/speechtotext/v3.1"
        self.models_base_url = f"https://{self.region}.api.cognitive.microsoft.com/speechtotext/v3.2"
        
        self.headers = {
            'Ocp-Apim-Subscription-Key': self.subscription_key,
            'Accept': 'application/json',
            'Content-Type': 'application/json'
        }
        
        # PERFORMANCE: Create persistent requests session with connection pooling
        self.session = requests.Session()
        self.session.headers.update(self.headers)
        
        # Configure connection pooling adapter
        adapter = requests.adapters.HTTPAdapter(
            pool_connections=10,      # Number of connection pools to cache
            pool_maxsize=20,          # Max connections per pool
            max_retries=3,            # Retry failed requests
            pool_block=False          # Don't block when pool is full
        )
        self.session.mount('https://', adapter)
        self.session.mount('http://', adapter)
        logger.info("HTTP connection pooling enabled (10 pools, 20 connections)")
        
        # PERFORMANCE: aiohttp session for async requests (created on demand)
        self._aiohttp_session: Optional[aiohttp.ClientSession] = None
        
        # Initialize Blob Storage if configured
        self.blob_service_client = None
        if config.IS_CONFIGURED:
            try:
                self.blob_service_client = self._create_blob_service_client()
                logger.info("Azure Blob Storage client initialized successfully")
            except Exception as ex:
                logger.error(f"Failed to initialize Azure Blob Storage client: {ex}")
                self.blob_service_client = None
        else:
            logger.info("Azure Blob Storage is not configured. Using placeholder mode.")
    
    def _create_blob_service_client(self) -> BlobServiceClient:
        """Create BlobServiceClient using Azure AD authentication"""
        blob_service_uri = config.BLOB_SERVICE_ENDPOINT
        
        if config.USE_MANAGED_IDENTITY:
            logger.info("Using DefaultAzureCredential (Managed Identity) for blob storage")
            credential = DefaultAzureCredential()
            return BlobServiceClient(account_url=blob_service_uri, credential=credential)
        else:
            logger.info("Using Service Principal (Client ID/Secret) for blob storage")
            credential = ClientSecretCredential(
                tenant_id=config.AZURE_TENANT_ID,
                client_id=config.AZURE_CLIENT_ID,
                client_secret=config.AZURE_CLIENT_SECRET
            )
            return BlobServiceClient(account_url=blob_service_uri, credential=credential)
    
    async def _get_aiohttp_session(self) -> aiohttp.ClientSession:
        """
        Get or create aiohttp session for async HTTP requests.
        
        PERFORMANCE: Reuses connections across async requests for better performance.
        """
        if self._aiohttp_session is None or self._aiohttp_session.closed:
            # Create session with connection pooling
            connector = aiohttp.TCPConnector(
                limit=20,           # Max simultaneous connections
                limit_per_host=10,  # Max per host
                ttl_dns_cache=300   # DNS cache TTL (5 minutes)
            )
            
            self._aiohttp_session = aiohttp.ClientSession(
                headers=self.headers,
                connector=connector,
                timeout=aiohttp.ClientTimeout(total=30)
            )
            logger.info("aiohttp session created with connection pooling")
        
        return self._aiohttp_session
    
    def __del__(self):
        """Cleanup aiohttp session on destruction"""
        if self._aiohttp_session and not self._aiohttp_session.closed:
            try:
                import asyncio
                asyncio.create_task(self._aiohttp_session.close())
            except:
                pass  # Ignore errors during cleanup
    
    async def create_batch_transcription(
        self,
        audio_file_paths: List[str],
        job_name: str,
        language: str = "en-US",
        enable_diarization: bool = True,
        min_speakers: Optional[int] = None,
        max_speakers: Optional[int] = None
    ) -> TranscriptionJob:
        """
        Create a batch transcription job
        
        Args:
            audio_file_paths: List of paths to audio files
            job_name: Name for the transcription job
            language: Language locale code
            enable_diarization: Enable speaker diarization
            min_speakers: Minimum number of speakers
            max_speakers: Maximum number of speakers
            
        Returns:
            TranscriptionJob object
            
        Raises:
            Exception: If job creation fails and blob storage is properly configured
        """
        # Use configuration defaults if not specified
        effective_min_speakers = min_speakers or config.DEFAULT_MIN_SPEAKERS
        effective_max_speakers = max_speakers or config.DEFAULT_MAX_SPEAKERS
        
        logger.info(f"Creating batch transcription: {job_name} with speaker range "
                   f"{effective_min_speakers}-{effective_max_speakers}")
        
        # Check if Blob Storage is configured
        if not self.blob_service_client or not config.IS_CONFIGURED:
            logger.warning("Blob Storage not configured. Creating placeholder job.")
            return self._create_placeholder_job(audio_file_paths, job_name)
        
        try:
            # Upload files to Blob Storage
            blob_urls = await self._upload_files_to_blob_storage(audio_file_paths)
            
            if not blob_urls:
                error_msg = "No files uploaded to blob storage successfully"
                logger.error(error_msg)
                raise Exception(error_msg)
            
            # Build request body
            request_body = {
                'contentUrls': blob_urls,
                'locale': language,
                'displayName': job_name,
                'properties': {
                    'diarizationEnabled': enable_diarization,
                    'diarization': {
                        'mode': 'Identity',
                        'speakers': {
                            'minCount': effective_min_speakers,
                            'maxCount': effective_max_speakers
                        }
                    },
                    'wordLevelTimestampsEnabled': True,
                    'punctuationMode': 'DictatedAndAutomatic',
                    'profanityFilterMode': 'Masked'
                }
            }
            
            logger.info(f"Submitting batch job with {len(blob_urls)} files and speaker range "
                       f"{effective_min_speakers}-{effective_max_speakers}")
            
            # PERFORMANCE: Use persistent session instead of creating new connection
            response = self.session.post(
                f"{self.base_url}/transcriptions",
                json=request_body
            )
            
            if not response.ok:
                error = response.text
                logger.error(f"Batch job creation failed: Status {response.status_code}, Error: {error}")
                raise Exception(f"Azure API error (Status {response.status_code}): {error}")
            
            job_data = response.json()
            self_url = job_data.get('self', '')
            job_id = self_url.split('/')[-1] if self_url else str(datetime.utcnow().timestamp())
            
            logger.info(f"Batch job created successfully: {job_id}")
            
            return TranscriptionJob(
                id=job_id,
                display_name=job_name,
                status="NotStarted",
                created_date_time=datetime.utcnow(),
                files=[f.split('/')[-1] for f in audio_file_paths]
            )
            
        except Exception as ex:
            logger.error(f"Error creating batch transcription job: {ex}", exc_info=True)
            # Re-raise the exception to let the caller handle it
            raise
    
    async def _upload_files_to_blob_storage(self, audio_file_paths: List[str]) -> List[str]:
        """
        Upload files to Azure Blob Storage using Service Principal or Managed Identity
        
        Returns blob URLs with SAS tokens for Azure Speech Service to access.
        
        Note: We use Service Principal/Managed Identity for uploading (secure),
        but generate temporary SAS tokens for Azure Speech Service to access the blobs.
        This is necessary because Azure Speech Service needs to read the files.
        
        Alternative: Configure Azure Speech Service with Managed Identity and grant it
        'Storage Blob Data Reader' role on the storage account to avoid SAS tokens entirely.
        """
        blob_urls = []
        
        if not self.blob_service_client:
            logger.warning("Blob service client is null, cannot upload files")
            return blob_urls
        
        try:
            # Get or create container
            container_client = self.blob_service_client.get_container_client(
                config.AZURE_STORAGE_CONTAINER_NAME
            )
            
            # Check if container exists, create if it doesn't
            try:
                # Check if container exists
                container_properties = container_client.get_container_properties()
                logger.info(f"Using existing blob container: {config.AZURE_STORAGE_CONTAINER_NAME}")
            except Exception as ex:
                # Container doesn't exist, try to create it
                try:
                    container_client.create_container()
                    logger.info(f"Created new blob container: {config.AZURE_STORAGE_CONTAINER_NAME}")
                except Exception as create_ex:
                    logger.error(f"Failed to create container: {create_ex}")
                    raise Exception(f"Container '{config.AZURE_STORAGE_CONTAINER_NAME}' does not exist and could not be created: {create_ex}")
            
            for file_path in audio_file_paths:
                try:
                    import os
                    import uuid
                    from datetime import datetime, timedelta
                    from azure.storage.blob import generate_blob_sas, BlobSasPermissions
                    
                    file_name = os.path.basename(file_path)
                    blob_name = f"{uuid.uuid4()}_{file_name}"
                    blob_client = container_client.get_blob_client(blob_name)
                    
                    logger.info(f"Uploading file to blob storage: {file_name} as {blob_name}")
                    
                    # Upload using Service Principal/Managed Identity (no SAS needed for us)
                    with open(file_path, 'rb') as data:
                        blob_client.upload_blob(data, overwrite=True)
                    
                    logger.info(f"File uploaded successfully: {blob_name}")
                    
                    # Generate SAS token for Azure Speech Service to access the blob
                    # This is a short-lived token (24 hours) specifically for Speech Service
                    # Alternative: Configure Speech Service Managed Identity with Storage Blob Data Reader role
                    try:
                        # Try to get user delegation key (works with Azure AD auth)
                        sas_token = self._generate_blob_sas_with_user_delegation(blob_name)
                        blob_url_with_sas = f"{blob_client.url}?{sas_token}"
                        blob_urls.append(blob_url_with_sas)
                        logger.info(f"Generated user delegation SAS for Speech Service access")
                    except Exception as sas_ex:
                        logger.warning(f"Could not generate user delegation SAS: {sas_ex}")
                        # Fallback: try account key SAS if available, otherwise use URL without SAS
                        # Note: URL without SAS will only work if Speech Service has Managed Identity access
                        blob_urls.append(blob_client.url)
                        logger.warning(f"Using blob URL without SAS - Speech Service must have Managed Identity access")
                    
                except Exception as ex:
                    logger.error(f"Failed to upload file to blob storage: {file_path} - {ex}")
                    continue
            
            logger.info(f"Successfully uploaded {len(blob_urls)} files to blob storage")
            
        except Exception as ex:
            logger.error(f"Error uploading files to blob storage: {ex}")
        
        return blob_urls
    
    def _generate_blob_sas_with_user_delegation(self, blob_name: str, expiry_hours: int = 24) -> str:
        """
        Generate a SAS token using user delegation key (Azure AD based)
        This is more secure than account key-based SAS
        
        Args:
            blob_name: Name of the blob
            expiry_hours: Hours until SAS expires (default 24)
            
        Returns:
            SAS token string
            
        Raises:
            Exception: If SAS generation fails
        """
        from datetime import datetime, timedelta
        from azure.storage.blob import generate_blob_sas, BlobSasPermissions, UserDelegationKey
        
        try:
            # Get user delegation key (requires Azure AD authentication)
            delegation_key_start_time = datetime.utcnow()
            delegation_key_expiry_time = delegation_key_start_time + timedelta(hours=expiry_hours)
            
            user_delegation_key = self.blob_service_client.get_user_delegation_key(
                key_start_time=delegation_key_start_time,
                key_expiry_time=delegation_key_expiry_time
            )
            
            # Generate SAS token using user delegation key
            sas_token = generate_blob_sas(
                account_name=config.AZURE_STORAGE_ACCOUNT_NAME,
                container_name=config.AZURE_STORAGE_CONTAINER_NAME,
                blob_name=blob_name,
                user_delegation_key=user_delegation_key,
                permission=BlobSasPermissions(read=True),
                expiry=delegation_key_expiry_time,
                start=delegation_key_start_time
            )
            
            return sas_token
            
        except Exception as ex:
            logger.error(f"Failed to generate user delegation SAS: {ex}")
            raise
    
    def _create_placeholder_job(self, audio_file_paths: List[str], job_name: str) -> TranscriptionJob:
        """Create a placeholder job when blob storage is not configured"""
        import uuid
        import os
        
        job_id = str(uuid.uuid4())
        logger.info(f"Created placeholder batch job: {job_id}")
        
        error_msg = None if config.IS_CONFIGURED else \
            "Placeholder job - Azure Blob Storage not configured"
        
        return TranscriptionJob(
            id=job_id,
            display_name=job_name,
            status="NotStarted",
            created_date_time=datetime.utcnow(),
            files=[os.path.basename(f) for f in audio_file_paths],
            error=error_msg
        )
    
    async def get_transcription_jobs(self, skip: int = 0, top: int = 100, cached_jobs: Optional[List[dict]] = None, force_refresh: bool = False) -> List[TranscriptionJob]:
        """
        Get list of batch transcription jobs from Azure Speech Service
        
        PERFORMANCE OPTIMIZATIONS:
        - Uses persistent HTTP session with connection pooling
        - Fetches job files in parallel using asyncio.gather
        - Caches completed/failed jobs to avoid redundant API calls
        
        Args:
            skip: Number of jobs to skip
            top: Maximum number of jobs to retrieve
            cached_jobs: List of cached job dicts with 'id' and 'status' to avoid re-fetching completed jobs
            force_refresh: If True, ignore cache and fetch all jobs from Azure
            
        Returns:
            List of TranscriptionJob objects
        """
        try:
            logger.info(f"Fetching transcription jobs (skip={skip}, top={top}, force_refresh={force_refresh})")
            
            # Build cache lookup for completed/failed jobs
            cached_completed_jobs = {}
            jobs_to_refresh = []
            
            if cached_jobs and not force_refresh:
                for cached_job in cached_jobs:
                    job_id = cached_job.get('id')
                    status = cached_job.get('status')
                    # Cache jobs that are in terminal states
                    if status in ['Succeeded', 'Failed']:
                        cached_completed_jobs[job_id] = cached_job
                    else:
                        jobs_to_refresh.append(job_id)
                
                logger.info(f"Using cache: {len(cached_completed_jobs)} completed/failed jobs, {len(jobs_to_refresh)} active jobs to refresh")
            
            # PERFORMANCE: Use persistent session for job list fetch
            response = self.session.get(
                f"{self.base_url}/transcriptions",
                params={'skip': skip, 'top': top}
            )
            
            if not response.ok:
                logger.error(f"Failed to fetch jobs: Status {response.status_code}")
                return []
            
            data = response.json()
            jobs = []
            
            if 'values' in data:
                # First pass: Parse all job data (fast, no I/O)
                jobs_to_process = []
                for job_data in data['values']:
                    job_id = job_data.get('self', '').split('/')[-1] if job_data.get('self') else job_data.get('id', '')
                    
                    # Use cached data for completed/failed jobs
                    if job_id in cached_completed_jobs:
                        logger.debug(f"?? Using cached data for completed job: {job_id}")
                        cached_job_obj = self._dict_to_job(cached_completed_jobs[job_id])
                        jobs.append(cached_job_obj)
                        
                        # ? FIX: ALWAYS fetch files if missing, regardless of cache status
                        if not cached_job_obj.files or len(cached_job_obj.files) == 0:
                            logger.info(f"?? Cached job {job_id} has no files - will fetch")
                            jobs_to_process.append(cached_job_obj)
                        
                        continue
                    
                    # Parsing job data...
                    job = self._parse_job_data(job_data)
                    jobs.append(job)
                    
                    # ? FIX: ALWAYS fetch files for ANY job with empty files array
                    # This handles page refresh scenario where contentUrls parsing may have failed
                    if not job.files or len(job.files) == 0:
                        logger.debug(f"?? Job {job_id} ({job.status}) has no files - will fetch")
                        jobs_to_process.append(job)
                
                # PERFORMANCE: Fetch files for all jobs that need them IN PARALLEL
                if jobs_to_process:
                    logger.info(f"?? Fetching files for {len(jobs_to_process)} jobs in parallel...")
                    
                    # Create tasks for parallel execution
                    file_fetch_tasks = [
                        self._get_job_files_async(job.id)
                        for job in jobs_to_process
                    ]
                    
                    # Execute all tasks concurrently
                    files_results = await asyncio.gather(*file_fetch_tasks, return_exceptions=True)
                    
                    # Update jobs with fetched files
                    for job, files_result in zip(jobs_to_process, files_results):
                        if isinstance(files_result, Exception):
                            logger.error(f"? Error fetching files for job {job.id}: {files_result}")
                        elif files_result and len(files_result) > 0:
                            job.files = files_result
                            logger.info(f"? Job {job.id} now has {len(files_result)} files: {files_result}")
                        else:
                            logger.warning(f"?? Job {job.id} - no files returned from API (status: {job.status})")
            
            logger.info(f"Retrieved {len(jobs)} transcription jobs ({len([j for j in jobs if j.id in cached_completed_jobs])} from cache)")
            return jobs
            
        except Exception as ex:
            logger.error(f"Error fetching transcription jobs: {ex}", exc_info=True)
            return []
    
    async def _get_job_files_async(self, job_id: str) -> List[str]:
        """
        Fetch job files using async HTTP client for better performance.
        
        PERFORMANCE: Uses aiohttp for true async I/O, allowing parallel requests.
        
        Args:
            job_id: The transcription job ID
            
        Returns:
            List of file names associated with the job
        """
        try:
            session = await self._get_aiohttp_session()
            
            url = f"{self.base_url}/transcriptions/{job_id}/files"
            
            async with session.get(url) as response:
                if not response.ok:
                    logger.warning(f"Failed to fetch files for job {job_id}: Status {response.status}")
                    return []
                
                files_data = await response.json()
                
                file_names = []
                report_url = None
                
                if 'values' in files_data:
                    for file_entry in files_data['values']:
                        file_kind = file_entry.get('kind', '')
                        
                        # Check for Audio kind or LanguageData
                        if file_kind and (file_kind.lower() == 'audio' or file_kind == 'LanguageData'):
                            name = file_entry.get('name')
                            if not name:
                                # Extract from content URL
                                content_url = file_entry.get('links', {}).get('contentUrl', '')
                                if content_url:
                                    name = content_url.split('/')[-1].split('?')[0]
                            if name:
                                file_names.append(name)
                        
                        # Find report file
                        elif file_kind == 'TranscriptionReport':
                            report_url = file_entry.get('links', {}).get('contentUrl')
                
                # If no audio files found, try parsing report
                if not file_names and report_url:
                    async with session.get(report_url) as report_response:
                        if report_response.ok:
                            report_data = await report_response.json()
                            
                            if 'details' in report_data:
                                for detail in report_data['details']:
                                    source = detail.get('source')
                                    if source:
                                        file_name = source.split('/')[-1].split('?')[0]
                                        if '_' in file_name:
                                            parts = file_name.split('_', 1)
                                            if len(parts) == 2:
                                                file_name = parts[1]
                                        file_names.append(file_name)
                
                return file_names
                
        except Exception as ex:
            logger.error(f"Error fetching files for job {job_id}: {ex}", exc_info=True)
            return []
    
    async def _get_job_files(self, job_id: str) -> List[str]:
        """
        Get the list of input files for a batch transcription job
        
        DEPRECATED: Use _get_job_files_async for better performance.
        Kept for backwards compatibility.
        
        Args:
            job_id: The transcription job ID
            
        Returns:
            List of file names associated with the job
        """
        # Delegate to async version
        return await self._get_job_files_async(job_id)
    
    async def get_transcription_job_status(self, job_id: str) -> Optional[TranscriptionJob]:
        """Get status of a specific batch transcription job"""
        try:
            logger.info(f"Fetching job status for: {job_id}")
            
            # PERFORMANCE: Use persistent session
            response = self.session.get(
                f"{self.base_url}/transcriptions/{job_id}"
            )
            
            if not response.ok:
                logger.error(f"Failed to fetch job status: Status {response.status_code}")
                return None
            
            job_data = response.json()
            job = self._parse_job_data(job_data)
            
            # Fetch files for this job from the /files endpoint
            files = await self._get_job_files(job.id)
            # Only update files if we got results from the API
            # Otherwise keep the files from contentUrls parsed in _parse_job_data
            if files:
                job.files = files
                logger.info(f"Job {job_id} has {len(files)} files from /files endpoint: {files}")
            else:
                logger.info(f"Job {job_id} keeping {len(job.files)} files from contentUrls: {job.files}")
            
            logger.info(f"Job {job_id} status: {job.status}")
            return job
            
        except Exception as ex:
            logger.error(f"Error fetching job status: {ex}", exc_info=True)
            return None
    
    def _dict_to_job(self, job_dict: dict) -> TranscriptionJob:
        """Convert a dictionary representation back to a TranscriptionJob object"""
        from models import TranscriptionProperties
        
        # Parse properties if present
        properties = None
        if job_dict.get('properties'):
            props_dict = job_dict['properties']
            properties = TranscriptionProperties(
                duration=props_dict.get('duration'),
                succeeded_count=props_dict.get('succeededCount'),
                failed_count=props_dict.get('failedCount'),
                error_message=props_dict.get('errorMessage')
            )
        
        # Parse dates
        created_date_time = None
        if job_dict.get('createdDateTime'):
            try:
                created_date_time = datetime.fromisoformat(job_dict['createdDateTime'])
            except:
                pass
        
        last_action_date_time = None
        if job_dict.get('lastActionDateTime'):
            try:
                last_action_date_time = datetime.fromisoformat(job_dict['lastActionDateTime'])
            except:
                pass
        
        return TranscriptionJob(
            id=job_dict.get('id', ''),
            display_name=job_dict.get('displayName', ''),
            status=job_dict.get('status', 'Unknown'),
            created_date_time=created_date_time,
            last_action_date_time=last_action_date_time,
            error=job_dict.get('error'),
            files=job_dict.get('files', []),
            results_url=job_dict.get('resultsUrl'),
            properties=properties,
            locale=job_dict.get('locale')
        )
    
    async def get_transcription_files_list(self, job_id: str) -> List[dict]:
        """Get list of available transcription result files for a job"""
        try:
            logger.info(f"Fetching transcription files list for job: {job_id}")
            
            # Fetch job details to get input file names for mapping
            job = await self.get_transcription_job_status(job_id)
            input_file_names = job.files if job else []
            logger.info(f"Input audio files for mapping: {input_file_names}")
            
            # PERFORMANCE: Use persistent session
            files_response = self.session.get(
                f"{self.base_url}/transcriptions/{job_id}/files"
            )

            if not files_response.ok:
                logger.error(f"Failed to fetch job files: Status {files_response.status_code}")
                return []
            
            files_data = files_response.json()
            transcription_files = []
            
            # Find all transcription result files
            if 'values' in files_data:
                for idx, file_entry in enumerate(files_data['values']):
                    if file_entry.get('kind') == 'Transcription':
                        url = file_entry.get('links', {}).get('contentUrl')
                        
                        # Parse SAS token info
                        sas_expiry = self._parse_sas_expiry(url) if url else None
                        is_expired = self._is_sas_expired(url) if url else False
                        
                        # Map result index to original input file name
                        file_name = f'File {idx + 1}'  # Default fallback
                        if idx < len(input_file_names):
                            file_name = input_file_names[idx]
                            logger.info(f"Mapped result {idx} to original file: {file_name}")
                        else:
                            logger.warning(f"No input file name for result index {idx}, using fallback: {file_name}")
                        
                        transcription_files.append({
                            'index': idx,
                            'name': file_name,
                            'url': url,
                            'size': file_entry.get('properties', {}).get('size', 0),
                            'sasExpiry': sas_expiry,
                            'sasExpired': is_expired
                        })
                        
                        if is_expired:
                            logger.warning(f"File {idx} '{file_name}' has EXPIRED SAS token (expired: {sas_expiry})")
                        elif sas_expiry:
                            # Calculate time until expiry for logging
                            try:
                                expiry_dt = datetime.strptime(sas_expiry, '%Y-%m-%dT%H:%M:%SZ')
                                time_diff = expiry_dt - datetime.utcnow()
                                hours_left = time_diff.total_seconds() / 3600
                                logger.info(f"File {idx} '{file_name}' SAS token valid for {hours_left:.1f} hours (until: {sas_expiry})")
                            except:
                                logger.info(f"File {idx} '{file_name}' SAS token valid until: {sas_expiry}")
                        else:
                            logger.warning(f"File {idx} '{file_name}' has no SAS expiry info")
            
            logger.info(f"Found {len(transcription_files)} transcription files for job {job_id}")
            return transcription_files
            
        except Exception as ex:
            logger.error(f"Error fetching transcription files list: {ex}", exc_info=True)
            return []
    
    async def get_transcription_results(self, job_id: str, file_indices: Optional[List[int]] = None) -> Optional['BatchTranscriptionResult']:
        """
        Get transcription results for a completed batch job
        
        Args:
            job_id: The transcription job ID
            file_indices: Optional list of file indices to process in specified order. If None, processes first file only.
                         If provided, files are concatenated in the given order.
        """
        from models import BatchTranscriptionResult, SpeakerInfo
        
        try:
            logger.info(f"Fetching transcription results for job: {job_id}")
            if file_indices:
                logger.info(f"Requested file indices: {file_indices}")
            
            # First, get job details to ensure it's completed and get input file names
            job = await self.get_transcription_job_status(job_id)
            if not job:
                logger.error(f"Job {job_id} not found")
                return None
            
            input_file_names = job.files if job.files else []
            logger.info(f"Input audio files for mapping: {input_file_names}")
            
            if job.status != "Succeeded":
                logger.warning(f"Job {job_id} is not in Succeeded status: {job.status}")
                return BatchTranscriptionResult(
                    success=False,
                    message=f"Job is not completed yet. Current status: {job.status}",
                    job_id=job_id,
                    display_name=job.display_name
                )
            
            # PERFORMANCE: Use persistent session for file list
            logger.info(f"Fetching file list for job {job_id}...")
            files_response = self.session.get(
                f"{self.base_url}/transcriptions/{job_id}/files"
            )

            if not files_response.ok:
                logger.error(f"Failed to fetch job files: Status {files_response.status_code}")
                logger.error(f"   Response: {files_response.text[:500]}")
                return None
            
            files_data = files_response.json()
            
            # Build list of all transcription files with mapped names
            all_transcription_files = []
            if 'values' in files_data:
                for idx, file_entry in enumerate(files_data['values']):
                    if file_entry.get('kind') == 'Transcription':
                        file_url = file_entry.get('links', {}).get('contentUrl')
                        
                        # Map result index to original input file name
                        file_name = f'File {idx + 1}'  # Default fallback
                        if idx < len(input_file_names):
                            file_name = input_file_names[idx]
                            logger.info(f"Mapped result {idx} to original file: {file_name}")
                        else:
                            logger.warning(f"No input file name for result index {idx}, using fallback: {file_name}")
                        
                        all_transcription_files.append({
                            'url': file_url,
                            'name': file_name,
                            'index': idx
                        })
                        logger.debug(f"   File {idx}: {file_name}")
            
            logger.info(f"Found {len(all_transcription_files)} transcription files")
            
            # Determine which files to process
            result_file_urls = []
            if file_indices:
                # Use the provided file indices to get fresh URLs
                logger.info(f"Processing {len(file_indices)} selected files by index: {file_indices}")
                for idx in file_indices:
                    if 0 <= idx < len(all_transcription_files):
                        file_info = all_transcription_files[idx]
                        result_file_urls.append(file_info['url'])
                        logger.info(f"   File {idx}: {file_info['name']}")
                    else:
                        logger.error(f"   File index {idx} is out of range (0-{len(all_transcription_files)-1})")
            else:
                # Legacy behavior: find the first transcription result file
                if all_transcription_files:
                    result_file_urls.append(all_transcription_files[0]['url'])
                    logger.info(f"Processing first file (default): {all_transcription_files[0]['name']}")
            
            if not result_file_urls:
                logger.error(f"No transcription result files found for job {job_id}")
                logger.error(f"   File indices requested: {file_indices}")
                logger.error(f"   Total files available: {len(all_transcription_files)}")
                return BatchTranscriptionResult(
                    success=False,
                    message="No transcription results found",
                    job_id=job_id,
                    display_name=job.display_name
                )
            
            # Process all selected files and concatenate segments
            all_segments = []
            all_raw_data = []
            time_offset = 0  # Track cumulative time offset for concatenation
            
            for file_idx, result_file_url in enumerate(result_file_urls):
                logger.info(f"Processing file {file_idx + 1}/{len(result_file_urls)}")
                logger.debug(f"   URL: {result_file_url[:100]}...")
                
                # Check if SAS token is expired
                if self._is_sas_expired(result_file_url):
                    expiry = self._parse_sas_expiry(result_file_url)
                    logger.error(f"SAS token EXPIRED for file {file_idx + 1} (expired: {expiry})")
                    logger.error(f"   Cannot download file - SAS token has expired. Re-fetch files list to get fresh tokens.")
                    continue
                
                # Log SAS expiry for debugging
                sas_expiry = self._parse_sas_expiry(result_file_url)
                if sas_expiry:
                    logger.info(f"   SAS token valid until: {sas_expiry}")
                
                # PERFORMANCE: Use persistent session for file download
                logger.info(f"   Downloading transcription file...")
                result_response = self.session.get(result_file_url)
                
                if not result_response.ok:
                    logger.error(f"Failed to download results: Status {result_response.status_code}")
                    if result_response.status_code == 404:
                        logger.error(f"   404 Error - File not found. This could be due to:")
                        logger.error(f"   1. Expired SAS token (check expiry: {sas_expiry})")
                        logger.error(f"   2. File deleted from Azure Storage")
                        logger.error(f"   3. Invalid URL format")
                    logger.error(f"   Response: {result_response.text[:500]}")
                    continue
                
                result_data = result_response.json()
                all_raw_data.append(result_data)
                logger.info(f"   File downloaded successfully")
                
                # Parse segments from results
                file_segments = self._parse_batch_transcription_segments(result_data)
                logger.info(f"   Parsed {len(file_segments)} segments")
                
                # Adjust segment timestamps for concatenation
                if file_idx > 0 and all_segments:
                    # Calculate time offset from previous file's last segment
                    last_segment = all_segments[-1]
                    time_offset = last_segment.offset_in_ticks + last_segment.duration_in_ticks
                    
                    # Apply offset to all segments in this file
                    for segment in file_segments:
                        segment.offset_in_ticks += time_offset
                
                all_segments.extend(file_segments)
                logger.info(f"   Added {len(file_segments)} segments from file {file_idx + 1}")
            
            if not all_segments:
                logger.error(f"No segments found in any transcription files for job {job_id}")
                return BatchTranscriptionResult(
                    success=False,
                    message="No transcription data found in result files",
                    job_id=job_id,
                    display_name=job.display_name
                )
            
            # Build full transcript
            transcript_lines = [f"[{s.speaker}]: {s.text}" for s in all_segments]
            full_transcript = "\n".join(transcript_lines)
            
            # Calculate available speakers
            available_speakers = sorted(list(set(s.speaker for s in all_segments if s.speaker.strip())))
            
            # Calculate speaker statistics
            speaker_groups = {}
            for segment in all_segments:
                if segment.speaker not in speaker_groups:
                    speaker_groups[segment.speaker] = []
                speaker_groups[segment.speaker].append(segment)
            
            speaker_statistics = []
            for speaker, speaker_segments in speaker_groups.items():
                total_time = sum(
                    s.end_time_in_seconds - s.start_time_in_seconds
                    for s in speaker_segments
                )
                first_appearance = min(s.start_time_in_seconds for s in speaker_segments)
                
                speaker_statistics.append(SpeakerInfo(
                    name=speaker,
                    segment_count=len(speaker_segments),
                    total_speak_time_seconds=total_time,
                    first_appearance_seconds=first_appearance
                ))
            
            speaker_statistics.sort(key=lambda x: x.first_appearance_seconds)
            
            import json
            files_processed_msg = f"{len(result_file_urls)} file(s)" if len(result_file_urls) > 1 else "1 file"
            
            logger.info(f"Successfully parsed {len(all_segments)} segments from {len(result_file_urls)} file(s) for job {job_id}")
            logger.info(f"   Speakers: {', '.join(available_speakers)}")
            
            result = BatchTranscriptionResult(
                success=True,
                message=f"Retrieved {len(all_segments)} segments from {files_processed_msg}",
                job_id=job_id,
                display_name=job.display_name,
                segments=all_segments,
                full_transcript=full_transcript,
                available_speakers=available_speakers,
                speaker_statistics=speaker_statistics,
                raw_json_data=json.dumps(all_raw_data, indent=2)
            )
            
            return result
            
        except Exception as ex:
            logger.error(f"Error fetching transcription results: {ex}", exc_info=True)
            return None
    
    async def delete_transcription_job(self, job_id: str) -> bool:
        """Delete a batch transcription job"""
        try:
            logger.info(f"Deleting transcription job: {job_id}")
            
            # PERFORMANCE: Use persistent session
            response = self.session.delete(
                f"{self.base_url}/transcriptions/{job_id}"
            )
            
            if not response.ok:
                logger.error(f"Failed to delete job: Status {response.status_code}, Error: {response.text}")
                return False
            
            logger.info(f"Batch job deleted successfully: {job_id}")
            return True
        
        except Exception as ex:
            logger.error(f"Error deleting transcription job: {ex}", exc_info=True)
            return False
    
    def get_supported_locales(self) -> List[str]:
        """
        Get list of supported locale codes from Azure Speech Service
        
        SIMPLIFIED VERSION: Returns just locale codes (e.g., ['en-US', 'es-ES'])
        Use get_locale_names() for full LocaleInfo objects with display names.
        
        Returns:
            List of locale code strings
        """
        # Check cache first
        if self._cached_locales and datetime.utcnow() < self._cache_expiration:
            logger.info("Using cached locales data")
            # Extract just the codes from LocaleInfo objects
            if isinstance(self._cached_locales[0], LocaleInfo):
                return [l.code for l in self._cached_locales]
            return self._cached_locales
        
        try:
            logger.info("Fetching supported locales from Azure Speech Service")
            
            # PERFORMANCE: Use persistent session
            response = self.session.get(
                f"{self.models_base_url}/models"
            )
            
            if not response.ok:
                logger.error(f"Failed to fetch locales: Status {response.status_code}")
                # Return common locales as fallback
                return self._get_common_locales_fallback()
            
            models_data = response.json()
            locales = []
            
            # Extract unique locale codes from model data
            seen_locales = set()
            for model in models_data.get('values', []):
                locale = model.get('locale')
                if locale and locale not in seen_locales:
                    locales.append(locale)
                    seen_locales.add(locale)
            
            # Sort locales alphabetically
            locales.sort()
            
            # Cache the results
            self._cached_locales = locales
            self._cache_expiration = datetime.utcnow() + timedelta(hours=1)
            
            logger.info(f"Retrieved {len(locales)} supported locales")
            return locales
        
        except Exception as ex:
            logger.error(f"Error fetching supported locales: {ex}", exc_info=True)
            # Return common locales as fallback
            return self._get_common_locales_fallback()
    
    def _get_common_locales_fallback(self) -> List[str]:
        """
        Return a list of common locales as fallback when API call fails.
        
        These are the most commonly used locales for speech recognition.
        """
        logger.warning("Using fallback list of common locales")
        return [
            'en-US', 'en-GB', 'en-AU', 'en-CA', 'en-IN',
            'es-ES', 'es-MX', 'fr-FR', 'fr-CA', 'de-DE',
            'it-IT', 'pt-BR', 'pt-PT', 'ja-JP', 'ko-KR',
            'zh-CN', 'zh-HK', 'zh-TW', 'nl-NL', 'ru-RU',
            'ar-SA', 'hi-IN', 'sv-SE', 'da-DK', 'fi-FI',
            'no-NO', 'pl-PL', 'tr-TR', 'th-TH', 'id-ID'
        ]
    
    def get_locale_names(self) -> List[LocaleInfo]:
        """Get locale names with additional information (region, language)"""
        if self._cached_locales_with_names and datetime.utcnow() < self._cache_expiration:
            logger.info("Using cached locale names data")
            return self._cached_locales_with_names
        
        try:
            logger.info("Fetching supported locales with names from Azure Speech Service")
            
            # PERFORMANCE: Use persistent session
            response = self.session.get(
                f"{self.models_base_url}/models"
            )
            
            if not response.ok:
                logger.error(f"Failed to fetch locales: Status {response.status_code}")
                return self._get_common_locales_with_names_fallback()
            
            models_data = response.json()
            locales_with_names = []
            
            # Extract locale info with names from model data
            for model in models_data.get('values', []):
                locale = model.get('locale')
                display_name = model.get('displayName')
                if locale and locale not in [l.code for l in locales_with_names]:
                    locales_with_names.append(LocaleInfo(code=locale, name=display_name))
            
            if not locales_with_names:
                logger.warning("No locales returned from Azure API, using fallback")
                return self._get_common_locales_with_names_fallback()
            
            self._cached_locales_with_names = locales_with_names
            self._cache_expiration = datetime.utcnow() + timedelta(hours=1)  # Cache for 1 hour
            
            logger.info(f"Retrieved {len(locales_with_names)} supported locales with names")
            return locales_with_names
        
        except Exception as ex:
            logger.error(f"Error fetching supported locales with names: {ex}", exc_info=True)
            return self._get_common_locales_with_names_fallback()
    
    def _get_common_locales_with_names_fallback(self) -> List[LocaleInfo]:
        """
        Return a list of common locales with friendly names as fallback when API call fails.
        
        These are the most commonly used locales for speech recognition.
        """
        logger.warning("Using fallback list of common locales with names")
        return [
            LocaleInfo(code='en-US', name='English (United States)'),
            LocaleInfo(code='en-GB', name='English (United Kingdom)'),
            LocaleInfo(code='en-AU', name='English (Australia)'),
            LocaleInfo(code='en-CA', name='English (Canada)'),
            LocaleInfo(code='en-IN', name='English (India)'),
            LocaleInfo(code='es-ES', name='Spanish (Spain)'),
            LocaleInfo(code='es-MX', name='Spanish (Mexico)'),
            LocaleInfo(code='fr-FR', name='French (France)'),
            LocaleInfo(code='fr-CA', name='French (Canada)'),
            LocaleInfo(code='de-DE', name='German (Germany)'),
            LocaleInfo(code='it-IT', name='Italian (Italy)'),
            LocaleInfo(code='pt-BR', name='Portuguese (Brazil)'),
            LocaleInfo(code='pt-PT', name='Portuguese (Portugal)'),
            LocaleInfo(code='ja-JP', name='Japanese (Japan)'),
            LocaleInfo(code='ko-KR', name='Korean (Korea)'),
            LocaleInfo(code='zh-CN', name='Chinese (Mandarin, Simplified)'),
            LocaleInfo(code='zh-HK', name='Chinese (Cantonese, Traditional)'),
            LocaleInfo(code='zh-TW', name='Chinese (Taiwanese Mandarin)'),
            LocaleInfo(code='nl-NL', name='Dutch (Netherlands)'),
            LocaleInfo(code='ru-RU', name='Russian (Russia)'),
            LocaleInfo(code='ar-SA', name='Arabic (Saudi Arabia)'),
            LocaleInfo(code='hi-IN', name='Hindi (India)'),
            LocaleInfo(code='sv-SE', name='Swedish (Sweden)'),
            LocaleInfo(code='da-DK', name='Danish (Denmark)'),
            LocaleInfo(code='fi-FI', name='Finnish (Finland)'),
            LocaleInfo(code='no-NO', name='Norwegian (Norway)'),
            LocaleInfo(code='pl-PL', name='Polish (Poland)'),
            LocaleInfo(code='tr-TR', name='Turkish (Turkey)'),
            LocaleInfo(code='th-TH', name='Thai (Thailand)'),
            LocaleInfo(code='id-ID', name='Indonesian (Indonesia)')
        ]
    
    def _parse_job_data(self, job_data: dict) -> TranscriptionJob:
        """Parse job data from Azure API response"""
        job_id = job_data.get('self', '').split('/')[-1] if job_data.get('self') else job_data.get('id', '')
        display_name = job_data.get('displayName', 'Unknown')
        status = job_data.get('status', 'Unknown')
        
        # Parse timestamps
        created_date_time = None
        last_action_date_time = None
        
        created_str = job_data.get('createdDateTime')
        if created_str:
            try:
                created_date_time = datetime.fromisoformat(created_str.replace('Z', '+00:00'))
            except:
                pass
        
        last_action_str = job_data.get('lastActionDateTime')
        if last_action_str:
            try:
                last_action_date_time = datetime.fromisoformat(last_action_str.replace('Z', '+00:00'))
            except:
                pass
        
        # Parse properties
        properties = None
        if 'properties' in job_data:
            props_data = job_data['properties']
            
            # Parse duration from ISO 8601 format to ticks
            duration_ticks = None
            duration_str = props_data.get('duration')
            if duration_str:
                duration_ticks = self._parse_duration_to_ticks(duration_str)
            
            properties = TranscriptionProperties(
                duration=duration_ticks,
                succeeded_count=props_data.get('succeededCount'),
                failed_count=props_data.get('failedCount'),
                error_message=props_data.get('error', {}).get('message')
            )
        
        # Parse file list from contentUrls
        files = []
        if 'contentUrls' in job_data:
            for url in job_data['contentUrls']:
                # Extract filename from URL
                file_name = url.split('/')[-1].split('?')[0]
                # Remove UUID prefix if present
                if '_' in file_name:
                    parts = file_name.split('_', 1)
                    if len(parts) == 2:
                        file_name = parts[1]
                files.append(file_name)
        
        # Parse locale
        locale = job_data.get('locale')
        
        # Parse error if present
        error = None
        if 'error' in job_data:
            error_data = job_data['error']
            error = error_data.get('message', str(error_data))
        
        return TranscriptionJob(
            id=job_id,
            display_name=display_name,
            status=status,
            created_date_time=created_date_time,
            last_action_date_time=last_action_date_time,
            properties=properties,
            files=files,
            locale=locale,
            error=error
        )
    
    def _parse_duration_to_ticks(self, duration_str: str) -> Optional[int]:
        """
        Parse ISO 8601 duration string to ticks (100-nanosecond intervals)
        
        Examples:
        - "PT1M30.5S" -> 90.5 seconds -> 905,000,000 ticks
        - "PT1H2M3S" -> 3,723 seconds -> 37,230,000,000 ticks
        - "PT0.5S" -> 0.5 seconds -> 5,000,000 ticks
        
        Args:
            duration_str: ISO 8601 duration string (e.g., "PT1H2M3.5S")
            
        Returns:
            Duration in ticks (100-nanosecond intervals), or None if parsing fails
        """
        if not duration_str:
            return None
        
        try:
            import re
            
            # Parse ISO 8601 duration: PT[hours]H[minutes]M[seconds]S
            pattern = r'PT(?:(\d+)H)?(?:(\d+)M)?(?:([\d.]+)S)?'
            match = re.match(pattern, duration_str)
            
            if not match:
                logger.warning(f"Could not parse duration string: {duration_str}")
                return None
            
            hours = int(match.group(1)) if match.group(1) else 0
            minutes = int(match.group(2)) if match.group(2) else 0
            seconds = float(match.group(3)) if match.group(3) else 0.0
            
            # Convert to total seconds
            total_seconds = hours * 3600 + minutes * 60 + seconds
            
            # Convert to ticks (1 tick = 100 nanoseconds = 10^-7 seconds)
            ticks = int(total_seconds * 10_000_000)
            
            return ticks
            
        except Exception as ex:
            logger.error(f"Error parsing duration '{duration_str}': {ex}")
            return None
    
    def _parse_batch_transcription_segments(self, result_data: dict) -> List['SpeakerSegment']:
        """Parse batch transcription segments from Azure API response"""
        from models import SpeakerSegment
        
        segments = []
        
        if 'recognizedPhrases' not in result_data:
            logger.warning("No 'recognizedPhrases' found in batch transcription result")
            return segments
        
        line_number = 1
        for phrase in result_data['recognizedPhrases']:
            # Get the best result
            if 'nBest' in phrase and len(phrase['nBest']) > 0:
                best = phrase['nBest'][0]
                
                # Extract speaker info
                speaker = phrase.get('speaker', 0)
                speaker_name = f"Speaker {speaker}" if speaker is not None else "Unknown"
                
                # Extract timing info
                offset_ticks = phrase.get('offsetInTicks', 0)
                duration_ticks = phrase.get('durationInTicks', 0)
                
                # Get text
                text = best.get('display', best.get('lexical', ''))
                
                if text.strip():
                    segment = SpeakerSegment(
                        line_number=line_number,
                        speaker=speaker_name,
                        text=text,
                        offset_in_ticks=offset_ticks,
                        duration_in_ticks=duration_ticks
                    )
                    segments.append(segment)
                    line_number += 1
        
        logger.info(f"Parsed {len(segments)} segments from batch transcription result")
        return segments
    
    def _parse_sas_expiry(self, url: str) -> Optional[str]:
        """Extract SAS token expiry timestamp from Azure Storage URL"""
        if not url:
            return None
        
        try:
            parsed = urlparse(url)
            query_params = parse_qs(parsed.query)
            
            # SAS expiry is in 'se' parameter
            if 'se' in query_params:
                expiry = query_params['se'][0]
                return expiry
        except Exception as ex:
            logger.debug(f"Could not parse SAS expiry from URL: {ex}")
        
        return None
    
    def _is_sas_expired(self, url: str) -> bool:
        """Check if SAS token in URL is expired"""
        expiry_str = self._parse_sas_expiry(url)
        if not expiry_str:
            return False
        
        try:
            # Parse expiry timestamp (format: 2024-01-15T10:30:00Z)
            expiry_dt = datetime.strptime(expiry_str, '%Y-%m-%dT%H:%M:%SZ')
            now = datetime.utcnow()
            return now >= expiry_dt
        except Exception as ex:
            logger.debug(f"Could not parse SAS expiry timestamp: {ex}")
            return False


# Create singleton instance
batch_transcription_service = BatchTranscriptionService()
