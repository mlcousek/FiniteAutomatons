(function(){
  function mapTypeToInt(t){
    switch(t){
      case 'DFA': return 0;
      case 'NFA': return 1;
      case 'EpsilonNFA': return 2;
      case 'DPDA': return 3;
      case 'NPDA': return 4;
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
    const closeBtn = document.getElementById('newAutomatonModalClose');

    if (!newBtn || !modalEl) return;

    const openModal = () => {
        modalEl.style.display = 'flex';
        document.body.style.overflow = 'hidden';
        modalEl.offsetHeight; 
        modalEl.classList.add('modal-open');
    };

    const closeModal = () => {
        modalEl.classList.remove('modal-open');
        setTimeout(() => {
            modalEl.style.display = 'none';
            document.body.style.overflow = '';
        }, 200);
    };

    newBtn.addEventListener('click', openModal);

    if (closeBtn) {
        closeBtn.addEventListener('click', closeModal);
    }

    window.addEventListener('click', (e) => { 
        if (e.target === modalEl) closeModal(); 
    });

    window.addEventListener('keydown', (e) => { 
        if (e.key === 'Escape' && modalEl.style.display === 'flex') closeModal(); 
    });

    modalEl.querySelectorAll('.type-select').forEach(btn => {
      btn.addEventListener('click', function(){
        const t = btn.dataset.type;
        closeModal();
        submitNewAutomaton(t);
      });
    });
  });
})();
