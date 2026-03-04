export class LayoutEngine {

    static applyLayout(cy, layoutName = 'dagre', options = {}) {
        if (!cy) {
            console.error('Cytoscape instance is required');
            return;
        }

        const nodeCount = cy.nodes().length;

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
            this._applyFallbackLayout(cy, nodeCount);
        }
    }

    static _getDagreLayout(options) {
        return {
            name: 'dagre',
            nodeSep: 150,           
            rankSep: 200,           
            rankDir: 'LR',          
            animate: true,
            animationDuration: 500,
            animationEasing: 'ease-out',
            fit: true,
            padding: 50,
            edgeSep: 20,            
            ranker: 'network-simplex' 
        };
    }

    static _getCircleLayout(options) {
        return {
            name: 'circle',
            animate: true,
            animationDuration: 500,
            animationEasing: 'ease-out',
            fit: true,
            padding: 50,
            radius: undefined,     
            startAngle: 3 / 2 * Math.PI, 
            sweep: 2 * Math.PI,     
            clockwise: true
        };
    }

    static _getGridLayout(options) {
        return {
            name: 'grid',
            animate: true,
            animationDuration: 500,
            animationEasing: 'ease-out',
            fit: true,
            padding: 50,
            rows: undefined,        
            cols: undefined,        
            condense: false,
            avoidOverlap: true,
            avoidOverlapPadding: 10
        };
    }

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
                if (node.data('isStart')) return 10;
                if (node.data('isAccepting')) return 5;
                return 1;
            },
            levelWidth: () => 2
        };
    }

    static _selectOptimalLayout(nodeCount, options) {
        if (nodeCount <= 5) {
            return 'circle';
        } else if (nodeCount <= 15) {
            return 'dagre';
        } else if (nodeCount <= 30) {
            return 'breadthfirst';
        } else {
            return 'cose'; 
        }
    }

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

    static applyCustomPositions(cy, positions) {
        if (!cy || !positions) return;

        positions.forEach(pos => {
            const node = cy.getElementById(`state-${pos.stateId}`);
            if (node) {
                node.position({ x: pos.x, y: pos.y });
            }
        });
    }

    static getNodePositions(cy) {
        if (!cy) return [];

        return cy.nodes().map(node => ({
            stateId: node.data('stateId'),
            x: node.position('x'),
            y: node.position('y')
        }));
    }

    static avoidOverlaps(cy) {
        if (!cy) return;

        const nodes = cy.nodes();
        const minDistance = 80; 

        for (let i = 0; i < 50; i++) { 
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

if (typeof window !== 'undefined') {
    window.LayoutEngine = LayoutEngine;
}
