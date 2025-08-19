<?php
defined('ABSPATH') || exit;

$config = isset($attributes['config']) && is_array($attributes['config'])
    ? $attributes['config']
    : ['pexelVideos' => [6394054, 30646036], 'transition' => 3];

// Sanitize
$ids = array_values(array_filter(array_map('intval', $config['pexelVideos'] ?? [])));
$transition = isset($config['transition']) ? intval($config['transition']) : 3;
$config = ['pexelVideos' => $ids, 'transition' => $transition];

$api = rest_url('pexels-proxy/v1/video');

printf(
  '<div class="hero-video-container" data-config="%s" data-api="%s"></div>',
  esc_attr(wp_json_encode($config)),
  esc_url($api)
);
