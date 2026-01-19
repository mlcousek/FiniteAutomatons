// automatonTypeControls.mjs
// Refactored: submit form to SwitchType only for these conversions:
// EpsilonNFA -> NFA, EpsilonNFA -> DFA, NFA -> DFA.
// Other button clicks just update highlighting client-side.
'use strict';

function initAutomatonTypeControls(){
    const form = document.getElementById('automatonForm');
    const targetField = document.getElementById('targetTypeField');
    const buttons = Array.from(document.querySelectorAll('.type-btn[data-type]'));
    if (!form || !targetField || !buttons.length) return;

    function currentType(){
        const active = buttons.find(b => b.classList.contains('active'));
        return active ? active.dataset.type : null;
    }

    function mapTypeToInt(t){
        switch(t){case 'DFA': return 0; case 'NFA': return 1; case 'EpsilonNFA': return 2; case 'PDA': return 3; default: return 0;}
    }

    function needServerConversion(from,to){
        if (!from || from === to) return false;
        return (from === 'EpsilonNFA' && (to === 'NFA' || to === 'DFA')) || (from === 'NFA' && to === 'DFA');
    }

    function applyDisableRules(active){
        buttons.forEach(b => b.removeAttribute('disabled'));
        switch(active){
            case 'DFA': disable(['NFA','EpsilonNFA','PDA']); break;
            case 'NFA': disable(['EpsilonNFA','PDA']); break;
            case 'EpsilonNFA': disable(['PDA']); break;
        }
    }
    function disable(types){ types.forEach(t => { const b = buttons.find(x=>x.dataset.type===t); if (b) b.setAttribute('disabled','');}); }

    buttons.forEach(btn => {
        btn.addEventListener('click', e => {
            if (btn.hasAttribute('disabled')) return;
            const from = currentType();
            const to = btn.dataset.type;
            if (needServerConversion(from,to)){
                targetField.value = mapTypeToInt(to);
                const originalAction = form.getAttribute('action');
                form.setAttribute('action','/AutomatonConversion/SwitchType');
                form.method='post';
                form.submit();
                if (originalAction) form.setAttribute('action', originalAction);
                return;
            }
            // client only
            buttons.forEach(b=>b.classList.remove('active'));
            btn.classList.add('active');
            applyDisableRules(to);
        });
    });

    applyDisableRules(currentType());
}

document.addEventListener('DOMContentLoaded', initAutomatonTypeControls);
