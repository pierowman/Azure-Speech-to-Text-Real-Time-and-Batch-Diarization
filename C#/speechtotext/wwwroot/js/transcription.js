// Global variables
let transcriptionData = null;
let goldenRecordData = null;
let audioPlayer = null;
let hasUnsavedChanges = false;
let availableSpeakers = new Set();
let speakerModal = null;
let reassignModal = null;
let currentReassignSpeaker = null;
let currentAbortController = null;
let auditLog = []; // Track all edits

// ============================================
// UTILITY FUNCTIONS (Must be defined first)
// ============================================

function showElement(elementId) {
    const el = document.getElementById(elementId);
    if (el) el.classList.remove('d-none');
}

function hideElement(elementId) {
    const el = document.getElementById(elementId);
    if (el) el.classList.add('d-none');
}

function hideAllAlerts() {
    const errorSection = document.getElementById('errorSection');
    hideElement('errorSection');
    hideElement('successSection');
    hideElement('resultsSection');
    hideElement('audioPlayerSection');
    
    // Reset error section to default danger style
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

function downloadBlob(blob, filename) {
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    window.URL.revokeObjectURL(url);
}

function getTimestamp() {
    return new Date().toISOString().replace(/[:.]/g, '-');
}

function handleBeforeUnload(e) {
    if (hasUnsavedChanges) {
        e.preventDefault();
        e.returnValue = '';
    }
}

// ============================================
// INITIALIZATION
// ============================================

// Initialize on DOM ready
document.addEventListener('DOMContentLoaded', function () {
    speakerModal = new bootstrap.Modal(document.getElementById('speakerModal'));
    reassignModal = new bootstrap.Modal(document.getElementById('reassignModal'));
    setupResultsEventListeners();
});

// Setup event listeners for results display
function setupResultsEventListeners() {
    const manageSpeakersBtn = document.getElementById('manageSpeakersBtn');
    if (manageSpeakersBtn) {
        manageSpeakersBtn.addEventListener('click', handleManageSpeakers);
    }
    
    const confirmReassignBtn = document.getElementById('confirmReassignBtn');
    if (confirmReassignBtn) {
        confirmReassignBtn.addEventListener('click', handleConfirmReassign);
    }
    
    const addSpeakerBtn = document.getElementById('addSpeakerBtn');
    if (addSpeakerBtn) {
        addSpeakerBtn.addEventListener('click', handleAddSpeaker);
    }
    
    const newSpeakerName = document.getElementById('newSpeakerName');
    if (newSpeakerName) {
        newSpeakerName.addEventListener('keypress', handleSpeakerNameKeypress);
    }
    
    const downloadGoldenRecordBtn = document.getElementById('downloadGoldenRecordBtn');
    if (downloadGoldenRecordBtn) {
        downloadGoldenRecordBtn.addEventListener('click', handleDownloadGoldenRecord);
    }
    
    const downloadReadableBtn = document.getElementById('downloadReadableBtn');
    if (downloadReadableBtn) {
        downloadReadableBtn.addEventListener('click', handleDownloadReadable);
    }
    
    const downloadWithTrackingBtn = document.getElementById('downloadWithTrackingBtn');
    if (downloadWithTrackingBtn) {
        downloadWithTrackingBtn.addEventListener('click', handleDownloadWithTracking);
    }
    
    const downloadAuditLogBtn = document.getElementById('downloadAuditLogBtn');
    if (downloadAuditLogBtn) {
        downloadAuditLogBtn.addEventListener('click', handleDownloadAuditLog);
    }
    
    const cancelTranscriptionBtn = document.getElementById('cancelTranscriptionBtn');
    if (cancelTranscriptionBtn) {
        cancelTranscriptionBtn.addEventListener('click', handleCancelTranscription);
    }
    
    window.addEventListener('beforeunload', handleBeforeUnload);
}

// ============================================
// MAIN TRANSCRIPTION FUNCTIONS
// ============================================

// Export function for mode selector to call
function displayTranscriptionResults(result) {
    console.log('displayTranscriptionResults called with:', {
        hasResult: !!result,
        hasSegments: !!result?.segments,
        segmentsCount: result?.segments?.length || 0,
        hasSuccess: 'success' in result,
        success: result?.success
    });
    
    // Validate result object
    if (!result) {
        console.error('? displayTranscriptionResults: result is null or undefined');
        showError('No transcription data provided');
        return;
    }
    
    // Check for batch transcription result structure (may have success property)
    if (result.success === false) {
        console.error('? displayTranscriptionResults: result.success is false');
        showError(result.message || 'Transcription failed');
        return;
    }
    
    // Validate segments exist
    if (!result.segments || !Array.isArray(result.segments)) {
        console.error('? displayTranscriptionResults: segments missing or not an array');
        showError('No transcription segments available');
        return;
    }
    
    if (result.segments.length === 0) {
        console.warn('?? displayTranscriptionResults: segments array is empty');
        showWarning('Transcription completed but no speech segments were detected in the audio');
        return;
    }
    
    console.log('? displayTranscriptionResults: validation passed, calling handleSuccessfulTranscription');
    handleSuccessfulTranscription(result);
}

// Make function available globally
window.displayTranscriptionResults = displayTranscriptionResults;

// Handle successful transcription
function handleSuccessfulTranscription(result) {
    showElement('successSection');
    transcriptionData = result;
    goldenRecordData = result.goldenRecordJsonData;
    hasUnsavedChanges = false;
    auditLog = []; // Reset audit log for new transcription

    buildAvailableSpeakersList();

    if (result.audioFileUrl) {
        setupAudioPlayer(result.audioFileUrl);
    }

    displayResults(result);
}

// Build available speakers list
function buildAvailableSpeakersList() {
    availableSpeakers.clear();

    // Use server-provided list if available, otherwise build from segments
    if (transcriptionData.availableSpeakers && transcriptionData.availableSpeakers.length > 0) {
        transcriptionData.availableSpeakers.forEach(speaker => {
            availableSpeakers.add(speaker);
        });
    } else {
        // Fallback: build from segments (backward compatibility)
        transcriptionData.segments.forEach(segment => {
            if (segment.speaker && segment.speaker.trim()) {
                availableSpeakers.add(segment.speaker);
            }
        });
    }
    
    // After updating the available speakers list, refresh all dropdowns
    refreshAllSpeakerDropdowns();
}

// Get speaker options HTML
function getSpeakerOptions(currentSpeaker) {
    // Use server-provided list if available, otherwise use availableSpeakers Set
    const speakers = transcriptionData?.availableSpeakers || Array.from(availableSpeakers).sort();
    return speakers.map(speaker => 
        `<option value="${speaker}" ${speaker === currentSpeaker ? 'selected' : ''}>${speaker}</option>`
    ).join('');
}

// Setup audio player
function setupAudioPlayer(audioUrl) {
    audioPlayer = document.getElementById('audioPlayer');
    audioPlayer.src = audioUrl;
    showElement('audioPlayerSection');
    audioPlayer.addEventListener('timeupdate', syncTranscriptWithAudio);
}

// Sync transcript with audio playback
function syncTranscriptWithAudio() {
    if (!transcriptionData || !transcriptionData.segments) return;

    const currentTime = audioPlayer.currentTime;

    transcriptionData.segments.forEach((segment, index) => {
        const segmentElement = document.getElementById(`segment-${index}`);
        if (!segmentElement) return;

        if (currentTime >= segment.startTimeInSeconds && currentTime < segment.endTimeInSeconds) {
            segmentElement.classList.add('active-segment');
            segmentElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
        } else {
            segmentElement.classList.remove('active-segment');
        }
    });
}

// Build speaker mappings
function buildSpeakerMappings(segments) {
    const mappings = new Map();

    segments.forEach(segment => {
        const currentSpeaker = segment.speaker;
        const originalSpeaker = segment.originalSpeaker || segment.speaker;

        if (!mappings.has(currentSpeaker)) {
            mappings.set(currentSpeaker, new Set());
        }
        mappings.get(currentSpeaker).add(originalSpeaker);
    });

    return mappings;
}

// Display results
function displayResults(result) {
    showElement('resultsSection');

    const segmentsList = document.getElementById('segmentsList');
    segmentsList.innerHTML = '';

    if (result.segments && result.segments.length > 0) {
        const speakerMappings = buildSpeakerMappings(result.segments);

        result.segments.forEach((segment, index) => {
            const item = createSegmentElement(segment, index, speakerMappings);
            segmentsList.appendChild(item);
        });

        setupSegmentEventListeners();
    } else {
        segmentsList.innerHTML = '<div class="alert alert-warning">No speech segments detected.</div>';
    }

    document.getElementById('fullTranscript').textContent = result.fullTranscript || 'No transcript available.';
}

// Create segment element
function createSegmentElement(segment, index, speakerMappings) {
    const originalSpeaker = segment.originalSpeaker || segment.speaker;
    const speakerName = segment.speaker || '';
    const speakerText = segment.text || '';
    const originalText = segment.originalText || segment.text;
    const startTime = segment.uiFormattedStartTime || new Date(segment.startTimeInSeconds * 1000).toISOString().substr(11, 8) || '';
    const lineNumber = segment.lineNumber || (index + 1); // Use server-assigned line number, fallback to index+1

    // Use server-calculated change detection with fallback
    const speakerChanged = segment.speakerWasChanged !== undefined ? segment.speakerWasChanged : (speakerName !== originalSpeaker);
    const textChanged = segment.textWasChanged !== undefined ? segment.textWasChanged : (speakerText !== originalText);

    // Use badge generator functions
    const originalSpeakerBadge = speakerChanged ? createOriginalSpeakerBadge(originalSpeaker) : '';
    const originalTextBadge = textChanged ? createOriginalTextBadge(originalText) : '';

    const item = document.createElement('div');
    item.className = 'list-group-item segment-item';
    item.id = `segment-${index}`;
    item.setAttribute('data-segment-index', index);

    item.innerHTML = `
        <div class="d-flex w-100 justify-content-between align-items-center mb-2">
            <div class="d-flex align-items-center gap-2">
                <span class="badge bg-secondary me-2" style="font-family: monospace; min-width: 40px;" title="Line ${lineNumber}">#${lineNumber}</span>
                <select class="form-select form-select-sm speaker-name-select" 
                        data-segment-index="${index}"
                        data-original-speaker="${originalSpeaker}"
                        title="Select speaker">
                    ${getSpeakerOptions(speakerName)}
                </select>
                ${originalSpeakerBadge}
            </div>
            <small class="text-muted">
                <i class="bi bi-clock"></i> ${startTime}
            </small>
        </div>
        <div class="transcript-text-container">
            <textarea class="form-control form-control-sm transcript-text-input d-none" 
                      data-segment-index="${index}"
                      data-original-text="${originalText}"
                      rows="3"
                      placeholder="Edit transcript text">${speakerText}</textarea>
            <p class="mb-1 transcript-text-display" 
               data-segment-index="${index}"
               style="cursor: pointer;"
               title="Click to edit transcript">${speakerText}</p>
            <div class="d-flex justify-content-between align-items-center mt-1">
                ${originalTextBadge}
                <div class="transcript-edit-actions d-none">
                    <button class="btn btn-sm btn-success me-1 save-transcript-btn" data-segment-index="${index}">
                        <i class="bi bi-check"></i> Save
                    </button>
                    <button class="btn btn-sm btn-secondary cancel-transcript-btn" data-segment-index="${index}">
                        <i class="bi bi-x"></i> Cancel
                    </button>
                </div>
            </div>
        </div>
    `;

    return item;
}

// Setup segment event listeners
function setupSegmentEventListeners() {
    document.querySelectorAll('.speaker-name-select').forEach(select => {
        select.addEventListener('change', handleSpeakerNameChange);
        select.addEventListener('click', e => e.stopPropagation());
    });

    document.querySelectorAll('.transcript-text-display').forEach(display => {
        display.addEventListener('click', function (e) {
            e.stopPropagation();
            const segmentIndex = this.getAttribute('data-segment-index');
            enterTranscriptEditMode(segmentIndex);
        });
    });

    document.querySelectorAll('.save-transcript-btn').forEach(btn => {
        btn.addEventListener('click', function (e) {
            e.stopPropagation();
            const segmentIndex = this.getAttribute('data-segment-index');
            saveTranscriptEdit(segmentIndex);
        });
    });

    document.querySelectorAll('.cancel-transcript-btn').forEach(btn => {
        btn.addEventListener('click', function (e) {
            e.stopPropagation();
            const segmentIndex = this.getAttribute('data-segment-index');
            cancelTranscriptEdit(segmentIndex);
        });
    });

    document.querySelectorAll('.segment-item').forEach(item => {
        item.addEventListener('click', function (e) {
            const segmentIndex = this.getAttribute('data-segment-index');
            const segment = transcriptionData.segments[segmentIndex];

            if (e.target.closest('.speaker-name-select, .transcript-text-input, .transcript-edit-actions')) {
                return;
            }

            if (audioPlayer && segment) {
                audioPlayer.currentTime = segment.startTimeInSeconds;
                if (audioPlayer.paused) {
                    audioPlayer.play();
                }
            }
        });
    });
}

// Manage speakers modal
function handleManageSpeakers() {
    updateSpeakersModal();
    speakerModal.show();
}

function updateSpeakersModal() {
    const tbody = document.getElementById('speakersTableBody');
    tbody.innerHTML = '';

    // Use server-provided statistics if available, otherwise calculate from segments
    let speakersInUse;
    
    if (transcriptionData.speakerStatistics && transcriptionData.speakerStatistics.length > 0) {
        // Use server-calculated statistics
        speakersInUse = new Map();
        transcriptionData.speakerStatistics.forEach(stat => {
            speakersInUse.set(stat.name, stat.segmentCount);
        });
    } else {
        // Fallback: calculate from segments (backward compatibility)
        speakersInUse = new Map();
        transcriptionData.segments.forEach(segment => {
            if (segment.speaker && segment.speaker.trim()) {
                const count = speakersInUse.get(segment.speaker) || 0;
                speakersInUse.set(segment.speaker, count + 1);
            }
        });
    }

    // Add any speakers from availableSpeakers that might have been manually added
    availableSpeakers.forEach(speaker => {
        if (!speakersInUse.has(speaker)) {
            speakersInUse.set(speaker, 0);
        }
    });

    const speakers = Array.from(speakersInUse.keys()).sort();

    speakers.forEach(speaker => {
        const segmentCount = speakersInUse.get(speaker);
        const row = document.createElement('tr');
        row.innerHTML = `
            <td><i class="bi bi-person"></i> ${speaker}</td>
            <td><span class="badge bg-primary rounded-pill">${segmentCount}</span></td>
            <td>
                <div class="btn-group btn-group-sm" role="group">
                    <button type="button" class="btn btn-outline-primary reassign-speaker-btn" data-speaker="${speaker}" ${segmentCount === 0 ? 'disabled' : ''} title="${segmentCount === 0 ? 'No segments to reassign' : 'Reassign to another speaker'}">
                        <i class="bi bi-arrow-left-right"></i> Reassign
                    </button>
                    <button type="button" class="btn btn-outline-danger delete-speaker-btn" data-speaker="${speaker}" ${segmentCount > 0 ? 'disabled' : ''} title="${segmentCount > 0 ? 'Cannot delete speaker with segments' : 'Delete speaker'}">
                        <i class="bi bi-trash"></i> Delete
                    </button>
                </div>
            </td>
        `;
        tbody.appendChild(row);
    });

    document.querySelectorAll('.reassign-speaker-btn').forEach(btn => {
        btn.addEventListener('click', function () {
            const speaker = this.getAttribute('data-speaker');
            showReassignModal(speaker);
        });
    });

    document.querySelectorAll('.delete-speaker-btn').forEach(btn => {
        btn.addEventListener('click', function () {
            const speaker = this.getAttribute('data-speaker');
            deleteSpeaker(speaker);
        });
    });
}

function showReassignModal(fromSpeaker) {
    currentReassignSpeaker = fromSpeaker;
    
    // Use server-provided statistics if available, otherwise count segments
    let segmentCount;
    if (transcriptionData.speakerStatistics && transcriptionData.speakerStatistics.length > 0) {
        const speakerInfo = transcriptionData.speakerStatistics.find(s => s.name === fromSpeaker);
        segmentCount = speakerInfo ? speakerInfo.segmentCount : 0;
    } else {
        // Fallback: count from segments
        segmentCount = transcriptionData.segments.filter(s => s.speaker === fromSpeaker).length;
    }

    document.getElementById('reassignFromSpeaker').textContent = fromSpeaker;
    document.getElementById('reassignSegmentCount').textContent = segmentCount;

    const select = document.getElementById('reassignToSpeaker');
    select.innerHTML = '';

    const otherSpeakers = Array.from(availableSpeakers).sort().filter(speaker => speaker !== fromSpeaker);
    
    if (otherSpeakers.length === 0) {
        alert('Error: No other speakers available for reassignment. Please add a speaker first.');
        return;
    }

    otherSpeakers.forEach(speaker => {
        const option = document.createElement('option');
        option.value = speaker;
        option.textContent = speaker;
        select.appendChild(option);
    });

    reassignModal.show();
}

async function handleConfirmReassign() {
    const fromSpeaker = currentReassignSpeaker;
    const toSpeaker = document.getElementById('reassignToSpeaker').value;

    if (!toSpeaker) {
        alert('Please select a speaker to reassign to');
        return;
    }

    // Validate not reassigning to same speaker
    if (fromSpeaker === toSpeaker) {
        alert('Cannot reassign to the same speaker');
        return;
    }

    // Save original state for rollback
    const originalSegments = JSON.parse(JSON.stringify(transcriptionData.segments));
    
    let updatedCount = 0;
    const affectedLineNumbers = [];
    
    transcriptionData.segments.forEach(segment => {
        if (segment.speaker === fromSpeaker) {
            segment.speaker = toSpeaker;
            affectedLineNumbers.push(segment.lineNumber);
            updatedCount++;
        }
    });

    // Log the reassignment
    auditLog.push({
        timestamp: new Date().toISOString(),
        changeType: 'SpeakerReassignment',
        lineNumber: null, // Multiple lines affected
        oldValue: fromSpeaker,
        newValue: toSpeaker,
        additionalInfo: `${updatedCount} segments reassigned (Lines: ${affectedLineNumbers.join(', ')})`
    });

    hasUnsavedChanges = true;

    try {
        const result = await updateSegmentsOnServer();
        
        // Update all transcription data with server response
        transcriptionData.segments = result.segments;
        transcriptionData.rawJsonData = result.rawJsonData;
        transcriptionData.fullTranscript = result.fullTranscript;
        transcriptionData.audioFileUrl = result.audioFileUrl;
        transcriptionData.goldenRecordJsonData = result.goldenRecordJsonData;
        transcriptionData.auditLog = result.auditLog;
        transcriptionData.availableSpeakers = result.availableSpeakers;
        transcriptionData.speakerStatistics = result.speakerStatistics;

        document.getElementById('fullTranscript').textContent = result.fullTranscript;
        
        // Rebuild available speakers list from server response (includes dropdown refresh)
        buildAvailableSpeakersList();
        
        // Refresh the display to show updated speakers
        displayResults(transcriptionData);

        reassignModal.hide();
        updateSpeakersModal();

        alert(`Successfully reassigned ${updatedCount} segment(s) from "${fromSpeaker}" to "${toSpeaker}"`);
    } catch (error) {
        // Rollback to original state on error
        transcriptionData.segments = originalSegments;
        // Remove the failed audit log entry
        auditLog.pop();
        alert('Error reassigning speakers: ' + error.message + '\nChanges have been rolled back.');
        console.error('Reassignment error:', error);
        
        // Refresh display to show rolled back state
        buildAvailableSpeakersList();
        displayResults(transcriptionData);
    }
}

function deleteSpeaker(speaker) {
    // Use server-provided statistics if available, otherwise count segments
    let segmentCount;
    if (transcriptionData.speakerStatistics && transcriptionData.speakerStatistics.length > 0) {
        const speakerInfo = transcriptionData.speakerStatistics.find(s => s.name === speaker);
        segmentCount = speakerInfo ? speakerInfo.segmentCount : 0;
    } else {
        // Fallback: count from segments
        segmentCount = transcriptionData.segments.filter(s => s.speaker === speaker).length;
    }

    if (segmentCount > 0) {
        alert(`Cannot delete "${speaker}" because it is assigned to ${segmentCount} segment(s). Please reassign these segments first.`);
        return;
    }

    if (confirm(`Are you sure you want to delete the speaker "${speaker}"?\n\nThis speaker is not assigned to any segments.`)) {
        availableSpeakers.delete(speaker);
        
        // Refresh all dropdowns to remove the deleted speaker
        refreshAllSpeakerDropdowns();
        
        // Update the modal to remove the speaker from the list
        updateSpeakersModal();
        
        alert(`Speaker "${speaker}" deleted successfully!`);
    }
}

function handleAddSpeaker() {
    const input = document.getElementById('newSpeakerName');
    const newSpeaker = input.value.trim();

    // Validate not empty
    if (!newSpeaker) {
        alert('Please enter a speaker name');
        return;
    }

    // Validate no special characters that could cause issues
    if (newSpeaker.includes('<') || newSpeaker.includes('>')) {
        alert('Speaker name cannot contain < or > characters');
        return;
    }

    // Case-insensitive duplicate check
    const lowerCaseNewSpeaker = newSpeaker.toLowerCase();
    const existingSpeakers = Array.from(availableSpeakers);
    const isDuplicate = existingSpeakers.some(speaker => 
        speaker.toLowerCase() === lowerCaseNewSpeaker
    );

    if (isDuplicate) {
        alert('This speaker already exists (case-insensitive match)');
        return;
    }

    // Validate length
    if (newSpeaker.length > 100) {
        alert('Speaker name is too long (max 100 characters)');
        return;
    }

    // Add speaker to available speakers set
    availableSpeakers.add(newSpeaker);
    input.value = '';
    
    // Refresh all dropdowns to include the new speaker
    refreshAllSpeakerDropdowns();
    
    // Update the modal to show the new speaker (with 0 segments)
    updateSpeakersModal();
    
    alert(`Speaker "${newSpeaker}" added successfully! You can now assign segments to this speaker.`);
}

// Refresh all speaker dropdowns with current available speakers
function refreshAllSpeakerDropdowns() {
    document.querySelectorAll('.speaker-name-select').forEach(select => {
        const currentValue = select.value;
        const segmentIndex = select.getAttribute('data-segment-index');
        
        // Rebuild options
        select.innerHTML = getSpeakerOptions(currentValue);
    });
}

// Create badge for original speaker name
function createOriginalSpeakerBadge(originalSpeaker) {
    if (!originalSpeaker) return '';
    return `<span class="badge bg-info text-dark" title="Original speaker from Azure: ${originalSpeaker}">
                Original: ${originalSpeaker}
            </span>`;
}

// Create badge for original transcript text
function createOriginalTextBadge(originalText) {
    if (!originalText) return '';
    const truncated = originalText.length > 50 ? originalText.substring(0, 50) + '...' : originalText;
    return `<small class="text-muted" title="Original text from Azure: ${originalText}">
                <i class="bi bi-info-circle"></i> Original: ${truncated}
            </small>`;
}

// Handle speaker name change from dropdown
async function handleSpeakerNameChange(event) {
    const select = event.target;
    const segmentIndex = parseInt(select.getAttribute('data-segment-index'));
    const newSpeaker = select.value;
    const oldSpeaker = transcriptionData.segments[segmentIndex].speaker;
    
    if (newSpeaker === oldSpeaker) {
        return; // No change
    }
    
    // Save original state for rollback
    const originalSegments = JSON.parse(JSON.stringify(transcriptionData.segments));
    
    // Update the segment
    transcriptionData.segments[segmentIndex].speaker = newSpeaker;
    
    // Log the change
    auditLog.push({
        timestamp: new Date().toISOString(),
        changeType: 'SpeakerEdit',
        lineNumber: transcriptionData.segments[segmentIndex].lineNumber,
        oldValue: oldSpeaker,
        newValue: newSpeaker,
        additionalInfo: null
    });
    
    hasUnsavedChanges = true;
    
    try {
        const result = await updateSegmentsOnServer();
        
        // Update all transcription data with server response
        transcriptionData.segments = result.segments;
        transcriptionData.rawJsonData = result.rawJsonData;
        transcriptionData.fullTranscript = result.fullTranscript;
        transcriptionData.audioFileUrl = result.audioFileUrl;
        transcriptionData.goldenRecordJsonData = result.goldenRecordJsonData;
        transcriptionData.auditLog = result.auditLog;
        transcriptionData.availableSpeakers = result.availableSpeakers;
        transcriptionData.speakerStatistics = result.speakerStatistics;
        
        document.getElementById('fullTranscript').textContent = result.fullTranscript;
        
        // Rebuild available speakers list from server response
        buildAvailableSpeakersList();
        
        // Refresh the display to show updated speakers
        displayResults(transcriptionData);
        
    } catch (error) {
        // Rollback on error
        transcriptionData.segments = originalSegments;
        auditLog.pop();
        alert('Error updating speaker: ' + error.message);
        console.error('Speaker update error:', error);
        displayResults(transcriptionData);
    }
}

// Enter transcript edit mode
function enterTranscriptEditMode(segmentIndex) {
    const segment = document.querySelector(`[data-segment-index="${segmentIndex}"]`);
    if (!segment) return;
    
    const display = segment.querySelector('.transcript-text-display');
    const textarea = segment.querySelector('.transcript-text-input');
    const actions = segment.querySelector('.transcript-edit-actions');
    
    if (display && textarea && actions) {
        display.classList.add('d-none');
        textarea.classList.remove('d-none');
        actions.classList.remove('d-none');
        textarea.focus();
        textarea.select();
    }
}

// Save transcript edit
async function saveTranscriptEdit(segmentIndex) {
    const segment = document.querySelector(`[data-segment-index="${segmentIndex}"]`);
    if (!segment) return;
    
    const textarea = segment.querySelector('.transcript-text-input');
    const newText = textarea.value.trim();
    
    if (!newText) {
        alert('Transcript text cannot be empty');
        return;
    }
    
    const oldText = transcriptionData.segments[segmentIndex].text;
    
    if (newText === oldText) {
        exitTranscriptEditMode(segmentIndex);
        return; // No change
    }
    
    // Save original state for rollback
    const originalSegments = JSON.parse(JSON.stringify(transcriptionData.segments));
    
    // Update the segment
    transcriptionData.segments[segmentIndex].text = newText;
    
    // Log the change
    auditLog.push({
        timestamp: new Date().toISOString(),
        changeType: 'TextEdit',
        lineNumber: transcriptionData.segments[segmentIndex].lineNumber,
        oldValue: oldText,
        newValue: newText,
        additionalInfo: null
    });
    
    hasUnsavedChanges = true;
    
    try {
        const result = await updateSegmentsOnServer();
        
        // Update all transcription data with server response
        transcriptionData.segments = result.segments;
        transcriptionData.rawJsonData = result.rawJsonData;
        transcriptionData.fullTranscript = result.fullTranscript;
        transcriptionData.audioFileUrl = result.audioFileUrl;
        transcriptionData.goldenRecordJsonData = result.goldenRecordJsonData;
        transcriptionData.auditLog = result.auditLog;
        transcriptionData.availableSpeakers = result.availableSpeakers;
        transcriptionData.speakerStatistics = result.speakerStatistics;
        
        document.getElementById('fullTranscript').textContent = result.fullTranscript;
        
        // Refresh the display
        displayResults(transcriptionData);
        
    } catch (error) {
        // Rollback on error
        transcriptionData.segments = originalSegments;
        auditLog.pop();
        alert('Error updating transcript: ' + error.message);
        console.error('Transcript update error:', error);
        displayResults(transcriptionData);
    }
}

// Cancel transcript edit
function cancelTranscriptEdit(segmentIndex) {
    const segment = document.querySelector(`[data-segment-index="${segmentIndex}"]`);
    if (!segment) return;
    
    const textarea = segment.querySelector('.transcript-text-input');
    
    // Restore original text
    textarea.value = transcriptionData.segments[segmentIndex].text;
    
    exitTranscriptEditMode(segmentIndex);
}

// Exit transcript edit mode
function exitTranscriptEditMode(segmentIndex) {
    const segment = document.querySelector(`[data-segment-index="${segmentIndex}"]`);
    if (!segment) return;
    
    const display = segment.querySelector('.transcript-text-display');
    const textarea = segment.querySelector('.transcript-text-input');
    const actions = segment.querySelector('.transcript-edit-actions');
    
    if (display && textarea && actions) {
        display.classList.remove('d-none');
        textarea.classList.add('d-none');
        actions.classList.add('d-none');
    }
}

// Update segments on server
async function updateSegmentsOnServer() {
    const response = await fetch('/Home/UpdateSpeakerNames', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            segments: transcriptionData.segments,
            audioFileUrl: transcriptionData.audioFileUrl,
            goldenRecordJsonData: transcriptionData.goldenRecordJsonData,
            auditLog: auditLog
        })
    });
    
    if (!response.ok) {
        throw new Error(`Server error: ${response.status}`);
    }
    
    const result = await response.json();
    
    if (!result.success) {
        throw new Error(result.message || 'Update failed');
    }
    
    return result;
}

// Handle speaker name keypress (Enter key)
function handleSpeakerNameKeypress(event) {
    if (event.key === 'Enter') {
        event.preventDefault();
        handleAddSpeaker();
    }
}

// Handle golden record download
async function handleDownloadGoldenRecord() {
    if (!goldenRecordData) {
        alert('No original record data available');
        return;
    }
    
    try {
        const response = await fetch('/Home/DownloadGoldenRecord', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                goldenRecordJsonData: goldenRecordData
            })
        });
        
        if (!response.ok) {
            throw new Error('Download failed');
        }
        
        const blob = await response.blob();
        downloadBlob(blob, `transcription_original_${getTimestamp()}.json`);
    } catch (error) {
        console.error('Download error:', error);
        alert('Error downloading original record: ' + error.message);
    }
}

// Handle readable download
async function handleDownloadReadable() {
    if (!transcriptionData) {
        alert('No transcription data available');
        return;
    }
    
    try {
        const response = await fetch('/Home/DownloadReadableText', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                fullTranscript: transcriptionData.fullTranscript,
                segments: transcriptionData.segments
            })
        });
        
        if (!response.ok) {
            throw new Error('Download failed');
        }
        
        const blob = await response.blob();
        downloadBlob(blob, `transcription_${getTimestamp()}.docx`);
    } catch (error) {
        console.error('Download error:', error);
        alert('Error downloading transcript: ' + error.message);
    }
}

// Handle download with tracking
async function handleDownloadWithTracking() {
    if (!transcriptionData) {
        alert('No transcription data available');
        return;
    }
    
    try {
        const response = await fetch('/Home/DownloadReadableTextWithTracking', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                fullTranscript: transcriptionData.fullTranscript,
                segments: transcriptionData.segments
            })
        });
        
        if (!response.ok) {
            throw new Error('Download failed');
        }
        
        const blob = await response.blob();
        downloadBlob(blob, `transcription_with_tracking_${getTimestamp()}.docx`);
    } catch (error) {
        console.error('Download error:', error);
        alert('Error downloading transcript with tracking: ' + error.message);
    }
}

// Handle audit log download
async function handleDownloadAuditLog() {
    if (!auditLog || auditLog.length === 0) {
        alert('No edit history available');
        return;
    }
    
    try {
        const response = await fetch('/Home/DownloadAuditLog', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                auditLog: auditLog
            })
        });
        
        if (!response.ok) {
            throw new Error('Download failed');
        }
        
        const blob = await response.blob();
        downloadBlob(blob, `audit_log_${getTimestamp()}.docx`);
    } catch (error) {
        console.error('Download error:', error);
        alert('Error downloading audit log: ' + error.message);
    }
}

// Handle cancellation
function handleCancelTranscription() {
    if (currentAbortController) {
        if (confirm('Are you sure you want to cancel the transcription?')) {
            currentAbortController.abort();
            hideElement('progressSection');
            showWarning('Transcription canceled');
        }
    }
}
