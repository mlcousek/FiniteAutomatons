const STORAGE_PREFIX = 'fa-canvas-layout:';
const MAX_ENTRIES = 50; 

function buildFingerprint(automatonType, stateIds) {
    const sorted = [...stateIds].sort((a, b) => a - b).join(',');
    return `${automatonType}-[${sorted}]`;
}

function capturePositions(cy) {
    const positions = {};
    cy.nodes().forEach(node => {
        if (node.hasClass('dummy')) return; // skip helper nodes
        const pos = node.position();
        positions[node.id()] = { x: Math.round(pos.x), y: Math.round(pos.y) };
    });
    return positions;
}

function applyPositions(cy, positions) {
    if (!positions || typeof positions !== 'object') return false;

    let applied = 0;
    cy.nodes().forEach(node => {
        if (node.hasClass('dummy')) return;
        const saved = positions[node.id()];
        if (saved && typeof saved.x === 'number' && typeof saved.y === 'number') {
            node.position({ x: saved.x, y: saved.y });
            applied++;
        }
    });
    return applied > 0;
}

function save(fingerprint, cy) {
    if (!fingerprint || !cy) return;

    try {
        const positions = capturePositions(cy);
        if (Object.keys(positions).length === 0) return;

        const allKeys = Object.keys(localStorage)
            .filter(k => k.startsWith(STORAGE_PREFIX));
        if (allKeys.length >= MAX_ENTRIES) {
            localStorage.removeItem(allKeys.sort()[0]);
        }

        const key = STORAGE_PREFIX + fingerprint;
        localStorage.setItem(key, JSON.stringify({
            savedAt: Date.now(),
            positions
        }));
    } catch (e) {
        console.warn('CanvasLayoutCache: could not save layout:', e);
    }
}

function load(fingerprint) {
    if (!fingerprint) return null;

    try {
        const raw = localStorage.getItem(STORAGE_PREFIX + fingerprint);
        if (!raw) return null;

        const parsed = JSON.parse(raw);
        if (parsed && parsed.positions && typeof parsed.positions === 'object') {
            return parsed.positions;
        }
    } catch (e) {
        console.warn('CanvasLayoutCache: could not load layout:', e);
    }
    return null;
}

function clear(fingerprint) {
    if (!fingerprint) return;
    try {
        localStorage.removeItem(STORAGE_PREFIX + fingerprint);
    } catch (e) {
        // ignore
    }
}

function clearAll() {
    try {
        const allKeys = Object.keys(localStorage)
            .filter(k => k.startsWith(STORAGE_PREFIX));
        allKeys.forEach(k => localStorage.removeItem(k));
    } catch (e) {
        // ignore
    }
}

export const CanvasLayoutCache = {
    buildFingerprint,
    capturePositions,
    applyPositions,
    save,
    load,
    clear,
    clearAll
};

if (typeof window !== 'undefined') {
    window.CanvasLayoutCache = CanvasLayoutCache;
}
