<?php
/**
 * Plugin Name: Block Delete User Content
 * Description: Prevents deletion of a user's posts/pages when deleting that user. Forces reassignment.
 */

defined('ABSPATH') || exit;

/**
 * Stop "delete all content" when deleting a user.
 *
 * @param int      $id       ID of the deleted user.
 * @param int|bool $reassign ID of the user to reassign posts to, or false if content should be deleted.
 */
add_action('delete_user', function( $id, $reassign ) {
    if ( $reassign === false ) {
        wp_die(
            __( 'Deleting a user\'s content is disabled. You must reassign their posts/pages to another user.' ),
            __( 'Deletion Blocked' ),
            [ 'response' => 403 ]
        );
    }
}, 5, 2);

