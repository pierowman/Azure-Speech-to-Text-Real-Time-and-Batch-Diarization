// Transcription Mode Selector
(function() {
    let currentMode = 'realtime';
    let currentAbortController = null;
    
    document.addEventListener('DOMContentLoaded', function() {
        initializeModeSelector();
        loadValidationRules();
        loadSupportedLocales(); // Add this line
        
        // Initialize jobs tab visibility based on current mode
        const jobsTabItem = document.getElementById('jobs-tab-item');
        if (jobsTabItem) {
            // Check if batch transcription is enabled
            const batchRadio = document.getElementById('modeBatch');
            const isBatchEnabled = batchRadio !== null;
            
            // Hide jobs tab initially for real-time mode
            // It will be shown when user switches to batch mode
            if (isBatchEnabled) {
                jobsTabItem.style.display = 'none';
                console.log('Jobs tab initialized as hidden (real-time mode by default)');
            } else {
                // If batch is disabled, hide the tab completely
                jobsTabItem.style.display = 'none';
                console.log('Jobs tab hidden (batch transcription disabled)');
            }
        }
    });

    function initializeModeSelector() {
        const realTimeRadio = document.getElementById('modeRealTime');
        const batchRadio = document.getElementById('modeBatch');
        
        if (realTimeRadio) realTimeRadio.addEventListener('change', () => switchMode('realtime'));
        if (batchRadio) batchRadio.addEventListener('change', () => switchMode('batch'));
        
        // Initialize file input handlers
        const batchFileInput = document.getElementById('audioFilesBatch');
        if (batchFileInput) {
            batchFileInput.addEventListener('change', updateFilesList);
        }
        
        // Initialize form handlers
        const realTimeForm = document.getElementById('realTimeForm');
        const batchForm = document.getElementById('batchForm');
        
        if (realTimeForm) realTimeForm.addEventListener('submit', handleRealTimeSubmit);
        if (batchForm) batchForm.addEventListener('submit', handleBatchSubmit);
        
        // Initialize cancel button
        const cancelBtn = document.getElementById('cancelTranscriptionBtn');
        if (cancelBtn) {
            cancelBtn.addEventListener('click', handleCancelTranscription);
        }
    }

    function switchMode(mode) {
        currentMode = mode;
        const realTimeForm = document.getElementById('realTimeForm');
        const batchForm = document.getElementById('batchForm');
        const jobsTabItem = document.getElementById('jobs-tab-item');
        const jobsTab = document.getElementById('jobs-tab');
        const resultsSection = document.getElementById('resultsSection');
        
        // Reset the entire view when switching modes
        resetView();
        
        if (mode === 'realtime') {
            if (realTimeForm) realTimeForm.classList.remove('d-none');
            if (batchForm) batchForm.classList.add('d-none');
            // Hide Jobs tab for real-time mode
            if (jobsTabItem) jobsTabItem.style.display = 'none';
            // Hide results section for real-time mode (will be shown when transcription completes)
            if (resultsSection) resultsSection.classList.add('d-none');
        } else if (mode === 'batch') {
            if (realTimeForm) realTimeForm.classList.add('d-none');
            if (batchForm) batchForm.classList.remove('d-none');
            
            // IMPORTANT: Show results section FIRST so the jobs tab becomes visible
            if (resultsSection) {
                resultsSection.classList.remove('d-none');
                console.log('? Results section shown for batch mode');
            }
            
            // Show Jobs tab for batch mode
            if (jobsTabItem) {
                jobsTabItem.style.display = '';
                console.log('? Jobs tab item made visible');
                // Re-initialize jobs tab when switching to batch mode
                if (typeof window.reinitializeJobsTab === 'function') {
                    console.log('Re-initializing jobs tab for batch mode...');
                    window.reinitializeJobsTab();
                }
            }
            
            // Automatically show the Batch Transcription Jobs tab
            if (jobsTab && jobsTabItem) {
                console.log('Batch mode selected - switching to Jobs tab');
                const tab = new bootstrap.Tab(jobsTab);
                tab.show();
                
                // Load jobs if the tab is visible
                if (typeof window.loadTranscriptionJobs === 'function') {
                    console.log('Loading transcription jobs...');
                    window.loadTranscriptionJobs();
                } else {
                    console.error('? loadTranscriptionJobs function not found!');
                }
            } else {
                console.error('? Jobs tab or jobs tab item not found!');
                console.log('jobsTab:', jobsTab);
                console.log('jobsTabItem:', jobsTabItem);
            }
        }
        
        console.log(`Switched to ${mode} mode - view reset`);
    }

    /**
     * Reset the entire view to initial state
     */
    function resetView() {
        // Reset progress tracking
        if (window.transcriptionProgress) {
            window.transcriptionProgress.reset();
        }
        
        // Hide all sections EXCEPT resultsSection (we'll handle it per mode)
        hideElement('progressSection');
        hideElement('errorSection');
        hideElement('successSection');
        hideElement('audioPlayerSection');
        // NOTE: Don't hide resultsSection here - it will be shown/hidden based on mode
        
        // Reset error section styling
        const errorSection = document.getElementById('errorSection');
        if (errorSection) {
            errorSection.classList.remove('alert-warning');
            errorSection.classList.add('alert-danger');
        }
        
        // Clear results (but don't hide the section)
        const segmentsList = document.getElementById('segmentsList');
        if (segmentsList) segmentsList.innerHTML = '';
        
        const fullTranscript = document.getElementById('fullTranscript');
        if (fullTranscript) fullTranscript.textContent = '';
        
        // Clear jobs list
        const jobsList = document.getElementById('jobsList');
        if (jobsList) jobsList.innerHTML = '';
        
        // Reset audio player
        const audioPlayer = document.getElementById('audioPlayer');
        if (audioPlayer) {
            audioPlayer.pause();
            audioPlayer.currentTime = 0;
            audioPlayer.src = '';
        }
        
        // Clear file inputs
        const realTimeFileInput = document.getElementById('audioFileRealTime');
        if (realTimeFileInput) realTimeFileInput.value = '';
        
        const batchFileInput = document.getElementById('audioFilesBatch');
        if (batchFileInput) batchFileInput.value = '';
        
        const filesList = document.getElementById('filesList');
        if (filesList) filesList.innerHTML = '';
        
        const batchJobNameInput = document.getElementById('batchJobName');
        if (batchJobNameInput) batchJobNameInput.value = '';
        
        // Reset buttons to default state
        const realTimeSubmitBtn = document.getElementById('realTimeSubmitBtn');
        const realTimeSubmitText = document.getElementById('realTimeSubmitText');
        const realTimeSubmitSpinner = document.getElementById('realTimeSubmitSpinner');
        if (realTimeSubmitBtn) realTimeSubmitBtn.disabled = false;
        if (realTimeSubmitText) realTimeSubmitText.textContent = 'Transcribe Now';
        if (realTimeSubmitSpinner) realTimeSubmitSpinner.classList.add('d-none');
        
        const batchSubmitBtn = document.getElementById('batchSubmitBtn');
        const batchSubmitText = document.getElementById('batchSubmitText');
        const batchSubmitSpinner = document.getElementById('batchSubmitSpinner');
        if (batchSubmitBtn) batchSubmitBtn.disabled = false;
        if (batchSubmitText) batchSubmitText.textContent = 'Submit Batch Job';
        if (batchSubmitSpinner) batchSubmitSpinner.classList.add('d-none');
        
        // Reset transcription data in transcription.js if available
        if (typeof window.transcriptionData !== 'undefined') {
            window.transcriptionData = null;
        }
        if (typeof window.goldenRecordData !== 'undefined') {
            window.goldenRecordData = null;
        }
        if (typeof window.hasUnsavedChanges !== 'undefined') {
            window.hasUnsavedChanges = false;
        }
        if (typeof window.auditLog !== 'undefined') {
            window.auditLog = [];
        }
        
        // Stop auto-refresh if enabled
        if (typeof stopAutoRefresh === 'function') {
            stopAutoRefresh();
        }
        
        // Uncheck auto-refresh checkbox
        const autoRefreshCheckbox = document.getElementById('autoRefreshJobsCheckbox');
        if (autoRefreshCheckbox) {
            autoRefreshCheckbox.checked = false;
        }
        
        // Switch back to the first tab (Speaker Segments)
        const segmentsTab = document.getElementById('segments-tab');
        if (segmentsTab) {
            const tab = new bootstrap.Tab(segmentsTab);
            tab.show();
        }
        
        console.log('View reset complete');
    }

    async function loadValidationRules() {
        try {
            // Load real-time rules
            const rtResponse = await fetch('/Home/GetValidationRules?mode=RealTime');
            const rtData = await rtResponse.json();
            const realTimeRules = document.getElementById('realTimeRules');
            if (rtData.success && realTimeRules) {
                realTimeRules.textContent = rtData.rules;
            }
            
            // Load batch rules
            const batchResponse = await fetch('/Home/GetValidationRules?mode=Batch');
            const batchData = await batchResponse.json();
            const batchRules = document.getElementById('batchRules');
            if (batchData.success && batchRules) {
                batchRules.textContent = batchData.rules;
            }
        } catch (error) {
            console.error('Error loading validation rules:', error);
        }
    }

    /**
     * Load supported locales from Azure Speech Service
     */
    async function loadSupportedLocales() {
        const localeSelect = document.getElementById('locale');
        if (!localeSelect) {
            console.warn('Locale select element not found');
            return;
        }
        
        try {
            console.log('Loading supported locales from Azure...');
            const response = await fetch('/Home/GetSupportedLocalesWithNames');
            const data = await response.json();
            
            if (data.success && data.locales && data.locales.length > 0) {
                console.log(`? Loaded ${data.count} supported locales`);
                
                // Clear the loading option
                localeSelect.innerHTML = '';
                
                // Get default locale from ViewData or use en-US
                const defaultLocale = localeSelect.getAttribute('data-default-locale') || 'en-US';
                
                // Add locales to dropdown
                data.locales.forEach(locale => {
                    const option = document.createElement('option');
                    option.value = locale.code;
                    option.text = locale.formattedName || locale.name;
                    
                    // Select the default locale
                    if (locale.code === defaultLocale) {
                        option.selected = true;
                    }
                    
                    localeSelect.appendChild(option);
                });
                
                console.log(`Default locale set to: ${defaultLocale}`);
            } else {
                console.error('Failed to load locales:', data.message || 'No locales returned');
                populateFallbackLocales(localeSelect);
            }
        } catch (error) {
            console.error('Error loading supported locales:', error);
            populateFallbackLocales(localeSelect);
        }
    }

    /**
     * Populate dropdown with fallback locales if API call fails
     */
    function populateFallbackLocales(selectElement) {
        console.warn('?? Using fallback locale list');
        
        selectElement.innerHTML = '';
        
        const fallbackLocales = [
            { code: 'en-US', name: 'English (United States)' },
            { code: 'en-GB', name: 'English (United Kingdom)' },
            { code: 'es-ES', name: 'Spanish (Spain)' },
            { code: 'es-MX', name: 'Spanish (Mexico)' },
            { code: 'fr-FR', name: 'French (France)' },
            { code: 'fr-CA', name: 'French (Canada)' },
            { code: 'de-DE', name: 'German (Germany)' },
            { code: 'it-IT', name: 'Italian (Italy)' },
            { code: 'pt-BR', name: 'Portuguese (Brazil)' },
            { code: 'pt-PT', name: 'Portuguese (Portugal)' },
            { code: 'ja-JP', name: 'Japanese (Japan)' },
            { code: 'ko-KR', name: 'Korean (Korea)' },
            { code: 'zh-CN', name: 'Chinese (Simplified, China)' },
            { code: 'zh-TW', name: 'Chinese (Traditional, Taiwan)' },
            { code: 'ar-SA', name: 'Arabic (Saudi Arabia)' },
            { code: 'hi-IN', name: 'Hindi (India)' },
            { code: 'ru-RU', name: 'Russian (Russia)' },
            { code: 'nl-NL', name: 'Dutch (Netherlands)' },
            { code: 'pl-PL', name: 'Polish (Poland)' },
            { code: 'sv-SE', name: 'Swedish (Sweden)' }
        ];
        
        const defaultLocale = selectElement.getAttribute('data-default-locale') || 'en-US';
        
        fallbackLocales.forEach(locale => {
            const option = document.createElement('option');
            option.value = locale.code;
            option.text = locale.name;
            
            if (locale.code === defaultLocale) {
                option.selected = true;
            }
            
            selectElement.appendChild(option);
        });
    }

    function updateFilesList() {
        const input = document.getElementById('audioFilesBatch');
        const filesList = document.getElementById('filesList');
        
        if (!input || !filesList) return;
        
        if (input.files.length === 0) {
            filesList.innerHTML = '';
            return;
        }
        
        let html = '<div class="alert alert-info py-2"><strong>Files selected:</strong><ul class="mb-0 mt-1">';
        for (let i = 0; i < input.files.length; i++) {
            const file = input.files[i];
            const sizeMB = (file.size / (1024 * 1024)).toFixed(2);
            html += `<li>${escapeHtml(file.name)} <span class="text-muted">(${sizeMB} MB)</span></li>`;
        }
        html += '</ul></div>';
        filesList.innerHTML = html;
    }

    async function handleRealTimeSubmit(e) {
        e.preventDefault();
        
        const submitBtn = document.getElementById('realTimeSubmitBtn');
        const submitText = document.getElementById('realTimeSubmitText');
        const submitSpinner = document.getElementById('realTimeSubmitSpinner');
        const fileInput = document.getElementById('audioFileRealTime');
        
        if (!fileInput || !fileInput.files.length) {
            showError('Please select a file');
            return;
        }
        
        const audioFile = fileInput.files[0];
        
        // Hide all alerts and initialize progress tracking
        hideAllAlerts();
        
        // Initialize enhanced progress tracking
        if (window.transcriptionProgress) {
            window.transcriptionProgress.initialize(audioFile);
        } else {
            // Fallback to simple progress if module not loaded
            showElement('progressSection');
        }
        
        // Disable button and show spinner
        if (submitBtn) submitBtn.disabled = true;
        if (submitText) submitText.textContent = 'Processing...';
        if (submitSpinner) submitSpinner.classList.remove('d-none');
        
        // Create abort controller for cancellation
        currentAbortController = new AbortController();
        
        try {
            const formData = new FormData();
            formData.append('audioFile', audioFile);
            
            // Note: Real-time transcription currently only supports English (en-US)
            // Locale parameter not needed as service will use default
            
            console.log('Starting real-time transcription for file:', audioFile.name);
            
            const response = await fetch('/Home/UploadAndTranscribe', {
                method: 'POST',
                body: formData,
                signal: currentAbortController.signal
            });
            
            console.log('Response status:', response.status);
            
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            
            const data = await response.json();
            console.log('Response data:', data);
            
            if (data.success) {
                // Complete progress tracking
                if (window.transcriptionProgress) {
                    window.transcriptionProgress.complete();
                } else {
                    hideElement('progressSection');
                }
                
                showElement('successSection');
                
                // Ensure Jobs tab is hidden for real-time results
                const jobsTabItem = document.getElementById('jobs-tab-item');
                if (jobsTabItem) jobsTabItem.style.display = 'none';
                
                // Call the global function from transcription.js
                if (typeof window.displayTranscriptionResults === 'function') {
                    window.displayTranscriptionResults(data);
                } else {
                    console.error('displayTranscriptionResults function not found');
                    showError('Results display function not available. Please refresh the page.');
                }
            } else {
                // Handle error in progress tracking
                if (window.transcriptionProgress) {
                    window.transcriptionProgress.error(data.message);
                } else {
                    hideElement('progressSection');
                }
                
                // Show detailed error message from backend
                const errorMessage = data.message || 'Transcription failed';
                console.error('Transcription failed:', errorMessage);
                
                // Provide helpful context based on error message
                let detailedError = errorMessage;
                
                if (errorMessage.includes('Subscription') || errorMessage.includes('key') || errorMessage.includes('credentials')) {
                    detailedError += '\n\nPlease check your Azure Speech Service credentials in appsettings.json.';
                } else if (errorMessage.includes('audio') || errorMessage.includes('format') || errorMessage.includes('file')) {
                    detailedError += '\n\nPlease ensure your audio file is in WAV, MP3, OGG, or FLAC format and is not corrupted.';
                } else if (errorMessage.includes('network') || errorMessage.includes('connection') || errorMessage.includes('timeout')) {
                    detailedError += '\n\nPlease check your internet connection and firewall settings.';
                } else if (errorMessage.includes('quota') || errorMessage.includes('throttle') || errorMessage.includes('limit')) {
                    detailedError += '\n\nYou may have exceeded your Azure Speech Service quota. Check your Azure Portal.';
                }
                
                showError(detailedError);
            }
        } catch (error) {
            // Handle error in progress tracking
            if (window.transcriptionProgress) {
                window.transcriptionProgress.error(error.message);
            } else {
                hideElement('progressSection');
            }
            
            if (error.name === 'AbortError') {
                showWarning('Transcription was canceled');
            } else {
                console.error('Transcription error:', error);
                
                // Provide more context for common errors
                let errorMessage = 'An error occurred during transcription';
                
                if (error.message.includes('Failed to fetch') || error.message.includes('NetworkError')) {
                    errorMessage = 'Network error: Unable to connect to the server. Please check your internet connection.';
                } else if (error.message.includes('HTTP error')) {
                    errorMessage = `Server error: ${error.message}. Please try again or contact support.`;
                } else if (error.message) {
                    errorMessage += ': ' + error.message;
                }
                
                errorMessage += '\n\nFor detailed troubleshooting, check the browser console (F12) and application logs.';
                
                showError(errorMessage);
            }
        } finally {
            // Re-enable button
            if (submitBtn) submitBtn.disabled = false;
            if (submitText) submitText.textContent = 'Transcribe Now';
            if (submitSpinner) submitSpinner.classList.add('d-none');
            currentAbortController = null;
        }
    }

    async function handleBatchSubmit(e) {
        e.preventDefault();
        
        const submitBtn = document.getElementById('batchSubmitBtn');
        const submitText = document.getElementById('batchSubmitText');
        const submitSpinner = document.getElementById('batchSubmitSpinner');
        const filesInput = document.getElementById('audioFilesBatch');
        const jobNameInput = document.getElementById('batchJobName');
        const minSpeakersInput = document.getElementById('minSpeakers');
        const maxSpeakersInput = document.getElementById('maxSpeakers');
        const localeInput = document.getElementById('locale');
        
        console.log('Batch submit - files selected:', filesInput?.files?.length || 0);
        
        if (!filesInput || !filesInput.files.length) {
            showError('Please select at least one file');
            return;
        }
        
        // Validate speaker counts
        const minSpeakers = parseInt(minSpeakersInput?.value || '1');
        const maxSpeakers = parseInt(maxSpeakersInput?.value || '10');
        
        if (minSpeakers > maxSpeakers) {
            showError('Minimum speakers cannot be greater than maximum speakers');
            return;
        }
        
        // Hide all alerts
        hideAllAlerts();
        
        // Disable button and show spinner
        if (submitBtn) submitBtn.disabled = true;
        if (submitText) submitText.textContent = 'Submitting...';
        if (submitSpinner) submitSpinner.classList.remove('d-none');
        
        try {
            const formData = new FormData();
            
            // Add all files - IMPORTANT: use 'audioFiles' to match controller parameter
            for (let i = 0; i < filesInput.files.length; i++) {
                formData.append('audioFiles', filesInput.files[i]);
                console.log(`Added file ${i + 1}:`, filesInput.files[i].name);
            }
            
            // Add job name if provided
            if (jobNameInput && jobNameInput.value.trim()) {
                formData.append('jobName', jobNameInput.value.trim());
                console.log('Job name:', jobNameInput.value.trim());
            }
            
            // Add speaker counts
            formData.append('minSpeakers', minSpeakers.toString());
            formData.append('maxSpeakers', maxSpeakers.toString());
            console.log('Speaker range:', minSpeakers, '-', maxSpeakers);
            
            // Add locale
            const locale = localeInput?.value || 'en-US';
            formData.append('locale', locale);
            console.log('Locale:', locale);
            
            console.log('Submitting form data to /Home/SubmitBatchTranscription');
            
            const response = await fetch('/Home/SubmitBatchTranscription', {
                method: 'POST',
                body: formData
            });
            
            console.log('Response status:', response.status);
            const data = await response.json();
            console.log('Response data:', data);
            
            if (data.success) {
                showSuccess(data.message);
                // Clear form
                filesInput.value = '';
                if (jobNameInput) jobNameInput.value = '';
                if (minSpeakersInput) minSpeakersInput.value = '1';
                if (maxSpeakersInput) maxSpeakersInput.value = '3';
                // Don't reset locale - keep user's selection
                const filesList = document.getElementById('filesList');
                if (filesList) filesList.innerHTML = '';
                
                // Ensure Jobs tab is visible for batch mode
                const jobsTabItem = document.getElementById('jobs-tab-item');
                if (jobsTabItem) {
                    jobsTabItem.style.display = '';
                    console.log('? Jobs tab made visible');
                }
                
                // Switch to results section first (make it visible)
                const resultsSection = document.getElementById('resultsSection');
                if (resultsSection) {
                    resultsSection.classList.remove('d-none');
                    console.log('? Results section shown');
                }
                
                // Ensure jobs tab is initialized
                if (typeof window.reinitializeJobsTab === 'function') {
                    console.log('Ensuring jobs tab is initialized...');
                    window.reinitializeJobsTab();
                }
                
                // Switch to Transcription Jobs tab
                const jobsTab = document.getElementById('jobs-tab');
                if (jobsTab) {
                    console.log('? Jobs tab found, attempting to show...');
                    
                    // Use Bootstrap's tab API to switch tabs
                    try {
                        const tab = new bootstrap.Tab(jobsTab);
                        tab.show();
                        console.log('? Jobs tab shown via Bootstrap');
                        
                        // IMMEDIATELY call loadTranscriptionJobs instead of waiting for event
                        // The shown.bs.tab event might not fire reliably
                        if (typeof window.loadTranscriptionJobs === 'function') {
                            console.log('? loadTranscriptionJobs found, calling IMMEDIATELY...');
                            // Call immediately first
                            window.loadTranscriptionJobs();
                            
                            // Also schedule a backup call in case the first one doesn't work
                            setTimeout(() => {
                                console.log('Backup loadTranscriptionJobs call (in case first failed)...');
                                // Only call if jobs list is still empty
                                const jobsList = document.getElementById('jobsList');
                                if (jobsList && jobsList.children.length === 0) {
                                    console.log('Jobs list still empty, calling loadTranscriptionJobs again');
                                    window.loadTranscriptionJobs();
                                } else {
                                    console.log('Jobs list already has content, skipping backup call');
                                }
                            }, 1000); // Backup call after 1 second
                        } else {
                            console.error('? loadTranscriptionJobs function not found!');
                            console.error('Available window functions:', Object.keys(window).filter(k => k.includes('load') || k.includes('Job')));
                        }
                    } catch (tabError) {
                        console.error('Error showing jobs tab:', tabError);
                    }
                } else {
                    console.error('? Jobs tab element not found!');
                }
            } else {
                showError(data.message || 'Batch submission failed');
            }
        } catch (error) {
            console.error('Batch submission error:', error);
            showError('An error occurred during batch submission: ' + error.message);
        } finally {
            // Re-enable button
            if (submitBtn) submitBtn.disabled = false;
            if (submitText) submitText.textContent = 'Submit Batch Job';
            if (submitSpinner) submitSpinner.classList.add('d-none');
        }
    }

    function handleCancelTranscription() {
        if (currentAbortController) {
            if (confirm('Are you sure you want to cancel the transcription?')) {
                currentAbortController.abort();
                hideElement('progressSection');
                showWarning('Transcription canceled');
            }
        }
    }

    // Helper functions
    function showElement(id) {
        const el = document.getElementById(id);
        if (el) el.classList.remove('d-none');
    }

    function hideElement(id) {
        const el = document.getElementById(id);
        if (el) el.classList.add('d-none');
    }

    function hideAllAlerts() {
        hideElement('errorSection');
        hideElement('successSection');
        const errorSection = document.getElementById('errorSection');
        if (errorSection) {
            errorSection.classList.remove('alert-warning');
            errorSection.classList.add('alert-danger');
        }
    }

    function showError(message) {
        const errorSection = document.getElementById('errorSection');
        const errorMessage = document.getElementById('errorMessage');
        const errorLabel = document.getElementById('errorLabel');
        
        if (errorLabel) errorLabel.textContent = 'Error:';
        if (errorMessage) errorMessage.textContent = message;
        if (errorSection) {
            errorSection.classList.remove('alert-warning');
            errorSection.classList.add('alert-danger');
            errorSection.classList.remove('d-none');
        }
    }

    function showWarning(message) {
        const errorSection = document.getElementById('errorSection');
        const errorMessage = document.getElementById('errorMessage');
        const errorLabel = document.getElementById('errorLabel');
        
        if (errorLabel) errorLabel.textContent = 'Warning:';
        if (errorMessage) errorMessage.textContent = message;
        if (errorSection) {
            errorSection.classList.remove('alert-danger');
            errorSection.classList.add('alert-warning');
            errorSection.classList.remove('d-none');
        }
    }

    function showSuccess(message) {
        const successSection = document.getElementById('successSection');
        if (successSection) {
            successSection.textContent = message;
            successSection.classList.remove('d-none');
        }
    }

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

    // Export for testing
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = {
            switchMode,
            resetView,
            loadValidationRules,
            updateFilesList,
            handleRealTimeSubmit,
            handleBatchSubmit,
            escapeHtml
        };
    }
})();
