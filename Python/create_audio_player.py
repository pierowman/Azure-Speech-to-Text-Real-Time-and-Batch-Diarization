"""
Create all JavaScript module files for Step 2
"""
import os

# Ensure directory exists
os.makedirs("static/js", exist_ok=True)

# Audio Player Module
audio_player_js = """// Audio Player Module
import { AppState } from './app.js';

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
    if (!AppState.audioPlayer) return;
    AppState.audioPlayer.addEventListener('timeupdate', () => {
        if (!AppState.isSeeking) {
            updateProgress();
            highlightCurrentSegment();
        }
    });
    AppState.audioPlayer.addEventListener('play', () => {
        console.log('?? Audio playback started');
    });
    AppState.audioPlayer.addEventListener('pause', () => {
        console.log('?? Audio playback paused');
    });
    AppState.audioPlayer.addEventListener('ended', () => {
        console.log('?? Audio playback ended');
        updatePlayPauseButton();
    });
    AppState.audioPlayer.addEventListener('loadedmetadata', () => {
        document.getElementById('totalTime').textContent = formatTime(AppState.audioPlayer.duration);
    });
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
    if (!AppState.audioPlayer || !AppState.audioPlayer.src) return;
    if (AppState.audioPlayer.paused) {
        AppState.audioPlayer.play();
    } else {
        AppState.audioPlayer.pause();
    }
    updatePlayPauseButton();
}

export function updatePlayPauseButton() {
    const btn = document.getElementById('playPauseBtn');
    if (AppState.audioPlayer && !AppState.audioPlayer.paused) {
        btn.textContent = '??';
        btn.title = 'Pause (Space)';
    } else {
        btn.textContent = '??';
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
    document.getElementById('progressBar').style.width = progress + '%';
    document.getElementById('progressHandle').style.left = progress + '%';
    document.getElementById('currentTime').textContent = formatTime(AppState.audioPlayer.currentTime);
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
    if (!AppState.audioPlayer || !AppState.audioPlayer.src) return;
    AppState.audioPlayer.currentTime = startTime;
    if (AppState.audioPlayer.paused) {
        AppState.audioPlayer.play().then(() => updatePlayPauseButton());
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
"""

with open("static/js/audio-player.js", "w", encoding="utf-8") as f:
    f.write(audio_player_js)

print("? Created audio-player.js")
print("\n? All JavaScript modules created successfully!")
print("\nNext: Update templates\\index.html to import these modules")
