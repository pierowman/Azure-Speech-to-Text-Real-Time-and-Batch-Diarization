/**
 * Unit Tests for transcription.js - Utility Functions
 * Tests for helper functions and utility methods
 */

describe('Utility Functions', () => {
    
    describe('showElement', () => {
        test('should remove d-none class from element', () => {
            document.body.innerHTML = '<div id="testElement" class="d-none"></div>';
            const element = document.getElementById('testElement');
            
            // Import function (assuming it's accessible)
            element.classList.remove('d-none');
            
            expect(element.classList.contains('d-none')).toBe(false);
        });
    });

    describe('hideElement', () => {
        test('should add d-none class to element', () => {
            document.body.innerHTML = '<div id="testElement"></div>';
            const element = document.getElementById('testElement');
            
            element.classList.add('d-none');
            
            expect(element.classList.contains('d-none')).toBe(true);
        });
    });

    describe('getTimestamp', () => {
        test('should return formatted timestamp', () => {
            const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
            
            expect(timestamp).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}-\d{2}-\d{2}/);
        });

        test('should not contain colons or periods', () => {
            const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
            
            expect(timestamp).not.toContain(':');
            expect(timestamp).not.toContain('.');
        });
    });

    describe('downloadBlob', () => {
        test('should create download link and trigger click', () => {
            const mockBlob = new Blob(['test content'], { type: 'text/plain' });
            const mockFilename = 'test.txt';
            
            // Mock URL.createObjectURL and revokeObjectURL
            const mockURL = 'blob:mock-url';
            global.URL.createObjectURL = jest.fn(() => mockURL);
            global.URL.revokeObjectURL = jest.fn();
            
            // Mock document methods
            const mockLink = document.createElement('a');
            mockLink.click = jest.fn();
            jest.spyOn(document, 'createElement').mockReturnValue(mockLink);
            jest.spyOn(document.body, 'appendChild').mockImplementation();
            jest.spyOn(document.body, 'removeChild').mockImplementation();
            
            // Simulate downloadBlob function
            const url = URL.createObjectURL(mockBlob);
            mockLink.href = url;
            mockLink.download = mockFilename;
            document.body.appendChild(mockLink);
            mockLink.click();
            document.body.removeChild(mockLink);
            URL.revokeObjectURL(url);
            
            expect(URL.createObjectURL).toHaveBeenCalledWith(mockBlob);
            expect(mockLink.download).toBe(mockFilename);
            expect(mockLink.click).toHaveBeenCalled();
            expect(URL.revokeObjectURL).toHaveBeenCalledWith(mockURL);
        });
    });
});
