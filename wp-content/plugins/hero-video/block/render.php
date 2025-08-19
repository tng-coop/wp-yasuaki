<?php
defined('ABSPATH') || exit;

$goat_id      = isset($attributes['goatId']) ? intval($attributes['goatId']) : 30646036;
$waterfall_id = isset($attributes['waterfallId']) ? intval($attributes['waterfallId']) : 6394054;

$api = rest_url('pexels-proxy/v1/video');

printf(
  '<div id="hero-video" class="hero-video-container" data-goat-id="%d" data-waterfall-id="%d" data-api="%s"></div>',
  $goat_id,
  $waterfall_id,
  esc_url($api)
);
