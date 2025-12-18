// Export Manager Module
// Handles downloading and exporting transcription data
import { AppState } from './app.js';
import { formatTime } from './audio-player.js';

export function downloadOriginal() {
    if (!AppState.transcriptionData) {
        alert('No transcription data available to download');
        return;
    }
    
    const jsonStr = JSON.stringify(AppState.transcriptionData, null, 2);
    const blob = new Blob([jsonStr], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    
    const a = document.createElement('a');
    a.href = url;
    a.download = `transcription_${new Date().toISOString().slice(0, 10)}.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
    
    console.log('?? Downloaded original JSON');
}

export async function downloadWord() {
    if (!AppState.transcriptionData || !AppState.transcriptionData.segments) {
        alert('No transcription data available to download');
        return;
    }
    
    try {
        console.log('?? Requesting transcription Word document download...');
        console.log(`?? Sending ${AppState.transcriptionData.segments.length} segments to server...`);
        
        // Call backend endpoint to generate Word document
        const response = await fetch('/download-readable-text', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                segments: AppState.transcriptionData.segments
            })
        });
        
        if (!response.ok) {
            const errorText = await response.text();
            console.error('? Server error:', errorText);
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        
        // Get the blob (Word document)
        const blob = await response.blob();
        const url = URL.createObjectURL(blob);
        
        // Create download link
        const a = document.createElement('a');
        a.href = url;
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19).replace('T', '_');
        a.download = `transcription_${timestamp}.docx`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
        
        console.log('? Transcription downloaded successfully as Word document');
    } catch (error) {
        console.error('? Failed to download transcription:', error);
        alert('Failed to download transcription document. Please try again.');
    }
}

export function copyToClipboard() {
    if (!AppState.transcriptionData || !AppState.transcriptionData.segments) {
        alert('No transcription data available to copy');
        return;
    }
    
    let textContent = '';
    AppState.transcriptionData.segments.forEach((segment) => {
        const speaker = segment.speaker || 'Unknown';
        const startTime = formatTime(segment.startTimeInSeconds || 0);
        const text = segment.text || '';
        textContent += `[${startTime}] ${speaker}: ${text}\n\n`;
    });
    
    navigator.clipboard.writeText(textContent)
        .then(() => {
            console.log('?? Copied to clipboard');
            // Show temporary success message
            const btn = event.target;
            const originalText = btn.textContent;
            btn.textContent = '? Copied!';
            btn.style.background = '#4caf50';
            setTimeout(() => {
                btn.textContent = originalText;
                btn.style.background = '';
            }, 2000);
        })
        .catch(err => {
            console.error('Failed to copy:', err);
            alert('Failed to copy to clipboard. Please try again.');
        });
}

export async function downloadCombinedDocument() {
    if (!AppState.transcriptionData || !AppState.transcriptionData.segments) {
        alert('No transcription data available to download');
        return;
    }
    
    try {
        console.log('?? Requesting combined document download...');
        
        // Get audit log from editHistory field
        const auditLog = AppState.transcriptionData.editHistory || [];
        
        console.log(`?? Sending ${AppState.transcriptionData.segments.length} segments and ${auditLog.length} audit log entries to server...`);
        
        // Call backend endpoint to generate combined Word document
        const response = await fetch('/download-combined-document', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                segments: AppState.transcriptionData.segments,
                auditLog: auditLog
            })
        });
        
        if (!response.ok) {
            const errorText = await response.text();
            console.error('? Server error:', errorText);
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        
        // Get the blob (Word document)
        const blob = await response.blob();
        const url = URL.createObjectURL(blob);
        
        // Create download link
        const a = document.createElement('a');
        a.href = url;
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19).replace('T', '_');
        a.download = `transcription_with_history_${timestamp}.docx`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
        
        console.log('? Combined document downloaded successfully');
    } catch (error) {
        console.error('? Failed to download combined document:', error);
        alert('Failed to download combined document. Please try again.');
    }
}

export async function downloadAuditLog() {
    if (!AppState.transcriptionData) {
        alert('No transcription data available');
        return;
    }
    
    try {
        console.log('?? Requesting audit log download...');
        
        // Get audit log from editHistory field
        const auditLog = AppState.transcriptionData.editHistory || [];
        
        // Check if there are any audit entries
        if (auditLog.length === 0) {
            alert('No edits have been made yet. The audit log only contains edits and speaker changes made after transcription.');
            return;
        }
        
        console.log(`?? Sending ${auditLog.length} audit log entries to server...`);
        
        // Call backend endpoint to generate Word document
        const response = await fetch('/download-audit-log', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                auditLog: auditLog
            })
        });
        
        if (!response.ok) {
            const errorText = await response.text();
            console.error('? Server error:', errorText);
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        
        // Get the blob (Word document)
        const blob = await response.blob();
        const url = URL.createObjectURL(blob);
        
        // Create download link
        const a = document.createElement('a');
        a.href = url;
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19).replace('T', '_');
        a.download = `transcription_audit_log_${timestamp}.docx`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
        
        console.log('? Audit log downloaded successfully as Word document');
    } catch (error) {
        console.error('? Failed to download audit log:', error);
        alert('Failed to download audit log. Please try again.');
    }
}

export function toggleAuditLog() {
    const auditLogSection = document.getElementById('auditLogSection');
    const auditLogEntries = document.getElementById('auditLogEntries');
    const btn = event.target;
    
    if (auditLogEntries.style.display === 'none') {
        auditLogEntries.style.display = 'block';
        btn.textContent = 'Hide Log';
    } else {
        auditLogEntries.style.display = 'none';
        btn.textContent = 'Show Log';
    }
}

// Expose functions to window for HTML onclick handlers
window.downloadOriginal = downloadOriginal;
window.downloadWord = downloadWord;
window.copyToClipboard = copyToClipboard;
window.downloadCombinedDocument = downloadCombinedDocument;
window.downloadAuditLog = downloadAuditLog;
window.toggleAuditLog = toggleAuditLog;
