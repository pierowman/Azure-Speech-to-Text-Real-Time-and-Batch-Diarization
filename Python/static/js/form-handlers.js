// Form Handlers Module
import { AppState, resetAppState } from './app.js';
import { 
    showLoading,
    showError, 
    hideError, 
    showSuccess, 
    hideSuccess,
    setProgressStage,
    resetProgressStages,
    showSegmentCounter,
    hideSegmentCounter,
    updateSegmentCount,
    resetSegmentCounter,
    startSegmentEstimation,
    stopSegmentEstimation,
    updateLoadingText,
    showBatchLoading,
    showBatchError,
    hideBatchError,
    showBatchSuccess,
    hideBatchSuccess,
    showBatchResults,
    hideBatchResults
} from './ui-helpers.js';
import { displayResults } from './transcription-display.js';
import { refreshJobList } from './batch-manager.js';

// Setup form handlers on page load
export function setupForms() {
    setupRealtimeForm();
    setupBatchForm();
    setupBatchFilePreview();
}

// Real-time transcription form
function setupRealtimeForm() {
    const form = document.getElementById('uploadForm');
    const transcribeBtn = document.getElementById('transcribeBtn');
    
    if (!form || !transcribeBtn) {
        console.warn('Real-time form elements not found');
        return;
    }
    
    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        
        const formData = new FormData();
        const audioFile = document.getElementById('audioFile').files[0];
        
        if (!audioFile) {
            showError('Please select an audio file');
            return;
        }
        
        console.log('='.repeat(80));
        console.log('\uD83D\uDCE1 STARTING TRANSCRIPTION REQUEST');
        console.log('File name:', audioFile.name);
        console.log('File size:', (audioFile.size / 1024 / 1024).toFixed(2), 'MB');
        console.log('File type:', audioFile.type);
        console.log('='.repeat(80));
        
        // ?? Reset application state before starting new transcription
        resetAppState();
        
        formData.append('audioFile', audioFile);
        
        // Disable button and show loading
        transcribeBtn.disabled = true;
        transcribeBtn.textContent = '\u23F3 Transcribing...';
        showLoading(true);
        hideError();
        hideSuccess();
        
        // Stage 1: Uploading
        setProgressStage('upload');
        updateLoadingText('\uD83D\uDCE4 Uploading Audio File...');
        
        try {
            console.log('\uD83D\uDCE4 Sending request to /upload-and-transcribe...');
            
            // Simulate upload progress
            const uploadDelay = Math.min(1000, audioFile.size / 10000);
            await new Promise(resolve => setTimeout(resolve, uploadDelay));
            
            // Stage 2: Processing
            setProgressStage('processing');
            updateLoadingText('\u2699\uFE0F Processing with Azure Speech...');
            showSegmentCounter();
            const fileSizeMB = audioFile.size / (1024 * 1024);
            startSegmentEstimation(fileSizeMB);
            
            const response = await fetch('/upload-and-transcribe', {
                method: 'POST',
                body: formData
            });
            
            console.log('\uD83D\uDCE5 Response received:');
            console.log('  Status:', response.status);
            console.log('  Status Text:', response.statusText);
            console.log('  OK:', response.ok);
            
            // Stage 3: Diarization
            setProgressStage('diarization');
            updateLoadingText('\uD83D\uDC65 Identifying Speakers...');
            
            const data = await response.json();
            
            // Stop estimation and update with actual count
            stopSegmentEstimation();
            
            if (data.segments && Array.isArray(data.segments)) {
                updateSegmentCount(data.segments.length);
                console.log(`\uD83D\uDCCA Received ${data.segments.length} segments from server`);
                await new Promise(resolve => setTimeout(resolve, 800));
            }
            
            // Stage 4: Finalizing
            setProgressStage('finalizing');
            updateLoadingText('\u2728 Finalizing Transcription...');
            await new Promise(resolve => setTimeout(resolve, 500));
            
            console.log('='.repeat(80));
            console.log('\uD83D\uDCCB RESPONSE DATA:');
            console.log(JSON.stringify(data, null, 2));
            console.log('='.repeat(80));
            
            if (data.success) {
                console.log('\u2705 Transcription successful!');
                AppState.transcriptionData = data;
                displayResults(data);
                showSuccess('\u2705 Transcription completed successfully!');
            } else {
                console.error('\u274C TRANSCRIPTION FAILED:');
                console.error('  Message:', data.message);
                showError(data.message || 'Transcription failed');
            }
        } catch (error) {
            console.error('='.repeat(80));
            console.error('\uD83D\uDEA8 EXCEPTION CAUGHT:');
            console.error('  Error type:', error.constructor.name);
            console.error('  Error message:', error.message);
            console.error('  Stack trace:', error.stack);
            console.error('='.repeat(80));
            showError('An error occurred: ' + error.message);
        } finally {
            showLoading(false);
            transcribeBtn.disabled = false;
            transcribeBtn.textContent = transcribeBtn.dataset.originalText || '?? Start Real-time Transcription';
        }
    });
    
    console.log('\u2705 Real-time form handler attached');
}

// Batch transcription form
function setupBatchForm() {
    const form = document.getElementById('batchForm');
    const submitBtn = document.getElementById('batchSubmitBtn');
    
    if (!form || !submitBtn) {
        console.warn('Batch form elements not found');
        return;
    }
    
    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        
        console.log('='.repeat(80));
        console.log('\uD83D\uDCE6 BATCH JOB SUBMISSION STARTED');
        console.log('='.repeat(80));
        
        const formData = new FormData();
        const audioFiles = document.getElementById('batchAudioFiles').files;
        let jobName = document.getElementById('batchJobName').value.trim();
        const locale = document.getElementById('batchLocale').value;
        const enableDiarization = document.getElementById('enableDiarization').checked;
        const minSpeakers = document.getElementById('minSpeakers').value;
        const maxSpeakers = document.getElementById('maxSpeakers').value;
        
        // Generate default job name if empty
        if (!jobName) {
            const now = new Date();
            jobName = `Batch Job ${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')} ${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}:${String(now.getSeconds()).padStart(2, '0')}`;
            console.log('\uD83D\uDCDD No job name provided, using auto-generated name:', jobName);
        }
        
        console.log('\uD83D\uDCC4 Form Data Collected:');
        console.log('  Files selected:', audioFiles.length);
        console.log('  Job name:', jobName);
        console.log('  Locale:', locale);
        console.log('  Enable diarization:', enableDiarization);
        console.log('  Min speakers:', minSpeakers);
        console.log('  Max speakers:', maxSpeakers);
        
        if (audioFiles.length === 0) {
            console.error('\u274C VALIDATION FAILED: No audio files selected');
            showBatchError('Please select at least one audio file');
            return;
        }
        
        console.log('\u2705 Validation passed');
        console.log('\uD83D\uDCC2 Files to upload:');
        for (let i = 0; i < audioFiles.length; i++) {
            const file = audioFiles[i];
            console.log(`  ${i + 1}. ${file.name} (${(file.size / 1024 / 1024).toFixed(2)} MB, type: ${file.type})`);
        }
        
        // Append all files
        console.log('\uD83D\uDCE5 Appending files to FormData...');
        for (let file of audioFiles) {
            formData.append('audioFiles', file);
        }
        
        formData.append('jobName', jobName);
        formData.append('locale', locale);
        formData.append('enableDiarization', enableDiarization);
        formData.append('minSpeakers', minSpeakers);
        formData.append('maxSpeakers', maxSpeakers);
        console.log('\u2705 FormData prepared');
        
        // Disable button and show loading
        console.log('\uD83D\uDD12 Disabling submit button...');
        submitBtn.disabled = true;
        submitBtn.textContent = '\u23F3 Submitting...';
        showBatchLoading(true);
        hideBatchError();
        hideBatchSuccess();
        document.getElementById('batchInfoMessage').style.display = 'none';
        hideBatchResults();
        console.log('\u2705 UI updated (button disabled, loading shown)');
        
        try {
            console.log('\uD83D\uDCE4 Sending POST request to /create-batch-transcription...');
            
            const response = await fetch('/create-batch-transcription', {
                method: 'POST',
                body: formData
            });
            
            console.log('\uD83D\uDCE5 Response received:');
            console.log('  Status:', response.status);
            console.log('  Status Text:', response.statusText);
            console.log('  OK:', response.ok);
            
            const data = await response.json();
            console.log('\uD83D\uDCCB Response Data:', JSON.stringify(data, null, 2));
            
            if (data.success) {
                console.log('\u2705 Batch job created successfully!');
                console.log('  Job ID:', data.job?.id || 'N/A');
                console.log('  Job Name:', data.job?.displayName || 'N/A');
                console.log('  Status:', data.job?.status || 'N/A');
                displayBatchResults(data);
                showBatchSuccess(data.message);
                
                // Refresh the job list
                if (AppState.currentTab === 'batch') {
                    setTimeout(() => refreshJobList(), 1000);
                }
                
                console.log('\u2705 Results displayed');
            } else {
                console.error('\u274C Batch job creation failed:');
                console.error('  Message:', data.message);
                showBatchError(data.message || 'Failed to create batch job');
            }
        } catch (error) {
            console.error('='.repeat(80));
            console.error('\uD83D\uDEA8 EXCEPTION CAUGHT DURING BATCH SUBMISSION:');
            console.error('  Error type:', error.constructor.name);
            console.error('  Error message:', error.message);
            console.error('  Stack trace:', error.stack);
            console.error('='.repeat(80));
            showBatchError('An error occurred: ' + error.message);
        } finally {
            console.log('\uD83D\uDD04 Re-enabling submit button...');
            showBatchLoading(false);
            submitBtn.disabled = false;
            submitBtn.textContent = submitBtn.dataset.originalText || '?? Submit Batch Job';
            console.log('\u2705 UI restored (button enabled, loading hidden)');
            console.log('='.repeat(80));
            console.log('\uD83C\uDFC1 BATCH JOB SUBMISSION COMPLETED');
            console.log('='.repeat(80));
        }
    });
    
    console.log('\u2705 Batch form handler attached');
}

// Batch file preview
function setupBatchFilePreview() {
    const fileInput = document.getElementById('batchAudioFiles');
    if (!fileInput) return;
    
    fileInput.addEventListener('change', function(e) {
        const files = Array.from(e.target.files);
        const fileList = document.getElementById('fileList');
        
        if (files.length > 0) {
            fileList.style.display = 'block';
            fileList.innerHTML = files.map(f => `
                <div class="file-item">
                    <span>\uD83C\uDFB5 ${f.name}</span>
                    <span style="color: #666; font-size: 0.9rem;">${(f.size / 1024 / 1024).toFixed(2)} MB</span>
                </div>
            `).join('');
        } else {
            fileList.style.display = 'none';
        }
    });
    
    console.log('\u2705 Batch file preview handler attached');
}

// Display batch results
function displayBatchResults(data) {
    const jobInfoDiv = document.getElementById('batchJobInfo');
    if (!jobInfoDiv) return;
    
    const job = data.job || {};
    
    let html = '<div class="batch-job-details">';
    html += `<p><strong>Job ID:</strong> ${job.id || 'N/A'}</p>`;
    html += `<p><strong>Job Name:</strong> ${job.displayName || job.display_name || 'N/A'}</p>`;
    html += `<p><strong>Created:</strong> ${job.createdDateTime || job.created_date_time || 'N/A'}</p>`;
    
    if (job.files && job.files.length > 0) {
        html += `<p><strong>Files:</strong> ${job.files.length} file(s)</p>`;
        html += '<ul class="file-list">';
        job.files.forEach(file => {
            html += `<li>${file}</li>`;
        });
        html += '</ul>';
    }
    
    if (job.error) {
        html += `<p><strong>Note:</strong> <em>${job.error}</em></p>`;
    }
    
    html += '</div>';
    
    jobInfoDiv.innerHTML = html;
    showBatchResults();
}

// Export functions
export { setupRealtimeForm, setupBatchForm };
