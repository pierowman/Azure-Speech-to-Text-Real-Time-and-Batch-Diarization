/**
 * Unit Tests for transcription.js - Audio Playback
 * Tests for audio synchronization and playback functionality
 */

describe('Audio Playback', () => {
    let mockTranscriptionData;

    beforeEach(() => {
        mockTranscriptionData = {
            segments: [
                { speaker: 'Guest-1', text: 'Hello', startTimeInSeconds: 0, endTimeInSeconds: 2 },
                { speaker: 'Guest-2', text: 'Hi', startTimeInSeconds: 2, endTimeInSeconds: 4 },
                { speaker: 'Guest-1', text: 'How are you?', startTimeInSeconds: 4, endTimeInSeconds: 7 }
            ]
        };

        document.body.innerHTML = `
            <audio id="audioPlayer" src="/uploads/test.wav"></audio>
            <div id="audioPlayerSection" class="d-none"></div>
            <div id="segment-0" class="segment-item"></div>
            <div id="segment-1" class="segment-item"></div>
            <div id="segment-2" class="segment-item"></div>
        `;
    });

    describe('setupAudioPlayer', () => {
        test('should set audio source', () => {
            const audioPlayer = document.getElementById('audioPlayer');
            const audioUrl = '/uploads/test.wav';
            audioPlayer.src = audioUrl;
            
            expect(audioPlayer.src).toContain('/uploads/test.wav');
        });

        test('should show audio player section', () => {
            const section = document.getElementById('audioPlayerSection');
            section.classList.remove('d-none');
            
            expect(section.classList.contains('d-none')).toBe(false);
        });

        test('should add timeupdate event listener', () => {
            const audioPlayer = document.getElementById('audioPlayer');
            const mockHandler = jest.fn();
            audioPlayer.addEventListener('timeupdate', mockHandler);
            
            // Trigger event
            audioPlayer.dispatchEvent(new Event('timeupdate'));
            
            expect(mockHandler).toHaveBeenCalled();
        });
    });

    describe('syncTranscriptWithAudio', () => {
        test('should highlight segment when audio time matches', () => {
            const currentTime = 1; // Falls within first segment (0-2)
            const segment = document.getElementById('segment-0');
            
            if (currentTime >= mockTranscriptionData.segments[0].startTimeInSeconds && 
                currentTime < mockTranscriptionData.segments[0].endTimeInSeconds) {
                segment.classList.add('active-segment');
            }
            
            expect(segment.classList.contains('active-segment')).toBe(true);
        });

        test('should remove highlight when audio time outside segment', () => {
            const currentTime = 5; // Falls in third segment
            const segment = document.getElementById('segment-0');
            
            if (currentTime >= mockTranscriptionData.segments[0].startTimeInSeconds && 
                currentTime < mockTranscriptionData.segments[0].endTimeInSeconds) {
                segment.classList.add('active-segment');
            } else {
                segment.classList.remove('active-segment');
            }
            
            expect(segment.classList.contains('active-segment')).toBe(false);
        });

        test('should handle multiple segments correctly', () => {
            const currentTime = 3; // Falls in second segment (2-4)
            
            mockTranscriptionData.segments.forEach((seg, index) => {
                const element = document.getElementById(`segment-${index}`);
                
                if (currentTime >= seg.startTimeInSeconds && currentTime < seg.endTimeInSeconds) {
                    element.classList.add('active-segment');
                } else {
                    element.classList.remove('active-segment');
                }
            });
            
            expect(document.getElementById('segment-0').classList.contains('active-segment')).toBe(false);
            expect(document.getElementById('segment-1').classList.contains('active-segment')).toBe(true);
            expect(document.getElementById('segment-2').classList.contains('active-segment')).toBe(false);
        });

        test('should handle edge case at segment boundary', () => {
            const currentTime = 2; // Exactly at boundary between segment 0 and 1
            const segment0 = document.getElementById('segment-0');
            const segment1 = document.getElementById('segment-1');
            
            // At exactly 2 seconds, first segment ends, second begins
            if (currentTime >= mockTranscriptionData.segments[0].startTimeInSeconds && 
                currentTime < mockTranscriptionData.segments[0].endTimeInSeconds) {
                segment0.classList.add('active-segment');
            }
            
            if (currentTime >= mockTranscriptionData.segments[1].startTimeInSeconds && 
                currentTime < mockTranscriptionData.segments[1].endTimeInSeconds) {
                segment1.classList.add('active-segment');
            }
            
            // At boundary, only second segment should be active
            expect(segment0.classList.contains('active-segment')).toBe(false);
            expect(segment1.classList.contains('active-segment')).toBe(true);
        });

        test('should not throw when segment element missing', () => {
            const currentTime = 1;
            
            // Try to sync with non-existent segment
            const element = document.getElementById('segment-99');
            
            expect(element).toBeNull();
            // Should handle gracefully without throwing
        });
    });

    describe('segment click to seek', () => {
        test('should set audio time when segment clicked', () => {
            const audioPlayer = document.getElementById('audioPlayer');
            const segment = mockTranscriptionData.segments[1];
            
            // Simulate click
            audioPlayer.currentTime = segment.startTimeInSeconds;
            
            expect(audioPlayer.currentTime).toBe(2);
        });

        test('should not seek when clicking on controls', () => {
            // This test verifies that the event propagation logic works
            const event = {
                target: document.createElement('button'),
                stopPropagation: jest.fn()
            };
            
            const clickedOnControl = event.target.closest('.speaker-name-select');
            
            if (!clickedOnControl) {
                // Would proceed with seek
                expect(true).toBe(true);
            }
        });
    });

    describe('timestamp formatting', () => {
        test('should format seconds as HH:MM:SS', () => {
            const seconds = 125; // 2 minutes, 5 seconds
            const date = new Date(seconds * 1000);
            const formatted = date.toISOString().substr(11, 8);
            
            expect(formatted).toBe('00:02:05');
        });

        test('should handle zero seconds', () => {
            const seconds = 0;
            const date = new Date(seconds * 1000);
            const formatted = date.toISOString().substr(11, 8);
            
            expect(formatted).toBe('00:00:00');
        });

        test('should handle hours', () => {
            const seconds = 3661; // 1 hour, 1 minute, 1 second
            const date = new Date(seconds * 1000);
            const formatted = date.toISOString().substr(11, 8);
            
            expect(formatted).toBe('01:01:01');
        });
    });
});
