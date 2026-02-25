// Modal handling for Generate Automaton with modern animations
(function() {
    const modal = document.getElementById('generateModal');
    const btn = document.getElementById('generateModalBtn');
    const close = document.getElementById('generateModalClose');
    const lockBtn = document.getElementById('generateModalLockBtn');
    const container = document.getElementById('generateModalSectionsContainer');

    let dragEl = null;
    let lockState = false;

    // Restore lock state from localStorage
    try { 
        lockState = localStorage.getItem('generateSectionsLocked') === 'true'; 
    } catch(e) { }

    const openModal = () => {
        if (!modal) return;
        modal.style.display = 'flex';
        document.body.style.overflow = 'hidden';
        
        // Trigger reflow for animation
        modal.offsetHeight;
        modal.classList.add('modal-open');
        
        // Populate options when opening
        populateOptionsForFamily(document.getElementById('generateFamilySelect')?.value || 'DFA');
        updateLockedClass();
    };

    const closeModal = () => {
        if (!modal) return;
        modal.classList.remove('modal-open');
        
        // Wait for animation to complete
        setTimeout(() => {
            modal.style.display = 'none';
            document.body.style.overflow = '';
        }, 200);
    };

    function updateLockedClass() {
        if (!container) return;
        container.querySelectorAll('.input-section-card').forEach(s => {
            s.classList.toggle('locked', lockState);
            if (!lockState) { 
                s.setAttribute('draggable', 'true'); 
            } else { 
                s.removeAttribute('draggable'); 
            }
        });
    }

    function setLocked(v) { 
        lockState = !!v; 
        updateLockedClass(); 
        try { 
            localStorage.setItem('generateSectionsLocked', lockState ? 'true' : 'false'); 
        } catch(e) { }
    }

    // Drag and drop event handlers
    if (container) {
        container.addEventListener('dragstart', function(e) {
            if (lockState) { 
                e.preventDefault(); 
                return; 
            }
            const target = e.target.closest('.input-section-card');
            if (!target) { 
                e.preventDefault(); 
                return; 
            }
            dragEl = target;
            target.classList.add('dragging');
            e.dataTransfer.effectAllowed = 'move';
            try { 
                e.dataTransfer.setData('text/plain', 'drag'); 
            } catch(_) { }
        });

        container.addEventListener('dragend', function(e) {
            if (dragEl) dragEl.classList.remove('dragging');
            dragEl = null;
            savePanelOrder();
        });

        container.addEventListener('dragover', function(e) {
            if (lockState) return;
            e.preventDefault();
            const after = getDragAfterElement(container, e.clientY);
            const dragging = container.querySelector('.dragging');
            if (!dragging) return;
            if (after == null) {
                container.appendChild(dragging);
            } else {
                container.insertBefore(dragging, after);
            }
        });
    }

    function getDragAfterElement(container, y) {
        const els = [...container.querySelectorAll('.input-section-card:not(.dragging)')];
        return els.reduce((closest, child) => {
            const box = child.getBoundingClientRect();
            const offset = y - box.top - box.height / 2;
            if (offset < 0 && (closest == null || offset > closest.offset)) {
                return { offset: offset, element: child };
            } else {
                return closest;
            }
        }, null)?.element || null;
    }

    function savePanelOrder() {
        if (!container) return;
        const order = Array.from(container.querySelectorAll('.input-section-card'))
                           .map(el => el.getAttribute('data-section-id'))
                           .filter(Boolean);
        
        try {
            const allPrefs = JSON.parse(localStorage.getItem('panelOrderPrefs') || '{}');
            allPrefs.generateAutomaton = order;
            localStorage.setItem('panelOrderPrefs', JSON.stringify(allPrefs));
        } catch(e) {}

        fetch('/api/preferences/panel-order', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                preferences: localStorage.getItem('panelOrderPrefs') || '{}'
            })
        }).catch(() => {});
    }

    async function restorePanelOrder() {
        if (!container) return;
        let prefsJSON = localStorage.getItem('panelOrderPrefs');
        try {
            const res = await fetch('/api/preferences/panel-order');
            if (res.ok) {
                const data = await res.json();
                if (data.preferences) {
                    prefsJSON = data.preferences;
                    localStorage.setItem('panelOrderPrefs', prefsJSON);
                }
            }
        } catch(e) {}

        if (prefsJSON) {
            try {
                const prefs = JSON.parse(prefsJSON);
                const order = prefs.generateAutomaton;
                if (Array.isArray(order) && order.length > 0) {
                    const sections = Array.from(container.querySelectorAll('.input-section-card'));
                    order.forEach(id => {
                        const el = sections.find(s => s.getAttribute('data-section-id') === id);
                        if (el) container.appendChild(el);
                    });
                }
            } catch(e) {}
        }
    }
    
    // Lock button handler
    if (lockBtn) {
        lockBtn.setAttribute('aria-pressed', lockState ? 'true' : 'false');
        const icon = lockBtn.querySelector('i');
        if (icon) { 
            icon.classList.toggle('fa-lock', lockState); 
            icon.classList.toggle('fa-lock-open', !lockState); 
        }

        lockBtn.addEventListener('click', function() {
            const currently = lockBtn.getAttribute('aria-pressed') === 'true';
            const next = !currently;
            lockBtn.setAttribute('aria-pressed', next ? 'true' : 'false');
            if (icon) { 
                icon.classList.toggle('fa-lock', next); 
                icon.classList.toggle('fa-lock-open', !next); 
            }
            setLocked(next);
        });
    }

    if (btn && modal) {
        btn.addEventListener('click', openModal);
    }
    
    if (close && modal) {
        close.addEventListener('click', closeModal);
    }
    
    // Close on backdrop click
    window.addEventListener('click', (e) => { 
        if (e.target === modal) closeModal(); 
    });
    
    // Close on Escape key
    window.addEventListener('keydown', (e) => { 
        if (e.key === 'Escape' && modal.style.display === 'flex') closeModal(); 
    });

    const familySelect = document.getElementById('generateFamilySelect');
    const optionSelect = document.getElementById('generateOptionSelect');

    function populateOptionsForFamily(family) {
        if (!optionSelect) return;
        optionSelect.innerHTML = '';
        if (family === 'DFA') {
            optionSelect.appendChild(new Option('Random DFA', 'random-dfa'));
            optionSelect.appendChild(new Option('Minimalized DFA', 'minimalized-dfa'));
            optionSelect.appendChild(new Option('DFA (un-minimalized)', 'unminimalized-dfa'));
        } else if (family === 'NFA') {
            optionSelect.appendChild(new Option('Random NFA', 'random-nfa'));
            optionSelect.appendChild(new Option('Nondeterministic NFA', 'nondet-nfa'));
        } else if (family === 'EpsilonNFA') {
            optionSelect.appendChild(new Option('Random ε-NFA', 'random-enfa'));
            optionSelect.appendChild(new Option('ε-NFA (with ε transitions)', 'enfa-eps'));
            optionSelect.appendChild(new Option('ε-NFA nondeterministic', 'enfa-nondet'));
        } else if (family === 'PDA') {
            optionSelect.appendChild(new Option('Random PDA', 'random-pda'));
            optionSelect.appendChild(new Option('PDA with push/pop pairs', 'pda-pushpop'));
        }
    }

    if (familySelect) {
        familySelect.addEventListener('change', () => populateOptionsForFamily(familySelect.value));
    }
    
    // Add keyboard focus trap
    if (modal) {
        modal.addEventListener('keydown', (e) => {
            if (e.key === 'Tab') {
                const focusableElements = modal.querySelectorAll(
                    'button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
                );
                const firstElement = focusableElements[0];
                const lastElement = focusableElements[focusableElements.length - 1];

                if (e.shiftKey && document.activeElement === firstElement) {
                    e.preventDefault();
                    lastElement.focus();
                } else if (!e.shiftKey && document.activeElement === lastElement) {
                    e.preventDefault();
                    firstElement.focus();
                }
            }
        });
    }

    restorePanelOrder();
})();

