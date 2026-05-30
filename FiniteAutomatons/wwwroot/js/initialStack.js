// Helper to convert comma-separated initial stack display (top-first) to the
// JSON serialization used by the hidden field `initialStackSerialized`.
// Internal storage convention: bottom-first (index 0 = bottom, last = top).
// The user enters symbols top-first so we reverse before storing.

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

        // User enters top-first; internal storage is bottom-first → reverse.
        const bottomFirst = [...charArray].reverse();
        hiddenField.value = JSON.stringify(bottomFirst);
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
