// Quick test to verify batch button is working
// Open browser console (F12) and paste this code

console.log('=== BATCH BUTTON VERIFICATION ===');

// Check if elements exist
const checks = {
    'Batch radio button': document.getElementById('modeBatch'),
    'Real-time radio button': document.getElementById('modeRealTime'),
    'Batch form': document.getElementById('batchForm'),
    'Real-time form': document.getElementById('realTimeForm'),
    'Jobs tab': document.getElementById('jobs-tab'),
    'Jobs tab item': document.getElementById('jobs-tab-item'),
    'loadTranscriptionJobs function': typeof window.loadTranscriptionJobs === 'function',
    'reinitializeJobsTab function': typeof window.reinitializeJobsTab === 'function'
};

console.log('\n?? Element Availability:');
Object.entries(checks).forEach(([name, exists]) => {
    const status = exists ? '?' : '?';
    console.log(`${status} ${name}: ${exists ? 'Found' : 'MISSING'}`);
});

// Check current state
const batchRadio = document.getElementById('modeBatch');
const realtimeRadio = document.getElementById('modeRealTime');
const batchForm = document.getElementById('batchForm');
const realTimeForm = document.getElementById('realTimeForm');
const jobsTabItem = document.getElementById('jobs-tab-item');

console.log('\n?? Current State:');
console.log(`  Mode: ${realtimeRadio?.checked ? 'Real-Time' : (batchRadio?.checked ? 'Batch' : 'Unknown')}`);
console.log(`  Batch form visible: ${batchForm && !batchForm.classList.contains('d-none')}`);
console.log(`  Real-time form visible: ${realTimeForm && !realTimeForm.classList.contains('d-none')}`);
console.log(`  Jobs tab visible: ${jobsTabItem && jobsTabItem.style.display !== 'none'}`);

// Simulate clicking batch button
console.log('\n?? Simulating batch button click...');
if (batchRadio) {
    batchRadio.click();
    
    setTimeout(() => {
        console.log('\n?? After Batch Click:');
        console.log(`  Mode: ${batchRadio.checked ? 'Batch ?' : 'NOT Batch ?'}`);
        console.log(`  Batch form visible: ${!batchForm.classList.contains('d-none') ? 'Yes ?' : 'No ?'}`);
        console.log(`  Real-time form hidden: ${realTimeForm.classList.contains('d-none') ? 'Yes ?' : 'No ?'}`);
        console.log(`  Jobs tab visible: ${jobsTabItem.style.display !== 'none' ? 'Yes ?' : 'No ?'}`);
        
        const activeTab = document.querySelector('.nav-link.active');
        console.log(`  Active tab: ${activeTab?.textContent?.trim() || 'Unknown'}`);
        
        console.log('\n? Batch button test complete!');
        console.log('If all checks show ?, the batch button is working correctly.');
    }, 500);
} else {
    console.error('? Batch radio button not found!');
}
