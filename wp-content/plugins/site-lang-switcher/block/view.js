(function () {
  document.addEventListener('DOMContentLoaded', function () {
    const storageKey = 'siteLang';
    const enEls = document.querySelectorAll('.lang-en');
    const jpEls = document.querySelectorAll('.lang-jp');
    const btnEn = document.getElementById('btn-en');
    const btnJp = document.getElementById('btn-jp');

    if (!btnEn || !btnJp) return; // block not present on this page

    function show(lang, save) {
      enEls.forEach(el => { el.style.display = (lang === 'en') ? '' : 'none'; });
      jpEls.forEach(el => { el.style.display = (lang === 'jp') ? '' : 'none'; });
      btnEn.classList.toggle('active', lang === 'en');
      btnJp.classList.toggle('active', lang === 'jp');
      if (save) localStorage.setItem(storageKey, lang);
    }

    // initial state: stored value or browser default
    let initial = localStorage.getItem(storageKey);
    if (!initial) {
      const userLang = (navigator.language || '').toLowerCase();
      initial = userLang.startsWith('ja') ? 'jp' : 'en';
    }
    show(initial);

    // click handlers
    btnEn.addEventListener('click', () => show('en', true));
    btnJp.addEventListener('click', () => show('jp', true));
  });
})();
