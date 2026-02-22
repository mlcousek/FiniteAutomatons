/**
 * AutomatonRenderer.js
 * Handles rendering logic for different automaton types (DFA, NFA, ε-NFA, PDA)
 * Provides Cytoscape stylesheets and element creation
 * 
 * @module AutomatonRenderer
 */

/**
 * Static class for rendering automatons
 */
export class AutomatonRenderer {
    /**
     * Main render method - dispatches to type-specific renderer
     * @param {Object} cy - Cytoscape instance
     * @param {Object} data - Automaton data
     */
    static render(cy, data) {
        if (!cy || !data) {
            throw new Error('Cytoscape instance and data are required');
        }

        const { type, states, transitions } = data;

        switch (type?.toUpperCase()) {
            case 'DFA':
                this.renderDFA(cy, states, transitions);
                break;
            case 'NFA':
                this.renderNFA(cy, states, transitions);
                break;
            case 'EPSILONNFA':
            case 'EPSILON-NFA':
            case 'ε-NFA':
                this.renderEpsilonNFA(cy, states, transitions);
                break;
            case 'PDA':
                this.renderPDA(cy, states, transitions);
                break;
            default:
                console.warn(`Unknown automaton type: ${type}, using generic renderer`);
                this.renderGeneric(cy, states, transitions);
        }
    }

    /**
     * Render DFA (Deterministic Finite Automaton)
     * @param {Object} cy - Cytoscape instance
     * @param {Array} states - State objects
     * @param {Array} transitions - Transition objects
     */
    static renderDFA(cy, states, transitions) {
        this._renderStates(cy, states);
        this._renderTransitions(cy, transitions, false, false);
    }

    /**
     * Render NFA (Nondeterministic Finite Automaton)
     * @param {Object} cy - Cytoscape instance
     * @param {Array} states - State objects
     * @param {Array} transitions - Transition objects
     */
    static renderNFA(cy, states, transitions) {
        this._renderStates(cy, states);
        this._renderTransitions(cy, transitions, false, false);
    }

    /**
     * Render ε-NFA (Epsilon-Nondeterministic Finite Automaton)
     * @param {Object} cy - Cytoscape instance
     * @param {Array} states - State objects
     * @param {Array} transitions - Transition objects
     */
    static renderEpsilonNFA(cy, states, transitions) {
        this._renderStates(cy, states);
        this._renderTransitions(cy, transitions, true, false);
    }

    /**
     * Render PDA (Pushdown Automaton)
     * @param {Object} cy - Cytoscape instance
     * @param {Array} states - State objects
     * @param {Array} transitions - Transition objects
     */
    static renderPDA(cy, states, transitions) {
        this._renderStates(cy, states);
        this._renderTransitions(cy, transitions, true, true);
    }

    /**
     * Generic renderer (fallback)
     * @param {Object} cy - Cytoscape instance
     * @param {Array} states - State objects
     * @param {Array} transitions - Transition objects
     */
    static renderGeneric(cy, states, transitions) {
        this._renderStates(cy, states);
        this._renderTransitions(cy, transitions, true, false);
    }

    /**
     * Private: Render state nodes
     * @private
     * @param {Object} cy - Cytoscape instance
     * @param {Array} states - State objects with {id, isStart, isAccepting}
     */
    static _renderStates(cy, states) {
        if (!states || states.length === 0) return;

        const nodes = states.map(state => ({
            group: 'nodes',
            data: {
                id: `state-${state.id}`,
                stateId: state.id,
                label: `q${state.id}`,
                isStart: state.isStart || false,
                isAccepting: state.isAccepting || false
            },
            classes: [
                state.isStart ? 'start' : '',
                state.isAccepting ? 'accepting' : ''
            ].filter(Boolean).join(' ')
        }));

        cy.add(nodes);
    }

    /**
     * Private: Render transition edges
     * @private
     * @param {Object} cy - Cytoscape instance
     * @param {Array} transitions - Transition objects
     * @param {boolean} allowEpsilon - Whether epsilon transitions are allowed
     * @param {boolean} isPDA - Whether this is a PDA (includes stack operations)
     */
    static _renderTransitions(cy, transitions, allowEpsilon, isPDA) {
        if (!transitions || transitions.length === 0) return;

        // Group transitions by from-to pair to handle multiple symbols
        const transitionMap = new Map();

        transitions.forEach(trans => {
            const key = `${trans.fromStateId}-${trans.toStateId}`;
            if (!transitionMap.has(key)) {
                transitionMap.set(key, []);
            }
            transitionMap.get(key).push(trans);
        });

        const edges = [];

        transitionMap.forEach((transList, key) => {
            const [fromId, toId] = key.split('-').map(Number);
            const firstTrans = transList[0];

            // Create label based on automaton type
            let label;
            let classes = [];

            if (isPDA) {
                // PDA: Format as "symbol, pop/push" (deduplicated)
                const pdaLabels = transList.map(t => {
                    const symbol = this._formatSymbol(t.symbol, allowEpsilon);
                    const pop = this._formatSymbol(t.stackPop, true) || 'ε';
                    const push = t.stackPush || 'ε';
                    return `${symbol}, ${pop}/${push}`;
                });
                // Remove duplicates using Set
                const uniqueLabels = [...new Set(pdaLabels)];
                label = uniqueLabels.join('\n');
                classes.push('pda');
            } else {
                // DFA/NFA/ε-NFA: Just list symbols (deduplicated)
                const symbols = transList.map(t => 
                    this._formatSymbol(t.symbol, allowEpsilon)
                );
                // Remove duplicates using Set
                const uniqueSymbols = [...new Set(symbols)];
                label = uniqueSymbols.join(', ');
            }

            // Check for self-loop
            if (fromId === toId) {
                classes.push('self-loop');
            }

            // Check for parallel edges (bidirectional)
            const reverseKey = `${toId}-${fromId}`;
            if (transitionMap.has(reverseKey)) {
                classes.push('parallel');
            }

            edges.push({
                group: 'edges',
                data: {
                    id: `edge-${fromId}-${toId}`,
                    source: `state-${fromId}`,
                    target: `state-${toId}`,
                    label: label,
                    symbol: label, // For tooltip
                    isPDA: isPDA,
                    stackPop: isPDA ? firstTrans.stackPop : undefined,
                    stackPush: isPDA ? firstTrans.stackPush : undefined
                },
                classes: classes.join(' ')
            });
        });

        cy.add(edges);
    }

    /**
     * Private: Format symbol for display
     * @private
     * @param {string|char} symbol - Symbol to format
     * @param {boolean} allowEpsilon - Whether to display ε for null/empty
     * @returns {string} Formatted symbol
     */
    static _formatSymbol(symbol, allowEpsilon) {
        if (symbol === null || symbol === undefined || symbol === '') {
            return allowEpsilon ? 'ε' : '';
        }
        if (symbol === '\0' || symbol === '\\0') {
            return 'ε';
        }
        return symbol.toString();
    }

    /**
     * Get Cytoscape stylesheet for automaton visualization
     * @param {string} automatonType - Type of automaton
     * @param {Object} overrides - Style overrides
     * @returns {Array} Cytoscape stylesheet array
     */
    static getStylesheet(automatonType, overrides = {}) {
        const baseStyles = [
            // ===== NODE STYLES =====
            {
                selector: 'node',
                style: {
                    'background-color': overrides.nodeBackgroundColor || '#ffffff',
                    'border-width': 2,
                    'border-color': overrides.nodeBorderColor || '#333333',
                    'label': 'data(label)',
                    'text-valign': 'center',
                    'text-halign': 'center',
                    'font-size': '14px',
                    'font-weight': 'bold',
                    'font-family': 'Arial, sans-serif',
                    'color': '#000000',
                    'width': 50,
                    'height': 50,
                    'transition-property': 'background-color, border-color, border-width',
                    'transition-duration': '0.3s'
                }
            },
            // Active state (during execution)
            {
                selector: 'node.active',
                style: {
                    'background-color': overrides.activeStateColor || '#b0b0b0',
                    'border-width': 4,
                    'border-color': overrides.activeStateBorderColor || '#b0b0b0',
                    'color': '#ffffff',
                    'box-shadow': '0 0 20px rgba(176, 176, 176, 0.6)'
                }
            },
            // Start state (entry point)
            {
                selector: 'node.start',
                style: {
                    'border-color': overrides.startStateColor || '#e76f51',
                    'border-width': 3
                }
            },
            // Accepting state (double circle effect)
            {
                selector: 'node.accepting',
                style: {
                    'border-width': 6,
                    'border-style': 'double',
                    'border-color': overrides.acceptingStateColor || '#f4a261'
                }
            },
            // Both start and accepting
            {
                selector: 'node.start.accepting',
                style: {
                    'border-width': 6,
                    'border-style': 'double',
                    'border-color': overrides.startAcceptingColor || '#e76f51'
                }
            },
            // Hover effect
            {
                selector: 'node:active',
                style: {
                    'overlay-opacity': 0.2,
                    'overlay-color': '#333'
                }
            },

            // ===== EDGE STYLES =====
            {
                selector: 'edge',
                style: {
                    'width': 2,
                    'line-color': overrides.edgeColor || '#555555',
                    'target-arrow-color': overrides.edgeColor || '#555555',
                    'target-arrow-shape': 'triangle',
                    'curve-style': 'bezier',           // Simple bezier curves (not too wavy)
                    'control-point-step-size': 50,     // Moderate curve
                    'label': 'data(label)',
                    'font-size': '13px',               // Slightly larger for visibility
                    'font-weight': '600',              // Bolder text
                    'font-family': 'Arial, sans-serif',
                    'text-rotation': 'autorotate',
                    'text-margin-y': -12,              // More space from edge
                    'text-background-color': '#ffffff',
                    'text-background-opacity': 0.95,   // More opaque background
                    'text-background-padding': '4px',  // More padding
                    'text-background-shape': 'roundrectangle',
                    'text-border-width': 1,
                    'text-border-color': '#cccccc',
                    'text-border-opacity': 0.5,
                    'z-index': 10,                     // Ensure labels are on top
                    'text-events': 'yes',              // Labels are interactive
                    'transition-property': 'line-color, width, target-arrow-color',
                    'transition-duration': '0.3s'
                }
            },
            // Active transition (during execution)
            {
                selector: 'edge.active',
                style: {
                    'width': 4,
                    'line-color': overrides.activeEdgeColor || '#f28c28',
                    'target-arrow-color': overrides.activeEdgeColor || '#f28c28',
                    'z-index': 999
                }
            },
            // Self-loop edges
            {
                selector: 'edge.self-loop',
                style: {
                    'curve-style': 'bezier',
                    'loop-direction': '0deg',
                    'loop-sweep': '120deg',
                    'control-point-step-size': 80,
                    'text-margin-y': -20
                }
            },
            // Parallel edges (multiple transitions between same states)
            {
                selector: 'edge.parallel',
                style: {
                    'curve-style': 'bezier',
                    'control-point-step-size': 70,     // Slightly more curved than normal
                    'text-margin-y': -15
                }
            },
            // PDA-specific edge styling
            {
                selector: 'edge.pda',
                style: {
                    'font-size': '11px',
                    'text-margin-y': -12,
                    'white-space': 'pre'
                }
            }
        ];

        return baseStyles;
    }

    /**
     * Get color scheme for theming
     * @param {string} theme - Theme name ('light', 'dark', 'high-contrast')
     * @returns {Object} Color overrides for the theme
     */
    static getThemeColors(theme) {
        const themes = {
            light: {
                nodeBackgroundColor: '#ffffff',
                nodeBorderColor: '#333333',
                activeStateColor: '#2a9d8f',
                startStateColor: '#e76f51',
                acceptingStateColor: '#f4a261',
                edgeColor: '#555555',
                activeEdgeColor: '#f28c28'
            },
            dark: {
                nodeBackgroundColor: '#2d3748',
                nodeBorderColor: '#e2e8f0',
                activeStateColor: '#38b2ac',
                startStateColor: '#fc8181',
                acceptingStateColor: '#f6ad55',
                edgeColor: '#cbd5e0',
                activeEdgeColor: '#ed8936'
            },
            highContrast: {
                nodeBackgroundColor: '#ffffff',
                nodeBorderColor: '#000000',
                activeStateColor: '#00ff00',
                startStateColor: '#ff0000',
                acceptingStateColor: '#0000ff',
                edgeColor: '#000000',
                activeEdgeColor: '#ff00ff'
            }
        };

        return themes[theme] || themes.light;
    }
}

// Expose to window for non-module usage
if (typeof window !== 'undefined') {
    window.AutomatonRenderer = AutomatonRenderer;
}
