// Transcription Jobs Management

let jobsData = [];
let autoRefreshTimer = null;
let autoRefreshEnabled = false;
let jobsTabInitialized = false;

// Get auto-refresh interval from configuration (default to 60 seconds if not set)
const AUTO_REFRESH_INTERVAL_MS = (window.appConfig?.batchJobAutoRefreshSeconds || 60) * 1000;

/**
 * Initialize jobs tab functionality
 */
function initializeJobsTab() {
    // Prevent double initialization
    if (jobsTabInitialized) {
        console.log('Jobs tab already initialized, skipping...');
        return;
    }
    
    // Check if jobs tab exists (it may be hidden by configuration)
    const jobsTab = document.getElementById('jobs-tab');
    if (!jobsTab) {
        console.log('Transcription Jobs tab is disabled by configuration');
        return;
    }

    console.log('Initializing jobs tab functionality...');
    console.log(`Auto-refresh configured for ${AUTO_REFRESH_INTERVAL_MS / 1000} seconds`);

    // Attach event listener to refresh button
    const refreshBtn = document.getElementById('refreshJobsBtn');
    if (refreshBtn) {
        // Remove any existing listeners
        const newRefreshBtn = refreshBtn.cloneNode(true);
        refreshBtn.parentNode.replaceChild(newRefreshBtn, refreshBtn);
        // Attach new listener
        newRefreshBtn.addEventListener('click', function(e) {
            e.preventDefault();
            console.log('Refresh button clicked');
            loadTranscriptionJobs();
        });
        console.log('? Refresh button event listener attached');
    } else {
        console.error('? Refresh button not found!');
    }

    // Attach event listener to auto-refresh checkbox
    const autoRefreshCheckbox = document.getElementById('autoRefreshJobsCheckbox');
    if (autoRefreshCheckbox) {
        autoRefreshCheckbox.addEventListener('change', handleAutoRefreshToggle);
        console.log('? Auto-refresh checkbox event listener attached');
    } else {
        console.warn('? Auto-refresh checkbox not found');
    }

    // Attach event listener to jobs tab to load jobs when clicked
    jobsTab.addEventListener('shown.bs.tab', function() {
        console.log('Jobs tab shown event fired');
        console.log('Current jobsData length:', jobsData.length);
        // Always try to load jobs when tab is shown
        console.log('Loading jobs...');
        loadTranscriptionJobs();
    });

    // Attach event listener to jobs tab to stop auto-refresh when hidden
    jobsTab.addEventListener('hidden.bs.tab', function() {
        console.log('Jobs tab hidden, stopping auto-refresh');
        stopAutoRefresh();
    });
    
    jobsTabInitialized = true;
    console.log('? Jobs tab initialization complete');
}

/**
 * Re-initialize jobs tab (force re-attach event listeners)
 */
function reinitializeJobsTab() {
    console.log('Re-initializing jobs tab...');
    jobsTabInitialized = false;
    initializeJobsTab();
}

// Expose reinitialize function globally
window.reinitializeJobsTab = reinitializeJobsTab;

/**
 * Handle auto-refresh checkbox toggle
 */
function handleAutoRefreshToggle(event) {
    autoRefreshEnabled = event.target.checked;
    
    if (autoRefreshEnabled) {
        console.log(`Auto-refresh enabled (${AUTO_REFRESH_INTERVAL_MS / 1000}s interval)`);
        // Load jobs immediately when enabled
        loadTranscriptionJobs();
    } else {
        console.log('Auto-refresh disabled');
        stopAutoRefresh();
    }
}

/**
 * Start auto-refresh timer
 */
function startAutoRefresh() {
    // Only start if enabled and there are running jobs
    if (!autoRefreshEnabled) {
        return;
    }
    
    const hasRunningJobs = jobsData.some(job => 
        job.status === 'Running' || job.status === 'NotStarted'
    );
    
    if (hasRunningJobs) {
        // Clear any existing timer
        stopAutoRefresh();
        
        console.log(`Starting auto-refresh timer (${AUTO_REFRESH_INTERVAL_MS / 1000}s)`);
        autoRefreshTimer = setTimeout(() => {
            console.log('Auto-refresh triggered');
            loadTranscriptionJobs();
        }, AUTO_REFRESH_INTERVAL_MS);
    } else {
        console.log('No running jobs, auto-refresh timer not started');
    }
}

/**
 * Stop auto-refresh timer
 */
function stopAutoRefresh() {
    if (autoRefreshTimer) {
        console.log('Stopping auto-refresh timer');
        clearTimeout(autoRefreshTimer);
        autoRefreshTimer = null;
    }
}

// Initialize on DOM ready
document.addEventListener('DOMContentLoaded', function() {
    console.log('DOM ready, initializing jobs tab...');
    initializeJobsTab();
});

// Also try to initialize after a delay (in case DOM ready already fired)
setTimeout(function() {
    if (!jobsTabInitialized) {
        console.log('Late initialization attempt...');
        initializeJobsTab();
    }
}, 1000);

/**
 * Load transcription jobs from the server
 */
async function loadTranscriptionJobs() {
    console.log('=== loadTranscriptionJobs called ===');
    const loadingSection = document.getElementById('jobsLoadingSection');
    const errorSection = document.getElementById('jobsErrorSection');
    const emptySection = document.getElementById('jobsEmptySection');
    const jobsList = document.getElementById('jobsList');
    const refreshBtn = document.getElementById('refreshJobsBtn');

    // Check if elements exist
    if (!jobsList) {
        console.error('? jobsList element not found!');
        return;
    }

    // Stop any existing auto-refresh timer
    stopAutoRefresh();

    // Show loading state
    if (loadingSection) loadingSection.classList.remove('d-none');
    if (errorSection) errorSection.classList.add('d-none');
    if (emptySection) emptySection.classList.add('d-none');
    if (jobsList) jobsList.innerHTML = '';
    if (refreshBtn) {
        refreshBtn.disabled = true;
        const icon = refreshBtn.querySelector('i');
        if (icon) icon.classList.add('spin');
    }

    try {
        console.log('Fetching transcription jobs from /Home/GetTranscriptionJobs');
        const response = await fetch('/Home/GetTranscriptionJobs', {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                'Cache-Control': 'no-cache'
            }
        });

        console.log('Response received, status:', response.status);
        
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        
        const data = await response.json();
        console.log('Jobs data received:', data);
        console.log('Success:', data.success);
        console.log('Jobs count:', data.jobs?.length || 0);

        if (data.success && data.jobs) {
            console.log(`Retrieved ${data.jobs.length} jobs`);
            jobsData = data.jobs;
            
            // Check if jobs have authentication-related errors
            const hasAuthErrors = data.jobs.some(job => 
                job.error && (
                    job.error.toLowerCase().includes('authentication') ||
                    job.error.toLowerCase().includes('authorization') ||
                    job.error.toLowerCase().includes('recordings uri')
                )
            );
            
            if (hasAuthErrors) {
                console.warn('Some jobs have authentication errors - this may indicate Azure Blob Storage is not configured');
            }
            
            displayTranscriptionJobs(data.jobs);
            
            // Start auto-refresh if enabled and there are running jobs
            startAutoRefresh();
        } else {
            let errorMessage = data.message || 'Failed to load transcription jobs';
            console.error('Failed to load jobs:', errorMessage);
            
            // Provide helpful guidance for common errors
            if (errorMessage.toLowerCase().includes('authentication') || 
                errorMessage.toLowerCase().includes('recordings uri')) {
                errorMessage += '\n\nNote: Batch transcription requires Azure Blob Storage to be configured. ' +
                              'See AZURE_BLOB_STORAGE_SETUP.md for setup instructions.';
            }
            
            showJobsError(errorMessage);
        }
    } catch (error) {
        console.error('Error loading transcription jobs:', error);
        console.error('Error details:', {
            name: error.name,
            message: error.message,
            stack: error.stack
        });
        
        let errorMessage = 'An error occurred while loading transcription jobs';
        
        // Check if error is related to network/CORS
        if (error.name === 'TypeError' && error.message.includes('fetch')) {
            errorMessage += ' (Network error - check your connection)';
        } else if (error.message) {
            errorMessage += ': ' + error.message;
        }
        
        showJobsError(errorMessage);
    } finally {
        // Hide loading state
        if (loadingSection) loadingSection.classList.add('d-none');
        if (refreshBtn) {
            refreshBtn.disabled = false;
            const icon = refreshBtn.querySelector('i');
            if (icon) icon.classList.remove('spin');
        }
        console.log('=== loadTranscriptionJobs completed ===');
    }
}

// Make loadTranscriptionJobs available globally for other scripts to call
window.loadTranscriptionJobs = loadTranscriptionJobs;
console.log('? transcription-jobs.js loaded, loadTranscriptionJobs exposed to window');
console.log('  typeof window.loadTranscriptionJobs:', typeof window.loadTranscriptionJobs);

/**
 * Display transcription jobs in the UI
 */
function displayTranscriptionJobs(jobs) {
    const jobsList = document.getElementById('jobsList');
    const emptySection = document.getElementById('jobsEmptySection');

    if (!jobs || jobs.length === 0) {
        if (emptySection) emptySection.classList.remove('d-none');
        return;
    }

    if (emptySection) emptySection.classList.add('d-none');
    if (!jobsList) return;

    jobsList.innerHTML = '';
    
    // DIAGNOSTIC: Log the first job to see what data we're getting
    if (jobs.length > 0) {
        console.log('=== FIRST JOB DATA STRUCTURE ===');
        const firstJob = jobs[0];
        console.log('Full job object:', firstJob);
        console.log('Job properties check:');
        console.log('  - locale:', firstJob.locale);
        console.log('  - formattedLocale:', firstJob.formattedLocale);
        console.log('  - formattedDuration:', firstJob.formattedDuration);
        console.log('  - totalFileCount:', firstJob.totalFileCount);
        console.log('  - files:', firstJob.files);
        console.log('  - files.length:', firstJob.files?.length);
        console.log('  - properties:', firstJob.properties);
        if (firstJob.properties) {
            console.log('  - properties.duration:', firstJob.properties.duration);
            console.log('  - properties.succeededCount:', firstJob.properties.succeededCount);
            console.log('  - properties.failedCount:', firstJob.properties.failedCount);
        }
        console.log('================================');
    }

    jobs.forEach(job => {
        const jobItem = createJobListItem(job);
        jobsList.appendChild(jobItem);
    });
}

/**
 * Create a list item element for a transcription job
 */
function createJobListItem(job) {
    const listItem = document.createElement('div');
    listItem.className = 'list-group-item job-list-item';
    listItem.setAttribute('data-job-id', job.id);
    listItem.style.cursor = 'pointer';
    
    // Add a data attribute to track if details have been fetched
    listItem.setAttribute('data-details-fetched', 'false');

    const statusBadge = getStatusBadge(job.status);
    const statusIcon = getStatusIcon(job.status);

    const createdDate = new Date(job.createdDateTime).toLocaleString();
    const lastActionDate = job.lastActionDateTime ? new Date(job.lastActionDateTime).toLocaleString() : 'N/A';

    // Locale display
    const locale = job.formattedLocale || job.locale || 'N/A';
    const localeHtml = locale !== 'N/A' 
        ? `<div><strong><i class="bi bi-translate"></i> Language:</strong> ${escapeHtml(locale)}</div>`
        : '';

    // Duration display
    const duration = job.formattedDuration || 'N/A';
    const durationHtml = duration !== 'N/A' 
        ? `<div><strong><i class="bi bi-clock"></i> Duration:</strong> ${duration}</div>`
        : '';

    // Success/Failure counts (only show if available)
    let successFailureHtml = '';
    if (job.properties && (job.properties.succeededCount !== null || job.properties.failedCount !== null)) {
        const succeeded = job.properties.succeededCount ?? 0;
        const failed = job.properties.failedCount ?? 0;
        
        if (succeeded > 0 || failed > 0) {
            successFailureHtml = `
                <div class="mt-2">
                    ${succeeded > 0 ? `
                        <small class="text-success">
                            <i class="bi bi-check-circle-fill"></i> ${succeeded} file${succeeded !== 1 ? 's' : ''} succeeded
                        </small>
                    ` : ''}
                    ${failed > 0 ? `
                        <small class="text-danger ${succeeded > 0 ? 'ms-2' : ''}">
                            <i class="bi bi-x-circle-fill"></i> ${failed} file${failed !== 1 ? 's' : ''} failed
                        </small>
                    ` : ''}
                </div>
            `;
        }
    }

    // Files section with collapse functionality
    let filesHtml = '';
    const fileCount = job.totalFileCount || 0;
    if (fileCount > 0) {
        const collapseId = `files-${job.id.replace(/[^a-zA-Z0-9]/g, '-')}`;
        
        filesHtml = `
            <div class="mt-2">
                <strong><i class="bi bi-file-earmark-music"></i> Files:</strong>
                <span class="badge bg-info">${fileCount} file${fileCount !== 1 ? 's' : ''}</span>
                ${fileCount > 1 && job.files && job.files.length > 0 ? `
                    <button class="btn btn-sm btn-link p-0 ms-1" type="button" data-bs-toggle="collapse" 
                            data-bs-target="#${collapseId}" aria-expanded="false" onclick="event.stopPropagation();">
                        <i class="bi bi-chevron-down"></i> <span class="toggle-text">Show Details</span>
                    </button>
                ` : ''}
            </div>
            ${fileCount > 1 && job.files && job.files.length > 0 ? `
                <div id="${collapseId}" class="collapse">
                    <ul class="mb-0 mt-2 small">
                        ${job.files.map(file => `<li>${escapeHtml(file)}</li>`).join('')}
                    </ul>
                </div>
            ` : fileCount === 1 && job.files && job.files.length > 0 ? `
                <div class="small text-muted ms-4">${escapeHtml(job.files[0])}</div>
            ` : ''}
        `;
    }

    // Details loading section (initially hidden)
    const detailsSection = `
        <div class="job-details-section mt-3 d-none" data-details-for="${job.id}">
            <div class="spinner-border spinner-border-sm text-primary" role="status">
                <span class="visually-hidden">Loading details...</span>
            </div>
            <span class="ms-2 text-muted">Loading additional details...</span>
        </div>
    `;

    let errorHtml = '';
    if (job.error) {
        errorHtml = `
            <div class="alert alert-danger mt-2 mb-0">
                <strong><i class="bi bi-exclamation-triangle"></i> Error:</strong> ${escapeHtml(job.error)}
            </div>
        `;
    }

    const canCancel = ['NotStarted', 'Running'].includes(job.status);
    const canViewResults = job.status === 'Succeeded';

    listItem.innerHTML = `
        <div class="d-flex justify-content-between align-items-start">
            <div class="flex-grow-1">
                <div class="d-flex align-items-center mb-2">
                    ${statusIcon}
                    <h6 class="mb-0 ms-2">${escapeHtml(job.displayName)}</h6>
                    ${statusBadge}
                    <small class="text-muted ms-2">
                        <i class="bi bi-hand-index-thumb"></i> Click for details
                    </small>
                </div>
                <div class="text-muted small job-summary-info">
                    <div><strong>Job ID:</strong> <code>${escapeHtml(job.id)}</code></div>
                    <div><strong>Created:</strong> ${createdDate}</div>
                    <div><strong>Last Action:</strong> ${lastActionDate}</div>
                    ${localeHtml}
                    ${durationHtml}
                </div>
                ${successFailureHtml}
                ${filesHtml}
                ${detailsSection}
                ${errorHtml}
            </div>
            <div class="ms-3 d-flex flex-column gap-2 job-action-buttons">
                ${canViewResults ? `
                    <button class="btn btn-sm btn-success view-results-btn" data-job-id="${escapeHtml(job.id)}" data-job-name="${escapeHtml(job.displayName)}" title="View Transcription Results" onclick="event.stopPropagation();">
                        <i class="bi bi-eye"></i> View Results
                    </button>
                ` : ''}

                ${canCancel ? `
                    <button class="btn btn-sm btn-danger cancel-job-btn" data-job-id="${escapeHtml(job.id)}" title="Cancel Job" onclick="event.stopPropagation();">
                        <i class="bi bi-x-circle"></i> Cancel
                    </button>
                ` : ''}
            </div>
        </div>
    `;

    // Add click handler to fetch details
    listItem.addEventListener('click', async function(e) {
        // Don't trigger if clicking on buttons or other interactive elements
        if (e.target.closest('.job-action-buttons') || 
            e.target.closest('[data-bs-toggle="collapse"]')) {
            return;
        }

        await fetchAndDisplayJobDetails(job.id, listItem);
    });

    // Attach view results button event
    const viewResultsBtn = listItem.querySelector('.view-results-btn');
    if (viewResultsBtn) {
        viewResultsBtn.addEventListener('click', () => viewBatchTranscriptionResults(job.id, job.displayName));
    }

    // Attach cancel button event
    const cancelBtn = listItem.querySelector('.cancel-job-btn');
    if (cancelBtn) {
        cancelBtn.addEventListener('click', () => cancelTranscriptionJob(job.id));
    }

    // Toggle collapse button text
    const collapseToggle = listItem.querySelector('[data-bs-toggle="collapse"]');
    if (collapseToggle) {
        const collapseElement = listItem.querySelector(collapseToggle.getAttribute('data-bs-target'));
        if (collapseElement) {
            collapseElement.addEventListener('show.bs.collapse', () => {
                const icon = collapseToggle.querySelector('i');
                const text = collapseToggle.querySelector('.toggle-text');
                if (icon) icon.className = 'bi bi-chevron-up';
                if (text) text.textContent = 'Hide Details';
            });
            collapseElement.addEventListener('hide.bs.collapse', () => {
                const icon = collapseToggle.querySelector('i');
                const text = collapseToggle.querySelector('.toggle-text');
                if (icon) icon.className = 'bi bi-chevron-down';
                if (text) text.textContent = 'Show Details';
            });
        }
    }

    return listItem;
}

/**
 * Fetch and display detailed job information
 */
async function fetchAndDisplayJobDetails(jobId, listItem) {
    // Check if details already fetched
    const alreadyFetched = listItem.getAttribute('data-details-fetched') === 'true';
    
    const detailsSection = listItem.querySelector(`[data-details-for="${jobId}"]`);
    
    if (alreadyFetched) {
        // Toggle visibility of existing details
        if (detailsSection) {
            detailsSection.classList.toggle('d-none');
        }
        return;
    }

    // Show loading state
    if (detailsSection) {
        detailsSection.classList.remove('d-none');
        detailsSection.innerHTML = `
            <div class="spinner-border spinner-border-sm text-primary" role="status">
                <span class="visually-hidden">Loading...</span>
            </div>
            <span class="ms-2 text-muted">Loading job details...</span>
        `;
    }

    try {
        console.log(`Fetching detailed info for job: ${jobId}`);
        
        const response = await fetch(`/Home/GetTranscriptionJob?jobId=${encodeURIComponent(jobId)}`, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                'Cache-Control': 'no-cache'
            }
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const jobDetails = await response.json();
        
        console.log('Job details received:', jobDetails);

        // Mark as fetched
        listItem.setAttribute('data-details-fetched', 'true');

        // Display the details
        if (detailsSection) {
            renderJobDetails(jobDetails, detailsSection);
        }

    } catch (error) {
        console.error('Error fetching job details:', error);
        
        if (detailsSection) {
            detailsSection.innerHTML = `
                <div class="alert alert-warning mb-0">
                    <i class="bi bi-exclamation-triangle"></i> 
                    Unable to load additional details. ${error.message}
                </div>
            `;
        }
    }
}

/**
 * Render detailed job information
 */
function renderJobDetails(job, container) {
    // Build detailed info HTML
    let detailsHtml = '<div class="card"><div class="card-body">';
    
    detailsHtml += '<h6 class="card-title"><i class="bi bi-info-circle"></i> Additional Details</h6>';
    
    // Content URLs
    if (job.contentUrls && job.contentUrls.length > 0) {
        detailsHtml += '<div class="mb-2"><strong>Audio Files:</strong></div>';
        detailsHtml += '<ul class="small mb-3">';
        job.contentUrls.forEach((url, idx) => {
            const fileName = url.split('/').pop().split('?')[0]; // Extract filename
            detailsHtml += `<li><code>${escapeHtml(decodeURIComponent(fileName))}</code></li>`;
        });
        detailsHtml += '</ul>';
    }
    
    // Results URL
    if (job.resultsUrl) {
        detailsHtml += `
            <div class="mb-2">
                <strong>Results URL:</strong>
                <div class="small"><code>${escapeHtml(job.resultsUrl)}</code></div>
            </div>
        `;
    }
    
    // Detailed properties
    if (job.properties) {
        detailsHtml += '<div class="mb-2"><strong>Job Properties:</strong></div>';
        detailsHtml += '<div class="small mb-2">';
        
        if (job.properties.duration !== null && job.properties.duration !== undefined) {
            const durationSeconds = job.properties.duration / 10000000;
            const hours = Math.floor(durationSeconds / 3600);
            const minutes = Math.floor((durationSeconds % 3600) / 60);
            const seconds = Math.floor(durationSeconds % 60);
            detailsHtml += `<div><i class="bi bi-clock-fill text-info"></i> Duration: ${hours}h ${minutes}m ${seconds}s (${job.properties.duration.toLocaleString()} ticks)</div>`;
        }
        
        if (job.properties.succeededCount !== null && job.properties.succeededCount !== undefined) {
            detailsHtml += `<div><i class="bi bi-check-circle-fill text-success"></i> Succeeded: ${job.properties.succeededCount}</div>`;
        }
        
        if (job.properties.failedCount !== null && job.properties.failedCount !== undefined) {
            detailsHtml += `<div><i class="bi bi-x-circle-fill text-danger"></i> Failed: ${job.properties.failedCount}</div>`;
        }
        
        if (job.properties.errorMessage) {
            detailsHtml += `<div class="text-danger"><i class="bi bi-exclamation-triangle-fill"></i> Error: ${escapeHtml(job.properties.errorMessage)}</div>`;
        }
        
        detailsHtml += '</div>';
    }
    
    // Files list (if available and different from what's already shown)
    if (job.files && job.files.length > 0 && job.files.length !== job.totalFileCount) {
        detailsHtml += '<div class="mb-2"><strong>Files in Job:</strong></div>';
        detailsHtml += '<ul class="small mb-2">';
        job.files.forEach(file => {
            detailsHtml += `<li>${escapeHtml(file)}</li>`;
        });
        detailsHtml += '</ul>';
    }
    
    // Links section
    const apiBaseUrl = window.location.origin;
    detailsHtml += '<div class="mt-3"><strong>API Links:</strong></div>';
    detailsHtml += '<div class="small">';
    detailsHtml += `<div><a href="/Home/GetTranscriptionJob?jobId=${encodeURIComponent(job.id)}" target="_blank" class="text-decoration-none"><i class="bi bi-link-45deg"></i> Job Details JSON</a></div>`;
    if (job.status === 'Succeeded') {
        detailsHtml += `<div><a href="/Home/GetBatchTranscriptionResults?jobId=${encodeURIComponent(job.id)}" target="_blank" class="text-decoration-none"><i class="bi bi-link-45deg"></i> Transcription Results JSON</a></div>`;
    }
    detailsHtml += '</div>';
    
    detailsHtml += '</div></div>'; // Close card-body and card
    
    container.innerHTML = detailsHtml;
}

/**
 * Get status badge HTML based on job status
 */
function getStatusBadge(status) {
    const statusLower = status.toLowerCase();
    let badgeClass = 'bg-secondary';

    switch (statusLower) {
        case 'succeeded':
            badgeClass = 'bg-success';
            break;
        case 'running':
            badgeClass = 'bg-primary';
            break;
        case 'failed':
            badgeClass = 'bg-danger';
            break;
        case 'notstarted':
            badgeClass = 'bg-warning text-dark';
            break;
    }

    return `<span class="badge ${badgeClass} ms-2">${escapeHtml(status)}</span>`;
}

/**
 * Get status icon based on job status
 */
function getStatusIcon(status) {
    const statusLower = status.toLowerCase();
    let iconClass = 'bi-question-circle';
    let iconColor = '#6c757d';

    switch (statusLower) {
        case 'succeeded':
            iconClass = 'bi-check-circle-fill';
            iconColor = '#198754';
            break;
        case 'running':
            iconClass = 'bi-arrow-repeat';
            iconColor = '#0d6efd';
            break;
        case 'failed':
            iconClass = 'bi-x-circle-fill';
            iconColor = '#dc3545';
            break;
        case 'notstarted':
            iconClass = 'bi-hourglass-split';
            iconColor = '#ffc107';
            break;
    }

    return `<i class="bi ${iconClass}" style="color: ${iconColor}; font-size: 1.5rem;"></i>`;
}

/**
 * Cancel a transcription job
 */
async function cancelTranscriptionJob(jobId) {
    if (!confirm('Are you sure you want to cancel this transcription job?')) {
        return;
    }

    const jobItem = document.querySelector(`[data-job-id="${jobId}"]`);
    const cancelBtn = jobItem?.querySelector('.cancel-job-btn');

    if (cancelBtn) {
        cancelBtn.disabled = true;
        cancelBtn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status"></span> Canceling...';
    }

    try {
        const response = await fetch('/Home/CancelTranscriptionJob', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ jobId: jobId })
        });

        const data = await response.json();

        if (data.success) {
            showAlert('success', 'Job canceled successfully');
            // Remove the job from the list
            if (jobItem) {
                jobItem.remove();
            }
            // Update the jobs data
            jobsData = jobsData.filter(j => j.id !== jobId);
            // Check if list is empty
            if (jobsData.length === 0) {
                const emptySection = document.getElementById('jobsEmptySection');
                if (emptySection) emptySection.classList.remove('d-none');
            }
            
            // Restart auto-refresh timer if enabled (to check remaining jobs)
            startAutoRefresh();
        } else {
            showAlert('danger', data.message || 'Failed to cancel job');
            if (cancelBtn) {
                cancelBtn.disabled = false;
                cancelBtn.innerHTML = '<i class="bi bi-x-circle"></i> Cancel';
            }
        }
    } catch (error) {
        console.error('Error canceling transcription job:', error);
        showAlert('danger', 'An error occurred while canceling the job');
        if (cancelBtn) {
            cancelBtn.disabled = false;
            cancelBtn.innerHTML = '<i class="bi bi-x-circle"></i> Cancel';
        }
    }
}

/**
 * Show error message in jobs section
 */
function showJobsError(message) {
    const errorSection = document.getElementById('jobsErrorSection');
    const errorMessage = document.getElementById('jobsErrorMessage');

    if (errorSection && errorMessage) {
        errorMessage.textContent = message;
        errorSection.classList.remove('d-none');
    }
}

/**
 * Show alert message
 */
function showAlert(type, message) {
    // Check if showAlertMessage function exists from transcription.js
    if (typeof showAlertMessage === 'function') {
        showAlertMessage(type, message);
    } else {
        // Fallback: use the existing error/success sections
        console.log(`Alert (${type}): ${message}`);
        
        if (type === 'success') {
            const successSection = document.getElementById('successSection');
            if (successSection) {
                successSection.textContent = message;
                successSection.classList.remove('d-none');
                // Auto-hide after 5 seconds
                setTimeout(() => successSection.classList.add('d-none'), 5000);
            }
        } else if (type === 'danger' || type === 'warning') {
            const errorSection = document.getElementById('errorSection');
            const errorMessage = document.getElementById('errorMessage');
            const errorLabel = document.getElementById('errorLabel');
            
            if (errorSection && errorMessage && errorLabel) {
                errorLabel.textContent = type === 'warning' ? 'Warning:' : 'Error:';
                errorMessage.textContent = message;
                errorSection.classList.remove('d-none');
                if (type === 'warning') {
                    errorSection.classList.remove('alert-danger');
                    errorSection.classList.add('alert-warning');
                } else {
                    errorSection.classList.remove('alert-warning');
                    errorSection.classList.add('alert-danger');
                }
            }
        }
    }
}

/**
 * Escape HTML to prevent XSS
 */
function escapeHtml(text) {
    const map = {
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#039;'
    };
    return text.replace(/[&<>"']/g, m => map[m]);
}

/**
 * View transcription results for a completed batch job
 */
async function viewBatchTranscriptionResults(jobId, jobName, fileIndex = null) {
    console.log(`Loading transcription results for job: ${jobId}, fileIndex: ${fileIndex}`);

    // Show loading indicator
    const loadingIndicator = document.getElementById('loading-indicator');
    const errorContainer = document.getElementById('error-container');
    
    if (loadingIndicator) {
        loadingIndicator.classList.remove('d-none');
        loadingIndicator.textContent = fileIndex !== null 
            ? 'Loading file transcription...' 
            : 'Loading transcription results...';
    }
    
    if (errorContainer) {
        errorContainer.classList.add('d-none');
    }

    try {
        // Build URL with optional fileIndex parameter
        let url = `/Home/GetBatchTranscriptionResults?jobId=${encodeURIComponent(jobId)}`;
        if (fileIndex !== null) {
            url += `&fileIndex=${fileIndex}`;
        }
        
        console.log(`Fetching from URL: ${url}`);
        
        const response = await fetch(url, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        console.log(`Response status: ${response.status}`);
        
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const data = await response.json();
        
        console.log(`? Successfully received response for job ${jobId}`);
        console.log('Response data structure:', {
            success: data.success,
            message: data.message,
            hasSegments: !!data.segments,
            segmentsCount: data.segments?.length || 0,
            totalFiles: data.totalFiles,
            hasFileResults: !!data.fileResults,
            fileResultsCount: data.fileResults?.length || 0,
            keys: Object.keys(data)
        });

        if (!data.success) {
            console.error('? Server returned success=false:', data.message);
            showAlert('danger', data.message || 'Failed to load transcription results');
            return;
        }

        // DETAILED LOGGING: Check segment structure
        if (data.segments && data.segments.length > 0) {
            console.log('? Segments found:', data.segments.length);
            console.log('First segment sample:', data.segments[0]);
            console.log('First segment has required properties:', {
                hasLineNumber: 'lineNumber' in data.segments[0],
                hasSpeaker: 'speaker' in data.segments[0],
                hasText: 'text' in data.segments[0],
                hasOffsetInTicks: 'offsetInTicks' in data.segments[0],
                hasDurationInTicks: 'durationInTicks' in data.segments[0],
                hasUIFormattedStartTime: 'uiFormattedStartTime' in data.segments[0]
            });
        } else {
            console.warn('?? NO SEGMENTS in response!');
            if (data.fileResults && data.fileResults.length > 0) {
                console.log('But fileResults exist:', data.fileResults.length);
                console.log('First fileResult:', data.fileResults[0]);
                if (data.fileResults[0].segments) {
                    console.log('First fileResult has segments:', data.fileResults[0].segments.length);
                }
            }
        }

        // Check if this is initial load (no fileIndex) and there are multiple files
        // Show modal only on initial load when multiple files exist
        if (fileIndex === null && data.totalFiles > 1 && data.fileResults && data.fileResults.length > 1) {
            console.log('Multiple files detected, showing file selection modal');
            showFileSelectionModal(jobId, jobName, data);
            return; // Stop here - let user choose
        }
        
        // Single file or specific file selected - display results
        console.log('Preparing to display transcription results...');
        
        // Validate that we have segments to display
        if (!data.segments || data.segments.length === 0) {
            console.error('? Cannot display results: No segments available');
            showAlert('warning', 'Transcription completed but no segments were found. The audio may not contain speech or there may have been an error during transcription.');
            return;
        }
        
        // Check if displayTranscriptionResults function exists
        if (typeof window.displayTranscriptionResults !== 'function') {
            console.error('? displayTranscriptionResults function not found on window object');
            console.log('Available window functions:', Object.keys(window).filter(k => k.includes('display')));
            showAlert('danger', 'Display function not available. Please refresh the page and try again.');
            return;
        }
        
        console.log('? Calling window.displayTranscriptionResults with data');
        console.log('Data being passed:', {
            segmentsCount: data.segments.length,
            hasFullTranscript: !!data.fullTranscript,
            hasAudioFileUrl: !!data.audioFileUrl,
            hasRawJsonData: !!data.rawJsonData,
            hasGoldenRecordJsonData: !!data.goldenRecordJsonData,
            hasAvailableSpeakers: !!data.availableSpeakers,
            speakersCount: data.availableSpeakers?.length || 0
        });
        
        // Display results using the existing transcription display function
        try {
            window.displayTranscriptionResults(data);
            console.log('? displayTranscriptionResults executed successfully');
        } catch (displayError) {
            console.error('? Error in displayTranscriptionResults:', displayError);
            console.error('Stack trace:', displayError.stack);
            showAlert('danger', `Error displaying results: ${displayError.message}. Check console for details.`);
            return;
        }
        
        // Switch to the Speaker Segments tab to show the transcription
        console.log('Switching to segments tab...');
        const segmentsTab = document.getElementById('segments-tab');
        if (segmentsTab) {
            const tab = new bootstrap.Tab(segmentsTab);
            tab.show();
            console.log('? Switched to segments-tab (Speaker Segments)');
        } else {
            console.warn('?? segments-tab element not found');
        }
        
        // Show success message
        const displayName = fileIndex !== null && data.totalFiles > 1
            ? `${jobName} - File ${fileIndex + 1}`
            : jobName;
        showAlert('success', `Loaded transcription results for "${displayName}" (${data.segments.length} segments)`);
        console.log('? All steps completed successfully');
        
    } catch (error) {
        console.error('? Error loading batch transcription results:', error);
        console.error('Error type:', error.name);
        console.error('Error message:', error.message);
        console.error('Stack trace:', error.stack);
        showAlert('danger', `An error occurred while loading the transcription results: ${error.message}. Please check the console for details.`);
    } finally {
        if (loadingIndicator) {
            loadingIndicator.classList.add('d-none');
        }
    }
}

/**
 * Show file selection modal for batch jobs with multiple files
 */
function showFileSelectionModal(jobId, jobName, data) {
    console.log('Creating file selection modal', {
        jobId,
        jobName,
        totalFiles: data.totalFiles,
        fileResultsCount: data.fileResults?.length
    });

    // Validate data
    if (!data.fileResults || data.fileResults.length === 0) {
        console.error('No file results available for modal');
        showAlert('danger', 'Unable to show file selection - no file information available');
        return;
    }

    // Store original file order for reordering
    let currentFileOrder = data.fileResults.map((file, index) => index);

    // Create modal HTML dynamically
    let modalHtml = `
        <div class="modal fade" id="fileSelectionModal" tabindex="-1" aria-labelledby="fileSelectionModalLabel" aria-hidden="true">
            <div class="modal-dialog modal-dialog-centered modal-lg">
                <div class="modal-content">
                    <div class="modal-header bg-primary text-white">
                        <h5 class="modal-title" id="fileSelectionModalLabel">
                            <i class="bi bi-collection"></i> Select Files to View
                        </h5>
                        <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>
                    </div>
                    <div class="modal-body">
                        <div class="alert alert-info d-flex align-items-center" role="alert">
                            <i class="bi bi-info-circle-fill me-2" style="font-size: 1.5rem;"></i>
                            <div>
                                This batch job contains <strong>${data.totalFiles} files</strong> with a total of <strong>${data.segments.length} segments</strong>. 
                                Choose how you want to view the results:
                            </div>
                        </div>
                        
                        <div class="mb-4">
                            <button class="btn btn-primary btn-lg w-100 d-flex align-items-center justify-content-center" id="viewAllFilesBtn">
                                <i class="bi bi-collection-fill me-2" style="font-size: 1.2rem;"></i>
                                <span>View All Files Combined (${data.segments.length} segments)</span>
                            </button>
                            <small class="text-muted d-block text-center mt-2">
                                <i class="bi bi-info-circle"></i> See all transcriptions from all files in a single view
                            </small>
                        </div>
                        
                        <hr>
                        
                        <div class="d-flex justify-content-between align-items-center mb-3">
                            <p class="fw-bold mb-0">
                                <i class="bi bi-files"></i> Or select an individual file to view:
                            </p>
                            <small class="text-muted">
                                <i class="bi bi-arrows-move"></i> Drag to reorder for combined view
                            </small>
                        </div>
                        <div class="list-group" id="fileListContainer">
                            <!-- File buttons will be inserted here -->
                        </div>
                    </div>
                    <div class="modal-footer">
                        <small class="text-muted me-auto">
                            <i class="bi bi-lightbulb"></i> Tip: You can reopen this dialog by clicking "View Results" again
                        </small>
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                    </div>
                </div>
            </div>
        </div>
    `;
    
    // Remove existing modal if present
    const existingModal = document.getElementById('fileSelectionModal');
    if (existingModal) {
        const existingModalInstance = bootstrap.Modal.getInstance(existingModal);
        if (existingModalInstance) {
            existingModalInstance.dispose();
        }
        existingModal.remove();
    }
    
    // Add modal to body
    document.body.insertAdjacentHTML('beforeend', modalHtml);
    
    const modalElement = document.getElementById('fileSelectionModal');
    const modal = new bootstrap.Modal(modalElement);
    const fileListContainer = document.getElementById('fileListContainer');
    const viewAllBtn = document.getElementById('viewAllFilesBtn');
    
    // Variables for drag and drop
    let draggedElement = null;
    let draggedIndex = null;
    
    // Function to create file button
    function createFileButton(file, index, originalIndex) {
        const fileBtn = document.createElement('button');
        fileBtn.type = 'button';
        fileBtn.className = 'list-group-item list-group-item-action d-flex justify-content-between align-items-center py-3';
        fileBtn.style.cursor = 'grab';
        fileBtn.setAttribute('draggable', 'true');
        fileBtn.setAttribute('data-original-index', originalIndex);
        fileBtn.setAttribute('data-current-index', index);
        
        const segmentCount = file.segments ? file.segments.length : 0;
        const duration = file.formattedDuration || '00:00:00';
        const fileName = file.fileName || `File ${originalIndex + 1}`;
        
        fileBtn.innerHTML = `
            <div class="d-flex align-items-center">
                <i class="bi bi-grip-vertical text-muted me-2" style="font-size: 1.2rem; cursor: grab;"></i>
                <i class="bi bi-file-earmark-music text-primary me-3" style="font-size: 1.5rem;"></i>
                <div>
                    <strong class="d-block">${escapeHtml(fileName)}</strong>
                    <small class="text-muted">Click to view this file's transcription</small>
                </div>
            </div>
            <div class="text-end">
                <span class="badge bg-secondary me-2">${segmentCount} segments</span>
                <span class="badge bg-info"><i class="bi bi-clock"></i> ${duration}</span>
            </div>
        `;
        
        // Drag and drop event handlers
        fileBtn.addEventListener('dragstart', function(e) {
            draggedElement = this;
            draggedIndex = parseInt(this.getAttribute('data-current-index'));
            this.style.opacity = '0.5';
            this.style.cursor = 'grabbing';
            e.dataTransfer.effectAllowed = 'move';
            e.dataTransfer.setData('text/html', this.innerHTML);
        });
        
        fileBtn.addEventListener('dragend', function() {
            this.style.opacity = '';
            this.style.cursor = 'grab';
            // Remove all drag-over styles
            document.querySelectorAll('.list-group-item').forEach(item => {
                item.style.borderTop = '';
                item.style.borderBottom = '';
            });
        });
        
        fileBtn.addEventListener('dragover', function(e) {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'move';
            
            if (draggedElement !== this) {
                const currentIndex = parseInt(this.getAttribute('data-current-index'));
                // Show visual feedback for drop zone
                if (currentIndex < draggedIndex) {
                    this.style.borderTop = '3px solid #0d6efd';
                    this.style.borderBottom = '';
                } else {
                    this.style.borderBottom = '3px solid #0d6efd';
                    this.style.borderTop = '';
                }
            }
            return false;
        });
        
        fileBtn.addEventListener('dragleave', function() {
            this.style.borderTop = '';
            this.style.borderBottom = '';
        });
        
        fileBtn.addEventListener('drop', function(e) {
            e.stopPropagation();
            e.preventDefault();
            
            if (draggedElement !== this) {
                const dropIndex = parseInt(this.getAttribute('data-current-index'));
                
                // Reorder the array
                currentFileOrder.splice(draggedIndex, 1);
                if (draggedIndex < dropIndex) {
                    currentFileOrder.splice(dropIndex - 1, 0, parseInt(draggedElement.getAttribute('data-original-index')));
                } else {
                    currentFileOrder.splice(dropIndex, 0, parseInt(draggedElement.getAttribute('data-original-index')));
                }
                
                console.log('Files reordered:', currentFileOrder);
                
                // Rebuild the list
                rebuildFileList();
            }
            
            this.style.borderTop = '';
            this.style.borderBottom = '';
            return false;
        });
        
        // Click handler for viewing individual file
        fileBtn.addEventListener('click', function(e) {
            // Don't trigger click when dragging
            if (e.target.classList.contains('bi-grip-vertical') || 
                this.style.opacity === '0.5') {
                return;
            }
            console.log(`User selected file ${originalIndex}: ${fileName}`);
            modal.hide();
            // Load the specific file using its original index
            viewBatchTranscriptionResults(jobId, jobName, originalIndex);
        });
        
        // Hover effects
        fileBtn.addEventListener('mouseenter', function() {
            if (this !== draggedElement) {
                this.style.backgroundColor = '#f8f9fa';
            }
        });
        fileBtn.addEventListener('mouseleave', function() {
            this.style.backgroundColor = '';
        });
        
        return fileBtn;
    }
    
    // Function to rebuild file list based on current order
    function rebuildFileList() {
        fileListContainer.innerHTML = '';
        currentFileOrder.forEach((originalIndex, newIndex) => {
            const file = data.fileResults[originalIndex];
            const fileBtn = createFileButton(file, newIndex, originalIndex);
            fileListContainer.appendChild(fileBtn);
        });
    }
    
    // Initial file list build
    rebuildFileList();
    
    // Set up "View All" button with reordering support
    viewAllBtn.onclick = () => {
        console.log('User selected to view all files combined');
        console.log('File order:', currentFileOrder);
        
        // Check if files were reordered
        const wasReordered = currentFileOrder.some((val, idx) => val !== idx);
        
        if (wasReordered) {
            console.log('Files were reordered, applying new order to combined view');
            
            // Create reordered data
            const reorderedData = {
                ...data,
                fileResults: currentFileOrder.map(idx => data.fileResults[idx]),
                segments: [],
                fullTranscript: '',
                availableSpeakers: new Set(),
                speakerStatistics: []
            };
            
            // Rebuild segments in new order with updated line numbers
            let lineNumber = 1;
            const speakerMap = new Map();
            const fullTranscriptParts = [];
            
            currentFileOrder.forEach(originalIdx => {
                const file = data.fileResults[originalIdx];
                if (file.segments) {
                    file.segments.forEach(segment => {
                        // Create new segment with updated line number
                        const newSegment = {
                            ...segment,
                            lineNumber: lineNumber++
                        };
                        reorderedData.segments.push(newSegment);
                        
                        // Track speakers
                        reorderedData.availableSpeakers.add(segment.speaker);
                        
                        // Build transcript
                        const time = segment.uiFormattedStartTime || segment.formattedStartTime || '00:00:00';
                        fullTranscriptParts.push(`[${time}] ${segment.speaker}: ${segment.text}`);
                        
                        // Track speaker statistics
                        if (!speakerMap.has(segment.speaker)) {
                            speakerMap.set(segment.speaker, {
                                name: segment.speaker,
                                segmentCount: 0,
                                totalSpeakTimeSeconds: 0,
                                firstAppearanceSeconds: segment.startTimeInSeconds || 0
                            });
                        }
                        const speakerInfo = speakerMap.get(segment.speaker);
                        speakerInfo.segmentCount++;
                        speakerInfo.totalSpeakTimeSeconds += (segment.endTimeInSeconds || 0) - (segment.startTimeInSeconds || 0);
                    });
                }
            });
            reorderedData.availableSpeakers = Array.from(reorderedData.availableSpeakers).sort();
            reorderedData.fullTranscript = fullTranscriptParts.join('\n');
            reorderedData.speakerStatistics = Array.from(speakerMap.values())
                .sort((a, b) => a.firstAppearanceSeconds - b.firstAppearanceSeconds);
            
            console.log('Reordered data prepared:', {
                segmentCount: reorderedData.segments.length,
                fileCount: reorderedData.fileResults.length,
                speakerCount: reorderedData.availableSpeakers.length
            });
            
            data = reorderedData;
        }
        
        modal.hide();
        
        // Display the combined results
        if (typeof window.displayTranscriptionResults === 'function') {
            try {
                window.displayTranscriptionResults(data);
                console.log('? displayTranscriptionResults executed successfully for combined view');
                
                // Switch to the Speaker Segments tab
                const segmentsTab = document.getElementById('segments-tab');
                if (segmentsTab) {
                    const tab = new bootstrap.Tab(segmentsTab);
                    tab.show();
                    console.log('? Switched to segments-tab (Speaker Segments)');
                } else {
                    console.warn('?? segments-tab element not found');
                }
                
                const orderMsg = wasReordered ? ' (custom order applied)' : '';
                showAlert('success', `Loaded combined results for "${jobName}" (${data.totalFiles} files, ${data.segments.length} segments)${orderMsg}`);
            } catch (displayError) {
                console.error('? Error in displayTranscriptionResults:', displayError);
                showAlert('danger', `Unable to display results: ${displayError.message}. Please refresh the page and try again.`);
            }
        } else {
            showAlert('danger', 'Unable to display results. Please refresh the page and try again.');
        }
    };
    
    // Clean up modal when hidden
    modalElement.addEventListener('hidden.bs.modal', function () {
        const modalInstance = bootstrap.Modal.getInstance(modalElement);
        if (modalInstance) {
            modalInstance.dispose();
        }
        modalElement.remove();
    });
    
    // Show the modal
    console.log('Showing file selection modal');
    modal.show();
}
