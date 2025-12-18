// Locale Manager Module
import { AppState } from './app.js';
import { getSupportedLocales } from './api-client.js';

export async function loadSupportedLocales() {
    if (AppState.localesLoaded) {
        return;
    }
    
    try {
        const locales = await getSupportedLocales();
        AppState.supportedLocales = locales.locales || [];
        AppState.localesLoaded = true;
        
        populateLocaleDropdown('batchLocale', locales.locales);
        populateLocaleDropdown('realtimeLocale', locales.locales);
        
        console.log(`Loaded ${locales.locales.length} supported locales`);
    } catch (error) {
        console.error('Failed to load supported locales:', error);
        AppState.supportedLocales = [];
        AppState.localesLoaded = false;
    }
}

function populateLocaleDropdown(selectId, locales) {
    const select = document.getElementById(selectId);
    if (!select) return;
    
    const currentValue = select.value;
    const defaultValue = window.DEFAULT_LOCALE || 'en-US';
    
    select.innerHTML = '';
    
    locales.forEach(locale => {
        const option = document.createElement('option');
        option.value = locale.code;  // Backend uses 'code' not 'locale'
        option.textContent = getLocaleFriendlyName(locale);
        select.appendChild(option);
    });
    
    if (currentValue && locales.some(l => l.code === currentValue)) {
        select.value = currentValue;
    } else if (locales.some(l => l.code === defaultValue)) {
        select.value = defaultValue;
    }
}

export function getLocaleFriendlyName(localeObj) {
    if (typeof localeObj === 'string') {
        return localeObj;
    }
    
    const locale = localeObj.code || 'Unknown';  // Backend uses 'code' not 'locale'
    const displayName = localeObj.name || locale;  // Backend uses 'name' not 'displayName'
    
    return `${displayName} (${locale})`;
}
