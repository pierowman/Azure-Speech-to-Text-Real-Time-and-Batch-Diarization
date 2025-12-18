// UI Helper Functions Module
import { AppState } from './app.js';

// Loading indicator
export function showLoading(show) {
    const loading = document.getElementById('loading');
    if (loading) {
        if (show) {
            loading.classList.add('show');
            resetProgressStages();
            resetSegmentCounter();
        } else {
            loading.classList.remove('show');
        }
    }
}

// Error messages
export function showError(message) {
    const errorDiv = document.getElementById('errorMessage');
    if (errorDiv) {
        errorDiv.textContent = message;
        errorDiv.style.display = 'block';
    }
}

export function hideError() {
    const errorDiv = document.getElementById('errorMessage');
    if (errorDiv) {
        errorDiv.style.display = 'none';
    }
}

// Success messages
export function showSuccess(message) {
    const successDiv = document.getElementById('successMessage');
    if (successDiv) {
        successDiv.textContent = message;
        successDiv.style.display = 'block';
    }
}

export function hideSuccess() {
    const successDiv = document.getElementById('successMessage');
    if (successDiv) {
        successDiv.style.display = 'none';
    }
}

// Results section
export function showResults() {
    const resultsSection = document.getElementById('resultsSection');
    if (resultsSection) {
        resultsSection.style.display = 'block';
    }
}

export function hideResults() {
    const resultsSection = document.getElementById('resultsSection');
    if (resultsSection) {
        resultsSection.style.display = 'none';
    }
}

// Segment counter functions
export function showSegmentCounter() {
    const counter = document.getElementById('segmentCounter');
    if (counter) {
        counter.classList.add('show');
    }
}

export function hideSegmentCounter() {
    const counter = document.getElementById('segmentCounter');
    if (counter) {
        counter.classList.remove('show');
    }
    stopSegmentEstimation();
}

export function updateSegmentCount(count) {
    const countElement = document.getElementById('segmentCount');
    if (countElement) {
        countElement.textContent = count;
        // Trigger animation by removing and re-adding class
        countElement.parentElement.style.animation = 'none';
        setTimeout(() => {
            countElement.parentElement.style.animation = '';
        }, 10);
        console.log(`?? Segments transcribed: ${count}`);
    }
}

export function resetSegmentCounter() {
    updateSegmentCount(0);
    hideSegmentCounter();
}

// Progress stage management
export function resetProgressStages() {
    const stages = document.querySelectorAll('.progress-stage');
    stages.forEach(stage => {
        stage.classList.remove('active', 'completed');
    });
    console.log('? Progress stages reset');
}

export function setProgressStage(stageName) {
    const stages = ['upload', 'processing', 'diarization', 'finalizing'];
    const currentIndex = stages.indexOf(stageName);
    
    if (currentIndex === -1) {
        console.warn('Unknown stage:', stageName);
        return;
    }
    
    // Mark all previous stages as completed
    for (let i = 0; i < currentIndex; i++) {
        const stage = document.getElementById(`stage-${stages[i]}`);
        if (stage) {
            stage.classList.remove('active');
            stage.classList.add('completed');
        }
    }
    
    // Mark current stage as active
    const currentStage = document.getElementById(`stage-${stageName}`);
    if (currentStage) {
        currentStage.classList.remove('completed');
        currentStage.classList.add('active');
    }
    
    // Mark all future stages as neither active nor completed
    for (let i = currentIndex + 1; i < stages.length; i++) {
        const stage = document.getElementById(`stage-${stages[i]}`);
        if (stage) {
            stage.classList.remove('active', 'completed');
        }
    }
    
    console.log(`?? Progress stage: ${stageName}`);
}

export function updateLoadingText(text) {
    const loadingMainText = document.getElementById('loadingMainText');
    if (loadingMainText) {
        loadingMainText.textContent = text;
    }
}

export function startSegmentEstimation(audioFileSizeMB) {
    stopSegmentEstimation(); // Clear any existing interval
    const label = document.querySelector('.segment-label');
    if (label) {
        label.textContent = 'segments transcribing (estimated)';
    }
    // Rough estimate: ~10 seconds per MB for WAV files
    const estimatedDurationSeconds = audioFileSizeMB * 10;
    const estimatedTotalSegments = Math.max(Math.floor(estimatedDurationSeconds / 7), 1);
    
    console.log(`?? Estimating ${estimatedTotalSegments} segments for ${audioFileSizeMB.toFixed(2)}MB audio`);
    
    let currentEstimate = 0;
    const incrementInterval = (20 * 1000) / estimatedTotalSegments; // Spread over ~20 seconds
    AppState.segmentEstimateInterval = setInterval(() => {
        if (currentEstimate < estimatedTotalSegments) {
            currentEstimate++;
            updateSegmentCount(currentEstimate);
        }
    }, Math.max(incrementInterval, 1000)); // At least 1 second between updates
}

export function stopSegmentEstimation() {
    if (AppState.segmentEstimateInterval) {
        clearInterval(AppState.segmentEstimateInterval);
        AppState.segmentEstimateInterval = null;
    }
    const label = document.querySelector('.segment-label');
    if (label) {
        label.textContent = 'segments transcribed';
    }
}

// Tab switching
export async function switchTab(tabName) {
    AppState.currentTab = tabName;
    
    // Update tab buttons
    document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.classList.remove('active');
    });
    event.target.classList.add('active');
    
    // Update tab content
    document.querySelectorAll('.tab-content').forEach(content => {
        content.classList.remove('active');
    });
    document.getElementById(`${tabName}-tab`).classList.add('active');

    // Pause audio if switching tabs
    if (AppState.audioPlayer && !AppState.audioPlayer.paused) {
        AppState.audioPlayer.pause();
        const { updatePlayPauseButton } = await import('./audio-player.js');
        updatePlayPauseButton();
    }

    // Load batch jobs and locales when switching to batch tab
    if (tabName === 'batch') {
        if (AppState.batchJobs.length === 0) {
            console.log('?? Switched to batch tab - loading jobs');
            const { refreshJobList } = await import('./batch-manager.js');
            refreshJobList();
        }
        if (!AppState.localesLoaded) {
            console.log('?? Loading supported locales...');
            const { loadSupportedLocales } = await import('./locale-manager.js');
            loadSupportedLocales();
        }
    }
}

// Batch UI helpers
export function showBatchError(message) {
    const errorElement = document.getElementById('batchErrorMessage');
    if (errorElement) {
        errorElement.textContent = message;
        errorElement.classList.add('show');
    }
}

export function hideBatchError() {
    const errorElement = document.getElementById('batchErrorMessage');
    if (errorElement) {
        errorElement.classList.remove('show');
    }
}

export function showBatchSuccess(message) {
    const successElement = document.getElementById('batchSuccessMessage');
    if (successElement) {
        successElement.textContent = message;
        successElement.classList.add('show');
        setTimeout(() => hideBatchSuccess(), 5000);
    }
}

export function hideBatchSuccess() {
    const successElement = document.getElementById('batchSuccessMessage');
    if (successElement) {
        successElement.classList.remove('show');
    }
}

export function showBatchInfo(message) {
    const infoElement = document.getElementById('batchInfoMessage');
    if (infoElement) {
        infoElement.textContent = message;
        infoElement.classList.add('show');
    }
}

export function hideBatchInfo() {
    const infoElement = document.getElementById('batchInfoMessage');
    if (infoElement) {
        infoElement.classList.remove('show');
    }
}

export function showBatchLoading(show) {
    const loadingElement = document.getElementById('batchLoading');
    if (loadingElement) {
        loadingElement.classList.toggle('show', show);
    }
}

export function showBatchResults() {
    const resultsSection = document.getElementById('batchResultsSection');
    if (resultsSection) {
        resultsSection.classList.add('show');
    }
}

export function hideBatchResults() {
    const resultsSection = document.getElementById('batchResultsSection');
    if (resultsSection) {
        resultsSection.classList.remove('show');
    }
}

// Make functions globally accessible
window.switchTab = switchTab;
