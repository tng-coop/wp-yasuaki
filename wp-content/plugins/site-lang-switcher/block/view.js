// view.js
// Module-based version of the language switcher view script

/**
 * Initialize the language switcher: set initial state and bind event handlers.
 */
function initSiteLangSwitcher() {
  const storageKey = 'siteLang';
  const enEls = document.querySelectorAll('.lang-en');
  const jpEls = document.querySelectorAll('.lang-jp');
  const btnEn = document.getElementById('btn-en');
  const btnJp = document.getElementById('btn-jp');

  if (!btnEn || !btnJp) {
    // Block not present on this page
    return;
  }

  /**
   * Show or hide elements and update active button state.
   * @param {'en'|'jp'} lang
   * @param {boolean} save
   */
  function show(lang, save) {
    enEls.forEach(el => { el.style.display = lang === 'en' ? '' : 'none'; });
    jpEls.forEach(el => { el.style.display = lang === 'jp' ? '' : 'none'; });
    btnEn.classList.toggle('active', lang === 'en');
    btnJp.classList.toggle('active', lang === 'jp');
    if (save) {
      localStorage.setItem(storageKey, lang);
    }
  }

  // Determine initial language from storage or browser default
  let initial = localStorage.getItem(storageKey);
  if (!initial) {
    const userLang = (navigator.language || '').toLowerCase();
    initial = userLang.startsWith('ja') ? 'jp' : 'en';
  }
  show(initial, false);

  // Attach click handlers
  btnEn.addEventListener('click', () => show('en', true));
  btnJp.addEventListener('click', () => show('jp', true));
}

// Automatically initialize on DOM ready
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initSiteLangSwitcher);
} else {
  initSiteLangSwitcher();
}

