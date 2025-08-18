<?php
// hero-video/block/render.php
if ( ! defined( 'ABSPATH' ) ) { exit; }

$goat_id      = isset( $attributes['goatId'] ) ? intval( $attributes['goatId'] ) : 30646036;
$waterfall_id = isset( $attributes['waterfallId'] ) ? intval( $attributes['waterfallId'] ) : 6394054;

$api = rest_url( 'hero-video/v1/pexels' );

printf(
  '<div id="hero-video" class="hero-video-container" data-goat-id="%d" data-waterfall-id="%d" data-api="%s"></div>',
  $goat_id,
  $waterfall_id,
  esc_url( $api )
);
