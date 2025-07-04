<?php
/**
 * MU-Plugin: Contributor Role Enhancements
 * Grants contributors the ability to reassign authorship and (optionally) work with private posts.
 */

// Hook into init so WP has loaded all roles
add_action( 'init', function() {
    // Grab the Contributor role
    $role = get_role( 'contributor' );
    if ( ! $role ) {
        return; // nothing to do if the role doesn't exist
    }

    // Core extra capabilities:
    $role->add_cap( 'edit_others_posts' );      // lets them open others’ posts—and shows the Author dropdown
    // $role->add_cap( 'edit_published_posts' );   // lets them edit published posts (and change authors there too)

    // OPTIONAL: if you also want them to see or edit private posts:
    $role->add_cap( 'read_private_posts' );
    $role->add_cap( 'edit_private_posts' );
} );

