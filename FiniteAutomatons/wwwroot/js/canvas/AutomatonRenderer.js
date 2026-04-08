export class AutomatonRenderer {
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
            case 'DPDA':
            case 'NPDA':
                this.renderPDA(cy, states, transitions);
                break;
            default:
                console.warn(`Unknown automaton type: ${type}, using generic renderer`);
                this.renderGeneric(cy, states, transitions);
        }
    }

    static renderDFA(cy, states, transitions) {
        this._renderStates(cy, states);
        this._renderTransitions(cy, transitions, false, false);
    }

    static renderNFA(cy, states, transitions) {
        this._renderStates(cy, states);
        this._renderTransitions(cy, transitions, false, false);
    }

    static renderEpsilonNFA(cy, states, transitions) {
        this._renderStates(cy, states);
        this._renderTransitions(cy, transitions, true, false);
    }

    static renderPDA(cy, states, transitions) {
        this._renderStates(cy, states);
        this._renderTransitions(cy, transitions, true, true);
    }

    static renderGeneric(cy, states, transitions) {
        this._renderStates(cy, states);
        this._renderTransitions(cy, transitions, true, false);
    }

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

    static _renderTransitions(cy, transitions, allowEpsilon, isPDA) {
        if (!transitions || transitions.length === 0) return;

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

            let label;
            let classes = [];

            if (isPDA) {
                const pdaLabels = transList.map(t => {
                    const symbol = this._formatSymbol(t.symbol, allowEpsilon);
                    const pop = this._formatSymbol(t.stackPop, true) || 'ε';
                    const push = t.stackPush || 'ε';
                    return `${symbol} (${pop}/${push})`;
                });
                const uniqueLabels = [...new Set(pdaLabels)];
                label = uniqueLabels.join('\n');
                classes.push('pda');
            } else {
                const symbols = transList.map(t => 
                    this._formatSymbol(t.symbol, allowEpsilon)
                );
                const uniqueSymbols = [...new Set(symbols)];
                label = uniqueSymbols.join(', ');
            }

            if (fromId === toId) {
                classes.push('self-loop');
            }

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
                    displayLabel: this._toDisplayLabel(label),
                    labelSizeHint: this._estimateLabelSizeHint(label),
                    symbol: firstTrans.symbol,
                    isPDA: isPDA,
                    stackPop: isPDA ? firstTrans.stackPop : undefined,
                    stackPush: isPDA ? firstTrans.stackPush : undefined
                },
                classes: classes.join(' ')
            });
        });

        cy.add(edges);
    }

    static _formatSymbol(symbol, allowEpsilon) {
        if (symbol === null || symbol === undefined || symbol === '') {
            return allowEpsilon ? 'ε' : '';
        }
        if (symbol === '\0' || symbol === '\\0') {
            return 'ε';
        }
        return symbol.toString();
    }

    static getStylesheet(automatonType, overrides = {}) {
        const baseStyles = [
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
            {
                selector: 'node.start',
                style: {
                    'border-color': overrides.startStateColor || '#e76f51',
                    'border-width': 3
                }
            },
            {
                selector: 'node.accepting',
                style: {
                    'border-width': 6,
                    'border-style': 'double',
                    'border-color': overrides.acceptingStateColor || '#f4a261'
                }
            },
            {
                selector: 'node.start.accepting',
                style: {
                    'border-width': 6,
                    'border-style': 'double',
                    'border-color': overrides.startAcceptingColor || '#e76f51'
                }
            },
            {
                selector: 'node:active',
                style: {
                    'overlay-opacity': 0.2,
                    'overlay-color': '#333'
                }
            },

            {
                selector: 'edge',
                style: {
                    'width': 2,
                    'line-color': overrides.edgeColor || '#555555',
                    'target-arrow-color': overrides.edgeColor || '#555555',
                    'target-arrow-shape': 'triangle',
                    'curve-style': 'bezier',           
                    'control-point-step-size': 50,     
                    'label': 'data(displayLabel)',
                    'font-size': '13px',               
                    'font-weight': '600',              
                    'font-family': 'Arial, sans-serif',
                    'text-rotation': 'autorotate',
                    'text-margin-y': -12,              
                    'text-background-color': '#ffffff',
                    'text-background-opacity': 0.95,   
                    'text-background-padding': '4px', 
                    'text-background-shape': 'roundrectangle',
                    'text-border-width': 1,
                    'text-border-color': '#cccccc',
                    'text-border-opacity': 0.5,
                    'z-index': 10,                     
                    'text-events': 'yes',             
                    'transition-property': 'line-color, width, target-arrow-color',
                    'transition-duration': '0.3s'
                }
            },
            {
                selector: 'edge.active',
                style: {
                    'width': 4,
                    'line-color': overrides.activeEdgeColor || '#f28c28',
                    'target-arrow-color': overrides.activeEdgeColor || '#f28c28',
                    'z-index': 999
                }
            },
            {
                selector: 'edge.active-branch-0',
                style: { 'line-color': '#e63946', 'target-arrow-color': '#e63946', 'width': 4 }
            },
            {
                selector: 'edge.active-branch-1',
                style: { 'line-color': '#2a9d8f', 'target-arrow-color': '#2a9d8f', 'width': 4 }
            },
            {
                selector: 'edge.active-branch-2',
                style: { 'line-color': '#f4a261', 'target-arrow-color': '#f4a261', 'width': 4 }
            },
            {
                selector: 'edge.active-branch-3',
                style: { 'line-color': '#6a4c93', 'target-arrow-color': '#6a4c93', 'width': 4 }
            },
            {
                selector: 'edge.active-branch-4',
                style: { 'line-color': '#1d3557', 'target-arrow-color': '#1d3557', 'width': 4 }
            },
            {
                selector: 'edge.active-branch-5',
                style: { 'line-color': '#ffb703', 'target-arrow-color': '#ffb703', 'width': 4 }
            },
            {
                selector: 'edge.active-branch-6',
                style: { 'line-color': '#8ac926', 'target-arrow-color': '#8ac926', 'width': 4 }
            },
            {
                selector: 'edge.active-branch-7',
                style: { 'line-color': '#1982c4', 'target-arrow-color': '#1982c4', 'width': 4 }
            },
            {
                selector: 'edge.active-branch-8',
                style: { 'line-color': '#ff6b6b', 'target-arrow-color': '#ff6b6b', 'width': 4 }
            },
            {
                selector: 'edge.active-branch-9',
                style: { 'line-color': '#4cc9f0', 'target-arrow-color': '#4cc9f0', 'width': 4 }
            },
            {
                selector: 'edge.active-branch-10',
                style: { 'line-color': '#f72585', 'target-arrow-color': '#f72585', 'width': 4 }
            },
            {
                selector: 'edge.active-branch-11',
                style: { 'line-color': '#7209b7', 'target-arrow-color': '#7209b7', 'width': 4 }
            },
            {
                selector: 'edge.active-branch-12',
                style: { 'line-color': '#3a0ca3', 'target-arrow-color': '#3a0ca3', 'width': 4 }
            },
            {
                selector: 'edge.active-branch-13',
                style: { 'line-color': '#4361ee', 'target-arrow-color': '#4361ee', 'width': 4 }
            },
            {
                selector: 'edge.active-branch-14',
                style: { 'line-color': '#2ec4b6', 'target-arrow-color': '#2ec4b6', 'width': 4 }
            },
            {
                selector: 'edge.active-branch-15',
                style: { 'line-color': '#ffd166', 'target-arrow-color': '#ffd166', 'width': 4 }
            },
            {
                selector: 'node.active-branch-0',
                style: { 'background-color': '#e63946', 'border-color': '#b02a35', 'color': '#ffffff' }
            },
            {
                selector: 'node.active-branch-1',
                style: { 'background-color': '#2a9d8f', 'border-color': '#1f7a6e', 'color': '#ffffff' }
            },
            {
                selector: 'node.active-branch-2',
                style: { 'background-color': '#f4a261', 'border-color': '#c67c4a', 'color': '#000000' }
            },
            {
                selector: 'node.active-branch-3',
                style: { 'background-color': '#6a4c93', 'border-color': '#51366e', 'color': '#ffffff' }
            },
            {
                selector: 'node.active-branch-4',
                style: { 'background-color': '#1d3557', 'border-color': '#16283f', 'color': '#ffffff' }
            },
            {
                selector: 'node.active-branch-5',
                style: { 'background-color': '#ffb703', 'border-color': '#cc9303', 'color': '#000000' }
            },
            {
                selector: 'node.active-branch-6',
                style: { 'background-color': '#8ac926', 'border-color': '#6fa61f', 'color': '#000000' }
            },
            {
                selector: 'node.active-branch-7',
                style: { 'background-color': '#1982c4', 'border-color': '#136489', 'color': '#ffffff' }
            },
            {
                selector: 'node.active-branch-8',
                style: { 'background-color': '#ff6b6b', 'border-color': '#d35454', 'color': '#ffffff' }
            },
            {
                selector: 'node.active-branch-9',
                style: { 'background-color': '#4cc9f0', 'border-color': '#3aa6bf', 'color': '#000000' }
            },
            {
                selector: 'node.active-branch-10',
                style: { 'background-color': '#f72585', 'border-color': '#c21a69', 'color': '#ffffff' }
            },
            {
                selector: 'node.active-branch-11',
                style: { 'background-color': '#7209b7', 'border-color': '#560686', 'color': '#ffffff' }
            },
            {
                selector: 'node.active-branch-12',
                style: { 'background-color': '#3a0ca3', 'border-color': '#2c086f', 'color': '#ffffff' }
            },
            {
                selector: 'node.active-branch-13',
                style: { 'background-color': '#4361ee', 'border-color': '#3348b8', 'color': '#ffffff' }
            },
            {
                selector: 'node.active-branch-14',
                style: { 'background-color': '#2ec4b6', 'border-color': '#27a79e', 'color': '#000000' }
            },
            {
                selector: 'node.active-branch-15',
                style: { 'background-color': '#ffd166', 'border-color': '#d9b154', 'color': '#000000' }
            },
            {
                selector: 'edge.self-loop',
                style: {
                    'curve-style': 'bezier',
                    'loop-direction': '0deg',
                    'loop-sweep': '120deg',
                        'control-point-step-size': 'mapData(labelSizeHint, 8, 40, 80, 220)',
                        'text-margin-y': 'mapData(labelSizeHint, 8, 40, -20, -56)'
                }
            },
            {
                selector: 'edge.parallel',
                style: {
                    'curve-style': 'bezier',
                    'control-point-step-size': 70,     
                    'text-margin-y': -15
                }
            },
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

    static _estimateLabelSizeHint(label) {
        if (!label || typeof label !== 'string') return 8;
        const lines = label.split('\n').map(l => l.trim()).filter(Boolean);
        const longest = lines.length ? Math.max(...lines.map(l => l.length)) : label.length;
        return Math.max(8, Math.min(40, longest));
    }

    static _toDisplayLabel(label) {
        return String(label || '').replace(/\n+/g, '   ');
    }

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

if (typeof window !== 'undefined') {
    window.AutomatonRenderer = AutomatonRenderer;
}
