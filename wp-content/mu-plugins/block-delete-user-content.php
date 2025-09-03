<?php
/**
 * Plugin Name: Block Delete User Content
 * Description: Forces reassignment when deleting a user; blocks the "delete all content" option.
 */

defined('ABSPATH') || exit;

add_action('load-users.php', function () {
    // Only act on the actual deletion submit step
    $action  = $_REQUEST['action']  ?? '';
    $action2 = $_REQUEST['action2'] ?? '';
    if ($action !== 'dodelete' && $action2 !== 'dodelete') {
        return;
    }

    // If the form tried to delete content instead of reassigning, stop it
    $delete_option = $_REQUEST['delete_option'] ?? '';
    if ($delete_option === 'delete') {
        wp_die(
            __( "Deleting a user's content is disabled. You must reassign their posts/pages to another user." ),
            __( 'Deletion Blocked' ),
            [ 'response' => 403 ]
        );
    }
}, 0);
