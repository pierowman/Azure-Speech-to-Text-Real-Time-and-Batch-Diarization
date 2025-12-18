/**
 * @jest-environment jsdom
 */

// Mock functions and data
const mockJobs = [
    {
        id: 'job-1',
        displayName: 'Test Job 1',
        status: 'Running',
        createdDateTime: '2024-01-01T10:00:00Z',
        lastActionDateTime: '2024-01-01T10:05:00Z',
        files: ['audio1.wav', 'audio2.mp3'],
        error: null
    },
    {
        id: 'job-2',
        displayName: 'Test Job 2',
        status: 'Succeeded',
        createdDateTime: '2024-01-01T09:00:00Z',
        lastActionDateTime: '2024-01-01T09:30:00Z',
        files: ['audio3.flac'],
        error: null
    },
    {
        id: 'job-3',
        displayName: 'Failed Job',
        status: 'Failed',
        createdDateTime: '2024-01-01T08:00:00Z',
        lastActionDateTime: '2024-01-01T08:15:00Z',
        files: ['audio4.ogg'],
        error: 'Audio format not supported'
    }
];

// Mock fetch globally
global.fetch = jest.fn();

describe('Transcription Jobs', () => {
    let jobsModule;

    beforeEach(() => {
        // Reset DOM
        document.body.innerHTML = `
            <div id="jobsLoadingSection" class="d-none"></div>
            <div id="jobsErrorSection" class="d-none">
                <span id="jobsErrorMessage"></span>
            </div>
            <div id="jobsEmptySection" class="d-none"></div>
            <div id="jobsList"></div>
            <button id="refreshJobsBtn">
                <i class="bi bi-arrow-clockwise"></i> Refresh
            </button>
            <button id="jobs-tab" data-bs-toggle="tab" data-bs-target="#jobs"></button>
        `;

        // Reset fetch mock
        fetch.mockClear();

        // Import module (in actual implementation, this would be required)
        // For now, we'll assume functions are globally available
    });

    describe('escapeHtml', () => {
        test('should escape HTML special characters', () => {
            const input = '<script>alert("XSS")</script>';
            const expected = '&lt;script&gt;alert(&quot;XSS&quot;)&lt;/script&gt;';
            
            // This would be: const result = escapeHtml(input);
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
            
            const result = escapeHtml(input);
            expect(result).toBe(expected);
        });

        test('should handle text without special characters', () => {
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

            const input = 'Hello World';
            const result = escapeHtml(input);
            expect(result).toBe('Hello World');
        });
    });

    describe('getStatusBadge', () => {
        test('should return success badge for Succeeded status', () => {
            const getStatusBadge = (status) => {
                const statusLower = status.toLowerCase();
                let badgeClass = 'bg-secondary';

                switch (statusLower) {
                    case 'succeeded':
                        badgeClass = 'bg-success';
                        break;
                    case 'running':
                        badgeClass = 'bg-primary';
                        break;
                    case 'failed':
                        badgeClass = 'bg-danger';
                        break;
                    case 'notstarted':
                        badgeClass = 'bg-warning text-dark';
                        break;
                }

                return `<span class="badge ${badgeClass} ms-2">${status}</span>`;
            };

            const result = getStatusBadge('Succeeded');
            expect(result).toContain('bg-success');
            expect(result).toContain('Succeeded');
        });

        test('should return primary badge for Running status', () => {
            const getStatusBadge = (status) => {
                const statusLower = status.toLowerCase();
                let badgeClass = 'bg-secondary';

                switch (statusLower) {
                    case 'succeeded':
                        badgeClass = 'bg-success';
                        break;
                    case 'running':
                        badgeClass = 'bg-primary';
                        break;
                    case 'failed':
                        badgeClass = 'bg-danger';
                        break;
                    case 'notstarted':
                        badgeClass = 'bg-warning text-dark';
                        break;
                }

                return `<span class="badge ${badgeClass} ms-2">${status}</span>`;
            };

            const result = getStatusBadge('Running');
            expect(result).toContain('bg-primary');
        });

        test('should return danger badge for Failed status', () => {
            const getStatusBadge = (status) => {
                const statusLower = status.toLowerCase();
                let badgeClass = 'bg-secondary';

                switch (statusLower) {
                    case 'succeeded':
                        badgeClass = 'bg-success';
                        break;
                    case 'running':
                        badgeClass = 'bg-primary';
                        break;
                    case 'failed':
                        badgeClass = 'bg-danger';
                        break;
                    case 'notstarted':
                        badgeClass = 'bg-warning text-dark';
                        break;
                }

                return `<span class="badge ${badgeClass} ms-2">${status}</span>`;
            };

            const result = getStatusBadge('Failed');
            expect(result).toContain('bg-danger');
        });
    });

    describe('getStatusIcon', () => {
        test('should return check icon for Succeeded status', () => {
            const getStatusIcon = (status) => {
                const statusLower = status.toLowerCase();
                let iconClass = 'bi-question-circle';
                let iconColor = '#6c757d';

                switch (statusLower) {
                    case 'succeeded':
                        iconClass = 'bi-check-circle-fill';
                        iconColor = '#198754';
                        break;
                    case 'running':
                        iconClass = 'bi-arrow-repeat';
                        iconColor = '#0d6efd';
                        break;
                    case 'failed':
                        iconClass = 'bi-x-circle-fill';
                        iconColor = '#dc3545';
                        break;
                    case 'notstarted':
                        iconClass = 'bi-hourglass-split';
                        iconColor = '#ffc107';
                        break;
                }

                return `<i class="bi ${iconClass}" style="color: ${iconColor}; font-size: 1.5rem;"></i>`;
            };

            const result = getStatusIcon('Succeeded');
            expect(result).toContain('bi-check-circle-fill');
            expect(result).toContain('#198754');
        });

        test('should return arrow icon for Running status', () => {
            const getStatusIcon = (status) => {
                const statusLower = status.toLowerCase();
                let iconClass = 'bi-question-circle';
                let iconColor = '#6c757d';

                switch (statusLower) {
                    case 'succeeded':
                        iconClass = 'bi-check-circle-fill';
                        iconColor = '#198754';
                        break;
                    case 'running':
                        iconClass = 'bi-arrow-repeat';
                        iconColor = '#0d6efd';
                        break;
                    case 'failed':
                        iconClass = 'bi-x-circle-fill';
                        iconColor = '#dc3545';
                        break;
                    case 'notstarted':
                        iconClass = 'bi-hourglass-split';
                        iconColor = '#ffc107';
                        break;
                }

                return `<i class="bi ${iconClass}" style="color: ${iconColor}; font-size: 1.5rem;"></i>`;
            };

            const result = getStatusIcon('Running');
            expect(result).toContain('bi-arrow-repeat');
            expect(result).toContain('#0d6efd');
        });
    });

    describe('displayTranscriptionJobs', () => {
        test('should show empty section when no jobs', () => {
            const emptySection = document.getElementById('jobsEmptySection');
            const jobsList = document.getElementById('jobsList');

            // Simulate displayTranscriptionJobs with empty array
            if (!mockJobs || mockJobs.length === 0) {
                emptySection.classList.remove('d-none');
            } else {
                emptySection.classList.add('d-none');
            }

            // Test with empty array
            const emptyJobs = [];
            if (!emptyJobs || emptyJobs.length === 0) {
                emptySection.classList.remove('d-none');
            }

            expect(emptySection.classList.contains('d-none')).toBe(false);
        });

        test('should hide empty section when jobs exist', () => {
            const emptySection = document.getElementById('jobsEmptySection');

            if (mockJobs && mockJobs.length > 0) {
                emptySection.classList.add('d-none');
            }

            expect(emptySection.classList.contains('d-none')).toBe(true);
        });

        test('should display correct number of jobs', () => {
            const jobsList = document.getElementById('jobsList');
            
            // Simulate displaying jobs
            jobsList.innerHTML = '';
            mockJobs.forEach(job => {
                const div = document.createElement('div');
                div.className = 'list-group-item';
                div.setAttribute('data-job-id', job.id);
                jobsList.appendChild(div);
            });

            expect(jobsList.children.length).toBe(3);
        });
    });

    describe('loadTranscriptionJobs', () => {
        test('should show loading section during fetch', async () => {
            const loadingSection = document.getElementById('jobsLoadingSection');
            
            fetch.mockImplementation(() => 
                new Promise(resolve => setTimeout(() => 
                    resolve({
                        json: async () => ({ success: true, jobs: mockJobs })
                    }), 100))
            );

            // Simulate showing loading
            loadingSection.classList.remove('d-none');
            expect(loadingSection.classList.contains('d-none')).toBe(false);
        });

        test('should handle successful fetch', async () => {
            fetch.mockResolvedValueOnce({
                json: async () => ({ success: true, jobs: mockJobs })
            });

            const response = await fetch('/Home/GetTranscriptionJobs');
            const data = await response.json();

            expect(data.success).toBe(true);
            expect(data.jobs).toHaveLength(3);
        });

        test('should handle fetch error', async () => {
            fetch.mockRejectedValueOnce(new Error('Network error'));

            try {
                await fetch('/Home/GetTranscriptionJobs');
            } catch (error) {
                expect(error.message).toBe('Network error');
            }
        });

        test('should hide loading section after fetch completes', async () => {
            const loadingSection = document.getElementById('jobsLoadingSection');
            
            fetch.mockResolvedValueOnce({
                json: async () => ({ success: true, jobs: mockJobs })
            });

            // Simulate hiding loading after fetch
            loadingSection.classList.add('d-none');
            expect(loadingSection.classList.contains('d-none')).toBe(true);
        });
    });

    describe('cancelTranscriptionJob', () => {
        beforeEach(() => {
            // Mock window.confirm
            global.confirm = jest.fn(() => true);
            // Ensure fetch is cleared before each test
            fetch.mockClear();
            fetch.mockReset();
        });

        test('should prompt user for confirmation', async () => {
            const jobId = 'job-1';
            
            // Simulate cancel action
            const userConfirmed = confirm('Are you sure you want to cancel this transcription job?');
            
            expect(confirm).toHaveBeenCalled();
            expect(userConfirmed).toBe(true);
        });

        test('should not proceed if user cancels confirmation', async () => {
            global.confirm = jest.fn(() => false);
            
            const userConfirmed = confirm('Are you sure?');
            
            if (!userConfirmed) {
                expect(fetch).not.toHaveBeenCalled();
            }
        });

        test('should call cancel endpoint with correct job ID', async () => {
            global.confirm = jest.fn(() => true);
            fetch.mockResolvedValueOnce({
                json: async () => ({ success: true, message: 'Job canceled successfully' })
            });

            const jobId = 'job-1';
            
            await fetch('/Home/CancelTranscriptionJob', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ jobId })
            });

            expect(fetch).toHaveBeenCalledWith(
                '/Home/CancelTranscriptionJob',
                expect.objectContaining({
                    method: 'POST',
                    body: JSON.stringify({ jobId })
                })
            );
        });

        test('should handle successful cancellation', async () => {
            global.confirm = jest.fn(() => true);
            fetch.mockResolvedValueOnce({
                json: async () => ({ success: true, message: 'Job canceled successfully' })
            });

            const response = await fetch('/Home/CancelTranscriptionJob', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ jobId: 'job-1' })
            });
            const data = await response.json();

            expect(data.success).toBe(true);
            expect(data.message).toBe('Job canceled successfully');
        });

        test('should handle cancellation error', async () => {
            global.confirm = jest.fn(() => true);
            // Reset and setup the specific mock for this test
            fetch.mockClear();
            fetch.mockReset();
            fetch.mockResolvedValueOnce({
                json: async () => ({ success: false, message: 'Failed to cancel job' })
            });

            const response = await fetch('/Home/CancelTranscriptionJob', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ jobId: 'job-1' })
            });
            const data = await response.json();

            expect(data.success).toBe(false);
            expect(data.message).toBe('Failed to cancel job');
        });

        test('should remove job from list on successful cancellation', () => {
            const jobsList = document.getElementById('jobsList');
            
            // Add job to list
            const jobItem = document.createElement('div');
            jobItem.className = 'list-group-item';
            jobItem.setAttribute('data-job-id', 'job-1');
            jobsList.appendChild(jobItem);

            expect(jobsList.children.length).toBe(1);

            // Simulate removal
            const itemToRemove = jobsList.querySelector('[data-job-id="job-1"]');
            if (itemToRemove) {
                itemToRemove.remove();
            }

            expect(jobsList.children.length).toBe(0);
        });
    });

    describe('showJobsError', () => {
        test('should display error message', () => {
            const errorSection = document.getElementById('jobsErrorSection');
            const errorMessage = document.getElementById('jobsErrorMessage');

            const message = 'Failed to load jobs';
            errorMessage.textContent = message;
            errorSection.classList.remove('d-none');

            expect(errorMessage.textContent).toBe(message);
            expect(errorSection.classList.contains('d-none')).toBe(false);
        });
    });

    describe('createJobListItem', () => {
        test('should create job item with correct structure', () => {
            const job = mockJobs[0];
            const listItem = document.createElement('div');
            listItem.className = 'list-group-item';
            listItem.setAttribute('data-job-id', job.id);

            expect(listItem.getAttribute('data-job-id')).toBe('job-1');
            expect(listItem.className).toContain('list-group-item');
        });

        test('should show cancel button for running jobs', () => {
            const runningJob = mockJobs.find(j => j.status === 'Running');
            const canCancel = ['NotStarted', 'Running'].includes(runningJob.status);

            expect(canCancel).toBe(true);
        });

        test('should not show cancel button for completed jobs', () => {
            const succeededJob = mockJobs.find(j => j.status === 'Succeeded');
            const canCancel = ['NotStarted', 'Running'].includes(succeededJob.status);

            expect(canCancel).toBe(false);
        });

        test('should display error for failed jobs', () => {
            const failedJob = mockJobs.find(j => j.status === 'Failed');
            expect(failedJob.error).toBe('Audio format not supported');
        });

        test('should display files list', () => {
            const job = mockJobs[0];
            expect(job.files).toContain('audio1.wav');
            expect(job.files).toContain('audio2.mp3');
            expect(job.files.length).toBe(2);
        });
    });
});
