// Speaker Manager Module
import { AppState } from './app.js';

export function openSpeakerManager() {
    if (!AppState.transcriptionData || !AppState.transcriptionData.segments) {
        alert('No transcription data available');
        return;
    }
    
    const modal = document.getElementById('speakerModal');
    const overlay = document.getElementById('speakerModalOverlay');
    modal.style.display = 'block';
    overlay.style.display = 'block';
    renderSpeakerList();
}

export function closeSpeakerManager() {
    const modal = document.getElementById('speakerModal');
    const overlay = document.getElementById('speakerModalOverlay');
    modal.style.display = 'none';
    overlay.style.display = 'none';
}

export function renderSpeakerList() {
    const segments = AppState.transcriptionData.segments;
    const speakers = {};
    
    segments.forEach((segment, index) => {
        const speaker = segment.speaker || 'Unknown';
        if (!speakers[speaker]) {
            speakers[speaker] = [];
        }
        speakers[speaker].push(index);
    });
    
    // Get all available speakers (including those with 0 segments)
    // Prefer availableSpeakers from AppState, but ensure it includes all segment speakers too
    let availableSpeakers = AppState.transcriptionData.availableSpeakers || [];
    
    // Merge with speakers from segments to ensure we don't miss any
    const segmentSpeakers = Object.keys(speakers);
    const allSpeakers = new Set([...availableSpeakers, ...segmentSpeakers]);
    
    // Update AppState to include merged list
    AppState.transcriptionData.availableSpeakers = Array.from(allSpeakers).sort();
    
    console.log('?? Rendering speaker list with speakers:', AppState.transcriptionData.availableSpeakers);
    
    const speakerList = document.getElementById('speakerList');
    speakerList.innerHTML = AppState.transcriptionData.availableSpeakers.map(speaker => {
        const count = speakers[speaker] ? speakers[speaker].length : 0;
        return `
            <div class="speaker-item" id="speaker-item-${escapeHtml(speaker)}">
                <div class="speaker-name">${escapeHtml(speaker)}</div>
                <div class="speaker-count">${count} segment${count !== 1 ? 's' : ''}</div>
                <div class="speaker-actions">
                    <button onclick="renameSpeaker('${escapeHtml(speaker)}')" class="btn-rename">\u270F\uFE0F Rename</button>
                    <button onclick="showReassignPopup('${escapeHtml(speaker)}')" class="btn-reassign">\uD83D\uDD04 Reassign</button>
                    ${count === 0 ? `<button onclick="deleteSpeaker('${escapeHtml(speaker)}')" class="btn-delete">\uD83D\uDDD1\uFE0F Delete</button>` : ''}
                </div>
            </div>
        `;
    }).join('');
}

export function addNewSpeaker() {
    const input = document.getElementById('newSpeakerName');
    const name = input.value.trim();
    
    if (!name) {
        alert('Please enter a speaker name');
        return;
    }
    
    // Get all speakers (from both segments and availableSpeakers array)
    const currentSpeakers = new Set(
        AppState.transcriptionData.segments.map(s => s.speaker)
    );
    
    // Also check availableSpeakers array if it exists
    if (AppState.transcriptionData.availableSpeakers) {
        AppState.transcriptionData.availableSpeakers.forEach(s => currentSpeakers.add(s));
    }
    
    if (currentSpeakers.has(name)) {
        alert('A speaker with this name already exists');
        return;
    }
    
    // Initialize availableSpeakers array if it doesn't exist
    if (!AppState.transcriptionData.availableSpeakers) {
        // Start with all speakers from segments
        AppState.transcriptionData.availableSpeakers = Array.from(new Set(
            AppState.transcriptionData.segments.map(s => s.speaker)
        )).sort();
    }
    
    // Add the new speaker to availableSpeakers
    AppState.transcriptionData.availableSpeakers.push(name);
    AppState.transcriptionData.availableSpeakers.sort();
    
    console.log(`? Added new speaker: "${name}"`);
    console.log(`?? Available speakers:`, AppState.transcriptionData.availableSpeakers);
    
    // Update server with new available speakers
    updateSpeakersOnServer();
    
    // Refresh the speaker list in the modal
    renderSpeakerList();
    
    // Refresh segment dropdowns to include new speaker
    refreshTranscriptionDisplay();
    
    // Clear input and show success message
    input.value = '';
    alert(`Speaker "${name}" added successfully and is now available for reassignment`);
}

export function renameSpeaker(oldName) {
    const newName = prompt(`Rename "${oldName}" to:`, oldName);
    if (!newName || !newName.trim() || newName.trim() === oldName) return;
    
    const trimmedName = newName.trim();
    
    // ?? SEND OLD VALUES TO SERVER FIRST (before updating local state)
    updateSpeakersOnServer(oldName, trimmedName, 'rename');
    
    // ?? Update local state (will be synced with server response)
    AppState.transcriptionData.segments.forEach(segment => {
        if (segment.speaker === oldName) {
            segment.speaker = trimmedName;
        }
    });
    
    renderSpeakerList();
    refreshTranscriptionDisplay();
}

export function showReassignPopup(fromSpeaker) {
    // Get all available speakers (including those with 0 segments) excluding the current one
    const availableSpeakers = AppState.transcriptionData.availableSpeakers || 
        Array.from(new Set(AppState.transcriptionData.segments.map(s => s.speaker)));
    
    const allSpeakers = availableSpeakers.filter(s => s !== fromSpeaker).sort();
    
    if (allSpeakers.length === 0) {
        alert('No other speakers available to reassign to.');
        return;
    }
    
    // Create popup HTML
    const popupHTML = `
        <div class="reassign-popup-overlay" id="reassignPopupOverlay" onclick="closeReassignPopup()"></div>
        <div class="reassign-popup" id="reassignPopup">
            <div class="reassign-popup-header">
                <h3>\uD83D\uDD04 Reassign Speaker</h3>
                <button class="reassign-popup-close" onclick="closeReassignPopup()">×</button>
            </div>
            <div class="reassign-popup-content">
                <p>Reassign all segments from <strong>"${escapeHtml(fromSpeaker)}"</strong> to:</p>
                <select id="reassignTargetSelect" class="reassign-select">
                    <option value="">-- Select a speaker --</option>
                    ${allSpeakers.map(speaker => `<option value="${escapeHtml(speaker)}">${escapeHtml(speaker)}</option>`).join('')}
                </select>
            </div>
            <div class="reassign-popup-actions">
                <button class="btn-cancel" onclick="closeReassignPopup()">Cancel</button>
                <button class="btn-confirm" onclick="confirmReassign('${escapeHtml(fromSpeaker)}')">Confirm Reassign</button>
            </div>
        </div>
    `;
    
    // Add popup to body
    const popupContainer = document.createElement('div');
    popupContainer.id = 'reassignPopupContainer';
    popupContainer.innerHTML = popupHTML;
    document.body.appendChild(popupContainer);
    
    // Focus on the select dropdown
    setTimeout(() => {
        document.getElementById('reassignTargetSelect')?.focus();
    }, 100);
}

export function closeReassignPopup() {
    const container = document.getElementById('reassignPopupContainer');
    if (container) {
        container.remove();
    }
}

export function confirmReassign(fromSpeaker) {
    const targetSelect = document.getElementById('reassignTargetSelect');
    const toSpeaker = targetSelect?.value;
    
    if (!toSpeaker) {
        alert('Please select a speaker to reassign to');
        return;
    }
    
    if (!confirm(`Reassign all segments from "${fromSpeaker}" to "${toSpeaker}"?`)) {
        return;
    }
    
    // Close the popup
    closeReassignPopup();
    
    // ?? SEND OLD VALUES TO SERVER FIRST (before updating local state)
    updateSpeakersOnServer(fromSpeaker, toSpeaker, 'reassign');
    
    // ?? Update local state (will be synced with server response)
    AppState.transcriptionData.segments.forEach(segment => {
        if (segment.speaker === fromSpeaker) {
            segment.speaker = toSpeaker;
        }
    });
    
    renderSpeakerList();
    refreshTranscriptionDisplay();
}

export function deleteSpeaker(speakerName) {
    if (!confirm(`Delete speaker "${speakerName}" and reassign all segments to "Unknown"?`)) {
        return;
    }
    
    // ?? SEND OLD VALUES TO SERVER FIRST (before updating local state)
    updateSpeakersOnServer(speakerName, 'Unknown', 'delete');
    
    // ?? Update local state (will be synced with server response)
    AppState.transcriptionData.segments.forEach(segment => {
        if (segment.speaker === speakerName) {
            segment.speaker = 'Unknown';
        }
    });
    
    renderSpeakerList();
    refreshTranscriptionDisplay();
}

export function changeSpeakerForSegment(segmentIndex, newSpeaker) {
    if (!AppState.transcriptionData || !AppState.transcriptionData.segments) {
        console.error('? No transcription data available');
        return;
    }
    
    const segment = AppState.transcriptionData.segments[segmentIndex];
    if (!segment) {
        console.error(`? Segment ${segmentIndex} not found`);
        return;
    }
    
    const oldSpeaker = segment.speaker;
    console.log(`?? Changing speaker for segment ${segmentIndex} from "${oldSpeaker}" to "${newSpeaker}"`);
    
    // Update segment speaker
    segment.speaker = newSpeaker;
    
    // Update speakers on server
    updateSpeakersOnServer();
    
    // Refresh display
    refreshTranscriptionDisplay();
}

async function updateSpeakersOnServer(oldSpeaker = null, newSpeaker = null, operationType = null) {
    try {
        const requestData = {
            segments: AppState.transcriptionData.segments,  // ?? Send CURRENT (old) values
            audioFileUrl: AppState.transcriptionData.audioFileUrl,
            goldenRecordJsonData: AppState.transcriptionData.goldenRecordJsonData,
            auditLog: AppState.transcriptionData.editHistory || [],
            availableSpeakers: AppState.transcriptionData.availableSpeakers || []  // ?? Send availableSpeakers
        };
        
        // ?? Add speaker change information if provided
        if (oldSpeaker && newSpeaker && operationType) {
            requestData.oldSpeaker = oldSpeaker;
            requestData.newSpeaker = newSpeaker;
            requestData.operationType = operationType;  // 'rename', 'reassign', or 'delete'
        }
        
        console.log('?? DEBUG: Request data being sent:', {
            segmentCount: requestData.segments.length,
            firstSegmentSpeaker: requestData.segments[0]?.speaker,
            allSpeakers: requestData.segments.map((s, i) => `[${i}]: ${s.speaker}`),
            auditLogCount: requestData.auditLog.length,
            availableSpeakersCount: requestData.availableSpeakers.length,
            availableSpeakers: requestData.availableSpeakers,
            hasGoldenRecord: !!requestData.goldenRecordJsonData,
            oldSpeaker: requestData.oldSpeaker || '(none)',
            newSpeaker: requestData.newSpeaker || '(none)',
            operationType: requestData.operationType || '(none)'
        });
        
        const response = await fetch('/update-speaker-names', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(requestData)
        });
        
        if (!response.ok) {
            throw new Error(`Server returned ${response.status}: ${response.statusText}`);
        }
        
        const result = await response.json();
        console.log('? Speakers updated on server:', result);
        console.log('?? DEBUG: Response audit log count:', result.auditLog?.length || 0);
        console.log('?? DEBUG: Response segments count:', result.segments?.length || 0);
        console.log('?? DEBUG: Response availableSpeakers:', result.availableSpeakers);
        console.log('?? DEBUG: Full auditLog array:', result.auditLog);
        
        // ?? Preserve availableSpeakers before updating from server
        const localAvailableSpeakers = AppState.transcriptionData.availableSpeakers;
        
        // Update transcription data with server response
        AppState.transcriptionData.segments = result.segments || AppState.transcriptionData.segments;
        AppState.transcriptionData.fullTranscript = result.fullTranscript || AppState.transcriptionData.fullTranscript;
        AppState.transcriptionData.rawJsonData = result.rawJsonData || AppState.transcriptionData.rawJsonData;
        AppState.transcriptionData.audioFileUrl = result.audioFileUrl || AppState.transcriptionData.audioFileUrl;
        AppState.transcriptionData.goldenRecordJsonData = result.goldenRecordJsonData || AppState.transcriptionData.goldenRecordJsonData;
        
        // ?? Preserve availableSpeakers - use server value if provided, otherwise keep local
        AppState.transcriptionData.availableSpeakers = result.availableSpeakers || localAvailableSpeakers;
        
        console.log('?? Preserved availableSpeakers:', AppState.transcriptionData.availableSpeakers);
        
        // ?? Update audit log from server response
        if (result.success && result.auditLog) {
            AppState.transcriptionData.editHistory = result.auditLog;
            console.log(`?? Audit log updated with ${result.auditLog.length} entries`);
            
            // ?? Refresh audit log UI
            const { updateAuditLog } = await import('./transcription-display.js');
            updateAuditLog(AppState.transcriptionData);
            console.log('?? Audit log UI refreshed');
        }
    } catch (error) {
        console.error('? Error updating speakers on server:', error);
        alert(`Failed to update speakers: ${error.message}`);
    }
}

async function refreshTranscriptionDisplay() {
    const { renderSegments } = await import('./transcription-display.js');
    renderSegments(AppState.transcriptionData.segments);
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Expose functions to window for HTML onclick handlers
window.openSpeakerManager = openSpeakerManager;
window.closeSpeakerManager = closeSpeakerManager;
window.addNewSpeaker = addNewSpeaker;
window.renameSpeaker = renameSpeaker;
window.showReassignPopup = showReassignPopup;
window.confirmReassign = confirmReassign;
window.closeReassignPopup = closeReassignPopup;
window.deleteSpeaker = deleteSpeaker;
window.changeSpeakerForSegment = changeSpeakerForSegment;
