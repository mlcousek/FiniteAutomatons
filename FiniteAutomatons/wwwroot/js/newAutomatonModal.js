(function(){
  function mapTypeToInt(t){
    switch(t){
      case 'DFA': return 0;
      case 'NFA': return 1;
      case 'EpsilonNFA': return 2;
      case 'PDA': return 3;
      default: return 0;
    }
  }

  function createHidden(name, value){
    const i = document.createElement('input');
    i.type = 'hidden';
    i.name = name;
    i.value = value;
    i.dataset.newAutomaton = 'true';
    return i;
  }

  function submitNewAutomaton(type){
    const form = document.createElement('form');
    form.method = 'post';
    form.action = '/AutomatonCreation/CreateAutomaton';

    form.appendChild(createHidden('Type', String(mapTypeToInt(type))));
    form.appendChild(createHidden('Input', ''));
    form.appendChild(createHidden('HasExecuted', 'false'));
    form.appendChild(createHidden('Position', '0'));
    form.appendChild(createHidden('IsCustomAutomaton', 'false'));

    form.appendChild(createHidden('States.Index', '0'));
    form.appendChild(createHidden('States[0].Id', '0'));
    form.appendChild(createHidden('States[0].IsStart', 'true'));
    form.appendChild(createHidden('States[0].IsAccepting', 'false'));

    document.body.appendChild(form);
    form.submit();
  }

  document.addEventListener('DOMContentLoaded', function(){
    const newBtn = document.getElementById('newAutomatonBtn');
    const modalEl = document.getElementById('newAutomatonModal');
    if (!newBtn || !modalEl) return;
    const bsModal = new bootstrap.Modal(modalEl, { backdrop: 'static' });
    newBtn.addEventListener('click', function(){ bsModal.show(); });

    modalEl.querySelectorAll('.type-select').forEach(btn => {
      btn.addEventListener('click', function(){
        const t = btn.dataset.type;
        // Proceed without additional browser confirm; modal already shows a warning
        bsModal.hide();
        submitNewAutomaton(t);
      });
    });
  });
})();
