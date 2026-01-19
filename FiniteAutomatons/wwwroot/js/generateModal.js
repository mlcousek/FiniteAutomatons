// Modal handling for Generate Automaton
(function() {
    const modal = document.getElementById('generateModal');
    const btn = document.getElementById('generateModalBtn');
    const close = document.getElementById('generateModalClose');
    const toggles = document.querySelectorAll('.accordion-toggle');

    const openModal = () => {
        if (!modal) return;
        modal.style.display = 'flex';
        document.body.style.overflow = 'hidden'; // prevent background scroll
    };

    const closeModal = () => {
        if (!modal) return;
        modal.style.display = 'none';
        document.body.style.overflow = ''; // restore
    };

    if (btn && modal) {
        btn.addEventListener('click', () => {
            openModal();
            populateOptionsForFamily(document.getElementById('generateFamilySelect')?.value || 'DFA');
        });
    }
    if (close && modal) {
        close.addEventListener('click', closeModal);
    }
    window.addEventListener('click', (e) => { if (e.target === modal) closeModal(); });
    window.addEventListener('keydown', (e) => { if (e.key === 'Escape') closeModal(); });

    toggles.forEach(t => {
        t.addEventListener('click', () => {
            const panel = t.nextElementSibling;
            if (!panel) return;
            panel.style.display = panel.style.display === 'none' ? 'block' : 'none';
        });
    });

    const familySelect = document.getElementById('generateFamilySelect');
    const optionSelect = document.getElementById('generateOptionSelect');
    const presetForm = document.getElementById('generatePresetForm');

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
        }
    }

    if (familySelect) {
        familySelect.addEventListener('change', () => populateOptionsForFamily(familySelect.value));
    }
})();
