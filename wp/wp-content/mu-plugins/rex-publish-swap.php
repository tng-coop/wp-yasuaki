<?php
/**
 * Plugin Name: REX Publish Swap (MU)
 * Description: On publish, if the post is a fork, swap into the original, copy terms/meta, and trash the staging.
 * Version: 0.1.0
 */

add_action('transition_post_status', 'rex_on_publish_swap', 10, 3);

function rex_on_publish_swap($new, $old, $post) {
  if ($new !== 'publish' || $old === 'publish') return;                 // Only when newly published
  if (wp_is_post_revision($post) || wp_is_post_autosave($post)) return;

  $staging_id = (int) $post->ID;
  $orig_id    = (int) get_post_meta($staging_id, '_rex_original_post_id', true);
  if (!$orig_id) return;                                               // Not a fork → nothing to do

  // Capability checks (type-aware).
  $type = get_post_type($orig_id);
  $caps = get_post_type_object($type)->cap;
  if (!current_user_can('edit_post', $orig_id) || !current_user_can($caps->publish_posts)) return;
  if (!current_user_can('delete_post', $staging_id)) return;

  // If the original is in trash, bring it back first.
  if (get_post_status($orig_id) === 'trash') {
    if (!current_user_can('delete_post', $orig_id)) return;
    wp_untrash_post($orig_id);
  }

  // Copy core fields from staging → original (keep original slug by default).
  wp_update_post([
    'ID'           => $orig_id,
    'post_title'   => $post->post_title,
    'post_content' => $post->post_content,
    'post_excerpt' => $post->post_excerpt,
    'post_status'  => 'publish',
    // 'post_name'  => $post->post_name, // Uncomment if you want to adopt the fork’s slug
  ]);

  // Copy terms.
  foreach (get_object_taxonomies($type) as $tax) {
    $terms = wp_get_object_terms($staging_id, $tax, ['fields' => 'ids']);
    if (!is_wp_error($terms)) wp_set_object_terms($orig_id, $terms, $tax, false);
  }

  // Copy meta: exclude internals but keep featured image, SEO fields, etc.
  rex_copy_meta($staging_id, $orig_id, [
    '_rex_%', '_edit_lock', '_edit_last', '_wp_old_slug', '_wp_trash_%'
  ]);

  // Optional breadcrumbs for audits
  update_post_meta($orig_id, '_rex_last_publish_source', $staging_id);

  // Trash the staging copy (idempotent if already trashed).
  wp_trash_post($staging_id);
}

function rex_copy_meta($from_id, $to_id, array $exclude_like) {
  $all = get_post_meta($from_id);
  foreach ($all as $key => $values) {
    foreach ($exclude_like as $pattern) {
      if (fnmatch($pattern, $key)) continue 2; // skip this key
    }
    delete_post_meta($to_id, $key);
    foreach ($values as $v) add_post_meta($to_id, $key, maybe_unserialize($v));
  }
}
