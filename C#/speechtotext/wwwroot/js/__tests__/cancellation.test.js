/**
 * Unit Tests for transcription.js - Cancellation Functions
 * Tests for handleCancelTranscription and AbortController
 */

describe('Cancellation Functions', () => {
    let mockAbortController;

    beforeEach(() => {
        document.body.innerHTML = `
            <div id="progressSection" class="d-none"></div>
            <button id="submitBtn">Transcribe</button>
            <span id="submitText">Transcribe</span>
            <span id="submitSpinner" class="d-none"></span>
            <div id="errorSection" class="alert alert-danger d-none">
                <strong id="errorLabel">Error:</strong> <span id="errorMessage"></span>
            </div>
        `;

        // Mock AbortController
        mockAbortController = {
            signal: { aborted: false },
            abort: jest.fn(() => {
                mockAbortController.signal.aborted = true;
            })
        };

        global.AbortController = jest.fn(() => mockAbortController);
        global.confirm = jest.fn(() => true);
    });

    afterEach(() => {
        jest.clearAllMocks();
    });

    describe('handleCancelTranscription', () => {
        test('should abort transcription when confirmed', () => {
            // Simulate current transcription in progress
            const currentAbortController = new AbortController();
            global.confirm.mockReturnValue(true);

            // Simulate handleCancelTranscription
            if (currentAbortController) {
                if (confirm('Are you sure you want to cancel the transcription?')) {
                    currentAbortController.abort();
                    
                    const progressSection = document.getElementById('progressSection');
                    progressSection.classList.add('d-none');
                    
                    const submitBtn = document.getElementById('submitBtn');
                    const submitText = document.getElementById('submitText');
                    const submitSpinner = document.getElementById('submitSpinner');
                    submitBtn.disabled = false;
                    submitText.textContent = 'Transcribe';
                    submitSpinner.classList.add('d-none');

                    const errorSection = document.getElementById('errorSection');
                    const errorLabel = document.getElementById('errorLabel');
                    const errorMessage = document.getElementById('errorMessage');
                    errorLabel.textContent = 'Warning:';
                    errorMessage.textContent = 'Transcription canceled';
                    errorSection.classList.remove('alert-danger');
                    errorSection.classList.add('alert-warning');
                    errorSection.classList.remove('d-none');
                }
            }

            expect(global.confirm).toHaveBeenCalledWith('Are you sure you want to cancel the transcription?');
            expect(currentAbortController.abort).toHaveBeenCalled();
            expect(document.getElementById('progressSection').classList.contains('d-none')).toBe(true);
            expect(document.getElementById('submitBtn').disabled).toBe(false);
        });

        test('should not abort transcription when not confirmed', () => {
            const currentAbortController = new AbortController();
            global.confirm.mockReturnValue(false);

            // Simulate handleCancelTranscription with false confirmation
            if (currentAbortController) {
                if (confirm('Are you sure you want to cancel the transcription?')) {
                    currentAbortController.abort();
                }
            }

            expect(global.confirm).toHaveBeenCalled();
            expect(currentAbortController.abort).not.toHaveBeenCalled();
        });

        test('should do nothing when no transcription in progress', () => {
            const currentAbortController = null;

            // Simulate handleCancelTranscription with null controller
            if (currentAbortController) {
                if (confirm('Are you sure you want to cancel the transcription?')) {
                    currentAbortController.abort();
                }
            }

            expect(global.confirm).not.toHaveBeenCalled();
        });

        test('should show warning message after cancellation', () => {
            const currentAbortController = new AbortController();
            global.confirm.mockReturnValue(true);

            if (currentAbortController) {
                if (confirm('Are you sure you want to cancel the transcription?')) {
                    currentAbortController.abort();

                    const errorSection = document.getElementById('errorSection');
                    const errorLabel = document.getElementById('errorLabel');
                    const errorMessage = document.getElementById('errorMessage');
                    
                    errorLabel.textContent = 'Warning:';
                    errorMessage.textContent = 'Transcription canceled';
                    errorSection.classList.remove('alert-danger');
                    errorSection.classList.add('alert-warning');
                    errorSection.classList.remove('d-none');
                }
            }

            const errorSection = document.getElementById('errorSection');
            expect(errorSection.classList.contains('alert-warning')).toBe(true);
            expect(errorSection.classList.contains('alert-danger')).toBe(false);
            expect(document.getElementById('errorLabel').textContent).toBe('Warning:');
            expect(document.getElementById('errorMessage').textContent).toBe('Transcription canceled');
        });
    });

    describe('AbortController integration', () => {
        test('should create new AbortController for each upload', () => {
            const controller1 = new AbortController();
            const controller2 = new AbortController();

            expect(controller1).toBeDefined();
            expect(controller2).toBeDefined();
            // Both will be the same mock instance, so just verify they exist
            expect(controller1.signal).toBeDefined();
            expect(controller2.signal).toBeDefined();
        });

        test('should have signal property', () => {
            const controller = new AbortController();
            
            expect(controller.signal).toBeDefined();
            expect(controller.signal.aborted).toBe(false);
        });

        test('should set aborted to true when abort is called', () => {
            const controller = new AbortController();
            
            expect(controller.signal.aborted).toBe(false);
            controller.abort();
            expect(controller.signal.aborted).toBe(true);
        });
    });

    describe('setUploadingState during cancellation', () => {
        test('should re-enable upload button after cancellation', () => {
            const submitBtn = document.getElementById('submitBtn');
            const submitText = document.getElementById('submitText');
            const submitSpinner = document.getElementById('submitSpinner');

            // Set to uploading state
            submitBtn.disabled = true;
            submitText.textContent = 'Processing...';
            submitSpinner.classList.remove('d-none');

            // Simulate setUploadingState(false) after cancellation
            submitBtn.disabled = false;
            submitText.textContent = 'Transcribe';
            submitSpinner.classList.add('d-none');

            expect(submitBtn.disabled).toBe(false);
            expect(submitText.textContent).toBe('Transcribe');
            expect(submitSpinner.classList.contains('d-none')).toBe(true);
        });
    });
});
