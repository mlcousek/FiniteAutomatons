# 🎨 Canvas Implementation - Phase 2: Interactive Editing

## Overview
Phase 2 adds interactive graph editing capabilities to the automaton canvas, allowing users to create, modify, and delete states and transitions directly in the visual editor.

## ✅ Phase 1 Complete
- ✅ Modern Cytoscape.js canvas rendering
- ✅ Pan, zoom, fit controls
- ✅ State highlighting during execution
- ✅ Multiple automaton type support (DFA/NFA/ε-NFA/PDA)
- ✅ Professional styling and animations

## 🚧 Phase 2: Interactive Editing

### Core Features

#### 1. Edit Mode Toggle
**Status**: 🔨 In Progress

**UI Components**:
- [ ] Lock/Unlock button in canvas toolbar
- [ ] Visual indicator when edit mode is active
- [ ] Disable editing during simulation

**Implementation**:
- Create `EditModeManager.js` class
- Add state management for edit/view modes
- Add button to canvas controls
- Sync with simulation state (disable when running)

---

#### 2. State Management
**Status**: ⏳ Planned

**Features**:
- [ ] **Add State**: Click empty space to create new state
- [ ] **Delete State**: Select state + Delete key
- [ ] **Move State**: Drag states to reposition
- [ ] **Edit Properties**: Right-click menu to toggle start/accepting

**Implementation Details**:

**Add State**:
```javascript
// Click empty canvas background
canvas.on('tap', function(event) {
    if (editMode && event.target === cy) {
        const newStateId = getNextStateId();
        cy.add({
            group: 'nodes',
            data: { id: `state-${newStateId}`, label: `q${newStateId}` },
            position: { x: event.position.x, y: event.position.y }
        });
    }
});
```

**Delete State**:
```javascript
// Keyboard handler
document.addEventListener('keydown', (e) => {
    if (editMode && e.key === 'Delete') {
        const selected = cy.$(':selected');
        if (selected.isNode()) {
            cy.remove(selected);
        }
    }
});
```

**Move State**:
```javascript
// Enable/disable node dragging based on edit mode
cy.nodes().grabify(); // Enable dragging in edit mode
cy.nodes().ungrabify(); // Disable in view mode
```

**Edit Properties**:
```javascript
// Right-click context menu
cy.on('cxttap', 'node', function(event) {
    const node = event.target;
    showContextMenu(node, {
        'Toggle Start State': () => toggleStartState(node),
        'Toggle Accepting': () => toggleAccepting(node)
    });
});
```

---

#### 3. Transition Management
**Status**: ⏳ Planned

**Features**:
- [ ] **Add Transition**: Click source → target → enter symbol
- [ ] **Delete Transition**: Select edge + Delete key
- [ ] **Edit Symbol**: Double-click edge to edit label

**Implementation Details**:

**Add Transition (Two-Click Mode)**:
```javascript
let sourceNode = null;

cy.on('tap', 'node', function(event) {
    if (!editMode) return;
    
    const node = event.target;
    
    if (!sourceNode) {
        // First click - select source
        sourceNode = node;
        node.addClass('source-selected');
    } else {
        // Second click - create edge
        const targetNode = node;
        showTransitionDialog(sourceNode, targetNode, (symbol) => {
            cy.add({
                group: 'edges',
                data: {
                    source: sourceNode.id(),
                    target: targetNode.id(),
                    label: symbol
                }
            });
        });
        sourceNode.removeClass('source-selected');
        sourceNode = null;
    }
});
```

**Delete Transition**:
```javascript
document.addEventListener('keydown', (e) => {
    if (editMode && e.key === 'Delete') {
        const selected = cy.$(':selected');
        if (selected.isEdge()) {
            cy.remove(selected);
        }
    }
});
```

**Edit Symbol**:
```javascript
cy.on('dbltap', 'edge', function(event) {
    if (!editMode) return;
    
    const edge = event.target;
    const currentLabel = edge.data('label');
    
    showEditDialog(currentLabel, (newLabel) => {
        edge.data('label', newLabel);
    });
});
```

---

#### 4. Undo/Redo System
**Status**: ⏳ Planned (Phase 2.5)

**Features**:
- [ ] Ctrl+Z for Undo
- [ ] Ctrl+Y for Redo
- [ ] Action history stack
- [ ] Visual feedback for undo/redo

**Implementation**:
```javascript
class ActionHistory {
    constructor() {
        this.undoStack = [];
        this.redoStack = [];
    }
    
    recordAction(action) {
        this.undoStack.push(action);
        this.redoStack = [];
    }
    
    undo() {
        const action = this.undoStack.pop();
        if (action) {
            action.undo();
            this.redoStack.push(action);
        }
    }
    
    redo() {
        const action = this.redoStack.pop();
        if (action) {
            action.redo();
            this.undoStack.push(action);
        }
    }
}
```

---

#### 5. Data Synchronization
**Status**: ⏳ Planned

**Critical**: Changes in canvas must update the form

**Strategy**:
```javascript
// After any edit operation
function syncToForm() {
    const states = cy.nodes().map(n => ({
        id: n.data('stateId'),
        isStart: n.hasClass('start'),
        isAccepting: n.hasClass('accepting')
    }));
    
    const transitions = cy.edges().map(e => ({
        fromStateId: e.source().data('stateId'),
        toStateId: e.target().data('stateId'),
        symbol: e.data('label')
    }));
    
    updateHiddenFormInputs(states, transitions);
}
```

---

## 📁 File Structure

### New Files to Create

```
wwwroot/js/canvas/
├── EditModeManager.js          ✅ Core edit mode logic
├── StateEditor.js              📝 State CRUD operations
├── TransitionEditor.js         📝 Transition CRUD operations
├── ContextMenuManager.js       📝 Right-click menus
├── TransitionDialog.js         📝 Symbol input dialog
└── CanvasFormSync.js           📝 Sync canvas ↔ form
```

### Modified Files

```
wwwroot/js/
├── home-canvas-integration.mjs  📝 Add edit mode integration
└── home.mjs                     📝 Disable simulate during edit

Views/Home/Index.cshtml           📝 Add lock/unlock button
```

---

## 🎯 Implementation Order

### Step 1: Edit Mode Toggle ✅ (Current)
- [ ] Create `EditModeManager.js`
- [ ] Add lock/unlock button
- [ ] Integrate with existing canvas

### Step 2: State Editing
- [ ] Create `StateEditor.js`
- [ ] Implement add/delete/move states
- [ ] Add context menu for properties

### Step 3: Transition Editing
- [ ] Create `TransitionEditor.js`
- [ ] Implement two-click transition creation
- [ ] Add symbol input dialog

### Step 4: Form Synchronization
- [ ] Create `CanvasFormSync.js`
- [ ] Update form on every canvas change
- [ ] Validate synchronized data

### Step 5: Undo/Redo (Optional)
- [ ] Implement action history
- [ ] Add keyboard shortcuts
- [ ] Add undo/redo buttons

---

## 🎨 UI/UX Design

### Edit Mode Visual Indicators

**Locked (View Only)**:
- 🔒 Lock icon button (gray)
- Nodes are not draggable
- No click handlers for editing

**Unlocked (Edit Mode)**:
- 🔓 Unlock icon button (green)
- Nodes are draggable
- Click handlers enabled
- Visual hint: "Click nodes to create transitions"

### Canvas States

| Mode | Pan/Zoom | Drag Nodes | Add States | Add Transitions | Delete |
|------|----------|------------|------------|-----------------|--------|
| **View** | ✅ | ❌ | ❌ | ❌ | ❌ |
| **Edit** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Simulate** | ✅ | ❌ | ❌ | ❌ | ❌ |

---

## 🔍 Technical Considerations

### 1. Conflict Prevention
- Disable edit mode during simulation
- Lock canvas when execution is running
- Show warning if user tries to edit during simulation

### 2. State ID Management
```javascript
function getNextStateId() {
    const existingIds = cy.nodes().map(n => n.data('stateId'));
    return Math.max(...existingIds, -1) + 1;
}
```

### 3. Start State Rules
- Only ONE start state allowed
- When marking a new start state, remove old one
- Validate before form submission

### 4. Self-Loops
- Detect when source === target
- Apply special styling (self-loop class)
- Position label correctly

---

## 🧪 Testing Checklist

### Unit Tests
- [ ] Edit mode enable/disable
- [ ] Add state with unique IDs
- [ ] Delete state removes edges
- [ ] Start state single-instance rule
- [ ] Form synchronization

### Integration Tests
- [ ] Create automaton in canvas
- [ ] Submit form with canvas data
- [ ] Reload page preserves automaton
- [ ] Switch automaton type preserves data

### E2E Tests
- [ ] User creates DFA in canvas
- [ ] User simulates created automaton
- [ ] User edits existing automaton
- [ ] User saves edited automaton

---

## 📊 Progress Tracking

**Phase 2 Progress**: 0% (0/5 features complete)

- [ ] Edit Mode Toggle
- [ ] State Management
- [ ] Transition Management
- [ ] Form Synchronization
- [ ] Undo/Redo

---

## 🚀 Next Steps

1. **NOW**: Implement `EditModeManager.js`
2. **Next**: Add lock/unlock button to UI
3. **Then**: Implement state adding/deleting
4. **Then**: Implement transition creation
5. **Finally**: Form synchronization

---

## 📝 Notes

- Keep Phase 1 read-only mode working
- Edit mode should feel natural and intuitive
- Validate all user inputs
- Provide clear visual feedback
- Test extensively before merging

---

**Last Updated**: 2025-01-XX  
**Status**: Phase 2 - In Progress  
**Next Milestone**: Edit Mode Toggle Complete
