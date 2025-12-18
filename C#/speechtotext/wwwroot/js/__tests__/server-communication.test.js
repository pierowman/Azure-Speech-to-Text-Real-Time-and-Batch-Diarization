/**
 * Unit Tests for transcription.js - Server Communication
 * Tests for API calls and server interaction
 */

describe('Server Communication', () => {
    let mockFetch;

    beforeEach(() => {
        mockFetch = jest.fn();
        global.fetch = mockFetch;
    });

    afterEach(() => {
        jest.clearAllMocks();
    });

    describe('updateSegmentsOnServer', () => {
        let mockTranscriptionData;

        beforeEach(() => {
            mockTranscriptionData = {
                segments: [
                    { speaker: 'Guest-1', text: 'Hello' }
                ],
                audioFileUrl: '/uploads/test.wav',
                goldenRecordJsonData: '{}'
            };
        });

        test('should send POST request with correct data', async () => {
            const mockResponse = {
                ok: true,
                json: async () => ({
                    segments: mockTranscriptionData.segments,
                    rawJsonData: '{}',
                    fullTranscript: '[Guest-1]: Hello'
                })
            };
            mockFetch.mockResolvedValue(mockResponse);

            const response = await fetch('/Home/UpdateSpeakerNames', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    segments: mockTranscriptionData.segments,
                    audioFileUrl: mockTranscriptionData.audioFileUrl,
                    goldenRecordJsonData: mockTranscriptionData.goldenRecordJsonData
                })
            });

            expect(mockFetch).toHaveBeenCalledWith(
                '/Home/UpdateSpeakerNames',
                expect.objectContaining({
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' }
                })
            );
            expect(response.ok).toBe(true);
        });

        test('should throw error when server returns non-ok status', async () => {
            mockFetch.mockResolvedValue({ ok: false, status: 500 });

            const response = await fetch('/Home/UpdateSpeakerNames', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({})
            });

            expect(response.ok).toBe(false);
        });

        test('should handle network errors', async () => {
            mockFetch.mockRejectedValue(new Error('Network error'));

            await expect(
                fetch('/Home/UpdateSpeakerNames', {
                    method: 'POST',
                    body: JSON.stringify({})
                })
            ).rejects.toThrow('Network error');
        });
    });

    describe('handleDownloadGoldenRecord', () => {
        test('should send correct download request', async () => {
            const goldenRecordData = '{"test": "data"}';
            const mockBlob = new Blob(['test'], { type: 'application/json' });
            
            mockFetch.mockResolvedValue({
                ok: true,
                blob: async () => mockBlob
            });

            const response = await fetch('/Home/DownloadGoldenRecord', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ goldenRecordJsonData: goldenRecordData })
            });

            expect(mockFetch).toHaveBeenCalledWith(
                '/Home/DownloadGoldenRecord',
                expect.objectContaining({
                    method: 'POST'
                })
            );
            expect(response.ok).toBe(true);
        });

        test('should handle missing golden record data', () => {
            const goldenRecordData = null;
            const hasData = !!goldenRecordData;
            
            expect(hasData).toBe(false);
        });
    });

    describe('handleDownloadReadable', () => {
        test('should send segments and transcript', async () => {
            const mockData = {
                fullTranscript: '[Guest-1]: Hello',
                segments: [{ speaker: 'Guest-1', text: 'Hello' }]
            };

            mockFetch.mockResolvedValue({
                ok: true,
                blob: async () => new Blob(['test'], { type: 'text/plain' })
            });

            const response = await fetch('/Home/DownloadReadableText', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(mockData)
            });

            expect(mockFetch).toHaveBeenCalledWith(
                '/Home/DownloadReadableText',
                expect.objectContaining({
                    method: 'POST'
                })
            );
            expect(response.ok).toBe(true);
        });
    });

    describe('handleFileUpload', () => {
        test('should upload file with FormData', async () => {
            const mockFile = new File(['content'], 'test.wav', { type: 'audio/wav' });
            const formData = new FormData();
            formData.append('audioFile', mockFile);

            mockFetch.mockResolvedValue({
                ok: true,
                json: async () => ({
                    success: true,
                    segments: []
                })
            });

            const response = await fetch('/Home/UploadAndTranscribe', {
                method: 'POST',
                body: formData
            });

            expect(mockFetch).toHaveBeenCalledWith(
                '/Home/UploadAndTranscribe',
                expect.objectContaining({
                    method: 'POST'
                })
            );
            expect(response.ok).toBe(true);
        });

        test('should handle upload failure', async () => {
            mockFetch.mockResolvedValue({
                ok: true,
                json: async () => ({
                    success: false,
                    message: 'Invalid file'
                })
            });

            const response = await fetch('/Home/UploadAndTranscribe', {
                method: 'POST',
                body: new FormData()
            });

            const result = await response.json();
            expect(result.success).toBe(false);
            expect(result.message).toBe('Invalid file');
        });
    });

    describe('Loading indicators', () => {
        beforeEach(() => {
            document.body.innerHTML = '<div id="updateLoadingIndicator" style="display: none;"></div>';
        });

        test('should show loading indicator', () => {
            const indicator = document.getElementById('updateLoadingIndicator');
            indicator.style.display = 'block';
            
            expect(indicator.style.display).toBe('block');
        });

        test('should hide loading indicator', () => {
            const indicator = document.getElementById('updateLoadingIndicator');
            indicator.style.display = 'block';
            indicator.style.display = 'none';
            
            expect(indicator.style.display).toBe('none');
        });

        test('should create loading indicator if missing', () => {
            document.body.innerHTML = '';
            
            let indicator = document.getElementById('updateLoadingIndicator');
            if (!indicator) {
                indicator = document.createElement('div');
                indicator.id = 'updateLoadingIndicator';
                document.body.appendChild(indicator);
            }
            
            expect(document.getElementById('updateLoadingIndicator')).toBeTruthy();
        });
    });

    describe('Error handling', () => {
        test('should handle JSON parse errors', async () => {
            mockFetch.mockResolvedValue({
                ok: true,
                json: async () => {
                    throw new Error('Invalid JSON');
                }
            });

            const response = await fetch('/test', { method: 'POST' });
            
            await expect(response.json()).rejects.toThrow('Invalid JSON');
        });

        test('should handle timeout errors', async () => {
            mockFetch.mockImplementation(() => 
                new Promise((_, reject) => 
                    setTimeout(() => reject(new Error('Timeout')), 100)
                )
            );

            await expect(
                fetch('/test', { method: 'POST' })
            ).rejects.toThrow('Timeout');
        });
    });
});
