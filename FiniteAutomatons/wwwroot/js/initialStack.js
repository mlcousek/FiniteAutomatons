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
        const charArray = symbols.map(s => s.charAt(0));
        hiddenField.value = JSON.stringify(charArray);
    } catch (e) {
        console.error('Failed to serialize initial stack:', e);
    }
}

document.addEventListener('DOMContentLoaded', function() {
    const displayField = document.getElementById('initialStackDisplayField');
    if (displayField && displayField.value) {
        updateInitialStackSerialized();
    }
});
