// Modal handling for Generate Automaton with modern animations
(function() {
    const modal = document.getElementById('generateModal');
    const btn = document.getElementById('generateModalBtn');
    const close = document.getElementById('generateModalClose');

    const openModal = () => {
        if (!modal) return;
        modal.style.display = 'flex';
        document.body.style.overflow = 'hidden';
        
        // Trigger reflow for animation
        modal.offsetHeight;
        modal.classList.add('modal-open');
        
        // Populate options when opening
        populateOptionsForFamily(document.getElementById('generateFamilySelect')?.value || 'DFA');
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
})();

