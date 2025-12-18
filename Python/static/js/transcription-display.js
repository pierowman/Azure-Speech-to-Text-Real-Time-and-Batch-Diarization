// Transcription Display Module
import { AppState } from './app.js';
import { formatTime, playSegment, setupAudioPlayerEvents } from './audio-player.js';
import { showResults } from './ui-helpers.js';

// Helper function to get friendly locale name
function getLocaleFriendlyName(localeCode) {
    const localeNames = {
        'en-US': 'English (United States)',
        'en-GB': 'English (United Kingdom)',
        'en-AU': 'English (Australia)',
        'en-CA': 'English (Canada)',
        'en-IN': 'English (India)',
        'es-ES': 'Spanish (Spain)',
        'es-MX': 'Spanish (Mexico)',
        'fr-FR': 'French (France)',
        'fr-CA': 'French (Canada)',
        'de-DE': 'German (Germany)',
        'it-IT': 'Italian (Italy)',
        'pt-BR': 'Portuguese (Brazil)',
        'pt-PT': 'Portuguese (Portugal)',
        'ja-JP': 'Japanese (Japan)',
        'ko-KR': 'Korean (Korea)',
        'zh-CN': 'Chinese (Simplified, China)',
        'zh-HK': 'Chinese (Traditional, Hong Kong)',
        'zh-TW': 'Chinese (Traditional, Taiwan)',
        'nl-NL': 'Dutch (Netherlands)',
        'ru-RU': 'Russian (Russia)',
        'ar-SA': 'Arabic (Saudi Arabia)',
        'hi-IN': 'Hindi (India)',
        'sv-SE': 'Swedish (Sweden)',
        'da-DK': 'Danish (Denmark)',
        'fi-FI': 'Finnish (Finland)',
        'no-NO': 'Norwegian (Norway)',
        'pl-PL': 'Polish (Poland)',
        'tr-TR': 'Turkish (Turkey)',
        'th-TH': 'Thai (Thailand)',
        'id-ID': 'Indonesian (Indonesia)'
    };
    
    return localeNames[localeCode] || localeCode;
}

export function displayResults(data) {
    AppState.transcriptionData = data;
    const segmentsContainer = document.getElementById('segments');
    const audioPlayerContainer = document.getElementById('audioPlayerContainer');
    const manageSpeakersBtn = document.getElementById('manageSpeakersBtn');
    const downloadAuditBtn = document.getElementById('downloadAuditBtn');
    const downloadCombinedBtn = document.getElementById('downloadCombinedBtn');
    const interactiveSegmentsHelper = document.getElementById('interactiveSegmentsHelper');
    const statsGrid = document.getElementById('statsGrid');
    
    if (!data || !data.segments || data.segments.length === 0) {
        if (segmentsContainer) {
            segmentsContainer.innerHTML = '<div class="empty-state">No transcription data available.</div>';
        }
        if (audioPlayerContainer) audioPlayerContainer.style.display = 'none';
        if (manageSpeakersBtn) manageSpeakersBtn.style.display = 'none';
        if (downloadAuditBtn) downloadAuditBtn.style.display = 'none';
        if (downloadCombinedBtn) downloadCombinedBtn.style.display = 'none';
        if (interactiveSegmentsHelper) interactiveSegmentsHelper.style.display = 'none';
        if (statsGrid) statsGrid.innerHTML = '';
        return;
    }
    
    // Setup audio player if audio file exists
    // Backend returns 'audioFileUrl' field (e.g., "/static/uploads/uuid.wav")
    if (data.audioFileUrl) {
        const audioUrl = data.audioFileUrl;
        console.log('?? Loading audio from:', audioUrl);
        AppState.audioPlayer.src = audioUrl;
        if (audioPlayerContainer) audioPlayerContainer.style.display = 'block';
        if (interactiveSegmentsHelper) interactiveSegmentsHelper.style.display = 'block';
        
        // CRITICAL: Setup audio player event listeners after loading audio
        console.log('?? Setting up audio player events...');
        setupAudioPlayerEvents();
    } else {
        console.log('?? No audio file URL provided (batch transcription mode)');
        if (audioPlayerContainer) audioPlayerContainer.style.display = 'none';
        if (interactiveSegmentsHelper) interactiveSegmentsHelper.style.display = 'none';
    }
    
    // Show speaker management and audit log buttons
    if (manageSpeakersBtn) manageSpeakersBtn.style.display = 'inline-block';
    if (downloadAuditBtn) downloadAuditBtn.style.display = 'inline-block';
    if (downloadCombinedBtn) downloadCombinedBtn.style.display = 'inline-block';
    
    // Populate stats grid
    populateStatsGrid(data, statsGrid);
    
    // Render segments
    renderSegments(data.segments);
    
    // Update audit log
    updateAuditLog(data);
    
    // Show results section
    showResults();
}

function populateStatsGrid(data, statsGrid) {
    if (!statsGrid) return;
    
    const segments = data.segments || [];
    const speakers = new Set(segments.map(s => s.speaker).filter(s => s));
    const totalDuration = segments.length > 0 ? Math.max(...segments.map(s => s.endTimeInSeconds || 0)) : 0;
    
    // Get friendly language name
    const localeCode = data.locale || 'en-US';
    const languageName = getLocaleFriendlyName(localeCode);
    
    const stats = [
        { label: 'Total Segments', value: segments.length, icon: '\uD83D\uDCCA' },  // ??
        { label: 'Speakers Identified', value: speakers.size, icon: '\uD83D\uDC65' },  // ??
        { label: 'Total Duration', value: formatTime(totalDuration), icon: '\u23F1\uFE0F' },  // ??
        { label: 'Language', value: languageName, icon: '\uD83C\uDF10' }  // ??
    ];
    
    statsGrid.innerHTML = stats.map(stat => `
        <div class="stat-card">
            <div class="stat-icon">${stat.icon}</div>
            <div class="stat-value">${stat.value}</div>
            <div class="stat-label">${stat.label}</div>
        </div>
    `).join('');
}

export function renderSegments(segments) {
    const segmentsContainer = document.getElementById('segments');
    
    if (!segmentsContainer) {
        console.error('Segments container not found');
        return;
    }
    
    if (!segments || segments.length === 0) {
        segmentsContainer.innerHTML = '<div class="empty-state">No segments to display.</div>';
        return;
    }
    
    // Generate segment HTML with speaker dropdown
    const allSpeakers = AppState.transcriptionData.availableSpeakers || 
        Array.from(new Set(segments.map(s => s.speaker || 'Unknown'))).sort();
    
    console.log('?? Available speakers for dropdowns:', allSpeakers);

    // ? Build a Set of segment indices that were INDIVIDUALLY edited (not bulk operations)
    const individuallyEditedSegments = new Set();
    const auditLog = AppState.transcriptionData.auditLog || AppState.transcriptionData.editHistory || [];
    
    auditLog.forEach(entry => {
        // Individual edit actions that should show edited badge
        const individualActions = ['segment_edit', 'speaker_change', 'edit_with_speaker_change', 'edit'];
        if (individualActions.includes(entry.action) && entry.segmentIndex !== undefined) {
            individuallyEditedSegments.add(entry.segmentIndex);
        }
    });
    
    console.log(`?? Found ${individuallyEditedSegments.size} individually edited segments`);

    const segmentsHTML = segments.map((segment, index) => {
        const { speaker, text, startTimeInSeconds, endTimeInSeconds, originalText, originalSpeaker } = segment;
        const startTime = parseFloat(startTimeInSeconds || 0);
        const endTime = parseFloat(endTimeInSeconds || 0);
        const lineNumber = segment.lineNumber || (index + 1);  // Use lineNumber from segment, or fall back to index + 1
        
        // ? IMPORTANT: Only show edited badge if segment was INDIVIDUALLY edited
        const wasIndividuallyEdited = individuallyEditedSegments.has(index);
        const editedClass = wasIndividuallyEdited ? ' edited' : '';

        // Generate speaker options for dropdown
        const speakerOptions = allSpeakers.map(spk => {
            const selected = spk === speaker ? 'selected' : '';
            return `<option value="${escapeHtml(spk)}" ${selected}>${escapeHtml(spk)}</option>`;
        }).join('');

        return `
            <div class="segment${editedClass}" data-index="${index}" data-start="${startTime}">
                <div class="segment-line">
                    <span class="segment-number">#${lineNumber}</span>
                    <select class="segment-speaker-dropdown" data-index="${index}" disabled>
                        ${speakerOptions}
                    </select>
                    <div class="segment-text" data-index="${index}">
                        ${escapeHtml(text)}
                    </div>
                    <span class="segment-time" data-start="${startTime}">
                        ${formatTime(startTime)} - ${formatTime(endTime)}
                    </span>
                </div>
                <div class="segment-edit-actions" id="edit-actions-${index}">
                    <button class="save-btn" onclick="event.stopPropagation(); saveSegmentEdit(${index})">\uD83D\uDCBE Save</button>
                    <button class="cancel-btn" onclick="event.stopPropagation(); cancelSegmentEdit(${index})">\u274C Cancel</button>
                </div>
            </div>
        `;
    }).join('');

    segmentsContainer.innerHTML = segmentsHTML;

    // IMPORTANT: Remove any existing event listeners before adding new one
    const oldContainer = segmentsContainer;
    const newContainer = oldContainer.cloneNode(true);
    oldContainer.parentNode.replaceChild(newContainer, oldContainer);
    
    // Now add event delegation to the clean container
    const freshContainer = document.getElementById('segments');
    freshContainer.addEventListener('click', handleSegmentClick);

    console.log(`? Rendered ${segments.length} segments with event delegation`);
}

function handleSegmentClick(e) {
    // DEBUG: Log every click to trace issues
    console.log('?? handleSegmentClick fired:', {
        target: e.target.tagName,
        classList: Array.from(e.target.classList),
        index: e.target.dataset?.index,
        isEditMode: AppState.isEditMode
    });
    
    // Ignore clicks on buttons
    if (e.target.tagName === 'BUTTON') {
        console.log('  ?? Ignoring button click');
        return;
    }
    
    // CRITICAL: Ignore clicks on textarea (segment-text-editable)
    if (e.target.tagName === 'TEXTAREA' || e.target.classList.contains('segment-text-editable')) {
        console.log('  ?? Ignoring textarea click (editing in progress)');
        e.stopPropagation();
        return;
    }
    
    // Get the segment container
    const segment = e.target.closest('.segment');
    if (!segment) {
        console.log('  ? Not inside a segment');
        return;
    }
    
    // If segment is already being edited, ignore all clicks except on textarea/buttons
    if (segment.classList.contains('editing')) {
        console.log('  ? Ignoring click on segment being edited');
        e.stopPropagation();
        e.preventDefault();
        return;
    }
    
    const index = parseInt(segment.dataset.index);
    
    // ? EDIT MODE: Clicking ANYWHERE on segment (including dropdown) starts editing
    if (AppState.isEditMode) {
        console.log(`  ?? Edit mode active - Starting edit for segment ${index}`);
        e.stopPropagation();
        e.preventDefault();
        window.startEditingSegment(index);
        return;
    }
    
    // ? NON-EDIT MODE: Different behaviors based on what was clicked
    
    // Handle clicks on dropdown - do nothing (dropdown is disabled anyway)
    if (e.target.tagName === 'SELECT' || e.target.classList.contains('segment-speaker-dropdown')) {
        console.log('  ?? Dropdown click ignored - edit mode not enabled');
        return;
    }
    
    // Handle timestamp clicks - always play audio
    if (e.target.classList.contains('segment-time')) {
        const startTime = parseFloat(e.target.dataset.start);
        console.log(`  ? Timestamp click: Playing audio from ${startTime}s`);
        if (!isNaN(startTime)) {
            playSegment(startTime);
        }
        return;
    }
    
    // Handle clicks on text or segment container - play audio
    const startTime = parseFloat(segment.dataset.start);
    console.log(`  ? Playing audio from ${startTime}s`);
    if (!isNaN(startTime)) {
        playSegment(startTime);
    }
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

export function updateAuditLog(data) {
    const auditLogEntries = document.getElementById('auditLogEntries');
    const auditLogSection = document.getElementById('auditLogSection');
    
    if (!auditLogEntries) return;
    
    // Check if there are any edits in the history
    const editHistory = data.editHistory || [];
    
    if (editHistory.length === 0) {
        // No edits yet - show initial transcription info
        const log = [];
        log.push(`Transcription completed at ${new Date(data.transcriptionDate || Date.now()).toLocaleString()}`);
        
        if (data.audioFile) log.push(`Audio file: ${data.audioFile}`);
        if (data.locale) {
            const languageName = getLocaleFriendlyName(data.locale);
            log.push(`Language: ${languageName}`);
        }
        if (data.speakerDiarization !== undefined) {
            log.push(`Speaker diarization: ${data.speakerDiarization ? 'Enabled' : 'Disabled'}`);
        }
        
        if (data.segments && data.segments.length > 0) {
            log.push(`Total segments: ${data.segments.length}`);
            
            const speakers = new Set(data.segments.map(s => s.speaker).filter(s => s));
            if (speakers.size > 0) log.push(`Speakers identified: ${speakers.size}`);
            
            const totalDuration = Math.max(...data.segments.map(s => s.endTimeInSeconds || 0));
            log.push(`Total duration: ${formatTime(totalDuration)}`);
        }
        
        auditLogEntries.innerHTML = log.map(entry => 
            `<div class="audit-entry">\uD83D\uDCCB ${escapeHtml(entry)}</div>`
        ).join('');
        
        // Hide audit log section if no edits
        if (auditLogSection) {
            auditLogSection.style.display = 'none';
        }
        
        return;
    }
    
    // Show edit history (actual audit log)
    console.log(`?? Displaying ${editHistory.length} audit log entries`);
    
    // Sort by timestamp (newest first)
    const sortedHistory = [...editHistory].sort((a, b) => {
        const timeA = new Date(a.timestamp || 0);
        const timeB = new Date(b.timestamp || 0);
        return timeB - timeA;
    });
    
    const entriesHTML = sortedHistory.map((entry, index) => {
        const editNumber = sortedHistory.length - index;
        const timestamp = new Date(entry.timestamp).toLocaleString();
        
        let changeHTML = '';
        let headerText = '';
        let metaHTML = '';
        
        // Handle bulk operations from Speaker Manager
        if (entry.action === 'bulk_speaker_rename') {
            headerText = `\uD83D\uDDC2\uFE0F Bulk Speaker Rename`;
            metaHTML = `
                <div class="audit-meta">
                    #\uFE0F\u20E3 ${entry.segmentCount || 0} segment(s) affected
                </div>
            `;
            changeHTML = `
                <div class="audit-change speaker-change">
                    <div><strong>Operation:</strong> ${escapeHtml(entry.description || 'Bulk speaker rename')}</div>
                    <div style="margin-top: 8px;">
                        <strong>Speaker Renamed:</strong>
                        <span class="old-value">${escapeHtml(entry.oldSpeaker || 'Unknown')}</span>
                        \u2192
                        <span class="new-value">${escapeHtml(entry.newSpeaker || 'Unknown')}</span>
                    </div>
                </div>
            `;
        } else if (entry.action === 'bulk_speaker_reassignment') {
            headerText = `\uD83D\uDDC2\uFE0F Bulk Speaker Reassignment`;
            metaHTML = `
                <div class="audit-meta">
                    #\uFE0F\u20E3 ${entry.segmentCount || 0} segment(s) affected
                </div>
            `;
            changeHTML = `
                <div class="audit-change speaker-change">
                    <div><strong>Operation:</strong> ${escapeHtml(entry.description || 'Bulk speaker reassignment')}</div>
                    <div style="margin-top: 8px;">
                        <strong>Segments Reassigned:</strong>
                        <span class="old-value">${escapeHtml(entry.oldSpeaker || 'Unknown')}</span>
                        \u2192
                        <span class="new-value">${escapeHtml(entry.newSpeaker || 'Unknown')}</span>
                    </div>
                </div>
            `;
        } else if (entry.action === 'bulk_speaker_delete') {
            headerText = `\uD83D\uDDC2\uFE0F Bulk Speaker Delete`;
            metaHTML = `
                <div class="audit-meta">
                    #\uFE0F\u20E3 ${entry.segmentCount || 0} segment(s) affected
                </div>
            `;
            changeHTML = `
                <div class="audit-change speaker-change">
                    <div><strong>Operation:</strong> ${escapeHtml(entry.description || 'Bulk speaker delete')}</div>
                    <div style="margin-top: 8px;">
                        <strong>Speaker Deleted:</strong>
                        <span class="old-value">${escapeHtml(entry.oldSpeaker || 'Unknown')}</span>
                        \u2192
                        <span class="new-value">${escapeHtml(entry.newSpeaker || 'Unknown')}</span>
                    </div>
                </div>
            `;
        } else if (entry.action === 'segment_edit' || entry.action === 'edit_with_speaker_change') {
            // Individual segment edit (with or without speaker change)
            headerText = `\u270F\uFE0F Segment Edit`;
            metaHTML = `
                <div class="audit-meta">
                    #\uFE0F\u20E3 Line ${entry.lineNumber || 'Unknown'}
                </div>
            `;
            
            changeHTML = '';
            
            // Show text change if present AND actually changed
            if (entry.oldText !== undefined && entry.newText !== undefined && entry.oldText !== entry.newText) {
                changeHTML += `
                    <div class="audit-change text-change">
                        <div><strong>Text Changed:</strong></div>
                        <div class="old-value">${escapeHtml(entry.oldText)}</div>
                        <div class="arrow">?</div>
                        <div class="new-value">${escapeHtml(entry.newText)}</div>
                    </div>
                `;
            }
            
            // Show speaker change if present AND actually changed
            if (entry.oldSpeaker && entry.newSpeaker && entry.oldSpeaker !== entry.newSpeaker) {
                changeHTML += `
                    <div class="audit-change speaker-change" style="margin-top: 10px;">
                        <strong>Speaker Change:</strong> 
                        <span class="old-value">${escapeHtml(entry.oldSpeaker)}</span> 
                        ? 
                        <span class="new-value">${escapeHtml(entry.newSpeaker)}</span>
                    </div>
                `;
            }
        } else if (entry.action === 'edit') {
            // Text-only edit (no speaker change)
            headerText = `\u270F\uFE0F Text Edit`;
            metaHTML = `
                <div class="audit-meta">
                    #\uFE0F\u20E3 Line ${entry.lineNumber || 'Unknown'}
                </div>
            `;
            
            // Show text change
            if (entry.oldText !== undefined && entry.newText !== undefined) {
                changeHTML = `
                    <div class="audit-change text-change">
                        <div><strong>Text Changed:</strong></div>
                        <div class="old-value">${escapeHtml(entry.oldText)}</div>
                        <div class="arrow">\u2193</div>
                        <div class="new-value">${escapeHtml(entry.newText)}</div>
                    </div>
                `;
            } else {
                // Fallback if text fields missing
                changeHTML = `<div class="audit-change">Text edited</div>`;
            }
        } else if (entry.action === 'speaker_change') {
            // Speaker change only
            headerText = `\uD83D\uDD04 Speaker Change`;
            metaHTML = `
                <div class="audit-meta">
                    #\uFE0F\u20E3 Line ${entry.lineNumber || 'Unknown'}
                </div>
            `;
            changeHTML = `
                <div class="audit-change speaker-change">
                    <strong>Speaker Change:</strong> 
                    <span class="old-value">${escapeHtml(entry.oldSpeaker || 'Unknown')}</span> 
                    \u2192 
                    <span class="new-value">${escapeHtml(entry.newSpeaker || 'Unknown')}</span>
                </div>
            `;
        } else {
            // Unknown action type - generic display
            headerText = `\uD83D\uDD0D ${escapeHtml(entry.action || 'Edit')}`;
            metaHTML = entry.lineNumber ? `
                <div class="audit-meta">
                    #\uFE0F\u20E3 Line ${entry.lineNumber}
                </div>
            ` : '';
            changeHTML = `<div class="audit-change">${escapeHtml(entry.description || 'Unknown change')}</div>`;
        }
        
        // Build the complete entry HTML
        return `
            <div class="audit-entry audit-entry-${editNumber}">
                <div class="audit-entry-header">
                    <span class="audit-entry-number">#${editNumber}</span>
                    <span class="audit-entry-title">${headerText}</span>
                    <span class="audit-entry-timestamp">\uD83D\uDD52 ${timestamp}</span>
                </div>
                ${metaHTML}
                ${changeHTML}
            </div>
        `;
    }).join('');
    
    auditLogEntries.innerHTML = entriesHTML;
    
    // Show audit log section since we have edits
    if (auditLogSection) {
        auditLogSection.style.display = 'block';
    }
}
