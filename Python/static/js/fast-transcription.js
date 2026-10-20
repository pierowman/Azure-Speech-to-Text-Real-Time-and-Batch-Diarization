import { AppState, resetAppState } from './app.js';
import { displayResults } from './transcription-display.js';

export function setupFastForm() {
    const form = document.getElementById('fastForm');
    if (!form) return;
    const button = document.getElementById('fastTranscribeBtn');
    const model = document.getElementById('fastModel');
    const status = document.getElementById('fastStatus');
    const error = document.getElementById('fastError');
    model.addEventListener('change', () => {
        document.getElementById('fastStyleGroup').hidden = model.value !== 'MAI-Transcribe-2';
    });
    form.addEventListener('submit', async (event) => {
        event.preventDefault();
        error.hidden = true;
        status.hidden = true;
        const file = document.getElementById('fastAudioFile').files[0];
        if (!file || file.size === 0 || file.size > Number(form.dataset.maxBytes)) {
            error.textContent = 'Select a nonempty audio file within the displayed size limit.';
            error.hidden = false;
            return;
        }
        if (AppState.isTranscribing) {
            error.textContent = 'Wait for the current transcription to finish.';
            error.hidden = false;
            return;
        }
        AppState.isTranscribing = true;
        button.disabled = true;
        document.getElementById('transcribeBtn').disabled = true;
        form.setAttribute('aria-busy', 'true');
        button.textContent = 'Transcribing...';
        resetAppState();
        document.getElementById('fastResultsHost').appendChild(document.getElementById('resultsSection'));
        const formData = new FormData(form);
        const modelLabel = model.options[model.selectedIndex].text;
        formData.set('enableDiarization', String(document.getElementById('fastDiarization').checked));
        status.textContent = 'Uploading and transcribing. Keep this page open until processing finishes.';
        status.hidden = false;
        try {
            const response = await fetch('/fast-transcribe', { method: 'POST', body: formData });
            if (!response.headers.get('content-type')?.includes('application/json')) {
                throw new Error(`The server returned HTTP ${response.status} without JSON. The upload may have exceeded a proxy limit or the request may have timed out.`);
            }
            const data = await response.json();
            if (!response.ok || !data.success) {
                throw new Error(data.message || data.description || `Transcription failed (HTTP ${response.status}).`);
            }
            displayResults(data);
            status.textContent = `${modelLabel}: ${data.message}`;
        } catch (ex) {
            console.error('Fast Transcription failed:', ex);
            status.hidden = true;
            error.textContent = ex.message;
            error.hidden = false;
        } finally {
            AppState.isTranscribing = false;
            button.disabled = false;
            document.getElementById('transcribeBtn').disabled = false;
            form.setAttribute('aria-busy', 'false');
            button.textContent = 'Start Fast Transcription';
        }
    });
}
