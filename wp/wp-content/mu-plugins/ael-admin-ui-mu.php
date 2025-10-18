<?php
/**
 * Plugin Name: Approved Email List — Admin UI (MU)
 * Description: MU plugin that adds a Users → Approved Emails screen for managing the approved_email_list option.
 * Author: Your Team
 * Version: 1.0.0
 */

if ( ! defined('ABSPATH') ) exit;

/**
 * Capability: Editors and above (edit_pages) to match API behavior.
 * Change to 'manage_options' if you want only admins.
 */
const AEL_CAP = 'edit_pages';
const AEL_OPTION = 'approved_email_list';

/** ---------- Helpers ---------- */

/** Get the normalized list (lowercased, unique, array). */
function ael_get_list(): array {
    $list = get_option(AEL_OPTION, []);
    if ( ! is_array($list) ) { $list = []; }
    $list = array_map('strval', $list);
    $list = array_map('strtolower', $list);
    $list = array_values(array_unique(array_filter($list, 'strlen')));
    sort($list);
    return $list;
}

/** Persist a normalized list. */
function ael_set_list(array $list): void {
    $list = array_map('strval', $list);
    $list = array_map('strtolower', $list);
    $list = array_values(array_unique(array_filter($list, 'strlen')));
    sort($list);
    update_option(AEL_OPTION, $list, true);
}

/** Sanitize a single email (returns '' if invalid). */
function ael_clean_email(string $raw): string {
    $email = sanitize_email($raw);
    return $email ? strtolower($email) : '';
}

/** Parse bulk input (newline/comma/semicolon/space separated). */
function ael_parse_bulk(string $raw): array {
    $parts = preg_split('/[\s,;]+/', $raw);
    $out = [];
    foreach ( $parts as $p ) {
        $e = ael_clean_email($p);
        if ( $e ) { $out[] = $e; }
    }
    return array_values(array_unique($out));
}

/** ---------- Admin UI ---------- */

add_action('admin_menu', function () {
    add_users_page(
        'Approved Emails',
        'Approved Emails',
        AEL_CAP,
        'ael-admin-ui',
        'ael_render_admin_page'
    );
});

/** Process form + render page. */
function ael_render_admin_page() {
    if ( ! current_user_can(AEL_CAP) ) {
        wp_die(esc_html__('Insufficient permissions.', 'ael'));
    }

    $notice = null;

    // Handle POST
    if ( 'POST' === $_SERVER['REQUEST_METHOD'] ) {
        check_admin_referer('ael_admin_ui');

        $list = ael_get_list();

        // Add one
        if ( isset($_POST['add_email']) ) {
            $email = ael_clean_email( wp_unslash($_POST['email'] ?? '') );
            if ( $email ) {
                if ( ! in_array($email, $list, true) ) {
                    $list[] = $email;
                    ael_set_list($list);
                    $notice = ['success', sprintf('Added “%s”.', esc_html($email))];
                } else {
                    $notice = ['warning', sprintf('“%s” is already on the list.', esc_html($email))];
                }
            } else {
                $notice = ['error', 'Please enter a valid email address.'];
            }
        }

        // Remove selected
        if ( ! empty($_POST['remove']) && is_array($_POST['remove']) ) {
            $to_remove = array_map(
                'ael_clean_email',
                array_map('wp_unslash', (array) $_POST['remove'])
            );
            $to_remove = array_values(array_filter($to_remove));
            if ( $to_remove ) {
                $list = array_values(array_diff($list, $to_remove));
                ael_set_list($list);
                $notice = ['success', 'Removed selected email(s).'];
            }
        }

        // Bulk import
        if ( isset($_POST['bulk_text']) ) {
            $bulk = ael_parse_bulk( wp_unslash($_POST['bulk_text']) );
            if ( $bulk ) {
                $merged = array_values(array_unique(array_merge($list, $bulk)));
                ael_set_list($merged);
                $added = count($merged) - count($list);
                $notice = ['success', sprintf('Imported %d email(s).', $added)];
            } else {
                $notice = ['warning', 'No valid emails found to import.'];
            }
        }
    }

    $list = ael_get_list();

    echo '<div class="wrap">';
    echo '<h1>Approved Emails</h1>';

    // Notices
    if ( $notice ) {
        [$type, $msg] = $notice;
        $class = 'notice notice-' . esc_attr($type);
        echo '<div class="' . $class . '"><p>' . esc_html($msg) . '</p></div>';
    }

    // Add form
    echo '<h2 class="title">Add Email</h2>';
    echo '<form method="post" style="margin-bottom:20px;">';
    wp_nonce_field('ael_admin_ui');
    echo '<p><input type="email" name="email" required class="regular-text" placeholder="user@example.com"> ';
    echo '<button class="button button-primary" name="add_email" value="1">Add</button></p>';
    echo '</form>';

    // Bulk import
    echo '<h2 class="title">Bulk Import</h2>';
    echo '<form method="post" style="margin-bottom:20px;">';
    wp_nonce_field('ael_admin_ui');
    echo '<p><textarea name="bulk_text" rows="5" class="large-text" placeholder="Paste emails separated by newlines, commas, or spaces"></textarea></p>';
    echo '<p><button class="button">Import</button></p>';
    echo '</form>';

    // Export
    echo '<h2 class="title">Export</h2>';
    echo '<p>Copy the current list (one per line):</p>';
    echo '<textarea readonly rows="6" class="large-text" onclick="this.select()">' . esc_textarea(implode("\n", $list)) . '</textarea>';

    // Table
    echo '<h2 class="title" style="margin-top:20px;">Current List (' . count($list) . ')</h2>';
    echo '<form method="post">';
    wp_nonce_field('ael_admin_ui');
    echo '<table class="widefat striped"><thead><tr><th style="width:90px">Remove</th><th>Email</th></tr></thead><tbody>';

    if ( $list ) {
        foreach ( $list as $e ) {
            echo '<tr>';
            echo '<td><label><input type="checkbox" name="remove[]" value="' . esc_attr($e) . '"> Remove</label></td>';
            echo '<td>' . esc_html($e) . '</td>';
            echo '</tr>';
        }
    } else {
        echo '<tr><td colspan="2"><em>No emails yet.</em></td></tr>';
    }

    echo '</tbody></table>';
    echo '<p><button class="button">Apply</button></p>';
    echo '</form>';

    echo '</div>';
}
