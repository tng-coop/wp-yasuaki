(function () {
  if (!document.body.classList.contains('tt25-debug-borders')) return;

  const body = document.body;
  const panel = document.getElementById('tt25-debug-legend');
  const tooltip = document.getElementById('tt25-dbg-tooltip');

  // State
  const state = {
    borders: true,
    labels: true,
    tooltip: false, // show on Alt by default
  };
  try {
    const persisted = JSON.parse(localStorage.getItem('tt25DbgState') || '{}');
    Object.assign(state, persisted);
  } catch (_) {}

  applyState();

  // Controls
  if (panel) {
    panel.addEventListener('click', (e) => {
      const btn = e.target.closest('[data-tt25-toggle]');
      if (!btn) return;
      const what = btn.getAttribute('data-tt25-toggle');
      if (what === 'borders') state.borders = !state.borders;
      if (what === 'labels')  state.labels  = !state.labels;
      if (what === 'tooltip') state.tooltip = !state.tooltip;
      persistState(); applyState();
    });
  }

  // Alt to show tooltip (unless always-on via state.tooltip)
  let altDown = false;
  window.addEventListener('keydown', (e) => { if (e.altKey) altDown = true; });
  window.addEventListener('keyup', (e) => { if (!e.altKey) altDown = false; });

  // Hover tracking for tooltip
  let lastTarget = null;
  document.addEventListener('mousemove', (e) => {
    const el = e.target.closest('[data-tt25]');
    lastTarget = el;
    maybeShowTooltip(e);
  });
  document.addEventListener('mouseleave', hideTooltip, true);
  document.addEventListener('scroll', hideTooltip, true);

  function maybeShowTooltip(e) {
    if (!lastTarget) return hideTooltip();
    if (!(state.tooltip || altDown)) return hideTooltip();

    const ds = lastTarget.dataset;
    const rows = [];

    pushKV('Block', ds.tt25Block);
    pushKV('Responsible', ds.tt25Resp);
    pushKV('Origin', ds.tt25Origin);
    if (ds.tt25PostId || ds.tt25PostType) pushKV('Post', `${ds.tt25PostType || 'post'} #${ds.tt25PostId || '?'}`);
    if (ds.tt25TemplatePart) {
      const area = ds.tt25TemplateArea ? ` [${ds.tt25TemplateArea}]` : '';
      const theme = ds.tt25TemplateTheme ? ` (theme ${ds.tt25TemplateTheme})` : '';
      pushKV('Template part', `${ds.tt25TemplatePart}${area}${theme}`);
    }
    if (ds.tt25Query) {
      try {
        const q = JSON.parse(ds.tt25Query);
        const parts = [];
        if (q.postType) parts.push(`type: ${Array.isArray(q.postType) ? q.postType.join(',') : q.postType}`);
        if (q.perPage)  parts.push(`perPage: ${q.perPage}`);
        if (q.order)    parts.push(`order: ${q.order}`);
        if (q.orderBy)  parts.push(`orderBy: ${Array.isArray(q.orderBy) ? q.orderBy.join(',') : q.orderBy}`);
        pushKV('Query', parts.join(' · ') || JSON.stringify(q));
      } catch (_) { pushKV('Query', ds.tt25Query); }
    }

    // High-level page context (from PHP)
    if (window.tt25Dbg) {
      const ctx = window.tt25Dbg;
      const meta = [];
      if (ctx.env) meta.push(`env: ${ctx.env}`);
      if (ctx.theme && ctx.theme.stylesheet) meta.push(`theme: ${ctx.theme.stylesheet}`);
      if (ctx.query && ctx.query.flags && ctx.query.flags.length) meta.push(`context: ${ctx.query.flags.join(', ')}`);
      if (meta.length) pushKV('Page', meta.join(' · '));
    }

    tooltip.innerHTML = rows.join('');
    tooltip.hidden = false;
    positionTooltip(e.clientX, e.clientY);
    function pushKV(k, v) {
      if (!v) return;
      rows.push(`<div><span class="k">${escapeHtml(k)}:</span> <span class="v">${escapeHtml(v)}</span></div>`);
    }
  }

  function hideTooltip() {
    if (tooltip) tooltip.hidden = true;
  }

  function positionTooltip(x, y) {
    const pad = 12;
    const w = tooltip.offsetWidth;
    const h = tooltip.offsetHeight;
    let left = x + pad;
    let top  = y + pad;
    // Keep on-screen
    const vw = Math.max(document.documentElement.clientWidth, window.innerWidth || 0);
    const vh = Math.max(document.documentElement.clientHeight, window.innerHeight || 0);
    if (left + w > vw - 8) left = vw - w - 8;
    if (top + h > vh - 8)  top  = vh - h - 8;
    tooltip.style.left = left + 'px';
    tooltip.style.top  = top  + 'px';
  }

  function applyState() {
    body.classList.toggle('no-borders', !state.borders);
    body.classList.toggle('no-labels',  !state.labels);
  }
  function persistState() {
    try { localStorage.setItem('tt25DbgState', JSON.stringify(state)); } catch (_) {}
  }
  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  }
})();

