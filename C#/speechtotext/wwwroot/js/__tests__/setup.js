// Test setup file
import '@testing-library/jest-dom';

// Mock Bootstrap Modal
global.bootstrap = {
    Modal: class {
        constructor(element) {
            this.element = element;
        }
        show() {
            this.element.style.display = 'block';
        }
        hide() {
            this.element.style.display = 'none';
        }
    }
};

// Mock fetch globally
global.fetch = jest.fn();

// Mock console methods to reduce noise in tests
global.console = {
    ...console,
    error: jest.fn(),
    log: jest.fn(),
    warn: jest.fn(),
};

// Mock alert
global.alert = jest.fn();

// Mock confirm
global.confirm = jest.fn(() => true);

// Setup DOM
beforeEach(() => {
    document.body.innerHTML = '';
    jest.clearAllMocks();
});
