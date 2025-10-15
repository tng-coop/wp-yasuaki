<?php
/**
 * Plugin Name: TT25 Debug Borders
 * Description: Color-coded borders & labels to visualize site composition (theme wrapper, header, footer, page, posts, loop, etc.), with per-block “responsible” info and tooltips.
 * Version: 0.3
 */
if (!defined('ABSPATH')) exit;

/** Enable/disable logic */
function tt25_dbgb_enabled(): bool {
  if (isset($_GET['debugborders']) && $_GET['debugborders'] === '0') return false;
  if (isset($_GET['debugborders'])) return true;
  if (isset($_GET['debug']) && stripos((string)$_GET['debug'], 'border') !== false) return true;
  if (defined('WP_DEBUG_BORDERS') && WP_DEBUG_BORDERS) return true;
  if (getenv('DEBUG_BORDERS') === '1') return true;
  $env = function_exists('wp_get_environment_type') ? wp_get_environment_type() : 'production';
  return in_array($env, ['local','development','staging'], true);
}

/** Enqueue assets */
add_action('wp_enqueue_scripts', function () {
  if (!tt25_dbgb_enabled()) return;
  $base_dir = WP_CONTENT_DIR . '/mu-plugins';
  $base_url = content_url('mu-plugins');

  $css = $base_dir . '/debug-borders.css';
  $js  = $base_dir . '/debug-borders.js';

  wp_enqueue_style('tt25-debug-borders', $base_url . '/debug-borders.css', [], file_exists($css) ? filemtime($css) : time());
  wp_enqueue_script('tt25-debug-borders', $base_url . '/debug-borders.js', [], file_exists($js) ? filemtime($js) : time(), true);

  // Pass high-level context to JS
  $env   = function_exists('wp_get_environment_type') ? wp_get_environment_type() : 'production';
  $theme = wp_get_theme();
  $ctx   = [
    'env'         => $env,
    'site'        => get_bloginfo('name'),
    'theme'       => ['name' => $theme->get('Name'), 'stylesheet' => $theme->get_stylesheet(), 'version' => $theme->get('Version')],
    'isBlockTheme'=> function_exists('wp_is_block_theme') ? wp_is_block_theme() : false,
  ];

  // Query context
  $q = $GLOBALS['wp_query'] ?? null;
  if ($q instanceof WP_Query) {
    $flags = [];
    foreach (['is_home','is_front_page','is_singular','is_page','is_single','is_archive','is_category','is_tag','is_search','is_404'] as $f) {
      if ($q->$f()) $flags[] = $f;
    }
    $ctx['query'] = [
      'flags' => $flags,
      'found' => (int) $q->found_posts,
      'post_type' => get_query_var('post_type') ?: 'post',
    ];
  }

  wp_localize_script('tt25-debug-borders', 'tt25Dbg', $ctx);
}, 99);

/** Add body class to activate CSS */
add_filter('body_class', function (array $classes) {
  if (tt25_dbgb_enabled()) $classes[] = 'tt25-debug-borders';
  return $classes;
});

/** Helpers */
function tt25_dbg_rel_path(string $path, string $root): string {
  return ltrim(str_replace('\\','/', substr($path, strlen($root))), '/');
}
function tt25_dbg_origin_for_block_type(?string $name): array {
  if (!$name) return ['type'=>'unknown','label'=>'unknown','path'=>null,'class'=>'unknown'];
  if (strpos($name, 'core/') === 0) return ['type'=>'core','label'=>'WordPress core','path'=>null,'class'=>'core'];
  $reg = WP_Block_Type_Registry::get_instance()->get_registered($name);
  if (!$reg) return ['type'=>'unknown','label'=>'unregistered','path'=>null,'class'=>'unknown'];

  $cb = $reg->render_callback ?? null;
  $file = null;
  try {
    if (is_string($cb) && function_exists($cb)) {
      $ref = new ReflectionFunction($cb); $file = $ref->getFileName();
    } elseif (is_array($cb) && count($cb) === 2) {
      $ref = new ReflectionMethod($cb[0], $cb[1]); $file = $ref->getFileName();
    }
  } catch (Throwable $e) { /* ignore */ }

  if ($file) {
    $file = str_replace('\\','/',$file);
    if (defined('WP_PLUGIN_DIR') && strpos($file, WP_PLUGIN_DIR) === 0) {
      return ['type'=>'plugin','label'=>'Plugin','path'=>tt25_dbg_rel_path($file, WP_PLUGIN_DIR), 'class'=>'plugin'];
    }
    $child = get_stylesheet_directory();
    $parent = get_template_directory();
    if ($child && strpos($file, str_replace('\\','/',$child)) === 0) {
      return ['type'=>'theme','label'=>'Theme (child)','path'=>tt25_dbg_rel_path($file, $child), 'class'=>'theme'];
    }
    if ($parent && strpos($file, str_replace('\\','/',$parent)) === 0) {
      return ['type'=>'theme','label'=>'Theme (parent)','path'=>tt25_dbg_rel_path($file, $parent), 'class'=>'theme'];
    }
  }
  // Heuristic: namespace hints (e.g., acf/, jetpack/)
  if (strpos($name, '/') !== false) {
    $ns = explode('/', $name, 2)[0];
    if ($ns !== 'core') return ['type'=>'thirdparty','label'=>$ns.' (3rd-party)','path'=>null,'class'=>'thirdparty'];
  }
  return ['type'=>'unknown','label'=>'unknown','path'=>null,'class'=>'unknown'];
}

function tt25_dbg_inject_attrs_on_root(string $html, array $attrs): string {
  if ($html === '' || $html[0] !== '<') return $html;
  if (!preg_match('/^<([a-zA-Z0-9\-]+)\b/', $html)) return $html;
  $attr_str = '';
  foreach ($attrs as $k=>$v) {
    if ($v === null) continue;
    $attr_str .= ' ' . $k . '="' . esc_attr(is_string($v) ? $v : wp_json_encode($v)) . '"';
  }
  return preg_replace('/^<([a-zA-Z0-9\-]+)\b/', '<$1' . $attr_str, $html, 1);
}
function tt25_dbg_prepend_label_chip(string $html, string $text, string $origin_class): string {
  if ($html === '' || $html[0] !== '<') return $html;
  $label = '<span class="tt25-dbg-label tt25-origin-' . esc_attr($origin_class) . '" aria-hidden="true">' . esc_html($text) . '</span>';
  return preg_replace('/^(<[^>]+>)/', '$1' . $label, $html, 1);
}

/** Build “responsible” summary from block + context */
function tt25_dbg_responsible(array $block, $instance): array {
  $name = $block['blockName'] ?? '';
  $attrs = $block['attrs'] ?? [];
  $ctx   = (is_object($instance) && property_exists($instance,'context')) ? (array)$instance->context : [];

  // Template part
  if ($name === 'core/template-part') {
    $slug = $attrs['slug'] ?? '';
    $area = $attrs['area'] ?? '';
    $theme= $attrs['theme'] ?? get_stylesheet();
    return ['kind'=>'template-part', 'label'=>"Template Part: {$slug} [{$area}] (theme: {$theme})"];
  }

  // Blocks inside post/page content
  if (!empty($ctx['postId']) && !empty($ctx['postType'])) {
    $post_id = (int) $ctx['postId'];
    $pt      = (string) $ctx['postType'];
    return ['kind'=>'post', 'label'=>"Post content: {$pt} #{$post_id}"];
  }

  // Query block
  if ($name === 'core/query') {
    $q = $attrs['query'] ?? [];
    $pt = $q['postType'] ?? 'post';
    $per = $q['perPage'] ?? '';
    return ['kind'=>'query', 'label'=>'Query loop: ' . (is_array($pt) ? implode(',', $pt) : $pt) . ($per ? " (perPage {$per})" : '')];
  }

  // Navigation block
  if ($name === 'core/navigation') {
    $nav = $attrs['ref'] ?? null;
    return ['kind'=>'navigation', 'label'=>$nav ? "Navigation (menu #{$nav})" : 'Navigation'];
  }

  // Fallback
  return ['kind'=>'unknown', 'label'=>'Template / layout'];
}

/** Annotate each rendered block with data-* attributes and a small chip */
add_filter('render_block', function (string $content, array $block, $instance) {
  if (!tt25_dbgb_enabled()) return $content;
  $name = $block['blockName'] ?? null;
  if (!$name || trim($content) === '') return $content;

  $origin = tt25_dbg_origin_for_block_type($name);
  $resp   = tt25_dbg_responsible($block, $instance);

  // Data attributes
  $data = [
    'data-tt25'             => '1',
    'data-tt25-block'       => $name,
    'data-tt25-origin'      => $origin['label'] . ($origin['path'] ? ' — ' . $origin['path'] : ''),
    'data-tt25-origin-key'  => $origin['type'],
    'data-tt25-resp'        => $resp['label'],
  ];

  // Post context (if available)
  if (is_object($instance) && property_exists($instance,'context')) {
    $ctx = (array)$instance->context;
    if (!empty($ctx['postId']))   $data['data-tt25-post-id']   = (string) $ctx['postId'];
    if (!empty($ctx['postType'])) $data['data-tt25-post-type'] = (string) $ctx['postType'];
  }

  // Special-case details
  $attrs = $block['attrs'] ?? [];
  if ($name === 'core/template-part') {
    if (!empty($attrs['slug']))  $data['data-tt25-template-part'] = $attrs['slug'];
    if (!empty($attrs['area']))  $data['data-tt25-template-area'] = $attrs['area'];
    if (!empty($attrs['theme'])) $data['data-tt25-template-theme']= $attrs['theme'];
  }
  if ($name === 'core/query' && !empty($attrs['query'])) {
    $data['data-tt25-query'] = wp_json_encode($attrs['query']);
  }

  // Inject attributes + label chip into the block's root element
  $label = $name;
  if ($resp['label']) $label .= ' · ' . $resp['label'];
  if ($origin['label']) $label .= ' · ' . $origin['label'];

  $content = tt25_dbg_inject_attrs_on_root($content, $data);
  $content = tt25_dbg_prepend_label_chip($content, $label, $origin['class']);

  return $content;
}, 10, 3);

/** Floating legend / controls */
add_action('wp_footer', function () {
  if (!tt25_dbgb_enabled()) return; ?>
  <div id="tt25-debug-legend" class="tt25-dbg-panel" aria-live="polite">
    <div class="tt25-dbg-row">
      <strong>Debug borders</strong>
      <button type="button" class="tt25-dbg-btn" data-tt25-toggle="labels" title="Toggle labels (L)">Labels</button>
      <button type="button" class="tt25-dbg-btn" data-tt25-toggle="tooltip" title="Toggle details (hold ⌥/Alt or click)">Details</button>
      <button type="button" class="tt25-dbg-btn" data-tt25-toggle="borders" title="Toggle borders (B)">Borders</button>
    </div>
    <div class="tt25-dbg-grid">
      <span class="swatch theme"></span><span>theme wrapper / template parts</span>
      <span class="swatch core"></span><span>core blocks</span>
      <span class="swatch plugin"></span><span>plugin/third-party blocks</span>
      <span class="swatch post"></span><span>post/page content</span>
      <span class="swatch loop"></span><span>query/loop</span>
      <span class="swatch nav"></span><span>navigation</span>
    </div>
    <div class="tt25-dbg-hint">Hold <kbd>Alt</kbd> to see a live tooltip for the element under your cursor. Add <code>?debugborders=0</code> to hide.</div>
  </div>
  <div id="tt25-dbg-tooltip" role="tooltip" hidden></div>
<?php }, 99);

