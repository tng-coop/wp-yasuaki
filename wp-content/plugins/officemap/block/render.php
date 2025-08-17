<?php
defined('ABSPATH') || exit;

/**
 * Dynamic render: prints a container with a server-generated nonce.
 * Use a data attribute selector so multiple instances work.
 */
$nonce = wp_create_nonce('officemap_nonce');

$attrs = get_block_wrapper_attributes([
    'class' => 'officemap-nonce',
    'data-officemap' => '1',
    'data-nonce' => esc_attr($nonce),
]);

// Show the nonce visibly for now so you can confirm it in editor/frontend.
// Later you can hide this and let JS consume it.
echo '<div ' . $attrs . '>
  <p><strong>OfficeMap Nonce:</strong> <code>' . esc_html($nonce) . '</code></p>
</div>';
