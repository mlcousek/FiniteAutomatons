/**
 * LayoutEngine.js
 * Handles graph layout algorithms for automaton visualization
 * Supports multiple layout strategies optimized for different automaton sizes
 * 
 * @module LayoutEngine
 */

/**
 * Static class for applying graph layouts
 */
export class LayoutEngine {
    /**
     * Apply layout to Cytoscape graph
     * @param {Object} cy - Cytoscape instance
     * @param {string} layoutName - Layout algorithm name
     * @param {Object} options - Additional options
     * @param {string} [options.automatonType] - Type of automaton
     * @param {number} [options.stateCount] - Number of states
     */
    static applyLayout(cy, layoutName = 'dagre', options = {}) {
        if (!cy) {
            console.error('Cytoscape instance is required');
            return;
        }

        const nodeCount = cy.nodes().length;
        
        // Auto-select layout if 'auto' is specified
        if (layoutName === 'auto') {
            layoutName = this._selectOptimalLayout(nodeCount, options);
        }

        let layoutConfig;

        switch (layoutName.toLowerCase()) {
            case 'dagre':
                layoutConfig = this._getDagreLayout(options);
                break;
            case 'circle':
                layoutConfig = this._getCircleLayout(options);
                break;
            case 'grid':
                layoutConfig = this._getGridLayout(options);
                break;
            case 'breadthfirst':
            case 'bfs':
                layoutConfig = this._getBreadthFirstLayout(options);
                break;
            case 'cose':
                layoutConfig = this._getCoseLayout(options);
                break;
            case 'concentric':
                layoutConfig = this._getConcentricLayout(options);
                break;
            case 'preset':
                // Use existing positions
                layoutConfig = { name: 'preset' };
                break;
            default:
                console.warn(`Unknown layout: ${layoutName}, falling back to dagre`);
                layoutConfig = this._getDagreLayout(options);
        }

        try {
            const layout = cy.layout(layoutConfig);
            layout.run();
        } catch (error) {
            console.error('Layout failed:', error);
            // Fallback to simple grid layout
            this._applyFallbackLayout(cy, nodeCount);
        }
    }

    /**
     * Dagre layout - Hierarchical, great for automatons
     * @private
     * @param {Object} options - Layout options
     * @returns {Object} Cytoscape layout configuration
     */
    static _getDagreLayout(options) {
        return {
            name: 'dagre',
            nodeSep: 150,           // Increased horizontal spacing to avoid edge overlap
            rankSep: 200,           // Increased vertical spacing between ranks
            rankDir: 'LR',          // Left-to-right direction
            animate: true,
            animationDuration: 500,
            animationEasing: 'ease-out',
            fit: true,
            padding: 50,
            // Better edge routing
            edgeSep: 20,            // Separation between edges
            ranker: 'network-simplex' // Better ranking algorithm
        };
    }

    /**
     * Circle layout - Good for small automatons
     * @private
     * @param {Object} options - Layout options
     * @returns {Object} Cytoscape layout configuration
     */
    static _getCircleLayout(options) {
        return {
            name: 'circle',
            animate: true,
            animationDuration: 500,
            animationEasing: 'ease-out',
            fit: true,
            padding: 50,
            radius: undefined,      // Auto-calculate radius
            startAngle: 3 / 2 * Math.PI, // Start at top
            sweep: 2 * Math.PI,     // Full circle
            clockwise: true
        };
    }

    /**
     * Grid layout - Simple grid arrangement
     * @private
     * @param {Object} options - Layout options
     * @returns {Object} Cytoscape layout configuration
     */
    static _getGridLayout(options) {
        return {
            name: 'grid',
            animate: true,
            animationDuration: 500,
            animationEasing: 'ease-out',
            fit: true,
            padding: 50,
            rows: undefined,        // Auto-calculate
            cols: undefined,        // Auto-calculate
            condense: false,
            avoidOverlap: true,
            avoidOverlapPadding: 10
        };
    }

    /**
     * Breadth-first layout - Tree-like structure
     * @private
     * @param {Object} options - Layout options
     * @returns {Object} Cytoscape layout configuration
     */
    static _getBreadthFirstLayout(options) {
        return {
            name: 'breadthfirst',
            animate: true,
            animationDuration: 500,
            animationEasing: 'ease-out',
            fit: true,
            padding: 50,
            directed: true,
            spacingFactor: 1.5,
            grid: false,
            avoidOverlap: true
        };
    }

    /**
     * COSE layout - Force-directed (physics simulation)
     * @private
     * @param {Object} options - Layout options
     * @returns {Object} Cytoscape layout configuration
     */
    static _getCoseLayout(options) {
        return {
            name: 'cose',
            animate: true,
            animationDuration: 1000,
            animationEasing: 'ease-out',
            fit: true,
            padding: 50,
            nodeRepulsion: 400000,
            idealEdgeLength: 100,
            edgeElasticity: 100,
            nestingFactor: 5,
            gravity: 80,
            numIter: 1000,
            initialTemp: 200,
            coolingFactor: 0.95,
            minTemp: 1.0
        };
    }

    /**
     * Concentric layout - Nodes in concentric circles
     * @private
     * @param {Object} options - Layout options
     * @returns {Object} Cytoscape layout configuration
     */
    static _getConcentricLayout(options) {
        return {
            name: 'concentric',
            animate: true,
            animationDuration: 500,
            animationEasing: 'ease-out',
            fit: true,
            padding: 50,
            startAngle: 3 / 2 * Math.PI,
            sweep: 2 * Math.PI,
            clockwise: true,
            equidistant: false,
            minNodeSpacing: 50,
            concentric: (node) => {
                // Place start state at center
                if (node.data('isStart')) return 10;
                // Then accepting states
                if (node.data('isAccepting')) return 5;
                // Others on outer rings
                return 1;
            },
            levelWidth: () => 2
        };
    }

    /**
     * Select optimal layout based on automaton characteristics
     * @private
     * @param {number} nodeCount - Number of nodes
     * @param {Object} options - Additional options
     * @returns {string} Recommended layout name
     */
    static _selectOptimalLayout(nodeCount, options) {
        if (nodeCount <= 5) {
            return 'circle';
        } else if (nodeCount <= 15) {
            return 'dagre';
        } else if (nodeCount <= 30) {
            return 'breadthfirst';
        } else {
            return 'cose'; // Force-directed works better for large graphs
        }
    }

    /**
     * Fallback layout when primary layout fails
     * @private
     * @param {Object} cy - Cytoscape instance
     * @param {number} nodeCount - Number of nodes
     */
    static _applyFallbackLayout(cy, nodeCount) {
        console.warn('Applying fallback grid layout');
        
        const padding = 50;
        const containerWidth = cy.container().clientWidth - padding * 2;
        const containerHeight = cy.container().clientHeight - padding * 2;
        
        const cols = Math.ceil(Math.sqrt(nodeCount));
        const rows = Math.ceil(nodeCount / cols);
        
        const cellWidth = containerWidth / cols;
        const cellHeight = containerHeight / rows;

        const nodes = cy.nodes();
        nodes.forEach((node, index) => {
            const row = Math.floor(index / cols);
            const col = index % cols;
            
            const x = padding + col * cellWidth + cellWidth / 2;
            const y = padding + row * cellHeight + cellHeight / 2;
            
            node.position({ x, y });
        });
    }

    /**
     * Manually position states (for custom layouts)
     * @param {Object} cy - Cytoscape instance
     * @param {Array} positions - Array of {stateId, x, y}
     */
    static applyCustomPositions(cy, positions) {
        if (!cy || !positions) return;

        positions.forEach(pos => {
            const node = cy.getElementById(`state-${pos.stateId}`);
            if (node) {
                node.position({ x: pos.x, y: pos.y });
            }
        });
    }

    /**
     * Get current node positions (for saving layout)
     * @param {Object} cy - Cytoscape instance
     * @returns {Array} Array of {stateId, x, y}
     */
    static getNodePositions(cy) {
        if (!cy) return [];

        return cy.nodes().map(node => ({
            stateId: node.data('stateId'),
            x: node.position('x'),
            y: node.position('y')
        }));
    }

    /**
     * Arrange nodes to avoid overlaps
     * @param {Object} cy - Cytoscape instance
     */
    static avoidOverlaps(cy) {
        if (!cy) return;

        const nodes = cy.nodes();
        const minDistance = 80; // Minimum distance between nodes

        // Simple overlap resolution using force-based approach
        for (let i = 0; i < 50; i++) { // Max iterations
            let hadOverlap = false;

            for (let j = 0; j < nodes.length; j++) {
                for (let k = j + 1; k < nodes.length; k++) {
                    const node1 = nodes[j];
                    const node2 = nodes[k];

                    const pos1 = node1.position();
                    const pos2 = node2.position();

                    const dx = pos2.x - pos1.x;
                    const dy = pos2.y - pos1.y;
                    const distance = Math.sqrt(dx * dx + dy * dy);

                    if (distance < minDistance) {
                        hadOverlap = true;
                        
                        // Push nodes apart
                        const pushDistance = (minDistance - distance) / 2;
                        const angle = Math.atan2(dy, dx);
                        
                        node1.position({
                            x: pos1.x - Math.cos(angle) * pushDistance,
                            y: pos1.y - Math.sin(angle) * pushDistance
                        });
                        
                        node2.position({
                            x: pos2.x + Math.cos(angle) * pushDistance,
                            y: pos2.y + Math.sin(angle) * pushDistance
                        });
                    }
                }
            }

            if (!hadOverlap) break;
        }
    }

    /**
     * Get available layout names
     * @returns {Array<string>} List of available layout names
     */
    static getAvailableLayouts() {
        return [
            'auto',
            'dagre',
            'circle',
            'grid',
            'breadthfirst',
            'cose',
            'concentric',
            'preset'
        ];
    }

    /**
     * Get layout description
     * @param {string} layoutName - Layout name
     * @returns {string} Human-readable description
     */
    static getLayoutDescription(layoutName) {
        const descriptions = {
            auto: 'Automatically select optimal layout',
            dagre: 'Hierarchical layout (recommended for automatons)',
            circle: 'Circular arrangement (good for small graphs)',
            grid: 'Simple grid layout',
            breadthfirst: 'Tree-like breadth-first layout',
            cose: 'Force-directed physics simulation (good for large graphs)',
            concentric: 'Concentric circles (start state at center)',
            preset: 'Use existing positions'
        };

        return descriptions[layoutName] || 'Unknown layout';
    }
}

// Expose to window for non-module usage
if (typeof window !== 'undefined') {
    window.LayoutEngine = LayoutEngine;
}
