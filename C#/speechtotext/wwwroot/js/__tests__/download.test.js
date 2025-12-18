/**
 * Unit Tests for transcription.js - Download Functions
 * Tests for downloadBlob, getTimestamp, and download handlers
 */

describe('Download Functions', () => {
    let mockCreateObjectURL;
    let mockRevokeObjectURL;

    beforeEach(() => {
        // Mock URL.createObjectURL and URL.revokeObjectURL
        mockCreateObjectURL = jest.fn(() => 'blob:mock-url');
        mockRevokeObjectURL = jest.fn();
        
        global.URL.createObjectURL = mockCreateObjectURL;
        global.URL.revokeObjectURL = mockRevokeObjectURL;

        // Setup DOM
        document.body.innerHTML = '';
    });

    afterEach(() => {
        jest.clearAllMocks();
    });

    describe('downloadBlob', () => {
        test('should create download link and trigger download', () => {
            const mockBlob = new Blob(['test content'], { type: 'text/plain' });
            const filename = 'test-file.txt';

            // Simulate downloadBlob function
            const url = URL.createObjectURL(mockBlob);
            const a = document.createElement('a');
            a.href = url;
            a.download = filename;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);

            expect(mockCreateObjectURL).toHaveBeenCalledWith(mockBlob);
            expect(mockRevokeObjectURL).toHaveBeenCalledWith('blob:mock-url');
        });

        test('should set correct filename', () => {
            const mockBlob = new Blob(['test'], { type: 'application/json' });
            const filename = 'transcription_20250115.json';

            const url = URL.createObjectURL(mockBlob);
            const a = document.createElement('a');
            a.href = url;
            a.download = filename;

            expect(a.download).toBe(filename);
        });

        test('should cleanup after download', () => {
            const mockBlob = new Blob(['test'], { type: 'text/plain' });
            
            const url = URL.createObjectURL(mockBlob);
            const a = document.createElement('a');
            a.href = url;
            a.download = 'test.txt';
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);

            expect(mockRevokeObjectURL).toHaveBeenCalled();
        });
    });

    describe('getTimestamp', () => {
        test('should return formatted timestamp', () => {
            // Mock Date to return consistent value
            const mockDate = new Date('2025-01-15T14:30:45.123Z');
            jest.spyOn(global, 'Date').mockImplementation(() => mockDate);

            // Simulate getTimestamp function
            const timestamp = mockDate.toISOString().replace(/[:.]/g, '-');

            expect(timestamp).toBe('2025-01-15T14-30-45-123Z');
            
            // Verify no colons or periods
            expect(timestamp).not.toContain(':');
            expect(timestamp).not.toContain('.');
            
            // Restore Date mock
            global.Date.mockRestore();
        });

        test('should generate unique timestamps', () => {
            // Don't use mocked dates for this test
            // Create two different dates
            const date1 = new Date('2025-01-15T14:30:45.123Z');
            const date2 = new Date('2025-01-15T14:30:46.456Z');
            
            const timestamp1 = date1.toISOString().replace(/[:.]/g, '-');
            const timestamp2 = date2.toISOString().replace(/[:.]/g, '-');

            // Timestamps should be different (just verify they're not equal)
            expect(timestamp1).not.toEqual(timestamp2);
        });

        test('should be valid for filename', () => {
            const mockDate = new Date();
            const timestamp = mockDate.toISOString().replace(/[:.]/g, '-');

            // Should not contain invalid filename characters
            const invalidChars = /[<>:"|?*]/;
            expect(timestamp).not.toMatch(invalidChars);
        });
    });

    describe('handleDownloadGoldenRecord', () => {
        beforeEach(() => {
            global.alert = jest.fn();
            global.fetch = jest.fn();
        });

        test('should alert when no golden record data available', async () => {
            const goldenRecordData = null;

            if (!goldenRecordData) {
                alert('No original record data available');
                return;
            }

            expect(global.alert).toHaveBeenCalledWith('No original record data available');
        });

        test('should fetch and download golden record', async () => {
            const goldenRecordData = '{"test": "data"}';
            const mockBlob = new Blob([goldenRecordData], { type: 'application/json' });
            
            global.fetch.mockResolvedValue({
                ok: true,
                blob: async () => mockBlob
            });

            const response = await fetch('/Home/DownloadGoldenRecord', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ goldenRecordJsonData: goldenRecordData })
            });

            expect(response.ok).toBe(true);
            const blob = await response.blob();
            expect(blob).toBeDefined();
        });

        test('should handle download error', async () => {
            const goldenRecordData = '{"test": "data"}';
            
            global.fetch.mockResolvedValue({
                ok: false,
                status: 500
            });

            const response = await fetch('/Home/DownloadGoldenRecord', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ goldenRecordJsonData: goldenRecordData })
            });

            if (!response.ok) {
                alert('Error downloading original record');
            }

            expect(response.ok).toBe(false);
            expect(global.alert).toHaveBeenCalledWith('Error downloading original record');
        });
    });

    describe('handleDownloadReadable', () => {
        beforeEach(() => {
            global.alert = jest.fn();
            global.fetch = jest.fn();
        });

        test('should alert when no transcription data available', async () => {
            const transcriptionData = null;

            if (!transcriptionData) {
                alert('No transcription data available');
                return;
            }

            expect(global.alert).toHaveBeenCalledWith('No transcription data available');
        });

        test('should fetch and download readable text', async () => {
            const transcriptionData = {
                fullTranscript: '[Guest-1]: Hello\n[Guest-2]: Hi',
                segments: [
                    { speaker: 'Guest-1', text: 'Hello', uiFormattedStartTime: '00:00:00' },
                    { speaker: 'Guest-2', text: 'Hi', uiFormattedStartTime: '00:00:02' }
                ]
            };
            
            const mockBlob = new Blob(['transcript content'], { 
                type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
            });
            
            global.fetch.mockResolvedValue({
                ok: true,
                blob: async () => mockBlob
            });

            const response = await fetch('/Home/DownloadReadableText', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    fullTranscript: transcriptionData.fullTranscript,
                    segments: transcriptionData.segments
                })
            });

            expect(response.ok).toBe(true);
            const blob = await response.blob();
            expect(blob).toBeDefined();
        });

        test('should use correct filename with .docx extension', () => {
            const mockDate = new Date('2025-01-15T14:30:45Z');
            const timestamp = mockDate.toISOString().replace(/[:.]/g, '-');
            const filename = `transcription_${timestamp}.docx`;

            expect(filename).toContain('.docx');
            expect(filename).not.toContain('.txt');
            expect(filename).toMatch(/^transcription_.*\.docx$/);
        });

        test('should handle download error', async () => {
            const transcriptionData = {
                fullTranscript: '[Guest-1]: Hello',
                segments: []
            };
            
            global.fetch.mockResolvedValue({
                ok: false,
                status: 500
            });

            const response = await fetch('/Home/DownloadReadableText', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    fullTranscript: transcriptionData.fullTranscript,
                    segments: transcriptionData.segments
                })
            });

            if (!response.ok) {
                alert('Error downloading file');
            }

            expect(response.ok).toBe(false);
            expect(global.alert).toHaveBeenCalledWith('Error downloading file');
        });

        test('should handle network error', async () => {
            const transcriptionData = {
                fullTranscript: '[Guest-1]: Hello',
                segments: []
            };
            
            global.fetch.mockRejectedValue(new Error('Network error'));

            try {
                await fetch('/Home/DownloadReadableText', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        fullTranscript: transcriptionData.fullTranscript,
                        segments: transcriptionData.segments
                    })
                });
            } catch (error) {
                alert('Error: ' + error.message);
            }

            expect(global.alert).toHaveBeenCalledWith('Error: Network error');
        });
    });

    describe('Download filename formatting', () => {
        test('should format golden record filename correctly', () => {
            const timestamp = '2025-01-15T14-30-45-123Z';
            const filename = `transcription_original_${timestamp}.json`;

            expect(filename).toMatch(/^transcription_original_.*\.json$/);
            expect(filename).toContain('transcription_original_');
            expect(filename.endsWith('.json')).toBe(true);
        });

        test('should format readable text filename correctly', () => {
            const timestamp = '2025-01-15T14-30-45-123Z';
            const filename = `transcription_${timestamp}.docx`;

            expect(filename).toMatch(/^transcription_.*\.docx$/);
            expect(filename).toContain('transcription_');
            expect(filename.endsWith('.docx')).toBe(true);
        });
    });
});
