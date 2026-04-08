// Helper to convert comma-separated initial stack display to JSON serialization
// and update hidden field `initialStackSerialized`.

export function updateInitialStackSerialized() {
    const displayField = document.getElementById('initialStackDisplayField');
    const hiddenField = document.getElementById('initialStackSerialized');

    if (!displayField || !hiddenField) return;

    const value = displayField.value.trim();
    if (!value) {
        hiddenField.value = '';
        return;
    }

    const symbols = value.split(',').map(s => s.trim()).filter(s => s.length > 0);

    try {
        const charArray = [];
        for (const symbol of symbols) {
            // Accept both "A,B,C" and compact tokens like "ABC".
            // Stack alphabet is character-based, so expand each token to chars.
            const compact = symbol.replace(/\s+/g, '');
            for (const ch of compact) {
                charArray.push(ch);
            }
        }

        if (charArray.length === 0) {
            hiddenField.value = '';
            return;
        }

        if (charArray.length > 0 && charArray[0] !== '#') {
            charArray.unshift('#');
        }
        hiddenField.value = JSON.stringify(charArray);
    } catch (e) {
        console.error('Failed to serialize initial stack:', e);
    }
}

// Inline oninput handlers in Razor execute in global scope.
if (typeof window !== 'undefined') {
    window.updateInitialStackSerialized = updateInitialStackSerialized;
}

document.addEventListener('DOMContentLoaded', function() {
    const displayField = document.getElementById('initialStackDisplayField');
    if (!displayField) return;

    displayField.addEventListener('input', updateInitialStackSerialized);

    // Normalize hidden value on first render even when pre-filled.
    if (displayField.value) {
        updateInitialStackSerialized();
    }
});
