// Quick test to verify Jobs tab shows in batch mode
// Open browser console (F12) and paste this code

console.log('=== JOBS TAB VISIBILITY TEST ===');

// Check initial state
const elements = {
    batchBtn: document.getElementById('modeBatch'),
    resultsSection: document.getElementById('resultsSection'),
    jobsTabItem: document.getElementById('jobs-tab-item'),
    jobsTab: document.getElementById('jobs-tab')
};

console.log('\n?? Elements Found:');
Object.entries(elements).forEach(([name, el]) => {
    console.log(`${el ? '?' : '?'} ${name}: ${el ? 'Found' : 'MISSING'}`);
});

// Check initial visibility
console.log('\n?? Initial State:');
console.log(`  Results section visible: ${elements.resultsSection && !elements.resultsSection.classList.contains('d-none')}`);
console.log(`  Jobs tab item visible: ${elements.jobsTabItem && elements.jobsTabItem.style.display !== 'none'}`);

// Click batch button
if (elements.batchBtn) {
    console.log('\n?? Clicking Batch button...');
    elements.batchBtn.click();
    
    setTimeout(() => {
        console.log('\n?? After Batch Click:');
        
        const resultsVisible = elements.resultsSection && !elements.resultsSection.classList.contains('d-none');
        const jobsTabVisible = elements.jobsTabItem && elements.jobsTabItem.style.display !== 'none';
        const jobsTabActive = elements.jobsTab && elements.jobsTab.classList.contains('active');
        
        console.log(`  Results section visible: ${resultsVisible ? '? YES' : '? NO'}`);
        console.log(`  Jobs tab item visible: ${jobsTabVisible ? '? YES' : '? NO'}`);
        console.log(`  Jobs tab active: ${jobsTabActive ? '? YES' : '? NO'}`);
        
        // Check if jobs are loading
        const jobsList = document.getElementById('jobsList');
        const jobsLoading = document.getElementById('jobsLoadingSection');
        console.log(`  Jobs loading: ${jobsLoading && !jobsLoading.classList.contains('d-none') ? '? Yes' : '? No'}`);
        console.log(`  Jobs list populated: ${jobsList && jobsList.children.length > 0 ? '? Yes' : '? Loading...'}`);
        
        // Overall result
        const allGood = resultsVisible && jobsTabVisible && jobsTabActive;
        console.log(`\n${allGood ? '? SUCCESS' : '? FAILED'}: Jobs tab ${allGood ? 'is' : 'is NOT'} showing correctly!`);
        
        if (!allGood) {
            console.error('\n?? Debugging Info:');
            console.error('  Results section classes:', elements.resultsSection?.className);
            console.error('  Jobs tab item display:', elements.jobsTabItem?.style.display);
            console.error('  Jobs tab classes:', elements.jobsTab?.className);
        }
    }, 1000);
} else {
    console.error('? Batch button not found!');
}
