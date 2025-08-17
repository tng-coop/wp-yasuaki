// view.js (ESM) — runs automatically on frontend AND editor due to "viewScriptModule" in block.json

function boot(el) {
  if (!el || el.__officemapBooted) return;
  const nonce = el.dataset?.nonce;
  if (!nonce) return;

  el.__officemapBooted = true; // avoid double-inits
  console.log('OfficeMap nonce (instance):', nonce, el);

  // TODO: your real init code goes here (fetch/REST/etc)
}

function initExisting() {
  document.querySelectorAll('[data-officemap="1"]').forEach(boot);
}

function observeNewOnes() {
  const mo = new MutationObserver((muts) => {
    for (const m of muts) {
      for (const n of m.addedNodes) {
        if (n.nodeType !== 1) continue;
        if (n.matches?.('[data-officemap="1"]')) boot(n);
        n.querySelectorAll?.('[data-officemap="1"]').forEach(boot);
      }
    }
  });
  mo.observe(document.body, { childList: true, subtree: true });
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', () => {
    initExisting();
    observeNewOnes();
  });
} else {
  initExisting();
  observeNewOnes();
}
