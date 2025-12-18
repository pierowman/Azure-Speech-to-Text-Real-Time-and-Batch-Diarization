# -*- coding: utf-8 -*-
"""
Create transcription-display and edit-manager modules
"""
import os

# Ensure directory exists
os.makedirs("static/js", exist_ok=True)

# Module 1: transcription-display.js
transcription_display_js = """// Transcription Display Module
import { AppState } from './app.js';
import { formatTime, playSegment } from './audio-player.js';
import { showResults } from './ui-helpers.js';

export function displayResults(data) {
    AppState.transcriptionData = data;
    console.log('Display results:', data);
    
    const resultContent = document.getElementById('resultContent');
    const audioSection = document.getElementById('audioSection');
    const exportSection = document.getElementById('exportSection');
    
    if (!data || !data.segments || data.segments.length === 0) {
        resultContent.innerHTML = '<div class="empty-state">No transcription data available.</div>';
        audioSection.style.display = 'none';
        exportSection.style.display = 'none';
        return;
    }
    
    if (data.audioFile) {
        const audioUrl = `/temp_audio/${data.audioFile}`;
        console.log('Loading audio from:', audioUrl);
        AppState.audioPlayer.src = audioUrl;
        audioSection.style.display = 'block';
    } else {
        audioSection.style.display = 'none';
    }
    
    exportSection.style.display = 'block';
    renderSegments(data.segments);
    updateAuditLog(data);
    showResults();
    
    console.log('Results displayed successfully');
}

export function renderSegments(segments) {
    const resultContent = document.getElementById('resultContent');
    
    if (!segments || segments.length === 0) {
        resultContent.innerHTML = '<div class="empty-state">No segments to display.</div>';
        return;
    }
    
    const segmentsHTML = segments.map((segment, index) => {
        const startTime = segment.startTimeInSeconds || 0;
        const endTime = segment.endTimeInSeconds || 0;
        const speaker = segment.speaker || 'Unknown';
        const text = segment.text || '';
        
        return `
            <div class="segment" data-index="${index}" data-start="${startTime}">
                <div class="segment-header">
                    <span class="segment-speaker">${escapeHtml(speaker)}</span>
                    <span class="segment-time">${formatTime(startTime)} - ${formatTime(endTime)}</span>
                    <button class="play-segment-btn" onclick="playSegment(${startTime})" title="Play from here">
                        Play
                    </button>
                </div>
                <div class="segment-text" data-index="${index}">
                    ${escapeHtml(text)}
                </div>
            </div>
        `;
    }).join('');
    
    resultContent.innerHTML = segmentsHTML;
    console.log(`Rendered ${segments.length} segments`);
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

export function updateAuditLog(data) {
    const auditLog = document.getElementById('auditLog');
    if (!auditLog) return;
    
    const log = [];
    log.push(`Transcription completed at ${new Date(data.transcriptionDate || Date.now()).toLocaleString()}`);
    
    if (data.audioFile) {
        log.push(`Audio file: ${data.audioFile}`);
    }
    
    if (data.locale) {
        log.push(`Language: ${data.locale}`);
    }
    
    if (data.speakerDiarization !== undefined) {
        log.push(`Speaker diarization: ${data.speakerDiarization ? 'Enabled' : 'Disabled'}`);
    }
    
    if (data.segments && data.segments.length > 0) {
        log.push(`Total segments: ${data.segments.length}`);
        
        const speakers = new Set(data.segments.map(s => s.speaker).filter(s => s));
        if (speakers.size > 0) {
            log.push(`Speakers identified: ${speakers.size}`);
        }
        
        const totalDuration = Math.max(...data.segments.map(s => s.endTimeInSeconds || 0));
        log.push(`Total duration: ${formatTime(totalDuration)}`);
    }
    
    auditLog.innerHTML = log.map(entry => `<div class="audit-entry">• ${escapeHtml(entry)}</div>`).join('');
}
"""

with open("static/js/transcription-display.js", "w", encoding="utf-8") as f:
    f.write(transcription_display_js)

print("Created transcription-display.js")

# Module 2: edit-manager.js
edit_manager_js = """// Edit Manager Module
import { AppState } from './app.js';

export function toggleEditMode() {
    AppState.isEditMode = !AppState.isEditMode;
    const btn = document.getElementById('editModeBtn');
    
    if (AppState.isEditMode) {
        btn.classList.add('active');
        btn.textContent = 'Editing Mode (Click segments to edit)';
        enableEditMode();
    } else {
        btn.classList.remove('active');
        btn.textContent = 'Enable Edit Mode';
        disableEditMode();
    }
    
    console.log('Edit mode:', AppState.isEditMode ? 'ON' : 'OFF');
}

function enableEditMode() {
    const segments = document.querySelectorAll('.segment-text');
    segments.forEach((segment, index) => {
        segment.style.cursor = 'pointer';
        segment.onclick = () => startEditingSegment(index);
    });
}

function disableEditMode() {
    if (AppState.editingSegmentIndex !== null) {
        cancelSegmentEdit();
    }
    const segments = document.querySelectorAll('.segment-text');
    segments.forEach(segment => {
        segment.style.cursor = 'default';
        segment.onclick = null;
    });
}

export function startEditingSegment(index) {
    if (!AppState.isEditMode) return;
    if (AppState.editingSegmentIndex !== null) {
        cancelSegmentEdit();
    }
    
    const segments = document.querySelectorAll('.segment-text');
    const segmentElement = segments[index];
    const segment = AppState.transcriptionData.segments[index];
    
    const originalText = segment.text;
    AppState.editingSegmentIndex = index;
    
    segmentElement.innerHTML = `
        <textarea class="segment-edit-textarea">${escapeHtml(originalText)}</textarea>
        <div class="segment-edit-actions">
            <button class="btn-save" onclick="saveSegmentEdit(${index})">Save</button>
            <button class="btn-cancel" onclick="cancelSegmentEdit()">Cancel</button>
        </div>
    `;
    
    const textarea = segmentElement.querySelector('textarea');
    textarea.focus();
    textarea.select();
    
    console.log(`Editing segment ${index}`);
}

export function saveSegmentEdit(index) {
    const segments = document.querySelectorAll('.segment-text');
    const segmentElement = segments[index];
    const textarea = segmentElement.querySelector('textarea');
    
    if (!textarea) return;
    
    const newText = textarea.value.trim();
    
    if (!newText) {
        alert('Text cannot be empty');
        return;
    }
    
    AppState.transcriptionData.segments[index].text = newText;
    segmentElement.textContent = newText;
    AppState.editingSegmentIndex = null;
    
    updateSegmentOnServer(index, newText);
    
    console.log(`Saved segment ${index}`);
}

export function cancelSegmentEdit() {
    if (AppState.editingSegmentIndex === null) return;
    
    const segments = document.querySelectorAll('.segment-text');
    const segmentElement = segments[AppState.editingSegmentIndex];
    const segment = AppState.transcriptionData.segments[AppState.editingSegmentIndex];
    
    segmentElement.textContent = segment.text;
    AppState.editingSegmentIndex = null;
    
    console.log('Cancelled edit');
}

async function updateSegmentOnServer(index, newText) {
    try {
        const response = await fetch('/update_segment', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ index, text: newText })
        });
        
        if (!response.ok) {
            throw new Error('Failed to update segment on server');
        }
        
        console.log('Segment updated on server');
    } catch (error) {
        console.error('Error updating segment:', error);
        alert('Failed to save changes to server. Changes are saved locally only.');
    }
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

window.toggleEditMode = toggleEditMode;
window.startEditingSegment = startEditingSegment;
window.saveSegmentEdit = saveSegmentEdit;
window.cancelSegmentEdit = cancelSegmentEdit;
"""

with open("static/js/edit-manager.js", "w", encoding="utf-8") as f:
    f.write(edit_manager_js)

print("Created edit-manager.js")
print("\nCompleted!")
