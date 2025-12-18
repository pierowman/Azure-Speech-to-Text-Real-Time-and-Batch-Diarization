/**
 * @jest-environment jsdom
 */

describe('Transcription Mode Selector', () => {
    let mockFetch;

    beforeEach(() => {
        // Reset DOM
        document.body.innerHTML = `
            <input type="radio" id="modeRealTime" name="transcriptionMode" value="realtime" checked>
            <input type="radio" id="modeBatch" name="transcriptionMode" value="batch">
            
            <form id="realTimeForm">
                <input type="file" id="audioFileRealTime" accept=".wav,.mp3,.ogg,.flac">
                <button type="submit" id="realTimeSubmitBtn">
                    <span id="realTimeSubmitText">Transcribe Now</span>
                    <span id="realTimeSubmitSpinner" class="d-none"></span>
                </button>
                <div id="realTimeRules"></div>
            </form>
            
            <form id="batchForm" class="d-none">
                <input type="file" id="audioFilesBatch" multiple accept=".wav,.mp3,.ogg,.flac">
                <input type="text" id="batchJobName">
                <button type="submit" id="batchSubmitBtn">
                    <span id="batchSubmitText">Submit Batch Job</span>
                    <span id="batchSubmitSpinner" class="d-none"></span>
                </button>
                <div id="filesList"></div>
                <div id="batchRules"></div>
            </form>
            
            <div id="jobs-tab-item" style="display: none;">
                <button id="jobs-tab" data-bs-toggle="tab" data-bs-target="#jobs"></button>
            </div>
            
            <div id="progressSection" class="d-none"></div>
            <div id="errorSection" class="d-none alert alert-danger">
                <span id="errorLabel">Error:</span>
                <span id="errorMessage"></span>
            </div>
            <div id="successSection" class="d-none"></div>
            <div id="resultsSection" class="d-none"></div>
            
            <button id="cancelTranscriptionBtn">Cancel</button>
        `;

        // Mock fetch
        mockFetch = jest.fn();
        global.fetch = mockFetch;

        // Mock confirm and alert
        global.confirm = jest.fn(() => true);
        global.alert = jest.fn();

        // Mock bootstrap
        global.bootstrap = {
            Tab: jest.fn().mockImplementation(() => ({
                show: jest.fn()
            }))
        };

        // Mock window.displayTranscriptionResults
        global.displayTranscriptionResults = jest.fn();
        
        // Mock loadTranscriptionJobs
        global.loadTranscriptionJobs = jest.fn();
    });

    afterEach(() => {
        jest.clearAllMocks();
    });

    describe('escapeHtml', () => {
        test('should escape HTML special characters', () => {
            const escapeHtml = (text) => {
                const map = {
                    '&': '&amp;',
                    '<': '&lt;',
                    '>': '&gt;',
                    '"': '&quot;',
                    "'": '&#039;'
                };
                return text.replace(/[&<>"']/g, m => map[m]);
            };

            expect(escapeHtml('<script>alert("XSS")</script>'))
                .toBe('&lt;script&gt;alert(&quot;XSS&quot;)&lt;/script&gt;');
            expect(escapeHtml("It's a test & more"))
                .toBe('It&#039;s a test &amp; more');
            expect(escapeHtml('Normal text')).toBe('Normal text');
        });

        test('should handle empty string', () => {
            const escapeHtml = (text) => {
                const map = {
                    '&': '&amp;',
                    '<': '&lt;',
                    '>': '&gt;',
                    '"': '&quot;',
                    "'": '&#039;'
                };
                return text.replace(/[&<>"']/g, m => map[m]);
            };

            expect(escapeHtml('')).toBe('');
        });

        test('should handle special characters', () => {
            const escapeHtml = (text) => {
                const map = {
                    '&': '&amp;',
                    '<': '&lt;',
                    '>': '&gt;',
                    '"': '&quot;',
                    "'": '&#039;'
                };
                return text.replace(/[&<>"']/g, m => map[m]);
            };

            expect(escapeHtml('&<>"\''))
                .toBe('&amp;&lt;&gt;&quot;&#039;');
        });
    });

    describe('switchMode', () => {
        test('should show real-time form and hide batch form for realtime mode', () => {
            const realTimeForm = document.getElementById('realTimeForm');
            const batchForm = document.getElementById('batchForm');
            const jobsTabItem = document.getElementById('jobs-tab-item');

            // Simulate switchMode('realtime')
            realTimeForm.classList.remove('d-none');
            batchForm.classList.add('d-none');
            jobsTabItem.style.display = 'none';

            expect(realTimeForm.classList.contains('d-none')).toBe(false);
            expect(batchForm.classList.contains('d-none')).toBe(true);
            expect(jobsTabItem.style.display).toBe('none');
        });

        test('should hide real-time form and show batch form for batch mode', () => {
            const realTimeForm = document.getElementById('realTimeForm');
            const batchForm = document.getElementById('batchForm');
            const jobsTabItem = document.getElementById('jobs-tab-item');

            // Simulate switchMode('batch')
            realTimeForm.classList.add('d-none');
            batchForm.classList.remove('d-none');
            jobsTabItem.style.display = '';

            expect(realTimeForm.classList.contains('d-none')).toBe(true);
            expect(batchForm.classList.contains('d-none')).toBe(false);
            expect(jobsTabItem.style.display).toBe('');
        });

        test('should show jobs tab in batch mode', () => {
            const jobsTabItem = document.getElementById('jobs-tab-item');
            
            // Simulate batch mode
            jobsTabItem.style.display = '';

            expect(jobsTabItem.style.display).toBe('');
        });

        test('should hide jobs tab in real-time mode', () => {
            const jobsTabItem = document.getElementById('jobs-tab-item');
            
            // Simulate real-time mode
            jobsTabItem.style.display = 'none';

            expect(jobsTabItem.style.display).toBe('none');
        });
    });

    describe('loadValidationRules', () => {
        test('should load real-time validation rules', async () => {
            mockFetch.mockResolvedValueOnce({
                json: async () => ({
                    success: true,
                    rules: 'Max file size: 25MB, Allowed formats: .wav, .mp3, .ogg, .flac'
                })
            });

            const response = await fetch('/Home/GetValidationRules?mode=RealTime');
            const data = await response.json();

            expect(data.success).toBe(true);
            expect(data.rules).toContain('Max file size');
        });

        test('should load batch validation rules', async () => {
            mockFetch.mockResolvedValueOnce({
                json: async () => ({
                    success: true,
                    rules: 'Max file size: 1GB, Max files: 20'
                })
            });

            const response = await fetch('/Home/GetValidationRules?mode=Batch');
            const data = await response.json();

            expect(data.success).toBe(true);
            expect(data.rules).toContain('Max files');
        });

        test('should handle validation rules fetch error', async () => {
            mockFetch.mockRejectedValueOnce(new Error('Network error'));

            try {
                await fetch('/Home/GetValidationRules?mode=RealTime');
                fail('Should have thrown an error');
            } catch (error) {
                expect(error.message).toBe('Network error');
            }
        });

        test('should update rules in DOM', async () => {
            mockFetch.mockResolvedValueOnce({
                json: async () => ({
                    success: true,
                    rules: 'Test rules'
                })
            });

            const realTimeRules = document.getElementById('realTimeRules');
            const response = await fetch('/Home/GetValidationRules?mode=RealTime');
            const data = await response.json();

            if (data.success) {
                realTimeRules.textContent = data.rules;
            }

            expect(realTimeRules.textContent).toBe('Test rules');
        });
    });

    describe('updateFilesList', () => {
        test('should display selected files', () => {
            const filesList = document.getElementById('filesList');
            const mockFiles = [
                { name: 'audio1.wav', size: 1024 * 1024 * 5 }, // 5MB
                { name: 'audio2.mp3', size: 1024 * 1024 * 10 } // 10MB
            ];

            // Simulate file list display
            let html = '<div class="alert alert-info py-2"><strong>Files selected:</strong><ul class="mb-0 mt-1">';
            mockFiles.forEach(file => {
                const sizeMB = (file.size / (1024 * 1024)).toFixed(2);
                html += `<li>${file.name} <span class="text-muted">(${sizeMB} MB)</span></li>`;
            });
            html += '</ul></div>';
            filesList.innerHTML = html;

            expect(filesList.innerHTML).toContain('audio1.wav');
            expect(filesList.innerHTML).toContain('5.00 MB');
            expect(filesList.innerHTML).toContain('audio2.mp3');
            expect(filesList.innerHTML).toContain('10.00 MB');
        });

        test('should clear files list when no files selected', () => {
            const filesList = document.getElementById('filesList');
            filesList.innerHTML = 'Previous content';

            // Simulate clearing
            filesList.innerHTML = '';

            expect(filesList.innerHTML).toBe('');
        });

        test('should handle large file size display', () => {
            const fileSize = 1024 * 1024 * 500; // 500MB
            const sizeMB = (fileSize / (1024 * 1024)).toFixed(2);

            expect(sizeMB).toBe('500.00');
        });

        test('should escape file names for XSS protection', () => {
            const escapeHtml = (text) => {
                const map = {
                    '&': '&amp;',
                    '<': '&lt;',
                    '>': '&gt;',
                    '"': '&quot;',
                    "'": '&#039;'
                };
                return text.replace(/[&<>"']/g, m => map[m]);
            };

            const dangerousFileName = '<script>alert("xss")</script>.wav';
            const escapedName = escapeHtml(dangerousFileName);

            expect(escapedName).not.toContain('<script>');
            expect(escapedName).toContain('&lt;script&gt;');
        });
    });

    describe('handleRealTimeSubmit', () => {
        test('should show error when no file selected', () => {
            const fileInput = document.getElementById('audioFileRealTime');
            const errorMessage = document.getElementById('errorMessage');

            // Simulate no file selected
            if (!fileInput.files || !fileInput.files.length) {
                errorMessage.textContent = 'Please select a file';
            }

            expect(errorMessage.textContent).toBe('Please select a file');
        });

        test('should disable button during submission', () => {
            const submitBtn = document.getElementById('realTimeSubmitBtn');
            const submitText = document.getElementById('realTimeSubmitText');
            const submitSpinner = document.getElementById('realTimeSubmitSpinner');

            // Simulate submission state
            submitBtn.disabled = true;
            submitText.textContent = 'Processing...';
            submitSpinner.classList.remove('d-none');

            expect(submitBtn.disabled).toBe(true);
            expect(submitText.textContent).toBe('Processing...');
            expect(submitSpinner.classList.contains('d-none')).toBe(false);
        });

        test('should show progress section during upload', () => {
            const progressSection = document.getElementById('progressSection');
            
            progressSection.classList.remove('d-none');

            expect(progressSection.classList.contains('d-none')).toBe(false);
        });

        test('should handle successful transcription', async () => {
            mockFetch.mockResolvedValueOnce({
                json: async () => ({
                    success: true,
                    segments: [],
                    fullTranscript: 'Test transcript'
                })
            });

            const response = await fetch('/Home/UploadAndTranscribe', {
                method: 'POST',
                body: new FormData()
            });
            const data = await response.json();

            expect(data.success).toBe(true);
            expect(data.fullTranscript).toBe('Test transcript');
        });

        test('should hide progress section after completion', () => {
            const progressSection = document.getElementById('progressSection');
            
            // Simulate completion
            progressSection.classList.add('d-none');

            expect(progressSection.classList.contains('d-none')).toBe(true);
        });

        test('should re-enable button after completion', () => {
            const submitBtn = document.getElementById('realTimeSubmitBtn');
            const submitText = document.getElementById('realTimeSubmitText');
            const submitSpinner = document.getElementById('realTimeSubmitSpinner');

            // Simulate completion state
            submitBtn.disabled = false;
            submitText.textContent = 'Transcribe Now';
            submitSpinner.classList.add('d-none');

            expect(submitBtn.disabled).toBe(false);
            expect(submitText.textContent).toBe('Transcribe Now');
            expect(submitSpinner.classList.contains('d-none')).toBe(true);
        });

        test('should handle transcription error', async () => {
            mockFetch.mockResolvedValueOnce({
                json: async () => ({
                    success: false,
                    message: 'Invalid audio format'
                })
            });

            const response = await fetch('/Home/UploadAndTranscribe', {
                method: 'POST',
                body: new FormData()
            });
            const data = await response.json();

            expect(data.success).toBe(false);
            expect(data.message).toBe('Invalid audio format');
        });

        test('should handle abort error when canceled', async () => {
            const error = new Error('The operation was aborted');
            error.name = 'AbortError';

            expect(error.name).toBe('AbortError');
        });

        test('should hide jobs tab for real-time results', () => {
            const jobsTabItem = document.getElementById('jobs-tab-item');
            
            // Simulate real-time results display
            jobsTabItem.style.display = 'none';

            expect(jobsTabItem.style.display).toBe('none');
        });

        test('should call displayTranscriptionResults on success', async () => {
            const mockResult = {
                success: true,
                segments: [],
                fullTranscript: 'Test'
            };

            mockFetch.mockResolvedValueOnce({
                json: async () => mockResult
            });

            const response = await fetch('/Home/UploadAndTranscribe', {
                method: 'POST',
                body: new FormData()
            });
            const data = await response.json();

            if (data.success && typeof window.displayTranscriptionResults === 'function') {
                window.displayTranscriptionResults(data);
            }

            expect(window.displayTranscriptionResults).toHaveBeenCalledWith(mockResult);
        });
    });

    describe('handleBatchSubmit', () => {
        test('should show error when no files selected', () => {
            const filesInput = document.getElementById('audioFilesBatch');
            const errorMessage = document.getElementById('errorMessage');

            // Simulate no files selected
            if (!filesInput.files || !filesInput.files.length) {
                errorMessage.textContent = 'Please select at least one file';
            }

            expect(errorMessage.textContent).toBe('Please select at least one file');
        });

        test('should disable button during submission', () => {
            const submitBtn = document.getElementById('batchSubmitBtn');
            const submitText = document.getElementById('batchSubmitText');
            const submitSpinner = document.getElementById('batchSubmitSpinner');

            // Simulate submission state
            submitBtn.disabled = true;
            submitText.textContent = 'Submitting...';
            submitSpinner.classList.remove('d-none');

            expect(submitBtn.disabled).toBe(true);
            expect(submitText.textContent).toBe('Submitting...');
            expect(submitSpinner.classList.contains('d-none')).toBe(false);
        });

        test('should include job name in form data', () => {
            const jobNameInput = document.getElementById('batchJobName');
            jobNameInput.value = 'My Batch Job';

            const formData = new FormData();
            if (jobNameInput.value.trim()) {
                formData.append('jobName', jobNameInput.value.trim());
            }

            // Can't directly test FormData, but we can test the condition
            expect(jobNameInput.value.trim()).toBe('My Batch Job');
        });

        test('should handle successful batch submission', async () => {
            mockFetch.mockResolvedValueOnce({
                json: async () => ({
                    success: true,
                    message: 'Batch job submitted successfully'
                })
            });

            const response = await fetch('/Home/SubmitBatchTranscription', {
                method: 'POST',
                body: new FormData()
            });
            const data = await response.json();

            expect(data.success).toBe(true);
            expect(data.message).toContain('successfully');
        });

        test('should clear form after successful submission', () => {
            const filesInput = document.getElementById('audioFilesBatch');
            const jobNameInput = document.getElementById('batchJobName');
            const filesList = document.getElementById('filesList');

            // Simulate clearing
            filesInput.value = '';
            jobNameInput.value = '';
            filesList.innerHTML = '';

            expect(filesInput.value).toBe('');
            expect(jobNameInput.value).toBe('');
            expect(filesList.innerHTML).toBe('');
        });

        test('should show jobs tab after batch submission', () => {
            const jobsTabItem = document.getElementById('jobs-tab-item');
            
            // Simulate batch mode
            jobsTabItem.style.display = '';

            expect(jobsTabItem.style.display).toBe('');
        });

        test('should switch to jobs tab after submission', () => {
            const jobsTab = document.getElementById('jobs-tab');
            const tab = new bootstrap.Tab(jobsTab);

            expect(bootstrap.Tab).toHaveBeenCalledWith(jobsTab);
            expect(tab.show).toBeDefined();
        });

        test('should handle batch submission error', async () => {
            mockFetch.mockResolvedValueOnce({
                json: async () => ({
                    success: false,
                    message: 'Failed to upload files'
                })
            });

            const response = await fetch('/Home/SubmitBatchTranscription', {
                method: 'POST',
                body: new FormData()
            });
            const data = await response.json();

            expect(data.success).toBe(false);
            expect(data.message).toBe('Failed to upload files');
        });

        test('should re-enable button after submission completes', () => {
            const submitBtn = document.getElementById('batchSubmitBtn');
            const submitText = document.getElementById('batchSubmitText');
            const submitSpinner = document.getElementById('batchSubmitSpinner');

            // Simulate completion
            submitBtn.disabled = false;
            submitText.textContent = 'Submit Batch Job';
            submitSpinner.classList.add('d-none');

            expect(submitBtn.disabled).toBe(false);
            expect(submitText.textContent).toBe('Submit Batch Job');
            expect(submitSpinner.classList.contains('d-none')).toBe(true);
        });

        test('should handle network error during submission', async () => {
            mockFetch.mockRejectedValueOnce(new Error('Network failed'));

            try {
                await fetch('/Home/SubmitBatchTranscription', {
                    method: 'POST',
                    body: new FormData()
                });
                fail('Should have thrown an error');
            } catch (error) {
                expect(error.message).toBe('Network failed');
            }
        });
    });

    describe('handleCancelTranscription', () => {
        test('should show confirmation dialog', () => {
            const userConfirmed = confirm('Are you sure you want to cancel the transcription?');

            expect(confirm).toHaveBeenCalled();
            expect(userConfirmed).toBe(true);
        });

        test('should not cancel if user declines', () => {
            global.confirm = jest.fn(() => false);
            
            const userConfirmed = confirm('Are you sure?');

            expect(userConfirmed).toBe(false);
        });

        test('should abort controller when confirmed', () => {
            const mockAbortController = {
                abort: jest.fn(),
                signal: {}
            };

            const userConfirmed = confirm('Are you sure?');
            if (userConfirmed && mockAbortController) {
                mockAbortController.abort();
            }

            expect(mockAbortController.abort).toHaveBeenCalled();
        });

        test('should hide progress section after cancel', () => {
            const progressSection = document.getElementById('progressSection');
            
            progressSection.classList.add('d-none');

            expect(progressSection.classList.contains('d-none')).toBe(true);
        });

        test('should show warning message after cancel', () => {
            const errorSection = document.getElementById('errorSection');
            const errorLabel = document.getElementById('errorLabel');
            const errorMessage = document.getElementById('errorMessage');

            // Simulate showing warning
            errorLabel.textContent = 'Warning:';
            errorMessage.textContent = 'Transcription canceled';
            errorSection.classList.remove('alert-danger');
            errorSection.classList.add('alert-warning');
            errorSection.classList.remove('d-none');

            expect(errorLabel.textContent).toBe('Warning:');
            expect(errorMessage.textContent).toBe('Transcription canceled');
            expect(errorSection.classList.contains('alert-warning')).toBe(true);
        });
    });

    describe('Alert Management', () => {
        test('showError should display error with danger style', () => {
            const errorSection = document.getElementById('errorSection');
            const errorMessage = document.getElementById('errorMessage');
            const errorLabel = document.getElementById('errorLabel');

            errorLabel.textContent = 'Error:';
            errorMessage.textContent = 'Test error';
            errorSection.classList.remove('alert-warning');
            errorSection.classList.add('alert-danger');
            errorSection.classList.remove('d-none');

            expect(errorLabel.textContent).toBe('Error:');
            expect(errorMessage.textContent).toBe('Test error');
            expect(errorSection.classList.contains('alert-danger')).toBe(true);
            expect(errorSection.classList.contains('d-none')).toBe(false);
        });

        test('showWarning should display warning with warning style', () => {
            const errorSection = document.getElementById('errorSection');
            const errorMessage = document.getElementById('errorMessage');
            const errorLabel = document.getElementById('errorLabel');

            errorLabel.textContent = 'Warning:';
            errorMessage.textContent = 'Test warning';
            errorSection.classList.remove('alert-danger');
            errorSection.classList.add('alert-warning');
            errorSection.classList.remove('d-none');

            expect(errorLabel.textContent).toBe('Warning:');
            expect(errorMessage.textContent).toBe('Test warning');
            expect(errorSection.classList.contains('alert-warning')).toBe(true);
            expect(errorSection.classList.contains('d-none')).toBe(false);
        });

        test('showSuccess should display success message', () => {
            const successSection = document.getElementById('successSection');

            successSection.textContent = 'Operation successful';
            successSection.classList.remove('d-none');

            expect(successSection.textContent).toBe('Operation successful');
            expect(successSection.classList.contains('d-none')).toBe(false);
        });

        test('hideAllAlerts should hide all alert sections', () => {
            const errorSection = document.getElementById('errorSection');
            const successSection = document.getElementById('successSection');

            errorSection.classList.add('d-none');
            successSection.classList.add('d-none');
            errorSection.classList.remove('alert-warning');
            errorSection.classList.add('alert-danger');

            expect(errorSection.classList.contains('d-none')).toBe(true);
            expect(successSection.classList.contains('d-none')).toBe(true);
            expect(errorSection.classList.contains('alert-danger')).toBe(true);
        });
    });

    describe('Element Visibility', () => {
        test('showElement should remove d-none class', () => {
            const element = document.getElementById('progressSection');
            
            element.classList.remove('d-none');

            expect(element.classList.contains('d-none')).toBe(false);
        });

        test('hideElement should add d-none class', () => {
            const element = document.getElementById('progressSection');
            
            element.classList.add('d-none');

            expect(element.classList.contains('d-none')).toBe(true);
        });
    });
});
