// Batch Manager Module
import { AppState, resetAppState } from './app.js';
import { showBatchError, showBatchInfo, showBatchLoading, showBatchSuccess } from './ui-helpers.js';
import { displayResults } from './transcription-display.js';

export async function refreshJobList(forceRefresh = false) {
    console.log(`Refreshing batch job list${forceRefresh ? ' (forced)' : ''}...`);
    
    const loadingElement = document.getElementById('jobListLoading');
    const containerElement = document.getElementById('jobListContainer');
    const refreshBtn = document.getElementById('manualRefreshBtn');
    
    if (loadingElement) loadingElement.style.display = 'none';
    if (refreshBtn) refreshBtn.disabled = true;
    
    const estimatedCount = AppState.batchJobs.length > 0 ? AppState.batchJobs.length : 10;
    showProgressiveLoading(estimatedCount, true);
    
    try {
        const now = Date.now();
        const SAS_TOKEN_LIFETIME_MS = 12 * 60 * 60 * 1000;
        const REFRESH_BEFORE_EXPIRY_MS = 1 * 60 * 60 * 1000;
        const CACHE_VALID_DURATION_MS = SAS_TOKEN_LIFETIME_MS - REFRESH_BEFORE_EXPIRY_MS;
        
        const cachedJobs = forceRefresh ? [] : AppState.batchJobs
            .filter(job => {
                if (job.status !== 'Succeeded' && job.status !== 'Failed') return false;
                if (!job.lastFetchTime) return false;
                const timeSinceFetch = now - job.lastFetchTime;
                if (timeSinceFetch > CACHE_VALID_DURATION_MS) return false;
                return true;
            })
            .map(job => ({
                id: job.id,
                status: job.status,
                displayName: job.displayName,
                createdDateTime: job.createdDateTime,
                lastActionDateTime: job.lastActionDateTime,
                files: job.files,
                properties: job.properties,
                locale: job.locale,
                error: job.error,
                totalFileCount: job.totalFileCount,
                formattedDuration: job.formattedDuration,
                lastFetchTime: job.lastFetchTime
            }));
        
        let response;
        if (cachedJobs.length > 0) {
            response = await fetch('/batch-jobs', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ cachedJobs, forceRefresh })
            });
        } else {
            response = await fetch('/batch-jobs');
        }
        
        const data = await response.json();
        
        if (data.success) {
            const fetchTime = Date.now();
            AppState.batchJobs = data.jobs || [];
            
            if (AppState.batchJobs.length === 0) {
                if (containerElement) containerElement.innerHTML = '';
                renderJobList();
                return;
            }
            
            updateLoadingMessage(AppState.batchJobs.length, false);
            
            AppState.batchJobs.forEach(job => {
                if (!job.lastFetchTime || !cachedJobs.some(cached => cached.id === job.id)) {
                    job.lastFetchTime = fetchTime;
                }
            });
            
            const sortedJobs = [...AppState.batchJobs].sort((a, b) => {
                const dateA = new Date(a.createdDateTime || 0);
                const dateB = new Date(b.createdDateTime || 0);
                return dateB - dateA;
            });
            
            for (let i = 0; i < sortedJobs.length; i++) {
                setTimeout(() => {
                    if (i < 10) {
                        replaceSkeletonWithJob(sortedJobs[i], i);
                    } else {
                        const container = document.getElementById('jobListContainer');
                        if (container) {
                            const jobCard = document.createElement('div');
                            jobCard.innerHTML = renderJobCard(sortedJobs[i]);
                            jobCard.firstElementChild.classList.add('loading-complete');
                            const lastCard = container.lastElementChild;
                            if (lastCard) lastCard.after(jobCard.firstElementChild);
                        }
                    }
                    
                    updateLoadingProgress(i + 1, sortedJobs.length);
                    
                    if (i === sortedJobs.length - 1) {
                        setTimeout(() => {
                            const progressDiv = document.getElementById('loadingProgress');
                            if (progressDiv) {
                                progressDiv.style.transition = 'opacity 0.3s';
                                progressDiv.style.opacity = '0';
                                setTimeout(() => progressDiv.remove(), 300);
                            }
                        }, 500);
                    }
                }, i * 50);
            }
            
            setTimeout(() => checkCacheExpiration(), sortedJobs.length * 50 + 500);
        } else {
            showJobListError(data.message || 'Failed to fetch batch jobs');
        }
    } catch (error) {
        console.error('Error fetching batch jobs:', error);
        showJobListError('Error connecting to server: ' + error.message);
    } finally {
        if (loadingElement) loadingElement.style.display = 'none';
        if (refreshBtn) refreshBtn.disabled = false;
    }
}

export function renderJobList() {
    const container = document.getElementById('jobListContainer');
    if (!container) return;
    
    if (AppState.batchJobs.length === 0) {
        container.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">\u{1F4C2}</div>
                <div class="empty-state-text">No batch transcription jobs found</div>
                <p style="color: #999; font-size: 0.9rem; margin-top: 10px;">
                    Create a new batch job using the form below
                </p>
            </div>
        `;
        return;
    }
    
    const sortedJobs = [...AppState.batchJobs].sort((a, b) => {
        const dateA = new Date(a.createdDateTime || 0);
        const dateB = new Date(b.createdDateTime || 0);
        return dateB - dateA;
    });
    
    container.innerHTML = sortedJobs.map(job => renderJobCard(job)).join('');
}

function renderJobCard(job) {
    const isExpanded = AppState.expandedJobId === job.id;
    const statusClass = (job.status || 'Unknown').toLowerCase().replace(/\s+/g, '');
    const createdDate = job.createdDateTime ? new Date(job.createdDateTime).toLocaleString() : 'N/A';
    const lastActionDate = job.lastActionDateTime ? new Date(job.lastActionDateTime).toLocaleString() : 'N/A';
    const statusIcon = getStatusIcon(job.status);
    
    return `
        <div class="job-card ${isExpanded ? 'expanded' : ''}" onclick="toggleJobCard('${job.id}')">
            <div class="job-card-header">
                <div class="job-card-title">
                    <h3>${statusIcon} ${job.displayName || 'Untitled Job'}</h3>
                    <div class="job-id">ID: ${job.id}</div>
                </div>
                <span class="job-status ${statusClass}">${job.status || 'Unknown'}</span>
            </div>
            <div class="job-metadata">
                <div class="job-metadata-item">
                    <div class="job-metadata-label">Created</div>
                    <div class="job-metadata-value">${createdDate}</div>
                </div>
                <div class="job-metadata-item">
                    <div class="job-metadata-label">Last Updated</div>
                    <div class="job-metadata-value">${lastActionDate}</div>
                </div>
                <div class="job-metadata-item">
                    <div class="job-metadata-label">Files</div>
                    <div class="job-metadata-value">${job.totalFileCount || job.files?.length || 0} file(s)</div>
                </div>
                <div class="job-metadata-item">
                    <div class="job-metadata-label">Language</div>
                    <div class="job-metadata-value">${getLocaleFriendlyName(job.locale)}</div>
                </div>
            </div>
            <div class="job-details">
                ${job.properties ? renderJobProperties(job.properties, job.formattedDuration) : ''}
                ${job.files && job.files.length > 0 ? renderJobFiles(job.files) : ''}
                ${job.error ? `<div class="job-error-message"><strong>\u{26A0}\u{FE0F} Error:</strong> ${job.error}</div>` : ''}
                <div class="job-actions" onclick="event.stopPropagation()">
                    ${job.status === 'Succeeded' ? `<button class="job-action-btn view-results" onclick="viewJobResults('${job.id}')">\u{1F4CA} View Results</button>` : ''}
                    ${job.status === 'Running' || job.status === 'NotStarted' ? `<button class="job-action-btn refresh-status" onclick="refreshJobStatus('${job.id}')">\u{1F504} Refresh Status</button>` : ''}
                    <button class="job-action-btn delete" onclick="deleteJob('${job.id}', '${(job.displayName || 'this job').replace(/'/g, '\\\\')}')">\u{1F5D1}\u{FE0F} Delete</button>
                </div>
            </div>
        </div>
    `;
}

function renderJobProperties(properties, formattedDuration) {
    let html = '<div class="job-properties">';
    if (properties.succeededCount !== null && properties.succeededCount !== undefined) {
        html += `<div class="job-property"><div class="job-property-label">Succeeded</div><div class="job-property-value">${properties.succeededCount}</div></div>`;
    }
    if (properties.failedCount !== null && properties.failedCount !== undefined) {
        html += `<div class="job-property"><div class="job-property-label">Failed</div><div class="job-property-value">${properties.failedCount}</div></div>`;
    }
    if (formattedDuration) {
        html += `<div class="job-property"><div class="job-property-label">Duration</div><div class="job-property-value">${formattedDuration}</div></div>`;
    }
    html += '</div>';
    return html;
}

function renderJobFiles(files) {
    return `
        <div class="job-files-list">
            <h4>\u{1F4C1} Files (${files.length})</h4>
            <ul>${files.map(file => `<li>${file}</li>`).join('')}</ul>
        </div>
    `;
}

function getStatusIcon(status) {
    const icons = {
        'NotStarted': '\u{23F3}',
        'Running': '\u{2699}\u{FE0F}',
        'Succeeded': '\u{2705}',
        'Failed': '\u{274C}'
    };
    return icons[status] || '\u{2753}';
}

function getLocaleFriendlyName(localeCode) {
    if (!localeCode) return 'N/A';
    
    // Try to find the locale in the cached supported locales list
    if (AppState.supportedLocales && AppState.supportedLocales.length > 0) {
        const locale = AppState.supportedLocales.find(l => l.code === localeCode);
        if (locale && locale.name) {
            return locale.name;
        }
    }
    
    // Fallback: return the locale code itself
    return localeCode;
}

export function toggleJobCard(jobId) {
    AppState.expandedJobId = AppState.expandedJobId === jobId ? null : jobId;
    renderJobList();
}

export async function viewJobResults(jobId) {
    showBatchLoading(true);
    
    try {
        const filesResponse = await fetch(`/batch-job/${jobId}/files`);
        const filesData = await filesResponse.json();
        
        if (!filesData.success) {
            showBatchError(filesData.message || 'Failed to retrieve job files');
            showBatchLoading(false);
            return;
        }
        
        showBatchLoading(false);
        
        if (filesData.files.length === 1) {
            await loadJobResults(jobId, null);
            return;
        }
        
        if (filesData.files.length > 1) {
            showFileSelectionModal(jobId, filesData.files);
            return;
        }
        
        showBatchError('No transcription result files found for this job');
    } catch (error) {
        console.error('Error viewing job results:', error);
        showBatchError('Error retrieving job results: ' + error.message);
        showBatchLoading(false);
    }
}

async function loadJobResults(jobId, fileIndices) {
    console.log(`?? loadJobResults called with:`, { jobId, fileIndices });
    
    // ?? Reset application state before loading batch results
    resetAppState();
    
    showBatchLoading(true);
    
    try {
        let response;
        if (fileIndices && fileIndices.length > 0) {
            const url = `/batch-job/${jobId}/results`;
            const payload = { fileIndices };
            console.log(`?? POST ${url}`, payload);
            
            response = await fetch(url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            
            console.log(`?? Response status: ${response.status} ${response.statusText}`);
        } else {
            const url = `/batch-job/${jobId}/results`;
            console.log(`?? GET ${url}`);
            response = await fetch(url);
            console.log(`?? Response status: ${response.status} ${response.statusText}`);
        }
        
        if (!response.ok) {
            const errorText = await response.text();
            console.error(`? Request failed:`, errorText);
            showBatchError(`Server returned ${response.status}: ${response.statusText}`);
            return;
        }
        
        const data = await response.json();
        
        if (data.success) {
            AppState.transcriptionData = {
                success: true,
                message: data.message,
                segments: data.segments || [],
                fullTranscript: data.fullTranscript || '',
                availableSpeakers: data.availableSpeakers || [],
                speakerStatistics: data.speakerStatistics || [],
                rawJsonData: data.rawJsonData || '',
                goldenRecordJsonData: data.rawJsonData || '',
                auditLog: []
            };
            
            displayResults(AppState.transcriptionData);
            document.getElementById('tab-realtime').click();
            document.getElementById('resultsSection').scrollIntoView({ behavior: 'smooth' });
            showBatchSuccess(`Loaded transcription results from job: ${data.displayName || jobId}`);
        } else {
            showBatchError(data.message || 'Failed to retrieve job results');
        }
    } catch (error) {
        console.error('? Error loading job results:', error);
        showBatchError('Error retrieving job results: ' + error.message);
    } finally {
        showBatchLoading(false);
    }
}

export async function deleteJob(jobId, jobName) {
    if (!confirm(`Are you sure you want to delete job "${jobName}"?\n\nThis action cannot be undone.`)) {
        return;
    }
    
    try {
        const response = await fetch(`/batch-job/${jobId}`, { method: 'DELETE' });
        const data = await response.json();
        
        if (data.success) {
            showBatchSuccess(`Job "${jobName}" deleted successfully`);
            AppState.batchJobs = AppState.batchJobs.filter(j => j.id !== jobId);
            renderJobList();
            setTimeout(() => showBatchSuccess(''), 3000);
        } else {
            showBatchError(data.message || 'Failed to delete job');
        }
    } catch (error) {
        console.error('Error deleting job:', error);
        showBatchError('Error deleting job: ' + error.message);
    }
}

export function toggleAutoRefresh(enabled) {
    AppState.isAutoRefreshEnabled = enabled;
    const statusElement = document.getElementById('autoRefreshStatus');
    
    if (enabled) {
        refreshJobList();
        AppState.autoRefreshInterval = setInterval(() => {
            if (AppState.currentTab === 'batch') {
                refreshJobList();
            }
        }, AppState.autoRefreshSeconds * 1000);
        
        if (statusElement) {
            statusElement.textContent = `(every ${AppState.autoRefreshSeconds}s)`;
        }
    } else {
        if (AppState.autoRefreshInterval) {
            clearInterval(AppState.autoRefreshInterval);
            AppState.autoRefreshInterval = null;
        }
        if (statusElement) statusElement.textContent = '';
    }
}

function showProgressiveLoading(total, isFetching) {
    const container = document.getElementById('jobListContainer');
    if (!container) return;
    
    const statusText = isFetching ? 'Fetching jobs from Azure...' : 'Loading jobs: ';
    const countDisplay = isFetching ? '' : `<strong id="loadingCount">0</strong> / <strong id="loadingTotal">${total}</strong>`;
    
    let html = `<div class="loading-progress" id="loadingProgress"><div class="spinner-small"></div><span id="loadingProgressText">${statusText}${countDisplay}</span></div>`;
    for (let i = 0; i < Math.min(total, 10); i++) {
        html += createSkeletonCard(i);
    }
    container.innerHTML = html;
}

function createSkeletonCard(index) {
    return `
        <div class="job-card-skeleton" id="skeleton-${index}">
            <div class="skeleton-header">
                <div class="skeleton-title"><div class="skeleton-line title"></div><div class="skeleton-line subtitle"></div></div>
                <div class="skeleton-line status"></div>
            </div>
            <div class="skeleton-metadata">
                ${[1,2,3,4].map(() => '<div class="skeleton-metadata-item"><div class="skeleton-line label"></div><div class="skeleton-line value"></div></div>').join('')}
            </div>
        </div>
    `;
}

function updateLoadingMessage(total, isFetching) {
    const progressTextElement = document.getElementById('loadingProgressText');
    if (!progressTextElement) return;
    
    const statusText = isFetching ? 'Fetching jobs from Azure...' : 'Loading jobs: ';
    const countDisplay = isFetching ? '' : `<strong id="loadingCount">0</strong> / <strong id="loadingTotal">${total}</strong>`;
    progressTextElement.innerHTML = `${statusText}${countDisplay}`;
}

function updateLoadingProgress(current, total) {
    const countElement = document.getElementById('loadingCount');
    const totalElement = document.getElementById('loadingTotal');
    if (countElement) countElement.textContent = current;
    if (totalElement) totalElement.textContent = total;
}

function replaceSkeletonWithJob(job, index) {
    const skeleton = document.getElementById(`skeleton-${index}`);
    if (skeleton) {
        const jobCard = document.createElement('div');
        jobCard.innerHTML = renderJobCard(job);
        jobCard.firstElementChild.classList.add('loading-complete');
        skeleton.replaceWith(jobCard.firstElementChild);
    }
}

function showJobListError(message) {
    const container = document.getElementById('jobListContainer');
    if (container) {
        container.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">\u{274C}</div>
                <div class="empty-state-text" style="color: #f44336;">${message}</div>
                <button onclick="refreshJobList()" style="margin-top: 20px;" class="refresh-btn">\u{1F504} Try Again</button>
            </div>
        `;
    }
}

function checkCacheExpiration() {
    const now = Date.now();
    const SAS_TOKEN_LIFETIME_MS = 12 * 60 * 60 * 1000;
    const REFRESH_BEFORE_EXPIRY_MS = 1 * 60 * 60 * 1000;
    const WARNING_THRESHOLD_MS = 2 * 60 * 60 * 1000;
    
    const completedJobs = AppState.batchJobs.filter(job => 
        (job.status === 'Succeeded' || job.status === 'Failed') && job.lastFetchTime
    );
    
    if (completedJobs.length === 0) return;
    
    const oldestFetchTime = Math.min(...completedJobs.map(j => j.lastFetchTime));
    const timeSinceFetch = now - oldestFetchTime;
    const timeUntilRefresh = (SAS_TOKEN_LIFETIME_MS - REFRESH_BEFORE_EXPIRY_MS) - timeSinceFetch;
    
    const statusElement = document.getElementById('autoRefreshStatus');
    if (!statusElement) return;
    
    if (timeUntilRefresh <= WARNING_THRESHOLD_MS && timeUntilRefresh > 0) {
        const hoursLeft = Math.max(0, Math.ceil(timeUntilRefresh / 1000 / 60 / 60));
        // Use alarm clock emoji for cache expiration warning
        statusElement.innerHTML = `<span style="color: #ff9800;">\u23F0 Cache expires in ${hoursLeft}h</span>`;
    } else if (timeUntilRefresh <= 0) {
        // Use cross mark emoji for expired cache
        statusElement.innerHTML = `<span style="color: #f44336;">\u274C Cache expired</span>`;
    } else {
        const hoursValid = Math.floor(timeUntilRefresh / 1000 / 60 / 60);
        if (completedJobs.length > 0) {
            // Use check mark emoji for valid cache
            statusElement.innerHTML = `<span style="color: #4caf50;">\u2705 ${completedJobs.length} cached (valid ${hoursValid}h)</span>`;
        }
    }
}

let currentJobIdForModal = null;
let currentJobFilesForModal = [];
let draggedFileIndex = null;

function showFileSelectionModal(jobId, files) {
    console.log('?? Showing file selection modal for job:', jobId, 'with', files.length, 'files');
    console.log('?? File objects received:', JSON.stringify(files, null, 2));
    
    currentJobIdForModal = jobId;
    currentJobFilesForModal = files;
    
    const modal = document.getElementById('fileSelectionModal');
    const fileList = document.getElementById('fileSelectionList');
    const confirmBtn = document.getElementById('confirmFileSelectionBtn');
    
    // Populate file list with checkboxes and drag handles
    // Files are objects with properties: { index, name, url, size, sasExpiry, sasExpired }
    fileList.innerHTML = files.map((file, index) => {
        const fileName = typeof file === 'string' ? file : (file.name || `File ${index + 1}`);
        console.log(`   File ${index}: name="${fileName}", typeof=${typeof file}, hasName=${typeof file === 'object' && file.name ? 'yes' : 'no'}`);
        return `
            <div class="file-selection-item" 
                 draggable="true" 
                 data-file-index="${index}"
                 ondragstart="handleFileDragStart(event)"
                 ondragover="handleFileDragOver(event)"
                 ondrop="handleFileDrop(event)"
                 ondragenter="handleFileDragEnter(event)"
                 ondragleave="handleFileDragLeave(event)"
                 ondragend="handleFileDragEnd(event)">
                <span class="file-drag-handle"></span>
                <label class="file-checkbox-label">
                    <input type="checkbox" class="file-checkbox" data-file-index="${index}" onchange="updateFileSelection()">
                    <span class="file-name">${fileName}</span>
                </label>
            </div>
        `;
    }).join('');
    
    // Show modal
    modal.style.display = 'flex';
    
    // Disable confirm button initially
    confirmBtn.disabled = true;
    
    updateFileSelection();
}

function handleFileDragStart(event) {
    const item = event.currentTarget;
    draggedFileIndex = parseInt(item.dataset.fileIndex);
    item.classList.add('dragging');
    event.dataTransfer.effectAllowed = 'move';
    event.dataTransfer.setData('text/html', item.innerHTML);
    console.log('?? Drag started: index', draggedFileIndex);
}

function handleFileDragOver(event) {
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
}

function handleFileDragEnter(event) {
    const item = event.currentTarget;
    const currentIndex = parseInt(item.dataset.fileIndex);
    
    // Don't highlight the dragged item itself
    if (currentIndex !== draggedFileIndex) {
        item.classList.add('drag-over');
    }
}

function handleFileDragLeave(event) {
    const item = event.currentTarget;
    item.classList.remove('drag-over');
}

function handleFileDrop(event) {
    event.preventDefault();
    event.stopPropagation();
    
    const item = event.currentTarget;
    const targetIndex = parseInt(item.dataset.fileIndex);
    
    item.classList.remove('drag-over');
    
    // Don't do anything if dropped on itself
    if (draggedFileIndex === null || draggedFileIndex === targetIndex) {
        return;
    }
    
    console.log(`?? Drop: moving file from index ${draggedFileIndex} to ${targetIndex}`);
    
    // Get the checked state BEFORE reordering by mapping files to their checked state
    const checkedFiles = new Set();
    document.querySelectorAll('.file-checkbox:checked').forEach(cb => {
        const idx = parseInt(cb.dataset.fileIndex);
        checkedFiles.add(currentJobFilesForModal[idx]);
    });
    
    // Reorder the files array
    const draggedFile = currentJobFilesForModal[draggedFileIndex];
    currentJobFilesForModal.splice(draggedFileIndex, 1);
    currentJobFilesForModal.splice(targetIndex, 0, draggedFile);
    
    // Re-render the list
    const fileList = document.getElementById('fileSelectionList');
    
    fileList.innerHTML = currentJobFilesForModal.map((file, index) => {
        const fileName = typeof file === 'string' ? file : (file.name || `File ${index + 1}`);
        const isChecked = checkedFiles.has(file);
        return `
            <div class="file-selection-item" 
                 draggable="true" 
                 data-file-index="${index}"
                 ondragstart="handleFileDragStart(event)"
                 ondragover="handleFileDragOver(event)"
                 ondrop="handleFileDrop(event)"
                 ondragenter="handleFileDragEnter(event)"
                 ondragleave="handleFileDragLeave(event)"
                 ondragend="handleFileDragEnd(event)">
                <span class="file-drag-handle"></span>
                <label class="file-checkbox-label">
                    <input type="checkbox" class="file-checkbox" data-file-index="${index}" ${isChecked ? 'checked' : ''} onchange="updateFileSelection()">
                    <span class="file-name">${fileName}</span>
                </label>
            </div>
        `;
    }).join('');
    
    updateFileSelection();
}

function handleFileDragEnd(event) {
    const item = event.currentTarget;
    item.classList.remove('dragging');
    
    // Remove drag-over class from all items
    document.querySelectorAll('.file-selection-item').forEach(el => {
        el.classList.remove('drag-over');
    });
    
    draggedFileIndex = null;
    console.log('? Drag ended');
}

function closeFileSelectionModal() {
    const modal = document.getElementById('fileSelectionModal');
    modal.style.display = 'none';
    currentJobIdForModal = null;
    currentJobFilesForModal = [];
}

function updateFileSelection() {
    const checkboxes = document.querySelectorAll('.file-checkbox');
    const selectedCount = Array.from(checkboxes).filter(cb => cb.checked).length;
    const countDisplay = document.getElementById('selectedFileCount');
    const confirmBtn = document.getElementById('confirmFileSelectionBtn');
    
    countDisplay.textContent = `${selectedCount} file(s) selected`;
    confirmBtn.disabled = selectedCount === 0;
}

async function confirmFileSelection() {
    const checkboxes = document.querySelectorAll('.file-checkbox');
    const selectedIndices = Array.from(checkboxes)
        .filter(cb => cb.checked)
        .map(cb => parseInt(cb.dataset.fileIndex));
    
    if (selectedIndices.length === 0) {
        showBatchError('Please select at least one file');
        return;
    }
    
    console.log(`? Confirmed selection: ${selectedIndices.length} file(s) by index: ${selectedIndices.join(',')}`);
    
    // Log file names for debugging
    const selectedFileNames = selectedIndices.map(i => {
        const file = currentJobFilesForModal[i];
        return typeof file === 'string' ? file : (file.name || `File ${i}`);
    }).join(', ');
    console.log(`   File names: ${selectedFileNames}`);
    
    // IMPORTANT: Save job ID before closing modal (which clears it)
    const jobId = currentJobIdForModal;
    
    // Close modal
    closeFileSelectionModal();
    
    // Load results with selected file indices
    await loadJobResults(jobId, selectedIndices);
}

// Expose functions to window for HTML onclick handlers
window.toggleJobCard = toggleJobCard;
window.viewJobResults = viewJobResults;
window.deleteJob = deleteJob;
window.toggleAutoRefresh = toggleAutoRefresh;
window.refreshJobList = refreshJobList;
window.closeFileSelectionModal = closeFileSelectionModal;
window.updateFileSelection = updateFileSelection;
window.confirmFileSelection = confirmFileSelection;
window.handleFileDragStart = handleFileDragStart;
window.handleFileDragOver = handleFileDragOver;
window.handleFileDragEnter = handleFileDragEnter;
window.handleFileDragLeave = handleFileDragLeave;
window.handleFileDrop = handleFileDrop;
window.handleFileDragEnd = handleFileDragEnd;
