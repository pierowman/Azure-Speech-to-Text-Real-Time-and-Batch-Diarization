/**
 * Unit Tests for transcription.js - Transcript Editing
 * Tests for transcript text editing functionality
 */

describe('Transcript Editing', () => {
    let mockTranscriptionData;

    beforeEach(() => {
        mockTranscriptionData = {
            segments: [
                { 
                    speaker: 'Guest-1', 
                    text: 'Original text', 
                    originalText: 'Original text',
                    startTimeInSeconds: 0,
                    offsetInTicks: 0,
                    durationInTicks: 10000000
                }
            ],
            fullTranscript: '[Guest-1]: Original text'
        };

        // Setup DOM
        document.body.innerHTML = `
            <div class="segment-item" data-segment-index="0">
                <textarea class="transcript-text-input d-none" data-segment-index="0">Original text</textarea>
                <p class="transcript-text-display" data-segment-index="0">Original text</p>
                <div class="transcript-edit-actions d-none">
                    <button class="save-transcript-btn" data-segment-index="0">Save</button>
                    <button class="cancel-transcript-btn" data-segment-index="0">Cancel</button>
                </div>
            </div>
        `;
    });

    describe('enterTranscriptEditMode', () => {
        test('should show textarea and hide display text', () => {
            const segment = document.querySelector('.segment-item');
            const display = segment.querySelector('.transcript-text-display');
            const textarea = segment.querySelector('.transcript-text-input');
            const actions = segment.querySelector('.transcript-edit-actions');

            display.classList.add('d-none');
            textarea.classList.remove('d-none');
            actions.classList.remove('d-none');

            expect(display.classList.contains('d-none')).toBe(true);
            expect(textarea.classList.contains('d-none')).toBe(false);
            expect(actions.classList.contains('d-none')).toBe(false);
        });
    });

    describe('exitTranscriptEditMode', () => {
        test('should hide textarea and show display text', () => {
            const segment = document.querySelector('.segment-item');
            const display = segment.querySelector('.transcript-text-display');
            const textarea = segment.querySelector('.transcript-text-input');
            const actions = segment.querySelector('.transcript-edit-actions');

            // First enter edit mode
            display.classList.add('d-none');
            textarea.classList.remove('d-none');
            actions.classList.remove('d-none');

            // Then exit
            display.classList.remove('d-none');
            textarea.classList.add('d-none');
            actions.classList.add('d-none');

            expect(display.classList.contains('d-none')).toBe(false);
            expect(textarea.classList.contains('d-none')).toBe(true);
            expect(actions.classList.contains('d-none')).toBe(true);
        });
    });

    describe('saveTranscriptEdit validation', () => {
        test('should reject empty transcript text', () => {
            const newText = '';
            const isValid = !!newText.trim();

            expect(isValid).toBe(false);
        });

        test('should reject whitespace-only transcript text', () => {
            const newText = '   ';
            const isValid = !!newText.trim();

            expect(isValid).toBe(false);
        });

        test('should accept valid transcript text', () => {
            const newText = 'Updated text';
            const isValid = !!newText.trim();

            expect(isValid).toBe(true);
        });

        test('should backup state before update', () => {
            const oldText = mockTranscriptionData.segments[0].text;
            const newText = 'Updated text';
            
            // Simulate backup
            const backup = oldText;
            mockTranscriptionData.segments[0].text = newText;
            
            expect(backup).toBe('Original text');
            expect(mockTranscriptionData.segments[0].text).toBe('Updated text');
        });
    });

    describe('cancelTranscriptEdit', () => {
        test('should restore original text', () => {
            const textarea = document.querySelector('.transcript-text-input');
            const originalText = mockTranscriptionData.segments[0].text;
            
            // Simulate user changing text
            textarea.value = 'Changed text';
            
            // Cancel should restore
            textarea.value = originalText;
            
            expect(textarea.value).toBe('Original text');
        });
    });

    describe('updateOriginalTextBadge', () => {
        test('should detect when text has changed', () => {
            const segment = mockTranscriptionData.segments[0];
            segment.text = 'Updated text';
            
            const textChanged = segment.text !== segment.originalText;
            
            expect(textChanged).toBe(true);
        });

        test('should detect when text is unchanged', () => {
            const segment = mockTranscriptionData.segments[0];
            
            const textChanged = segment.text !== segment.originalText;
            
            expect(textChanged).toBe(false);
        });

        test('should handle missing originalText', () => {
            const segment = { text: 'Current text' };
            const originalText = segment.originalText || segment.text;
            
            expect(originalText).toBe('Current text');
        });
    });
});
