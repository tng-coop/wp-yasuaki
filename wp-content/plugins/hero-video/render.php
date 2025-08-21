<?php
defined('ABSPATH') || exit;

/**
 * Map the chosen configName (saved by the editor) to a full config array.
 * This block is intended for the FRONT PAGE preset by default.
 */
$name = isset($attributes['configName']) && is_string($attributes['configName'])
  ? $attributes['configName']
  : 'frontpage';

$presets = [
  // FRONT PAGE preset (default)
  'frontpage' => [
    'pexelVideos' => [6394054, 30646036],
    'transition'  => 3,
  ],

  // Other examples—adjust as needed
  'aboutpage' => [
    'pexelVideos' => [1111111, 2222222],
    'transition'  => 5,
  ],
  'landingA' => [
    'pexelVideos' => [1234567, 7654321],
    'transition'  => 2,
  ],
];

// pick preset or fall back to frontpage
$config = $presets[$name] ?? $presets['frontpage'];

/** sanitize */
$ids = array_values(array_filter(array_map('intval', $config['pexelVideos'] ?? [])));
$transition = isset($config['transition']) ? intval($config['transition']) : 3;
$config = ['pexelVideos' => $ids, 'transition' => $transition];

$api = rest_url('pexels-proxy/v1/video');

printf(
  '<div class="hero-video-container" data-config="%s" data-api="%s"></div>',
  esc_attr(wp_json_encode($config)),
  esc_url($api)
);
