// API Client Module
// Centralized API communication functions

export async function fetchWithErrorHandling(url, options = {}) {
    try {
        const response = await fetch(url, options);
        
        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`HTTP ${response.status}: ${errorText || response.statusText}`);
        }
        
        return response;
    } catch (error) {
        console.error(`API Error for ${url}:`, error);
        throw error;
    }
}

export async function uploadAudioFile(formData, onProgress) {
    const xhr = new XMLHttpRequest();
    
    return new Promise((resolve, reject) => {
        xhr.upload.addEventListener('progress', (e) => {
            if (e.lengthComputable && onProgress) {
                const percentComplete = (e.loaded / e.total) * 100;
                onProgress(percentComplete);
            }
        });
        
        xhr.addEventListener('load', () => {
            if (xhr.status >= 200 && xhr.status < 300) {
                try {
                    const response = JSON.parse(xhr.responseText);
                    resolve(response);
                } catch (error) {
                    reject(new Error('Invalid JSON response'));
                }
            } else {
                reject(new Error(`Upload failed: ${xhr.status} ${xhr.statusText}`));
            }
        });
        
        xhr.addEventListener('error', () => {
            reject(new Error('Network error during upload'));
        });
        
        xhr.addEventListener('abort', () => {
            reject(new Error('Upload cancelled'));
        });
        
        xhr.open('POST', '/transcribe_realtime');
        xhr.send(formData);
    });
}

export async function transcribeBatch(formData) {
    const response = await fetchWithErrorHandling('/transcribe_batch', {
        method: 'POST',
        body: formData
    });
    return await response.json();
}

export async function getBatchJobs() {
    const response = await fetchWithErrorHandling('/batch_jobs');
    return await response.json();
}

export async function getBatchJobResults(jobId) {
    const response = await fetchWithErrorHandling(`/batch_job/${jobId}/results`);
    return await response.json();
}

export async function deleteBatchJob(jobId) {
    const response = await fetchWithErrorHandling(`/batch_job/${jobId}`, {
        method: 'DELETE'
    });
    return await response.json();
}

export async function downloadBatchResults(jobId, format) {
    const response = await fetchWithErrorHandling(`/batch_job/${jobId}/download?format=${format}`);
    return response;
}

export async function getSupportedLocales() {
    const response = await fetchWithErrorHandling('/supported-locales-with-names');
    return await response.json();
}

export async function updateSegment(index, text) {
    const response = await fetchWithErrorHandling('/update_segment', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ index, text })
    });
    return await response.json();
}

export async function updateSpeakers(segments) {
    const response = await fetchWithErrorHandling('/update_speakers', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ segments })
    });
    return await response.json();
}

export async function exportToWord() {
    const response = await fetchWithErrorHandling('/export_word', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ transcriptionData: window.AppState?.transcriptionData })
    });
    const blob = await response.blob();
    return blob;
}

export async function exportToJson() {
    const response = await fetchWithErrorHandling('/export_json', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ transcriptionData: window.AppState?.transcriptionData })
    });
    const blob = await response.blob();
    return blob;
}

export async function exportAuditLog() {
    const response = await fetchWithErrorHandling('/export_audit_log', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ transcriptionData: window.AppState?.transcriptionData })
    });
    const blob = await response.blob();
    return blob;
}
