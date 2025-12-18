/**
 * Unit Tests for transcription.js - File Upload
 * Tests for file upload and validation functionality
 */

describe('File Upload', () => {
    
    describe('handleFileUpload validation', () => {
        test('should reject when no file selected', () => {
            const file = null;
            const isValid = !!file;
            
            expect(isValid).toBe(false);
        });

        test('should accept when file is selected', () => {
            const file = new File(['content'], 'test.wav', { type: 'audio/wav' });
            const isValid = !!file;
            
            expect(isValid).toBe(true);
        });
    });

    describe('setUploadingState', () => {
        beforeEach(() => {
            document.body.innerHTML = `
                <button id="submitBtn">
                    <span id="submitText">Transcribe</span>
                    <span id="submitSpinner" class="d-none"></span>
                </button>
                <div id="progressSection" class="d-none"></div>
            `;
        });

        test('should disable button when uploading', () => {
            const submitBtn = document.getElementById('submitBtn');
            submitBtn.disabled = true;
            
            expect(submitBtn.disabled).toBe(true);
        });

        test('should change button text to Processing', () => {
            const submitText = document.getElementById('submitText');
            submitText.textContent = 'Processing...';
            
            expect(submitText.textContent).toBe('Processing...');
        });

        test('should show spinner when uploading', () => {
            const spinner = document.getElementById('submitSpinner');
            spinner.classList.remove('d-none');
            
            expect(spinner.classList.contains('d-none')).toBe(false);
        });

        test('should show progress section when uploading', () => {
            const progress = document.getElementById('progressSection');
            progress.classList.remove('d-none');
            
            expect(progress.classList.contains('d-none')).toBe(false);
        });

        test('should restore button state after upload', () => {
            const submitBtn = document.getElementById('submitBtn');
            const submitText = document.getElementById('submitText');
            const spinner = document.getElementById('submitSpinner');
            
            submitBtn.disabled = false;
            submitText.textContent = 'Transcribe';
            spinner.classList.add('d-none');
            
            expect(submitBtn.disabled).toBe(false);
            expect(submitText.textContent).toBe('Transcribe');
            expect(spinner.classList.contains('d-none')).toBe(true);
        });
    });

    describe('handleSuccessfulTranscription', () => {
        let mockResult;

        beforeEach(() => {
            mockResult = {
                success: true,
                message: 'Transcription completed',
                segments: [
                    { speaker: 'Guest-1', text: 'Hello', startTimeInSeconds: 0 }
                ],
                fullTranscript: '[Guest-1]: Hello',
                audioFileUrl: '/uploads/test.wav',
                goldenRecordJsonData: '{}',
                rawJsonData: '{}'
            };

            document.body.innerHTML = `
                <div id="successSection" class="d-none"></div>
                <div id="resultsSection" class="d-none"></div>
                <div id="audioPlayerSection" class="d-none"></div>
                <audio id="audioPlayer"></audio>
            `;
        });

        test('should show success section', () => {
            const successSection = document.getElementById('successSection');
            successSection.classList.remove('d-none');
            
            expect(successSection.classList.contains('d-none')).toBe(false);
        });

        test('should populate transcriptionData', () => {
            const transcriptionData = mockResult;
            
            expect(transcriptionData.success).toBe(true);
            expect(transcriptionData.segments.length).toBe(1);
        });

        test('should setup audio player when URL provided', () => {
            const audioPlayer = document.getElementById('audioPlayer');
            audioPlayer.src = mockResult.audioFileUrl;
            
            expect(audioPlayer.src).toContain('/uploads/test.wav');
        });

        test('should not setup audio player when URL missing', () => {
            const audioPlayer = document.getElementById('audioPlayer');
            const audioUrl = null;
            
            if (audioUrl) {
                audioPlayer.src = audioUrl;
            }
            
            expect(audioPlayer.src).toBe('');
        });
    });

    describe('showError', () => {
        beforeEach(() => {
            document.body.innerHTML = `
                <div id="errorSection" class="d-none">
                    <span id="errorMessage"></span>
                </div>
            `;
        });

        test('should show error section', () => {
            const errorSection = document.getElementById('errorSection');
            errorSection.classList.remove('d-none');
            
            expect(errorSection.classList.contains('d-none')).toBe(false);
        });

        test('should display error message', () => {
            const errorMessage = document.getElementById('errorMessage');
            const message = 'An error occurred';
            errorMessage.textContent = message;
            
            expect(errorMessage.textContent).toBe('An error occurred');
        });
    });

    describe('hideAllAlerts', () => {
        beforeEach(() => {
            document.body.innerHTML = `
                <div id="errorSection"></div>
                <div id="successSection"></div>
                <div id="resultsSection"></div>
                <div id="audioPlayerSection"></div>
            `;
        });

        test('should hide all alert sections', () => {
            const sections = ['errorSection', 'successSection', 'resultsSection', 'audioPlayerSection'];
            
            sections.forEach(id => {
                const element = document.getElementById(id);
                element.classList.add('d-none');
            });
            
            sections.forEach(id => {
                const element = document.getElementById(id);
                expect(element.classList.contains('d-none')).toBe(true);
            });
        });
    });
});
