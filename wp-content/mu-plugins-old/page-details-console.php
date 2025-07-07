<?php
/**
 * Plugin Name: Page Details & Background Video Loader (MU)
 * Description: Only on page “aa”: logs page info, shows a clock, and cross-fades videos using optimal resolution for current screen.
 */

add_action( 'wp_enqueue_scripts', function() {
    if ( ! is_page( 'aa' ) ) {
        return;
    }
    wp_register_style( 'paadb-hero-inline', false );
    wp_enqueue_style( 'paadb-hero-inline' );

    $svg_url = esc_url( WPMU_PLUGIN_URL . '/kanagawa.svg' );

    $css = <<<CSS
.js-hero-hook {
  position:   relative;
  overflow:   hidden;
  width:      100%;
  height:     30vh;
}

/* ensure the video sits below the overlay */
.js-hero-hook video {
  position: relative;
  z-index: 1;
}

/* create an absolutely‐positioned overlay with a semi-transparent tint behind the SVG */
.js-hero-hook::before {
  content: "";
  position: absolute;
  top:    5vh;
  left:   10px;
  right:  calc(100vw - 400px);
  bottom: 5vh;
  /* first layer: 50% black tint; second layer: the SVG centered & contained */
  background-color: rgba(24, 51, 105, 0.29);
  background-image: url("{$svg_url}");
  background-repeat: no-repeat;
  background-position: center;
  background-size: contain;
  z-index: 2;
  pointer-events: none;
}

CSS;

    wp_add_inline_style( 'paadb-hero-inline', $css );

    // Enqueue local JS library
    $lib_path = WPMU_PLUGIN_DIR . '/my-lib/dist/my-lib.js';
    wp_enqueue_script(
        'my-local-lib',
        WPMU_PLUGIN_URL . '/my-lib/dist/my-lib.js',
        [],
        filemtime( $lib_path ),
        true
    );

    // Load full JSON data for each Pexels video ID
    $ids = [6394054, 30646036];
    $video_data = [];
    foreach ( $ids as $id ) {
        $file = WPMU_PLUGIN_DIR . "/pexels-video-{$id}.json";
        if ( file_exists( $file ) ) {
            $json = json_decode( file_get_contents( $file ), true );
            if ( ! empty( $json['video_files'] ) ) {
                $video_data[ $id ] = $json['video_files'];
            }
        }
    }

    // Page info
    $page_id = get_queried_object_id();
    $title   = get_the_title( $page_id );
    $slug    = get_post_field( 'post_name', $page_id );
    $url     = get_permalink( $page_id );

    // Register and enqueue external script
    $script_rel = '/page-details-console.js';
    wp_register_script(
        'page-details-console',
        WPMU_PLUGIN_URL . $script_rel,
        [ 'my-local-lib' ],
        filemtime( WPMU_PLUGIN_DIR . $script_rel ),
        true
    );

    $local_data = [
        'pageId'    => $page_id,
        'title'     => $title,
        'slug'      => $slug,
        'url'       => $url,
        'videoData' => $video_data,
    ];

    wp_add_inline_script( 'page-details-console', 'var pageDetailsData = ' . wp_json_encode( $local_data ) . ';', 'before' );
    wp_enqueue_script( 'page-details-console' );
}, 1 );
