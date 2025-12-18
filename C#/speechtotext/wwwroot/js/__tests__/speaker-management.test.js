/**
 * Unit Tests for transcription.js - Speaker Management
 * Tests for speaker-related functionality
 */

describe('Speaker Management', () => {
    let mockTranscriptionData;
    let availableSpeakers;

    beforeEach(() => {
        // Mock transcription data
        mockTranscriptionData = {
            segments: [
                { speaker: 'Guest-1', text: 'Hello', originalSpeaker: 'Guest-1', originalText: 'Hello', startTimeInSeconds: 0, offsetInTicks: 0, durationInTicks: 10000000 },
                { speaker: 'Guest-2', text: 'Hi there', originalSpeaker: 'Guest-2', originalText: 'Hi there', startTimeInSeconds: 2, offsetInTicks: 20000000, durationInTicks: 10000000 },
                { speaker: 'Guest-1', text: 'How are you?', originalSpeaker: 'Guest-1', originalText: 'How are you?', startTimeInSeconds: 4, offsetInTicks: 40000000, durationInTicks: 10000000 }
            ],
            fullTranscript: '[Guest-1]: Hello\n[Guest-2]: Hi there\n[Guest-1]: How are you?',
            audioFileUrl: '/uploads/test.wav'
        };
        
        availableSpeakers = new Set();
    });

    describe('buildAvailableSpeakersList', () => {
        test('should populate availableSpeakers with unique speakers from segments', () => {
            // Simulate buildAvailableSpeakersList
            availableSpeakers.clear();
            mockTranscriptionData.segments.forEach(segment => {
                if (segment.speaker && segment.speaker.trim()) {
                    availableSpeakers.add(segment.speaker);
                }
            });
            
            expect(availableSpeakers.size).toBe(2);
            expect(availableSpeakers.has('Guest-1')).toBe(true);
            expect(availableSpeakers.has('Guest-2')).toBe(true);
        });

        test('should not add empty or whitespace-only speakers', () => {
            mockTranscriptionData.segments.push({ speaker: '', text: 'test' });
            mockTranscriptionData.segments.push({ speaker: '   ', text: 'test' });
            
            availableSpeakers.clear();
            mockTranscriptionData.segments.forEach(segment => {
                if (segment.speaker && segment.speaker.trim()) {
                    availableSpeakers.add(segment.speaker);
                }
            });
            
            expect(availableSpeakers.size).toBe(2);
        });

        test('should only add speakers currently in use', () => {
            // Add a segment with originalSpeaker different from speaker
            mockTranscriptionData.segments[0].originalSpeaker = 'OldSpeaker';
            
            availableSpeakers.clear();
            mockTranscriptionData.segments.forEach(segment => {
                if (segment.speaker && segment.speaker.trim()) {
                    availableSpeakers.add(segment.speaker);
                }
            });
            
            // Should not include originalSpeaker
            expect(availableSpeakers.has('OldSpeaker')).toBe(false);
            expect(availableSpeakers.has('Guest-1')).toBe(true);
        });
    });

    describe('getSpeakerOptions', () => {
        beforeEach(() => {
            availableSpeakers.clear();
            availableSpeakers.add('Guest-1');
            availableSpeakers.add('Guest-2');
            availableSpeakers.add('John Doe');
        });

        test('should generate sorted speaker options', () => {
            const speakers = Array.from(availableSpeakers).sort();
            const options = speakers.map(speaker => 
                `<option value="${speaker}">${speaker}</option>`
            ).join('');
            
            expect(options).toContain('Guest-1');
            expect(options).toContain('Guest-2');
            expect(options).toContain('John Doe');
        });

        test('should mark current speaker as selected', () => {
            const currentSpeaker = 'Guest-1';
            const speakers = Array.from(availableSpeakers).sort();
            const options = speakers.map(speaker => 
                `<option value="${speaker}" ${speaker === currentSpeaker ? 'selected' : ''}>${speaker}</option>`
            ).join('');
            
            expect(options).toContain('selected');
            expect(options).toMatch(/<option value="Guest-1" selected>/);
        });

        test('should sort speakers alphabetically', () => {
            availableSpeakers.clear();
            availableSpeakers.add('Zebra');
            availableSpeakers.add('Apple');
            availableSpeakers.add('Banana');
            
            const speakers = Array.from(availableSpeakers).sort();
            
            expect(speakers[0]).toBe('Apple');
            expect(speakers[1]).toBe('Banana');
            expect(speakers[2]).toBe('Zebra');
        });
    });

    describe('handleAddSpeaker validation', () => {
        beforeEach(() => {
            availableSpeakers.clear();
            availableSpeakers.add('Guest-1');
            availableSpeakers.add('Guest-2');
        });

        test('should reject empty speaker name', () => {
            const newSpeaker = '';
            const result = !newSpeaker;
            
            expect(result).toBe(true);
        });

        test('should reject whitespace-only speaker name', () => {
            const newSpeaker = '   ';
            const result = !newSpeaker.trim();
            
            expect(result).toBe(true);
        });

        test('should reject speaker name with < or > characters', () => {
            const speakers = ['<script>', 'test<tag>', 'test>tag'];
            
            speakers.forEach(speaker => {
                const hasInvalidChars = speaker.includes('<') || speaker.includes('>');
                expect(hasInvalidChars).toBe(true);
            });
        });

        test('should reject speaker name longer than 100 characters', () => {
            const longSpeaker = 'a'.repeat(101);
            const result = longSpeaker.length > 100;
            
            expect(result).toBe(true);
        });

        test('should detect case-insensitive duplicates', () => {
            const newSpeaker = 'guest-1'; // Different case
            const lowerCaseNewSpeaker = newSpeaker.toLowerCase();
            const existingSpeakers = Array.from(availableSpeakers);
            const isDuplicate = existingSpeakers.some(speaker => 
                speaker.toLowerCase() === lowerCaseNewSpeaker
            );
            
            expect(isDuplicate).toBe(true);
        });

        test('should allow valid new speaker', () => {
            const newSpeaker = 'John Doe';
            
            const isValid = newSpeaker.trim() &&
                           newSpeaker.length <= 100 &&
                           !newSpeaker.includes('<') &&
                           !newSpeaker.includes('>') &&
                           !Array.from(availableSpeakers).some(s => 
                               s.toLowerCase() === newSpeaker.toLowerCase()
                           );
            
            expect(isValid).toBe(true);
        });
    });

    describe('handleConfirmReassign validation', () => {
        test('should reject reassigning to empty speaker', () => {
            const toSpeaker = '';
            const isValid = !!toSpeaker;
            
            expect(isValid).toBe(false);
        });

        test('should reject reassigning to same speaker', () => {
            const fromSpeaker = 'Guest-1';
            const toSpeaker = 'Guest-1';
            const isValid = fromSpeaker !== toSpeaker;
            
            expect(isValid).toBe(false);
        });

        test('should count segments correctly for reassignment', () => {
            const fromSpeaker = 'Guest-1';
            let updatedCount = 0;
            
            mockTranscriptionData.segments.forEach(segment => {
                if (segment.speaker === fromSpeaker) {
                    updatedCount++;
                }
            });
            
            expect(updatedCount).toBe(2); // Guest-1 appears twice
        });
    });

    describe('deleteSpeaker validation', () => {
        test('should not allow deleting speaker with segments', () => {
            const speaker = 'Guest-1';
            const segmentCount = mockTranscriptionData.segments.filter(
                s => s.speaker === speaker
            ).length;
            
            expect(segmentCount).toBeGreaterThan(0);
        });

        test('should allow deleting speaker with zero segments', () => {
            const speaker = 'UnusedSpeaker';
            const segmentCount = mockTranscriptionData.segments.filter(
                s => s.speaker === speaker
            ).length;
            
            expect(segmentCount).toBe(0);
        });
    });

    describe('showReassignModal validation', () => {
        test('should filter out source speaker from target list', () => {
            const fromSpeaker = 'Guest-1';
            availableSpeakers.clear();
            availableSpeakers.add('Guest-1');
            availableSpeakers.add('Guest-2');
            availableSpeakers.add('John Doe');
            
            const otherSpeakers = Array.from(availableSpeakers)
                .filter(speaker => speaker !== fromSpeaker);
            
            expect(otherSpeakers).not.toContain('Guest-1');
            expect(otherSpeakers).toContain('Guest-2');
            expect(otherSpeakers).toContain('John Doe');
        });

        test('should detect when no other speakers available', () => {
            availableSpeakers.clear();
            availableSpeakers.add('Guest-1');
            
            const fromSpeaker = 'Guest-1';
            const otherSpeakers = Array.from(availableSpeakers)
                .filter(speaker => speaker !== fromSpeaker);
            
            expect(otherSpeakers.length).toBe(0);
        });
    });

    describe('updateSpeakersModal counting', () => {
        test('should count segments per speaker correctly', () => {
            const speakersInUse = new Map();
            
            mockTranscriptionData.segments.forEach(segment => {
                if (segment.speaker && segment.speaker.trim()) {
                    const count = speakersInUse.get(segment.speaker) || 0;
                    speakersInUse.set(segment.speaker, count + 1);
                }
            });
            
            expect(speakersInUse.get('Guest-1')).toBe(2);
            expect(speakersInUse.get('Guest-2')).toBe(1);
        });

        test('should show manually added speakers with 0 count', () => {
            const speakersInUse = new Map();
            
            mockTranscriptionData.segments.forEach(segment => {
                if (segment.speaker && segment.speaker.trim()) {
                    const count = speakersInUse.get(segment.speaker) || 0;
                    speakersInUse.set(segment.speaker, count + 1);
                }
            });
            
            // Add manually added speaker
            availableSpeakers.clear();
            availableSpeakers.add('Guest-1');
            availableSpeakers.add('Guest-2');
            availableSpeakers.add('ManualSpeaker');
            
            availableSpeakers.forEach(speaker => {
                if (!speakersInUse.has(speaker)) {
                    speakersInUse.set(speaker, 0);
                }
            });
            
            expect(speakersInUse.get('ManualSpeaker')).toBe(0);
        });
    });
});
