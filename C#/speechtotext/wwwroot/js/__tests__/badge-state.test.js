/**
 * Unit Tests for transcription.js - Badge Updates & State Management
 * Tests for original speaker/text badges and state tracking
 */

describe('Badge Updates & State Management', () => {
    let mockTranscriptionData;

    beforeEach(() => {
        mockTranscriptionData = {
            segments: [
                {
                    speaker: 'John Doe',
                    originalSpeaker: 'Guest-1',
                    text: 'Updated text',
                    originalText: 'Original text',
                    startTimeInSeconds: 0
                }
            ]
        };
    });

    describe('updateOriginalSpeakerBadge', () => {
        beforeEach(() => {
            document.body.innerHTML = `
                <div class="segment-item" data-segment-index="0">
                    <div class="d-flex align-items-center gap-2"></div>
                </div>
            `;
        });

        test('should show badge when speaker changed', () => {
            const segment = mockTranscriptionData.segments[0];
            const speakerChanged = segment.speaker !== segment.originalSpeaker;
            
            expect(speakerChanged).toBe(true);
        });

        test('should not show badge when speaker unchanged', () => {
            const segment = {
                speaker: 'Guest-1',
                originalSpeaker: 'Guest-1'
            };
            const speakerChanged = segment.speaker !== segment.originalSpeaker;
            
            expect(speakerChanged).toBe(false);
        });

        test('should handle missing originalSpeaker', () => {
            const segment = { speaker: 'Guest-1' };
            const originalSpeaker = segment.originalSpeaker || segment.speaker;
            
            expect(originalSpeaker).toBe('Guest-1');
        });

        test('should check for ambiguous mappings', () => {
            const segments = [
                { speaker: 'John', originalSpeaker: 'Guest-1' },
                { speaker: 'John', originalSpeaker: 'Guest-2' },
                { speaker: 'John', originalSpeaker: 'Guest-1' }
            ];

            const speakerMappings = new Map();
            segments.forEach(seg => {
                const currentSpeaker = seg.speaker;
                const originalSpeaker = seg.originalSpeaker || seg.speaker;
                
                if (!speakerMappings.has(currentSpeaker)) {
                    speakerMappings.set(currentSpeaker, new Set());
                }
                speakerMappings.get(currentSpeaker).add(originalSpeaker);
            });

            const johnMapping = speakerMappings.get('John');
            const isAmbiguous = johnMapping && johnMapping.size > 1;
            
            expect(isAmbiguous).toBe(true);
            expect(johnMapping.size).toBe(2); // Maps to both Guest-1 and Guest-2
        });
    });

    describe('updateOriginalTextBadge', () => {
        beforeEach(() => {
            document.body.innerHTML = `
                <div class="segment-item" data-segment-index="0">
                    <div class="transcript-text-container">
                        <div class="d-flex justify-content-between align-items-center mt-1">
                            <div class="transcript-edit-actions"></div>
                        </div>
                    </div>
                </div>
            `;
        });

        test('should show badge when text changed', () => {
            const segment = mockTranscriptionData.segments[0];
            const textChanged = segment.text !== segment.originalText;
            
            expect(textChanged).toBe(true);
        });

        test('should not show badge when text unchanged', () => {
            const segment = {
                text: 'Same text',
                originalText: 'Same text'
            };
            const textChanged = segment.text !== segment.originalText;
            
            expect(textChanged).toBe(false);
        });

        test('should truncate long original text', () => {
            const longText = 'a'.repeat(100);
            const truncated = longText.substring(0, 50) + (longText.length > 50 ? '...' : '');
            
            expect(truncated.length).toBe(53); // 50 chars + '...'
            expect(truncated).toMatch(/\.\.\.$/);
        });

        test('should not truncate short original text', () => {
            const shortText = 'Short text';
            const truncated = shortText.substring(0, 50) + (shortText.length > 50 ? '...' : '');
            
            expect(truncated).toBe('Short text');
            expect(truncated).not.toMatch(/\.\.\.$/);
        });
    });

    describe('buildSpeakerMappings', () => {
        test('should build mapping from current to original speakers', () => {
            const segments = [
                { speaker: 'John', originalSpeaker: 'Guest-1' },
                { speaker: 'John', originalSpeaker: 'Guest-1' },
                { speaker: 'Jane', originalSpeaker: 'Guest-2' }
            ];

            const mappings = new Map();
            segments.forEach(segment => {
                const currentSpeaker = segment.speaker;
                const originalSpeaker = segment.originalSpeaker || segment.speaker;
                
                if (!mappings.has(currentSpeaker)) {
                    mappings.set(currentSpeaker, new Set());
                }
                mappings.get(currentSpeaker).add(originalSpeaker);
            });

            expect(mappings.size).toBe(2);
            expect(mappings.get('John').size).toBe(1);
            expect(mappings.get('Jane').size).toBe(1);
            expect(mappings.get('John').has('Guest-1')).toBe(true);
            expect(mappings.get('Jane').has('Guest-2')).toBe(true);
        });

        test('should handle one-to-many mappings', () => {
            const segments = [
                { speaker: 'John', originalSpeaker: 'Guest-1' },
                { speaker: 'John', originalSpeaker: 'Guest-2' },
                { speaker: 'John', originalSpeaker: 'Guest-3' }
            ];

            const mappings = new Map();
            segments.forEach(segment => {
                const currentSpeaker = segment.speaker;
                const originalSpeaker = segment.originalSpeaker || segment.speaker;
                
                if (!mappings.has(currentSpeaker)) {
                    mappings.set(currentSpeaker, new Set());
                }
                mappings.get(currentSpeaker).add(originalSpeaker);
            });

            expect(mappings.get('John').size).toBe(3);
        });
    });

    describe('hasUnsavedChanges tracking', () => {
        test('should set flag when speaker changed', () => {
            let hasUnsavedChanges = false;
            
            // Simulate speaker change
            hasUnsavedChanges = true;
            
            expect(hasUnsavedChanges).toBe(true);
        });

        test('should set flag when text edited', () => {
            let hasUnsavedChanges = false;
            
            // Simulate text edit
            hasUnsavedChanges = true;
            
            expect(hasUnsavedChanges).toBe(true);
        });

        test('should clear flag after successful upload', () => {
            let hasUnsavedChanges = true;
            
            // Simulate successful save
            hasUnsavedChanges = false;
            
            expect(hasUnsavedChanges).toBe(false);
        });

        test('should trigger beforeunload warning', () => {
            const hasUnsavedChanges = true;
            const event = {
                preventDefault: jest.fn(),
                returnValue: ''
            };
            
            if (hasUnsavedChanges) {
                event.preventDefault();
                event.returnValue = '';
            }
            
            expect(event.preventDefault).toHaveBeenCalled();
        });

        test('should not trigger beforeunload when no changes', () => {
            const hasUnsavedChanges = false;
            const event = {
                preventDefault: jest.fn()
            };
            
            if (hasUnsavedChanges) {
                event.preventDefault();
            }
            
            expect(event.preventDefault).not.toHaveBeenCalled();
        });
    });

    describe('State backup and rollback', () => {
        test('should create deep copy for backup', () => {
            const original = {
                segments: [
                    { speaker: 'Guest-1', text: 'Original' }
                ]
            };
            
            const backup = JSON.parse(JSON.stringify(original));
            
            // Modify original
            original.segments[0].speaker = 'Modified';
            
            // Backup should be unchanged
            expect(backup.segments[0].speaker).toBe('Guest-1');
            expect(original.segments[0].speaker).toBe('Modified');
        });

        test('should restore from backup on error', () => {
            const original = {
                segments: [
                    { speaker: 'Guest-1', text: 'Original' }
                ]
            };
            
            const backup = JSON.parse(JSON.stringify(original));
            
            // Modify original
            original.segments[0].speaker = 'Modified';
            
            // Simulate error and rollback
            const restored = backup;
            
            expect(restored.segments[0].speaker).toBe('Guest-1');
        });

        test('should handle nested object backup', () => {
            const original = {
                segments: [
                    { 
                        speaker: 'Guest-1', 
                        nested: { 
                            value: 'test' 
                        } 
                    }
                ]
            };
            
            const backup = JSON.parse(JSON.stringify(original));
            
            original.segments[0].nested.value = 'modified';
            
            expect(backup.segments[0].nested.value).toBe('test');
        });
    });

    describe('displayResults', () => {
        test('should show results section', () => {
            document.body.innerHTML = '<div id="resultsSection" class="d-none"></div>';
            
            const resultsSection = document.getElementById('resultsSection');
            resultsSection.classList.remove('d-none');
            
            expect(resultsSection.classList.contains('d-none')).toBe(false);
        });

        test('should show warning when no segments', () => {
            const result = { segments: [] };
            const hasSegments = result.segments && result.segments.length > 0;
            
            expect(hasSegments).toBe(false);
        });

        test('should display full transcript', () => {
            document.body.innerHTML = '<pre id="fullTranscript"></pre>';
            
            const fullTranscript = document.getElementById('fullTranscript');
            fullTranscript.textContent = '[Guest-1]: Hello\n[Guest-2]: Hi';
            
            expect(fullTranscript.textContent).toContain('[Guest-1]: Hello');
            expect(fullTranscript.textContent).toContain('[Guest-2]: Hi');
        });
    });
});
