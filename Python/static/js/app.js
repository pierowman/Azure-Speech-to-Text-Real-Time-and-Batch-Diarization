// Main Application Module
// Global application state and initialization

// Import all modules to ensure they load and expose their window functions
import './ui-helpers.js';
import { setupKeyboardShortcuts } from './audio-player.js';
import './edit-manager.js';
import './speaker-manager.js';
import './batch-manager.js';
import './export-manager.js';

export const AppState = {
    transcriptionData: null,
    currentTab: 'realtime',
    audioPlayer: null,
    isSeeking: false,
    currentActiveSegment: null,
    isEditMode: false,
    editingSegmentIndex: null,
    segmentEstimateInterval: null,
    batchJobs: [],
    autoRefreshInterval: null,
    isAutoRefreshEnabled: false,
    autoRefreshSeconds: window.BATCH_JOB_AUTO_REFRESH_SECONDS || 30,
    expandedJobId: null,
    supportedLocales: [],
    localesLoaded: false
};

// Reset application state for new transcription
export function resetAppState() {
    console.log('?? Resetting application state...');
    
    // Clear transcription data
    AppState.transcriptionData = null;
    AppState.currentActiveSegment = null;
    AppState.editingSegmentIndex = null;
    
    // Turn off edit mode
    AppState.isEditMode = false;
    const editModeToggle = document.getElementById('editModeToggle');
    if (editModeToggle) {
        editModeToggle.checked = false;
    }
    
    // Clear segments display
    const segmentsContainer = document.getElementById('segments');
    if (segmentsContainer) {
        segmentsContainer.innerHTML = '';
    }
    
    // Clear audit log
    const auditLogEntries = document.getElementById('auditLogEntries');
    if (auditLogEntries) {
        auditLogEntries.innerHTML = '';
    }
    
    // Hide audit log section
    const auditLogSection = document.getElementById('auditLogSection');
    if (auditLogSection) {
        auditLogSection.style.display = 'none';
    }
    
    // Clear stats grid
    const statsGrid = document.getElementById('statsGrid');
    if (statsGrid) {
        statsGrid.innerHTML = '';
    }
    
    // Stop audio player
    if (AppState.audioPlayer) {
        AppState.audioPlayer.pause();
        AppState.audioPlayer.currentTime = 0;
        AppState.audioPlayer.src = '';
    }
    
    // Hide audio player
    const audioPlayerContainer = document.getElementById('audioPlayerContainer');
    if (audioPlayerContainer) {
        audioPlayerContainer.style.display = 'none';
    }
    
    // Hide interactive segments helper
    const interactiveSegmentsHelper = document.getElementById('interactiveSegmentsHelper');
    if (interactiveSegmentsHelper) {
        interactiveSegmentsHelper.style.display = 'none';
    }
    
    // Hide manage speakers and download audit buttons
    const manageSpeakersBtn = document.getElementById('manageSpeakersBtn');
    if (manageSpeakersBtn) {
        manageSpeakersBtn.style.display = 'none';
    }
    
    const downloadAuditBtn = document.getElementById('downloadAuditBtn');
    if (downloadAuditBtn) {
        downloadAuditBtn.style.display = 'none';
    }
    
    // Hide results section
    const resultsSection = document.getElementById('resultsSection');
    if (resultsSection) {
        resultsSection.style.display = 'none';
    }
    
    console.log('? Application state reset complete');
}

// Initialize application
export function initializeApp() {
    console.log('?? Initializing Azure Speech-to-Text Application...');
    
    // Initialize audio player reference
    AppState.audioPlayer = document.getElementById('audioPlayer');
    
    // Setup keyboard shortcuts for audio playback
    setupKeyboardShortcuts();
    console.log('?? Keyboard shortcuts initialized');
    
    console.log('? Application initialized');
}

// Diagnostic function for debugging
window.debugAudioHighlight = function() {
    console.log('=== AUDIO HIGHLIGHT DIAGNOSTICS ===');
    console.log('Audio player:', AppState.audioPlayer);
    console.log('Audio src:', AppState.audioPlayer ? AppState.audioPlayer.src : 'N/A');
    console.log('Is playing:', AppState.audioPlayer && !AppState.audioPlayer.paused);
    console.log('Current time:', AppState.audioPlayer ? AppState.audioPlayer.currentTime.toFixed(2) + 's' : 'N/A');
    console.log('Duration:', AppState.audioPlayer ? AppState.audioPlayer.duration.toFixed(2) + 's' : 'N/A');
    console.log('---');
    console.log('Transcription data:', !!AppState.transcriptionData);
    console.log('Segments count:', AppState.transcriptionData ? AppState.transcriptionData.segments.length : 0);
    if (AppState.transcriptionData && AppState.transcriptionData.segments.length > 0) {
        console.log('First segment:', AppState.transcriptionData.segments[0]);
        console.log('Has startTimeInSeconds?', 'startTimeInSeconds' in AppState.transcriptionData.segments[0]);
        console.log('Has endTimeInSeconds?', 'endTimeInSeconds' in AppState.transcriptionData.segments[0]);
    }
    console.log('---');
    console.log('Segment elements:', document.querySelectorAll('.segment').length);
    console.log('Active segments:', document.querySelectorAll('.segment.active').length);
    console.log('Current active segment:', AppState.currentActiveSegment);
    console.log('=================================');
};
