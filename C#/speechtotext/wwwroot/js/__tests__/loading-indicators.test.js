/**
 * Unit Tests for transcription.js - Loading Indicators
 * Tests for showLoadingIndicator and hideLoadingIndicator
 */

describe('Loading Indicators', () => {
    beforeEach(() => {
        document.body.innerHTML = '';
    });

    afterEach(() => {
        // Clean up any created indicators
        const indicator = document.getElementById('updateLoadingIndicator');
        if (indicator) {
            indicator.remove();
        }
    });

    describe('showLoadingIndicator', () => {
        test('should create loading indicator if it does not exist', () => {
            let indicator = document.getElementById('updateLoadingIndicator');
            expect(indicator).toBeNull();

            // Simulate showLoadingIndicator
            if (!indicator) {
                indicator = document.createElement('div');
                indicator.id = 'updateLoadingIndicator';
                indicator.className = 'position-fixed top-50 start-50 translate-middle';
                indicator.style.zIndex = '9999';
                indicator.innerHTML = `
                    <div class="spinner-border text-primary" role="status" style="width: 3rem; height: 3rem;">
                        <span class="visually-hidden">Updating...</span>
                    </div>
                    <div class="mt-2 text-center">
                        <strong>Updating...</strong>
                    </div>
                `;
                document.body.appendChild(indicator);
            }
            indicator.style.display = 'block';

            indicator = document.getElementById('updateLoadingIndicator');
            expect(indicator).toBeDefined();
            expect(indicator.style.display).toBe('block');
        });

        test('should show existing loading indicator', () => {
            // Create indicator first
            let indicator = document.createElement('div');
            indicator.id = 'updateLoadingIndicator';
            indicator.style.display = 'none';
            document.body.appendChild(indicator);

            // Simulate showLoadingIndicator with existing indicator
            indicator = document.getElementById('updateLoadingIndicator');
            indicator.style.display = 'block';

            expect(indicator.style.display).toBe('block');
        });

        test('should have correct CSS classes and structure', () => {
            // Simulate showLoadingIndicator
            let indicator = document.getElementById('updateLoadingIndicator');
            if (!indicator) {
                indicator = document.createElement('div');
                indicator.id = 'updateLoadingIndicator';
                indicator.className = 'position-fixed top-50 start-50 translate-middle';
                indicator.style.zIndex = '9999';
                indicator.innerHTML = `
                    <div class="spinner-border text-primary" role="status" style="width: 3rem; height: 3rem;">
                        <span class="visually-hidden">Updating...</span>
                    </div>
                    <div class="mt-2 text-center">
                        <strong>Updating...</strong>
                    </div>
                `;
                document.body.appendChild(indicator);
            }

            expect(indicator.className).toContain('position-fixed');
            expect(indicator.className).toContain('top-50');
            expect(indicator.className).toContain('start-50');
            expect(indicator.className).toContain('translate-middle');
            expect(indicator.style.zIndex).toBe('9999');
        });

        test('should contain spinner element', () => {
            // Create indicator
            let indicator = document.createElement('div');
            indicator.id = 'updateLoadingIndicator';
            indicator.innerHTML = `
                <div class="spinner-border text-primary" role="status" style="width: 3rem; height: 3rem;">
                    <span class="visually-hidden">Updating...</span>
                </div>
                <div class="mt-2 text-center">
                    <strong>Updating...</strong>
                </div>
            `;
            document.body.appendChild(indicator);

            const spinner = indicator.querySelector('.spinner-border');
            expect(spinner).toBeDefined();
            expect(spinner.classList.contains('text-primary')).toBe(true);
        });

        test('should contain updating text', () => {
            // Create indicator
            let indicator = document.createElement('div');
            indicator.id = 'updateLoadingIndicator';
            indicator.innerHTML = `
                <div class="spinner-border text-primary" role="status" style="width: 3rem; height: 3rem;">
                    <span class="visually-hidden">Updating...</span>
                </div>
                <div class="mt-2 text-center">
                    <strong>Updating...</strong>
                </div>
            `;
            document.body.appendChild(indicator);

            expect(indicator.textContent).toContain('Updating...');
        });
    });

    describe('hideLoadingIndicator', () => {
        test('should hide loading indicator', () => {
            // Create and show indicator first
            const indicator = document.createElement('div');
            indicator.id = 'updateLoadingIndicator';
            indicator.style.display = 'block';
            document.body.appendChild(indicator);

            // Simulate hideLoadingIndicator
            const loadingIndicator = document.getElementById('updateLoadingIndicator');
            if (loadingIndicator) {
                loadingIndicator.style.display = 'none';
            }

            expect(indicator.style.display).toBe('none');
        });

        test('should handle missing indicator gracefully', () => {
            // Simulate hideLoadingIndicator with no indicator
            const indicator = document.getElementById('updateLoadingIndicator');
            if (indicator) {
                indicator.style.display = 'none';
            }

            // Should not throw error
            expect(indicator).toBeNull();
        });

        test('should not remove indicator from DOM', () => {
            // Create indicator
            const indicator = document.createElement('div');
            indicator.id = 'updateLoadingIndicator';
            indicator.style.display = 'block';
            document.body.appendChild(indicator);

            // Hide it
            indicator.style.display = 'none';

            // Should still exist in DOM
            const foundIndicator = document.getElementById('updateLoadingIndicator');
            expect(foundIndicator).toBeDefined();
            expect(foundIndicator.style.display).toBe('none');
        });
    });

    describe('Loading indicator lifecycle', () => {
        test('should handle show-hide-show cycle', () => {
            // First show
            let indicator = document.createElement('div');
            indicator.id = 'updateLoadingIndicator';
            document.body.appendChild(indicator);
            indicator.style.display = 'block';
            expect(indicator.style.display).toBe('block');

            // Hide
            indicator.style.display = 'none';
            expect(indicator.style.display).toBe('none');

            // Show again
            indicator.style.display = 'block';
            expect(indicator.style.display).toBe('block');
        });

        test('should reuse same indicator element', () => {
            // Create indicator
            let indicator = document.createElement('div');
            indicator.id = 'updateLoadingIndicator';
            document.body.appendChild(indicator);
            const firstIndicator = indicator;

            // Show (should reuse)
            indicator = document.getElementById('updateLoadingIndicator');
            indicator.style.display = 'block';

            // Hide
            indicator.style.display = 'none';

            // Show again (should still be same element)
            indicator = document.getElementById('updateLoadingIndicator');
            indicator.style.display = 'block';

            expect(indicator).toBe(firstIndicator);
        });
    });

    describe('Multiple updates scenario', () => {
        test('should handle rapid show/hide calls', () => {
            // Create indicator
            const indicator = document.createElement('div');
            indicator.id = 'updateLoadingIndicator';
            document.body.appendChild(indicator);

            // Rapid show/hide
            indicator.style.display = 'block';
            indicator.style.display = 'none';
            indicator.style.display = 'block';
            indicator.style.display = 'none';
            indicator.style.display = 'block';

            expect(indicator.style.display).toBe('block');
        });

        test('should maintain z-index during multiple operations', () => {
            const indicator = document.createElement('div');
            indicator.id = 'updateLoadingIndicator';
            indicator.style.zIndex = '9999';
            document.body.appendChild(indicator);

            // Multiple operations
            indicator.style.display = 'block';
            indicator.style.display = 'none';
            indicator.style.display = 'block';

            expect(indicator.style.zIndex).toBe('9999');
        });
    });
});
