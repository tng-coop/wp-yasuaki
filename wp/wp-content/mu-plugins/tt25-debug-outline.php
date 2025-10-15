<?php
/**
 * Plugin Name: TT25 Debug Outline (color‑coded)
 * Description: Color outlines + labels for block theme anatomy (TT25). Toggle via `?debug=boxes` or define('TT25_DEBUG_OUTLINE', true).
 * Version: 0.1.0
 */
if (!defined('ABSPATH')) exit;

function tt25_dbg_should_outline(): bool {
  if (is_admin()) return false; // front‑end only
  if (defined('TT25_DEBUG_OUTLINE') && TT25_DEBUG_OUTLINE) return true;
  if (isset($_GET['debug']) && $_GET['debug'] === 'boxes') return true;
  return false; // keep opt‑in for public
}

add_filter('body_class', function($classes){
  if (tt25_dbg_should_outline()) $classes[] = 'tt25-outline';
  return $classes;
});

add_action('wp_enqueue_scripts', function(){
  if (!tt25_dbg_should_outline()) return;

  // Inline CSS (no file shipping required)
  wp_register_style('tt25-dbg-outline', false, [], null);
  wp_enqueue_style('tt25-dbg-outline');
  $css = <<<'CSS'
:root{
  --c-root:#000; --c-header:#e53935; --c-footer:#c62828; --c-nav:#fb8c00;
  --c-group:#42a5f5; --c-query:#8e24aa; --c-post:#c2185b; --c-title:#2e7d32;
  --c-content:#6d4c41; --c-logo:#0288d1;
}
/* Outlines */
.tt25-outline .wp-site-blocks{ outline:3px solid var(--c-root); }
.tt25-outline header.wp-block-template-part{ outline:3px solid var(--c-header); }
.tt25-outline footer.wp-block-template-part{ outline:3px solid var(--c-footer); }
.tt25-outline .wp-block-template-part{ outline:2px solid #666; }
.tt25-outline .wp-block-navigation{ outline:2px dashed var(--c-nav); }
.tt25-outline .wp-block-group, .tt25-outline .wp-block-columns, .tt25-outline .wp-block-column{ outline:1.5px dashed var(--c-group); }
.tt25-outline .wp-block-query, .tt25-outline .wp-block-post-template{ outline:2px dashed var(--c-query); }
.tt25-outline .wp-block-post{ outline:2px solid var(--c-post); }
.tt25-outline .wp-block-post-title, .tt25-outline .wp-block-site-title{ outline:2px solid var(--c-title); }
.tt25-outline .wp-block-post-content, .tt25-outline .entry-content{ outline:2px solid var(--c-content); }
.tt25-outline .wp-block-site-logo{ outline:2px solid var(--c-logo); }
/* Labels */
.tt25-outline [data-block]{ position:relative; }
.tt25-outline [data-block]::before{
  content: attr(data-block);
  position:absolute; inset: auto auto calc(100% + 2px) -1px; /* top-left just outside */
  background: rgba(0,0,0,.75); color:#fff; padding:2px 6px; border-radius:4px;
  font: 600 11px/1 ui-monospace, SFMono-Regular, Menlo, Consolas, "Liberation Mono", monospace;
  pointer-events:none; z-index:2147483647; white-space:nowrap;
}
/* Legend */
#tt25-outline-legend{ position:fixed; right:12px; bottom:12px; background:rgba(20,20,20,.85); color:#fff; padding:10px 12px; border-radius:10px; border:1px solid rgba(255,255,255,.2); font:600 12px/1.3 system-ui,-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif; z-index:2147483647; max-width:260px }
#tt25-outline-legend .tt25-legend-title{ font-weight:700; margin:0 0 6px }
#tt25-outline-legend .tt25-legend-item{ display:flex; align-items:center; gap:8px; margin:4px 0; }
#tt25-outline-legend .sw{ width:14px; height:14px; border:3px solid currentColor; display:inline-block; }
#tt25-outline-legend .sw-root{ color:var(--c-root) }
#tt25-outline-legend .sw-header{ color:var(--c-header) }
#tt25-outline-legend .sw-footer{ color:var(--c-footer) }
#tt25-outline-legend .sw-nav{ color:var(--c-nav) }
#tt25-outline-legend .sw-group{ color:var(--c-group) }
#tt25-outline-legend .sw-query{ color:var(--c-query) }
#tt25-outline-legend .sw-post{ color:var(--c-post) }
CSS;
  wp_add_inline_style('tt25-dbg-outline', $css);

  // Inline JS to add labels + legend
  wp_register_script('tt25-dbg-outline', '', [], null, true);
  wp_enqueue_script('tt25-dbg-outline');
  $js = <<<'JS'
(function(){
  function label(el){
    if (el.classList.contains('wp-site-blocks')) return 'template: page';
    if (el.matches('header.wp-block-template-part')) return 'template-part: header';
    if (el.matches('footer.wp-block-template-part')) return 'template-part: footer';
    const cls = Array.from(el.classList).find(c => c.startsWith('wp-block-'));
    return cls ? 'block: ' + cls.replace('wp-block-','') : el.tagName.toLowerCase();
  }
  function apply(){
    const nodes = document.querySelectorAll(
      '.tt25-outline .wp-site-blocks,'+
      '.tt25-outline header.wp-block-template-part,'+
      '.tt25-outline footer.wp-block-template-part,'+
      '.tt25-outline [class*="wp-block-"]'
    );
    nodes.forEach(n => n.setAttribute('data-block', label(n)));

    const L = document.createElement('aside');
    L.id = 'tt25-outline-legend';
    L.innerHTML = [
      '<div class="tt25-legend-title">TT25 structure overlay</div>',
      '<div class="tt25-legend-item"><span class="sw sw-root"></span> template (.wp-site-blocks)</div>',
      '<div class="tt25-legend-item"><span class="sw sw-header"></span> header (template-part)</div>',
      '<div class="tt25-legend-item"><span class="sw sw-footer"></span> footer (template-part)</div>',
      '<div class="tt25-legend-item"><span class="sw sw-nav"></span> navigation</div>',
      '<div class="tt25-legend-item"><span class="sw sw-group"></span> group/columns</div>',
      '<div class="tt25-legend-item"><span class="sw sw-query"></span> query & post-template</div>',
      '<div class="tt25-legend-item"><span class="sw sw-post"></span> post (title/content)</div>'
    ].join('');
    document.body.appendChild(L);
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', apply);
  else apply();
})();
JS;
  wp_add_inline_script('tt25-dbg-outline', $js);
}, 20);
