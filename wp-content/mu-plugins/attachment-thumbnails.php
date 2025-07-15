<?php
/**
 * Plugin Name:     Attachment Thumbnail Support
 * Description:     Enables featured‐image (thumbnail) support for media attachments.
 * Author:          (you)
 * Version:         1.0
 */

// Hook into init so it's registered on every request.
add_action( 'init', function() {
    // Tell WP that attachments (media items) support 'thumbnail'
    add_post_type_support( 'attachment', 'thumbnail' );
} );