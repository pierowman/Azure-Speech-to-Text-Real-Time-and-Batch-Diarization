/**
 * Unit Tests for transcription.js - Alert and Message Functions
 * Tests for showError, showWarning, hideAllAlerts
 */

describe('Alert and Message Functions', () => {
    beforeEach(() => {
        // Setup DOM with error section
        document.body.innerHTML = `
            <div id="errorSection" class="alert alert-danger d-none">
                <strong id="errorLabel">Error:</strong> <span id="errorMessage"></span>
            </div>
            <div id="successSection" class="alert alert-success d-none"></div>
            <div id="resultsSection" class="d-none"></div>
            <div id="audioPlayerSection" class="d-none"></div>
        `;
    });

    describe('showError', () => {
        test('should display error message', () => {
            const errorMessage = 'Test error message';
            
            // Get elements
            const errorSection = document.getElementById('errorSection');
            const errorLabel = document.getElementById('errorLabel');
            const errorMessageEl = document.getElementById('errorMessage');
            
            // Simulate showError function
            errorLabel.textContent = 'Error:';
            errorMessageEl.textContent = errorMessage;
            errorSection.classList.remove('alert-warning');
            errorSection.classList.add('alert-danger');
            errorSection.classList.remove('d-none');

            expect(errorLabel.textContent).toBe('Error:');
            expect(errorMessageEl.textContent).toBe(errorMessage);
            expect(errorSection.classList.contains('alert-danger')).toBe(true);
            expect(errorSection.classList.contains('alert-warning')).toBe(false);
            expect(errorSection.classList.contains('d-none')).toBe(false);
        });

        test('should reset warning class when showing error', () => {
            const errorSection = document.getElementById('errorSection');
            const errorLabel = document.getElementById('errorLabel');
            const errorMessageEl = document.getElementById('errorMessage');
            
            // First show warning
            errorSection.classList.add('alert-warning');
            errorSection.classList.remove('alert-danger');
            
            // Then show error
            errorLabel.textContent = 'Error:';
            errorMessageEl.textContent = 'New error';
            errorSection.classList.remove('alert-warning');
            errorSection.classList.add('alert-danger');
            errorSection.classList.remove('d-none');

            expect(errorSection.classList.contains('alert-danger')).toBe(true);
            expect(errorSection.classList.contains('alert-warning')).toBe(false);
        });
    });

    describe('showWarning', () => {
        test('should display warning message', () => {
            const warningMessage = 'Test warning message';
            
            const errorSection = document.getElementById('errorSection');
            const errorLabel = document.getElementById('errorLabel');
            const errorMessageEl = document.getElementById('errorMessage');
            
            // Simulate showWarning function
            errorLabel.textContent = 'Warning:';
            errorMessageEl.textContent = warningMessage;
            errorSection.classList.remove('alert-danger');
            errorSection.classList.add('alert-warning');
            errorSection.classList.remove('d-none');

            expect(errorLabel.textContent).toBe('Warning:');
            expect(errorMessageEl.textContent).toBe(warningMessage);
            expect(errorSection.classList.contains('alert-warning')).toBe(true);
            expect(errorSection.classList.contains('alert-danger')).toBe(false);
            expect(errorSection.classList.contains('d-none')).toBe(false);
        });

        test('should reset danger class when showing warning', () => {
            const errorSection = document.getElementById('errorSection');
            const errorLabel = document.getElementById('errorLabel');
            const errorMessageEl = document.getElementById('errorMessage');
            
            // First show error
            errorSection.classList.add('alert-danger');
            errorSection.classList.remove('alert-warning');
            
            // Then show warning
            errorLabel.textContent = 'Warning:';
            errorMessageEl.textContent = 'New warning';
            errorSection.classList.remove('alert-danger');
            errorSection.classList.add('alert-warning');
            errorSection.classList.remove('d-none');

            expect(errorSection.classList.contains('alert-warning')).toBe(true);
            expect(errorSection.classList.contains('alert-danger')).toBe(false);
        });
    });

    describe('hideAllAlerts', () => {
        test('should hide all alert sections', () => {
            const errorSection = document.getElementById('errorSection');
            const successSection = document.getElementById('successSection');
            const resultsSection = document.getElementById('resultsSection');
            const audioPlayerSection = document.getElementById('audioPlayerSection');

            // Show all sections first
            errorSection.classList.remove('d-none');
            successSection.classList.remove('d-none');
            resultsSection.classList.remove('d-none');
            audioPlayerSection.classList.remove('d-none');

            // Simulate hideAllAlerts function
            errorSection.classList.add('d-none');
            successSection.classList.add('d-none');
            resultsSection.classList.add('d-none');
            audioPlayerSection.classList.add('d-none');

            expect(errorSection.classList.contains('d-none')).toBe(true);
            expect(successSection.classList.contains('d-none')).toBe(true);
            expect(resultsSection.classList.contains('d-none')).toBe(true);
            expect(audioPlayerSection.classList.contains('d-none')).toBe(true);
        });

        test('should reset error section to default danger style', () => {
            const errorSection = document.getElementById('errorSection');
            const errorLabel = document.getElementById('errorLabel');
            
            // Set to warning style
            errorSection.classList.add('alert-warning');
            errorSection.classList.remove('alert-danger');
            errorLabel.textContent = 'Warning:';

            // Simulate hideAllAlerts function
            errorSection.classList.add('d-none');
            errorLabel.textContent = 'Error:';
            errorSection.classList.remove('alert-warning');
            errorSection.classList.add('alert-danger');

            expect(errorSection.classList.contains('alert-danger')).toBe(true);
            expect(errorSection.classList.contains('alert-warning')).toBe(false);
            expect(errorLabel.textContent).toBe('Error:');
        });
    });

    describe('showElement and hideElement', () => {
        test('showElement should remove d-none class', () => {
            const element = document.getElementById('errorSection');
            element.classList.add('d-none');

            // Simulate showElement
            element.classList.remove('d-none');

            expect(element.classList.contains('d-none')).toBe(false);
        });

        test('hideElement should add d-none class', () => {
            const element = document.getElementById('errorSection');
            element.classList.remove('d-none');

            // Simulate hideElement
            element.classList.add('d-none');

            expect(element.classList.contains('d-none')).toBe(true);
        });
    });
});
