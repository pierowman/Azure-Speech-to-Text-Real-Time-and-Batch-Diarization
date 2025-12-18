// Edit Manager Module
import { AppState } from './app.js';
import { updateAuditLog } from './transcription-display.js';

export function toggleEditMode(checked) {
    console.log(`?? toggleEditMode called with checked=${checked}`);
    AppState.isEditMode = checked;
    
    // Get all speaker dropdowns
    const dropdowns = document.querySelectorAll('.segment-speaker-dropdown');
    
    if (checked) {
        // ? EDIT MODE ON: Disable all dropdowns
        // They will be enabled individually when a segment enters editing
        dropdowns.forEach(dropdown => {
            dropdown.disabled = true;
        });
        console.log(`?? Edit mode enabled - dropdowns disabled (will enable during segment edit)`);
    } else {
        // ? EDIT MODE OFF: Keep all dropdowns disabled
        dropdowns.forEach(dropdown => {
            dropdown.disabled = true;
        });
        console.log(`?? Edit mode disabled - dropdowns remain disabled`);
    }
    
    console.log(`? Edit mode ${checked ? 'enabled' : 'disabled'}, ${dropdowns.length} dropdowns updated`);
    
    // Update UI message
    const segments = document.querySelectorAll('.segment');
    if (checked) {
        segments.forEach(seg => seg.style.cursor = 'pointer');
    } else {
        segments.forEach(seg => seg.style.cursor = '');
    }
}

export async function changeSpeakerForSegment(index, newSpeaker) {
    console.log(`?? changeSpeakerForSegment called for index: ${index}, newSpeaker: ${newSpeaker}`);
    
    // This function should never be called because dropdowns are disabled
    // unless actively editing a segment. But if it somehow gets called,
    // we'll handle it gracefully.
    
    console.warn('?? changeSpeakerForSegment should not be called - dropdowns should be disabled');
    console.log('?? To change speaker: Click segment text to edit, change dropdown, then click Save');
    return;
}

export async function saveSegmentEdit(index) {
    console.log(`?? saveSegmentEdit called for index: ${index}`);
    console.trace('?? CALL STACK - What triggered saveSegmentEdit:');
    
    const textarea = document.querySelector(`.segment-text-editable[data-index="${index}"]`);
    if (!textarea) {
        console.error(`? Textarea not found for segment ${index}`);
        return;
    }
    
    const newText = textarea.value.trim();
    if (!newText) {
        alert('Text cannot be empty');
        return;
    }
    
    const segment = AppState.transcriptionData.segments[index];
    const oldText = segment.text;
    const oldSpeaker = segment.speaker;
    
    // Get the speaker dropdown to check if speaker was changed
    const speakerDropdown = document.querySelector(`.segment-speaker-dropdown[data-index="${index}"]`);
    const newSpeaker = speakerDropdown ? speakerDropdown.value : oldSpeaker;
    
    const textChanged = oldText !== newText;
    const speakerChanged = oldSpeaker !== newSpeaker;
    
    console.log(`?? Updating segment ${index}:`);
    console.log(`   Text: "${oldText}" ? "${newText}" (${textChanged ? 'CHANGED' : 'unchanged'})`);
    console.log(`   Speaker: "${oldSpeaker}" ? "${newSpeaker}" (${speakerChanged ? 'CHANGED' : 'unchanged'})`);
    
    // If nothing changed, just exit edit mode silently (no error)
    if (!textChanged && !speakerChanged) {
        console.log(`?? No changes detected for segment ${index} - exiting edit mode`);
        cancelSegmentEdit(index);
        return;
    }
    
    // Create new div to replace textarea
    const newDiv = document.createElement('div');
    newDiv.className = 'segment-text';
    newDiv.dataset.index = index;
    newDiv.textContent = newText;
    
    // Add click handler
    newDiv.addEventListener('click', (e) => {
        e.stopPropagation();
        if (AppState.isEditMode) {
            window.startEditingSegment(index);
        } else {
            const segmentEl = newDiv.closest('.segment');
            const startTime = parseFloat(segmentEl.getAttribute('data-start'));
            if (!isNaN(startTime)) {
                window.playSegment(startTime);
            }
        }
    });
    
    // Replace textarea with div
    textarea.replaceWith(newDiv);
    
    // Remove editing class
    const segmentEl = newDiv.closest('.segment');
    if (segmentEl) {
        segmentEl.classList.remove('editing');
        segmentEl.classList.add('edited');
    }
    
    // ? Disable speaker dropdown (no need to restore onchange handler)
    if (speakerDropdown) {
        speakerDropdown.disabled = true;
        console.log(`?? Speaker dropdown disabled`);
    }
    
    // Hide save/cancel buttons
    const editActions = document.getElementById(`edit-actions-${index}`);
    if (editActions) {
        editActions.style.display = 'none';
    }
    
    // Update server with both text and speaker changes
    // Pass segment object so server can update it after validation
    updateSegmentOnServer(index, newText, oldText, newSpeaker, oldSpeaker, textChanged, speakerChanged);
    
    console.log(`? Saved segment ${index} edit`);
}

export function cancelSegmentEdit(index) {
    console.log(`? cancelSegmentEdit called for index: ${index}`);
    
    const textarea = document.querySelector(`.segment-text-editable[data-index="${index}"]`);
    if (!textarea) {
        console.error(`? Textarea not found for segment ${index}`);
        return;
    }
    
    // Get original text and speaker
    const segment = AppState.transcriptionData.segments[index];
    const originalText = segment.text;
    
    // Revert speaker dropdown to original value and disable it
    const speakerDropdown = document.querySelector(`.segment-speaker-dropdown[data-index="${index}"]`);
    if (speakerDropdown) {
        const storedOriginalSpeaker = speakerDropdown.dataset.originalSpeaker;
        if (storedOriginalSpeaker) {
            speakerDropdown.value = storedOriginalSpeaker;
            console.log(`?? Reverted speaker to: "${storedOriginalSpeaker}"`);
        }
        
        // Clear any pending change flags
        delete speakerDropdown.dataset.pendingValue;
        delete speakerDropdown.dataset.originalValue;
        delete speakerDropdown.dataset.originalSpeaker;
        
        // \u2705 Disable dropdown (no need to restore onchange handler)
        speakerDropdown.disabled = true;
        console.log(`\uD83D\uDD12 Speaker dropdown disabled`);
    }
    
    // Create new div to replace textarea
    const newDiv = document.createElement('div');
    newDiv.className = 'segment-text';
    newDiv.dataset.index = index;
    newDiv.textContent = originalText;
    
    // Add click handler
    newDiv.addEventListener('click', (e) => {
        e.stopPropagation();
        if (AppState.isEditMode) {
            window.startEditingSegment(index);
        } else {
            const segmentEl = newDiv.closest('.segment');
            const startTime = parseFloat(segmentEl.getAttribute('data-start'));
            if (!isNaN(startTime)) {
                window.playSegment(startTime);
            }
        }
    });
    
    // Replace textarea with div
    textarea.replaceWith(newDiv);
    
    // Remove editing class and pending changes indicator
    const segmentEl = newDiv.closest('.segment');
    if (segmentEl) {
        segmentEl.classList.remove('editing');
        segmentEl.classList.remove('has-pending-changes');
    }
    
    // Hide save/cancel buttons
    const editActions = document.getElementById(`edit-actions-${index}`);
    if (editActions) {
        editActions.style.display = 'none';
    }
    
    console.log(`? Cancelled segment ${index} edit (text and speaker reverted)`);
}

export function startEditingSegment(index) {
    console.log(`?? startEditingSegment called for index: ${index}`);
    console.log(`?? AppState.isEditMode: ${AppState.isEditMode}`);
    
    if (!AppState.isEditMode) {
        console.warn('?? Cannot edit: Edit mode is not enabled');
        return;
    }
    
    const segmentTextDiv = document.querySelector(`.segment-text[data-index="${index}"]`);
    if (!segmentTextDiv) {
        console.error(`? Segment text div not found for index ${index}`);
        return;
    }
    
    const segment = AppState.transcriptionData.segments[index];
    if (!segment) {
        console.error(`? Segment data not found for index ${index}`);
        return;
    }
    
    const currentText = segment.text || '';
    const currentSpeaker = segment.speaker || 'Unknown';
    console.log(`?? Current text for segment ${index}: "${currentText}"`);
    console.log(`?? Current speaker for segment ${index}: "${currentSpeaker}"`);
    
    // Store original text and speaker for cancel
    segmentTextDiv.dataset.originalText = currentText;
    segmentTextDiv.dataset.originalSpeaker = currentSpeaker;
    
    // Get the speaker dropdown and enable it for editing
    const speakerDropdown = document.querySelector(`.segment-speaker-dropdown[data-index="${index}"]`);
    if (speakerDropdown) {
        // Check if there's a pending speaker change from before entering edit mode
        const pendingValue = speakerDropdown.dataset.pendingValue;
        const originalValue = speakerDropdown.dataset.originalValue;
        
        if (pendingValue) {
            console.log(`?? Found pending speaker change: "${originalValue}" ? "${pendingValue}"`);
            // Use the original value (before dropdown change) as the baseline
            speakerDropdown.dataset.originalSpeaker = originalValue;
            // Clear pending flags since we're now in edit mode
            delete speakerDropdown.dataset.pendingValue;
            delete speakerDropdown.dataset.originalValue;
        } else {
            // No pending change, use current speaker
            speakerDropdown.dataset.originalSpeaker = currentSpeaker;
        }
        
        // ? Enable the dropdown for this segment only
        speakerDropdown.disabled = false;
        
        // Disable the onchange handler - speaker changes will be saved when Save is clicked
        speakerDropdown.setAttribute('onchange', ''); // Remove inline handler
        console.log(`?? Speaker dropdown enabled for editing (onchange handler disabled)`);
    }
    
    // Remove pending changes indicator
    const segmentEl = document.querySelector(`.segment[data-index="${index}"]`);
    if (segmentEl) {
        segmentEl.classList.remove('has-pending-changes');
    }
    
    // Create textarea
    const textarea = document.createElement('textarea');
    textarea.value = currentText;
    textarea.className = 'segment-text-editable';
    textarea.dataset.index = index;
    
    // CRITICAL: Stop all click events from bubbling up to segment container
    textarea.addEventListener('click', (e) => {
        e.stopPropagation();
        e.stopImmediatePropagation();
    }, true);
    
    // CRITICAL: Stop mousedown events to prevent audio player interaction
    textarea.addEventListener('mousedown', (e) => {
        e.stopPropagation();
        e.stopImmediatePropagation();
    }, true);
    
    // CRITICAL: Stop all pointer events from bubbling
    textarea.addEventListener('pointerdown', (e) => {
        e.stopPropagation();
        e.stopImmediatePropagation();
    }, true);
    
    // Replace div with textarea
    segmentTextDiv.replaceWith(textarea);
    
    // Focus and select text
    textarea.focus();
    textarea.select();
    
    // Add segment to editing class
    if (segmentEl) {
        segmentEl.classList.add('editing');
    }
    
    // Show save/cancel buttons
    const editActions = document.getElementById(`edit-actions-${index}`);
    if (editActions) {
        editActions.style.display = 'flex';
        
        // DEBUG: Verify save button isn't being auto-clicked
        const saveBtn = editActions.querySelector('.save-btn');
        if (saveBtn) {
            console.log(`?? Save button found:`, {
                visible: editActions.style.display,
                onclick: saveBtn.getAttribute('onclick'),
                listeners: 'Cannot enumerate event listeners'
            });
        }
    }
    
    // Handle Enter key to save (but allow Shift+Enter for new lines)
    textarea.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            saveSegmentEdit(index);
        } else if (e.key === 'Escape') {
            e.preventDefault();
            cancelSegmentEdit(index);
        }
    });
    
    console.log(`? Started editing segment ${index}`);
}

async function updateSpeakerOnServer(index, newSpeaker, oldSpeaker) {
    try {
        const response = await fetch('/update-segment-text', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ 
                segmentIndex: index,
                newSpeaker: newSpeaker,
                segments: AppState.transcriptionData.segments,
                auditLog: AppState.transcriptionData.editHistory || [],
                audioFileUrl: AppState.transcriptionData.audioFileUrl,
                goldenRecordJsonData: AppState.transcriptionData.goldenRecordJsonData
            })
        });
        
        if (!response.ok) {
            throw new Error('Failed to update speaker on server');
        }
        
        const result = await response.json();
        console.log('? Speaker updated on server:', result);
        
        // Update transcription data with server response
        if (result.auditLog) {
            AppState.transcriptionData.editHistory = result.auditLog;
        }
        
        // Show success message
        if (result.message) {
            console.log(`? ${result.message}`);
        }
    } catch (error) {
        console.error('? Error updating speaker:', error);
        alert('Failed to save speaker change to server. Changes are saved locally only.');
    }
}

async function updateSegmentOnServer(index, newText, oldText, newSpeaker, oldSpeaker, textChanged, speakerChanged) {
    try {
        const segment = AppState.transcriptionData.segments[index];
        
        // Build request body with all necessary data
        // ?? IMPORTANT: Send segments array with OLD values (not updated yet)
        const requestBody = {
            segmentIndex: index,
            newText: newText,  // ?? ALWAYS include newText (backend expects it)
            segments: AppState.transcriptionData.segments,  // Contains OLD values
            auditLog: AppState.transcriptionData.editHistory || [],
            audioFileUrl: AppState.transcriptionData.audioFileUrl,
            goldenRecordJsonData: AppState.transcriptionData.goldenRecordJsonData
        };
        
        // Add speaker change if applicable
        if (speakerChanged) {
            requestBody.newSpeaker = newSpeaker;
        }
        
        console.log(`?? Sending update to server:`, {
            index,
            textChanged,
            speakerChanged,
            oldText: textChanged ? oldText : '(unchanged)',
            newText: newText,  // Always show
            oldSpeaker: speakerChanged ? oldSpeaker : '(unchanged)',
            newSpeaker: speakerChanged ? newSpeaker : '(unchanged)'
        });
        
        const response = await fetch('/update-segment-text', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(requestBody)
        });
        
        if (!response.ok) {
            const errorData = await response.json().catch(() => null);
            console.error('? Server error response:', errorData);
            throw new Error(errorData?.message || 'Failed to update segment on server');
        }
        
        const result = await response.json();
        console.log('? Segment updated on server:', result);
        
        // ?? NOW update local state with server's confirmed changes
        if (result.segments && result.segments[index]) {
            AppState.transcriptionData.segments[index] = result.segments[index];
            console.log(`? Updated local segment ${index} from server response`);
        }
        
        // Update transcription data with server response
        if (result.auditLog) {
            AppState.transcriptionData.editHistory = result.auditLog;
            console.log(`?? Audit log updated: ${result.auditLog.length} entries`);
            
            // ?? Update audit log display
            updateAuditLog(AppState.transcriptionData);
            console.log('?? Audit log UI refreshed');
        }
    } catch (error) {
        console.error('? Error updating segment:', error);
        alert(`Failed to save changes to server: ${error.message}\n\nChanges are visible locally but NOT saved. Please refresh the page to revert.`);
        // Note: UI already shows the changes (optimistic update), but server didn't confirm
        // User needs to refresh to see actual server state
    }
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Expose functions to window for HTML onclick handlers
window.toggleEditMode = toggleEditMode;
window.startEditingSegment = startEditingSegment;
window.saveSegmentEdit = saveSegmentEdit;
window.cancelSegmentEdit = cancelSegmentEdit;
window.changeSpeakerForSegment = changeSpeakerForSegment;
