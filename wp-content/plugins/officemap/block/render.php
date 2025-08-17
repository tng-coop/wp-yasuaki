<?php
/**
 * Dynamic renderer for the OfficeMap block.
 * - Fetches the first published "office-cpt" post via WP_Query (no REST roundtrip).
 * - Displays its Address (from post meta "data[Address]" if available).
 * - Keeps a nonce in data attributes for JS consumption.
 */
defined('ABSPATH') || exit;

$nonce = wp_create_nonce('officemap_nonce');

/**
 * Query the latest office-cpt item.
 * Adjust 'orderby'/'order' to your needs (e.g., menu_order, title, etc.).
 */
$q = new WP_Query([
    'post_type'      => 'office-cpt',
    'posts_per_page' => 1,
    'orderby'        => 'date',
    'order'          => 'DESC',
    'post_status'    => 'publish',
]);

$address = '';

if ($q->have_posts()) {
    $q->the_post();

    /**
     * Your REST sample shows an object under "data" with keys like Address, TEL, etc.
     * If that structure is saved in post meta (key "data"), we can read it here.
     * If not, replace this with the correct meta keys (e.g., get_post_meta(get_the_ID(), 'Address', true)).
     */
    $data = get_post_meta(get_the_ID(), 'data', true);

    if (is_array($data) && !empty($data['Address'])) {
        $address = $data['Address'];
    }

    wp_reset_postdata();
}

/**
 * Build wrapper attributes. We keep the nonce and a marker for the view.js script.
 */
$attrs = get_block_wrapper_attributes([
    'class'          => 'officemap-nonce',
    'data-officemap' => '1',
    'data-nonce'     => esc_attr($nonce),
]);

echo '<div ' . $attrs . '>';

if ($address) {
    echo '<p><strong>First Office Address:</strong> ' . esc_html($address) . '</p>';
} else {
    echo '<p><em>No office address found.</em></p>';
}

echo '</div>';
