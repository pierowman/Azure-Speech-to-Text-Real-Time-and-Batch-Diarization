// Transcription Progress Tracking
// Provides enhanced visual feedback during real-time transcription

(function() {
    'use strict';

    // Progress tracking state
    let progressState = {
        currentStep: 0,
        startTime: null,
        fileSize: 0,
        fileName: '',
        estimatedDuration: 0
    };

    // Progress steps
    const STEPS = {
        UPLOAD: 0,
        CONNECT: 1,
        ANALYZE: 2,
        TRANSCRIBE: 3,
        FINALIZE: 4
    };

    const STEP_ELEMENTS = {
        [STEPS.UPLOAD]: 'stepUpload',
        [STEPS.CONNECT]: 'stepConnect',
        [STEPS.ANALYZE]: 'stepAnalyze',
        [STEPS.TRANSCRIBE]: 'stepTranscribe',
        [STEPS.FINALIZE]: 'stepFinalize'
    };

    /**
     * Initialize progress tracking for a new transcription
     * @param {File} audioFile - The audio file being transcribed
     */
    function initializeProgress(audioFile) {
        if (!audioFile) {
            console.warn('No audio file provided to initializeProgress');
            return;
        }

        console.log('Initializing progress tracking for:', audioFile.name);

        // Reset state
        progressState = {
            currentStep: 0,
            startTime: new Date(),
            fileSize: audioFile.size,
            fileName: audioFile.name,
            estimatedDuration: estimateTranscriptionDuration(audioFile)
        };

        // Show progress section
        const progressSection = document.getElementById('progressSection');
        if (progressSection) {
            progressSection.classList.remove('d-none');
        }

        // Display file information
        displayFileInfo(audioFile);

        // Show detailed steps
        const progressSteps = document.getElementById('progressSteps');
        if (progressSteps) {
            progressSteps.classList.remove('d-none');
        }

        // Reset all steps to pending state
        resetAllSteps();

        // Start with upload step
        setStepStatus(STEPS.UPLOAD, 'active');
        
        // Automatically progress through initial steps with realistic timing
        setTimeout(() => {
            setStepStatus(STEPS.UPLOAD, 'completed');
            setStepStatus(STEPS.CONNECT, 'active');
        }, 500);

        setTimeout(() => {
            setStepStatus(STEPS.CONNECT, 'completed');
            setStepStatus(STEPS.ANALYZE, 'active');
        }, 1500);

        setTimeout(() => {
            setStepStatus(STEPS.ANALYZE, 'completed');
            setStepStatus(STEPS.TRANSCRIBE, 'active');
            
            // Show segment counter for transcription step
            const segmentCounter = document.getElementById('segmentCounter');
            if (segmentCounter) {
                segmentCounter.classList.remove('d-none');
            }
            
            // Start simulated progress
            startSimulatedProgress();
        }, 3000);
    }

    /**
     * Display file information in the progress section
     * @param {File} audioFile - The audio file being processed
     */
    function displayFileInfo(audioFile) {
        const fileInfo = document.getElementById('progressFileInfo');
        const fileName = document.getElementById('progressFileName');
        const fileSize = document.getElementById('progressFileSize');
        const fileDuration = document.getElementById('progressFileDuration');

        if (!fileInfo) return;

        fileInfo.classList.remove('d-none');

        if (fileName) {
            fileName.textContent = audioFile.name;
        }

        if (fileSize) {
            const sizeMB = (audioFile.size / (1024 * 1024)).toFixed(2);
            fileSize.innerHTML = `<i class="bi bi-hdd"></i> ${sizeMB} MB`;
        }

        // Try to get audio duration if possible
        if (fileDuration && audioFile.type.startsWith('audio/')) {
            const audio = new Audio();
            audio.preload = 'metadata';
            
            audio.addEventListener('loadedmetadata', function() {
                const duration = audio.duration;
                const minutes = Math.floor(duration / 60);
                const seconds = Math.floor(duration % 60);
                fileDuration.innerHTML = `<i class="bi bi-clock"></i> ${minutes}:${seconds.toString().padStart(2, '0')}`;
                
                // Update estimated duration based on actual audio length
                progressState.estimatedDuration = Math.max(duration * 1000, 5000); // At least 5 seconds
            });
            
            audio.src = URL.createObjectURL(audioFile);
        }
    }

    /**
     * Estimate transcription duration based on file size
     * @param {File} audioFile - The audio file
     * @returns {number} Estimated duration in milliseconds
     */
    function estimateTranscriptionDuration(audioFile) {
        // Rough estimate: ~1-2 seconds per MB for typical audio
        const sizeMB = audioFile.size / (1024 * 1024);
        const baseTime = sizeMB * 1500; // 1.5 seconds per MB
        
        // Minimum 5 seconds, maximum 60 seconds for UI purposes
        return Math.min(Math.max(baseTime, 5000), 60000);
    }

    /**
     * Start simulated progress for better user feedback
     * This provides visual feedback even though we don't have real progress from backend
     */
    function startSimulatedProgress() {
        const progressBar = document.getElementById('progressBar');
        const progressBarInner = document.getElementById('progressBarInner');
        
        if (!progressBar || !progressBarInner) return;
        
        progressBar.classList.remove('d-none');
        
        let progress = 10; // Start at 10%
        const maxProgress = 90; // Never go to 100% until actually complete
        const updateInterval = Math.max(progressState.estimatedDuration / 50, 200); // Update every ~2% or 200ms
        
        const timer = setInterval(() => {
            if (progress >= maxProgress) {
                clearInterval(timer);
                return;
            }
            
            // Slow down as we get closer to max
            const increment = (maxProgress - progress) / 20;
            progress += increment;
            
            progressBarInner.style.width = `${Math.round(progress)}%`;
        }, updateInterval);
        
        // Store timer for cleanup
        progressState.progressTimer = timer;
    }

    /**
     * Update segment count during transcription
     * @param {number} count - Number of segments processed
     */
    function updateSegmentCount(count) {
        const segmentCount = document.getElementById('segmentCount');
        if (segmentCount) {
            segmentCount.textContent = count;
        }
    }

    /**
     * Mark transcription as complete
     */
    function completeProgress() {
        console.log('Completing progress tracking');
        
        // Clear any timers
        if (progressState.progressTimer) {
            clearInterval(progressState.progressTimer);
        }
        
        // Mark final steps as completed
        setStepStatus(STEPS.TRANSCRIBE, 'completed');
        setStepStatus(STEPS.FINALIZE, 'active');
        
        // Complete progress bar
        const progressBarInner = document.getElementById('progressBarInner');
        if (progressBarInner) {
            progressBarInner.style.width = '100%';
            progressBarInner.classList.remove('progress-bar-animated');
        }
        
        // After brief delay, mark as completed
        setTimeout(() => {
            setStepStatus(STEPS.FINALIZE, 'completed');
            
            // Update main message
            const mainMessage = document.getElementById('progressMainMessage');
            if (mainMessage) {
                mainMessage.innerHTML = '<i class="bi bi-check-circle text-success"></i> Transcription completed successfully!';
            }
            
            // Hide progress after a moment to let user see completion
            setTimeout(() => {
                const progressSection = document.getElementById('progressSection');
                if (progressSection) {
                    progressSection.classList.add('d-none');
                }
            }, 2000);
        }, 500);
    }

    /**
     * Handle transcription error
     * @param {string} errorMessage - Error message to display
     */
    function handleProgressError(errorMessage) {
        console.log('Handling progress error:', errorMessage);
        
        // Clear any timers
        if (progressState.progressTimer) {
            clearInterval(progressState.progressTimer);
        }
        
        // Mark current step as failed
        if (progressState.currentStep !== null) {
            setStepStatus(progressState.currentStep, 'error');
        }
        
        // Update main message
        const mainMessage = document.getElementById('progressMainMessage');
        if (mainMessage) {
            mainMessage.innerHTML = '<i class="bi bi-x-circle text-danger"></i> Transcription failed';
        }
        
        // Hide progress bar
        const progressBar = document.getElementById('progressBar');
        if (progressBar) {
            progressBar.classList.add('d-none');
        }
        
        // Progress section will be hidden by error handler in mode selector
    }

    /**
     * Reset progress tracking
     */
    function resetProgress() {
        console.log('Resetting progress tracking');
        
        // Clear any timers
        if (progressState.progressTimer) {
            clearInterval(progressState.progressTimer);
        }
        
        // Reset state
        progressState = {
            currentStep: 0,
            startTime: null,
            fileSize: 0,
            fileName: '',
            estimatedDuration: 0
        };
        
        // Hide progress section
        const progressSection = document.getElementById('progressSection');
        if (progressSection) {
            progressSection.classList.add('d-none');
        }
        
        // Reset all visual elements
        resetAllSteps();
        
        const segmentCounter = document.getElementById('segmentCounter');
        if (segmentCounter) {
            segmentCounter.classList.add('d-none');
        }
        
        const segmentCount = document.getElementById('segmentCount');
        if (segmentCount) {
            segmentCount.textContent = '0';
        }
        
        const progressBar = document.getElementById('progressBar');
        if (progressBar) {
            progressBar.classList.add('d-none');
        }
        
        const progressBarInner = document.getElementById('progressBarInner');
        if (progressBarInner) {
            progressBarInner.style.width = '0%';
            progressBarInner.classList.add('progress-bar-animated');
        }
        
        const fileInfo = document.getElementById('progressFileInfo');
        if (fileInfo) {
            fileInfo.classList.add('d-none');
        }
        
        const progressSteps = document.getElementById('progressSteps');
        if (progressSteps) {
            progressSteps.classList.add('d-none');
        }
        
        const mainMessage = document.getElementById('progressMainMessage');
        if (mainMessage) {
            mainMessage.textContent = 'Processing your audio file...';
        }
    }

    /**
     * Set status for a specific step
     * @param {number} stepIndex - Index of the step
     * @param {string} status - Status: 'pending', 'active', 'completed', 'error'
     */
    function setStepStatus(stepIndex, status) {
        const stepId = STEP_ELEMENTS[stepIndex];
        if (!stepId) return;
        
        const stepElement = document.getElementById(stepId);
        if (!stepElement) return;
        
        const icon = stepElement.querySelector('.step-icon');
        const text = stepElement.querySelector('.step-text');
        
        if (!icon) return;
        
        // Remove all status classes
        icon.classList.remove('bi-circle', 'bi-arrow-repeat', 'bi-check-circle-fill', 'bi-x-circle-fill');
        icon.classList.remove('text-muted', 'text-primary', 'text-success', 'text-danger');
        
        // Set new status
        switch (status) {
            case 'pending':
                icon.classList.add('bi-circle', 'text-muted');
                if (text) text.classList.remove('fw-bold', 'text-success', 'text-danger');
                break;
                
            case 'active':
                icon.classList.add('bi-arrow-repeat', 'text-primary');
                if (text) {
                    text.classList.add('fw-bold');
                    text.classList.remove('text-success', 'text-danger');
                }
                progressState.currentStep = stepIndex;
                break;
                
            case 'completed':
                icon.classList.add('bi-check-circle-fill', 'text-success');
                if (text) {
                    text.classList.remove('fw-bold', 'text-danger');
                    text.classList.add('text-success');
                }
                break;
                
            case 'error':
                icon.classList.add('bi-x-circle-fill', 'text-danger');
                if (text) {
                    text.classList.remove('fw-bold', 'text-success');
                    text.classList.add('text-danger');
                }
                break;
        }
    }

    /**
     * Reset all steps to pending state
     */
    function resetAllSteps() {
        Object.values(STEPS).forEach(step => {
            setStepStatus(step, 'pending');
        });
    }

    // Expose functions globally
    window.transcriptionProgress = {
        initialize: initializeProgress,
        updateSegmentCount: updateSegmentCount,
        complete: completeProgress,
        error: handleProgressError,
        reset: resetProgress,
        STEPS: STEPS
    };

    console.log('Transcription progress tracking initialized');
})();
