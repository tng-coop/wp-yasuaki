<?php
/**
 * Plugin Name: Relax Authors (Edit Others' Posts)
 * Description: Allows the Author role to edit others' posts (and optionally delete them).
 */

add_action('init', function () {
    if (!function_exists('get_role')) return;
    $role = get_role('author');
    if (!$role) return;

    // Core relaxations
    $role->add_cap('edit_others_posts');
    $role->add_cap('edit_published_posts');

    // Optional: uncomment if you want Authors to delete others' posts
    // $role->add_cap('delete_others_posts');

    // Optional: usually keep this commented to protect published content
    // $role->add_cap('delete_published_posts');
});
