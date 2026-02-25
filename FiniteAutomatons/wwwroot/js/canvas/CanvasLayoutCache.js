/**
 * CanvasLayoutCache.js
 * Persists Cytoscape node positions to localStorage so that canvas layouts
 * survive page reloads. Each automaton's layout is keyed by a stable
 * "fingerprint" derived from its type and sorted state IDs.
 *
 * Usage:
 *   import { CanvasLayoutCache } from './CanvasLayoutCache.js';
 *
 *   const fingerprint = CanvasLayoutCache.buildFingerprint('DFA', [0, 1, 2]);
 *   CanvasLayoutCache.save(fingerprint, cy); // save all node positions
 *   const positions = CanvasLayoutCache.load(fingerprint); // returns Map or null
 *   CanvasLayoutCache.clear(fingerprint); // remove cache entry
 *
 * @module CanvasLayoutCache
 */

const STORAGE_PREFIX = 'fa-canvas-layout:';
const MAX_ENTRIES = 50; // guard against unbounded localStorage growth

/**
 * Build a stable fingerprint string for an automaton.
 * The fingerprint is independent of node display order so the same logical
 * automaton always maps to the same key.
 *
 * @param {string} automatonType  - e.g. 'DFA', 'NFA', 'EpsilonNFA', 'PDA'
 * @param {number[]} stateIds     - array of state IDs (any order)
 * @returns {string} fingerprint
 */
function buildFingerprint(automatonType, stateIds) {
    const sorted = [...stateIds].sort((a, b) => a - b).join(',');
    return `${automatonType}-[${sorted}]`;
}

/**
 * Capture all node positions from a Cytoscape instance into a plain object.
 * Only real state nodes (not dummy/helper nodes) are captured.
 *
 * @param {Object} cy - Cytoscape instance
 * @returns {{ [nodeId: string]: { x: number, y: number } }}
 */
function capturePositions(cy) {
    const positions = {};
    cy.nodes().forEach(node => {
        if (node.hasClass('dummy')) return; // skip helper nodes
        const pos = node.position();
        positions[node.id()] = { x: Math.round(pos.x), y: Math.round(pos.y) };
    });
    return positions;
}

/**
 * Apply a saved position map back to a Cytoscape instance.
 * Nodes missing from the saved map are left at their computed positions.
 *
 * @param {Object} cy - Cytoscape instance
 * @param {{ [nodeId: string]: { x: number, y: number } }} positions - saved positions
 * @returns {boolean} true if at least one position was applied
 */
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

/**
 * Save node positions for the given fingerprint.
 * Old entries beyond MAX_ENTRIES are evicted (LRU-lite: oldest key removed).
 *
 * @param {string} fingerprint - key returned by buildFingerprint()
 * @param {Object} cy          - Cytoscape instance
 */
function save(fingerprint, cy) {
    if (!fingerprint || !cy) return;

    try {
        const positions = capturePositions(cy);
        if (Object.keys(positions).length === 0) return;

        // Enforce max entry count (simple FIFO eviction)
        const allKeys = Object.keys(localStorage)
            .filter(k => k.startsWith(STORAGE_PREFIX));
        if (allKeys.length >= MAX_ENTRIES) {
            // Remove the oldest one (arbitrary but deterministic)
            localStorage.removeItem(allKeys.sort()[0]);
        }

        const key = STORAGE_PREFIX + fingerprint;
        localStorage.setItem(key, JSON.stringify({
            savedAt: Date.now(),
            positions
        }));
    } catch (e) {
        // localStorage may be disabled or full — silently ignore
        console.warn('CanvasLayoutCache: could not save layout:', e);
    }
}

/**
 * Load saved positions for the given fingerprint.
 *
 * @param {string} fingerprint
 * @returns {{ [nodeId: string]: { x: number, y: number } } | null}
 */
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

/**
 * Remove the cached layout for the given fingerprint.
 *
 * @param {string} fingerprint
 */
function clear(fingerprint) {
    if (!fingerprint) return;
    try {
        localStorage.removeItem(STORAGE_PREFIX + fingerprint);
    } catch (e) {
        // ignore
    }
}

/**
 * Remove ALL cached layouts (e.g. for a "clear cache" button).
 */
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

// Expose to window for non-module contexts (e.g. testing from browser console)
if (typeof window !== 'undefined') {
    window.CanvasLayoutCache = CanvasLayoutCache;
}
