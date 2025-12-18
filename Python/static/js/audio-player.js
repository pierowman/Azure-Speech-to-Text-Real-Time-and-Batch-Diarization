// Audio Player Module
import { AppState } from './app.js';

// Flag to prevent multiple event listener registrations
let audioEventsSetup = false;

export function formatTime(seconds) {
    if (isNaN(seconds)) return '0:00';
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const secs = Math.floor(seconds % 60);
    if (hours > 0) {
        return `${hours}:${minutes.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
    }
    return `${minutes}:${secs.toString().padStart(2, '0')}`;
}

export function setupAudioPlayerEvents() {
    if (!AppState.audioPlayer) {
        console.error('? Audio player element not found');
        return;
    }
    
    if (audioEventsSetup) {
        console.log('?? Audio events already set up, skipping...');
        return;
    }
    
    AppState.audioPlayer.addEventListener('timeupdate', () => {
        if (!AppState.isSeeking) {
            updateProgress();
            highlightCurrentSegment();
        }
    });
    
    AppState.audioPlayer.addEventListener('play', () => {
        console.log('?? Audio playback started');
        updatePlayPauseButton();
    });
    
    AppState.audioPlayer.addEventListener('pause', () => {
        console.log('?? Audio playback paused');
        updatePlayPauseButton();
    });
    
    AppState.audioPlayer.addEventListener('ended', () => {
        console.log('?? Audio playback ended');
        updatePlayPauseButton();
    });
    
    AppState.audioPlayer.addEventListener('loadedmetadata', () => {
        const totalTimeEl = document.getElementById('totalTime');
        if (totalTimeEl) {
            totalTimeEl.textContent = formatTime(AppState.audioPlayer.duration);
        }
        console.log('?? Audio metadata loaded - Duration:', formatTime(AppState.audioPlayer.duration));
    });
    
    AppState.audioPlayer.addEventListener('error', (e) => {
        console.error('? Audio playback error:', e);
        console.error('Error details:', AppState.audioPlayer.error);
    });
    
    audioEventsSetup = true;
    console.log('? Audio player events configured');
}

export function setupKeyboardShortcuts() {
    document.addEventListener('keydown', (e) => {
        if (AppState.currentTab !== 'realtime') return;
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
        if (e.target.isContentEditable) return;
        if (AppState.editingSegmentIndex !== null) return;
        switch(e.key) {
            case ' ': e.preventDefault(); togglePlayPause(); break;
            case 'ArrowLeft': e.preventDefault(); skipBackward(); break;
            case 'ArrowRight': e.preventDefault(); skipForward(); break;
        }
    });
}

export function togglePlayPause() {
    if (!AppState.audioPlayer || !AppState.audioPlayer.src) {
        console.warn('?? No audio loaded');
        return;
    }
    if (AppState.audioPlayer.paused) {
        AppState.audioPlayer.play().catch(e => {
            console.error('? Failed to play audio:', e);
        });
    } else {
        AppState.audioPlayer.pause();
    }
}

export function updatePlayPauseButton() {
    const btn = document.getElementById('playPauseBtn');
    if (!btn) return;
    
    if (AppState.audioPlayer && !AppState.audioPlayer.paused) {
        btn.textContent = '\u23F8\uFE0F';  // ?? Pause
        btn.title = 'Pause (Space)';
    } else {
        btn.textContent = '\u25B6\uFE0F';  // ?? Play
        btn.title = 'Play (Space)';
    }
}

export function skipBackward() {
    if (!AppState.audioPlayer) return;
    AppState.audioPlayer.currentTime = Math.max(0, AppState.audioPlayer.currentTime - 10);
}

export function skipForward() {
    if (!AppState.audioPlayer) return;
    AppState.audioPlayer.currentTime = Math.min(AppState.audioPlayer.duration, AppState.audioPlayer.currentTime + 10);
}

export function setPlaybackSpeed(speed) {
    if (!AppState.audioPlayer) return;
    AppState.audioPlayer.playbackRate = speed;
    document.querySelectorAll('.speed-btn').forEach(btn => btn.classList.remove('active'));
    event.target.classList.add('active');
}

export function setVolume(value) {
    if (!AppState.audioPlayer) return;
    AppState.audioPlayer.volume = value / 100;
}

export function updateProgress() {
    if (!AppState.audioPlayer || AppState.audioPlayer.duration === 0) return;
    const progress = (AppState.audioPlayer.currentTime / AppState.audioPlayer.duration) * 100;
    const progressBar = document.getElementById('progressBar');
    const progressHandle = document.getElementById('progressHandle');
    const currentTimeEl = document.getElementById('currentTime');
    
    if (progressBar) progressBar.style.width = progress + '%';
    if (progressHandle) progressHandle.style.left = progress + '%';
    if (currentTimeEl) currentTimeEl.textContent = formatTime(AppState.audioPlayer.currentTime);
}

export function startSeeking(e) {
    if (!AppState.audioPlayer || !AppState.audioPlayer.src) return;
    AppState.isSeeking = true;
    const progressContainer = document.getElementById('progressContainer');
    function seek(event) {
        const rect = progressContainer.getBoundingClientRect();
        const x = (event.clientX || event.touches[0].clientX) - rect.left;
        const percentage = Math.max(0, Math.min(1, x / rect.width));
        AppState.audioPlayer.currentTime = percentage * AppState.audioPlayer.duration;
        updateProgress();
    }
    function stopSeeking() {
        AppState.isSeeking = false;
        document.removeEventListener('mousemove', seek);
        document.removeEventListener('mouseup', stopSeeking);
        document.removeEventListener('touchmove', seek);
        document.removeEventListener('touchend', stopSeeking);
    }
    seek(e);
    document.addEventListener('mousemove', seek);
    document.addEventListener('mouseup', stopSeeking);
    document.addEventListener('touchmove', seek);
    document.addEventListener('touchend', stopSeeking);
}

export function playSegment(startTime) {
    if (!AppState.audioPlayer || !AppState.audioPlayer.src) {
        console.warn('?? Cannot play segment - no audio loaded');
        return;
    }
    
    console.log(`? Playing from ${formatTime(startTime)}`);
    AppState.audioPlayer.currentTime = startTime;
    
    if (AppState.audioPlayer.paused) {
        AppState.audioPlayer.play().then(() => {
            updatePlayPauseButton();
        }).catch(e => {
            console.error('? Failed to play audio:', e);
        });
    }
    updateProgress();
}

export function highlightCurrentSegment() {
    if (!AppState.transcriptionData || !AppState.transcriptionData.segments || !AppState.audioPlayer) return;
    
    const currentTime = AppState.audioPlayer.currentTime;
    let foundSegment = null;
    
    for (let segment of AppState.transcriptionData.segments) {
        const start = segment.startTimeInSeconds || 0;
        const end = segment.endTimeInSeconds || 0;
        if (currentTime >= start && currentTime < end) {
            foundSegment = segment;
            break;
        }
    }
    
    if (foundSegment && foundSegment !== AppState.currentActiveSegment) {
        document.querySelectorAll('.segment').forEach(el => el.classList.remove('active'));
        const segmentIndex = AppState.transcriptionData.segments.indexOf(foundSegment);
        const segmentElement = document.querySelectorAll('.segment')[segmentIndex];
        if (segmentElement) {
            segmentElement.classList.add('active');
            segmentElement.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }
        AppState.currentActiveSegment = foundSegment;
    } else if (!foundSegment && AppState.currentActiveSegment) {
        document.querySelectorAll('.segment').forEach(el => el.classList.remove('active'));
        AppState.currentActiveSegment = null;
    }
}

window.togglePlayPause = togglePlayPause;
window.skipBackward = skipBackward;
window.skipForward = skipForward;
window.setPlaybackSpeed = setPlaybackSpeed;
window.setVolume = setVolume;
window.startSeeking = startSeeking;
window.playSegment = playSegment;
