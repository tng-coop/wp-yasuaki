<?php
/**
 * Plugin Name: Branch User Assignment (MU)
 * Description: Editors+ can assign multiple branch taxonomy terms to users via checkboxes; Authors and below read-only. Posts inherit the user’s branch terms on save.
 * Author: your-team
 * Version: 1.2.0
 */

// ==============================
// Settings
// ==============================
const BUA_TAXONOMY = 'branch'; // change if your taxonomy slug is different

// ==============================
// Helpers
// ==============================
function bua_filter_existing_term_ids(array $ids, string $taxonomy) {
    if (empty($ids)) return [];
    $ids = array_map('intval', $ids);
    $ids = array_values(array_unique(array_filter($ids, fn($n) => $n > 0)));
    if (empty($ids)) return [];

    $existing = get_terms([
        'taxonomy'   => $taxonomy,
        'include'    => $ids,
        'hide_empty' => false,
        'fields'     => 'ids',
    ]);
    if (is_wp_error($existing) || empty($existing)) return [];
    $existing = array_map('intval', $existing);
    sort($existing);
    return $existing;
}

// ==============================
// User Profile UI (checkbox list)
// ==============================
add_action('show_user_profile', 'bua_user_field_checklist');
add_action('edit_user_profile', 'bua_user_field_checklist');
function bua_user_field_checklist($user) {
    // current selections
    $selected_ids = get_user_meta($user->ID, 'branch_term_ids', true);
    $selected_ids = is_array($selected_ids) ? array_map('intval', $selected_ids) : [];

    // who can edit?
    $can_edit_field = current_user_can('edit_others_posts'); // Editors and above
    if ($can_edit_field) {
        wp_nonce_field('bua_save_user_field', 'bua_user_field_nonce');
    }

    // pull all branch terms (flat list)
    $terms = [];
    if (taxonomy_exists(BUA_TAXONOMY)) {
        $terms = get_terms([
            'taxonomy'   => BUA_TAXONOMY,
            'hide_empty' => false,
            'orderby'    => 'name',
            'order'      => 'ASC',
        ]);
        if (is_wp_error($terms)) $terms = [];
    }
    ?>
    <h2>Branch Assignment</h2>
    <table class="form-table" role="presentation">
      <tr>
        <th><label>Branches</label></th>
        <td>
          <?php if (empty($terms)): ?>
            <p class="description">No <code><?php echo esc_html(BUA_TAXONOMY); ?></code> terms found. Create some first.</p>
          <?php else: ?>
            <fieldset>
              <legend class="screen-reader-text">Assign Branches</legend>
              <?php foreach ($terms as $term): 
                  $checked = in_array((int)$term->term_id, $selected_ids, true);
                  $id_attr = 'bua-branch-' . (int)$term->term_id;
              ?>
                <label for="<?php echo esc_attr($id_attr); ?>" style="display:block; margin:2px 0;">
                  <input
                    type="checkbox"
                    id="<?php echo esc_attr($id_attr); ?>"
                    name="branch_term_ids[]"
                    value="<?php echo (int)$term->term_id; ?>"
                    <?php checked($checked); ?>
                    <?php disabled(!$can_edit_field); ?>
                  />
                  <?php echo esc_html($term->name); ?>
                  <span class="description" style="opacity:.7;">(ID: <?php echo (int)$term->term_id; ?>)</span>
                </label>
              <?php endforeach; ?>
            </fieldset>
            <?php if (!$can_edit_field): ?>
              <p class="description">Editable by Editors and Administrators.</p>
            <?php else: ?>
              <p class="description">Select one or more branches to associate with this user.</p>
            <?php endif; ?>
          <?php endif; ?>
        </td>
      </tr>
    </table>
    <?php
}

// ==============================
// Save handler (Editors+ only)
// ==============================
add_action('personal_options_update', 'bua_save_user_field_checklist');
add_action('edit_user_profile_update', 'bua_save_user_field_checklist');
function bua_save_user_field_checklist($user_id) {
    // Must be allowed to edit the user, be Editor+, and have nonce
    if (!current_user_can('edit_user', $user_id)) return;
    if (!current_user_can('edit_others_posts')) return; // Editors and above
    if (!isset($_POST['bua_user_field_nonce']) || !wp_verify_nonce($_POST['bua_user_field_nonce'], 'bua_save_user_field')) return;

    // Collect checked IDs
    $ids = [];
    if (isset($_POST['branch_term_ids']) && is_array($_POST['branch_term_ids'])) {
        $ids = array_map('intval', $_POST['branch_term_ids']);
    }

    // Sanitize against existing terms
    $ids = bua_filter_existing_term_ids($ids, BUA_TAXONOMY);

    // Save or delete meta
    if (empty($ids)) {
        delete_user_meta($user_id, 'branch_term_ids');
    } else {
        update_user_meta($user_id, 'branch_term_ids', $ids);
    }
}

// ==============================
// Auto-assign branch terms on post save
// ==============================
add_action('save_post', 'bua_auto_assign_branch_terms', 10, 3);
function bua_auto_assign_branch_terms($post_id, $post, $update) {
    // Safety checks
    if (defined('DOING_AUTOSAVE') && DOING_AUTOSAVE) return;
    if (wp_is_post_revision($post_id) || wp_is_post_autosave($post_id)) return;
    if (!taxonomy_exists(BUA_TAXONOMY)) return;

    // Only for public post types (adjust if needed)
    $pto = get_post_type_object($post->post_type);
    if (!$pto || empty($pto->public)) return;

    $user_id = (int) $post->post_author;
    if ($user_id <= 0) return;

    $ids = get_user_meta($user_id, 'branch_term_ids', true);
    if (!is_array($ids) || empty($ids)) return;

    // Ensure terms still exist
    $ids = bua_filter_existing_term_ids($ids, BUA_TAXONOMY);
    if (empty($ids)) return;

    // Assign (replace existing branch terms)
    wp_set_post_terms($post_id, $ids, BUA_TAXONOMY, false);
}

// ==============================
// Admin UI: hide Branch taxonomy panel for Authors and below
// ==============================
add_action('admin_head', function () {
    if (!is_admin()) return;
    if (current_user_can('edit_others_posts')) return; // Editors+ keep seeing the UI

    $tax = esc_attr(BUA_TAXONOMY);
    echo '<style>
        .categorydiv.taxonomy-' . $tax . ' { display: none !important; }
        #' . $tax . 'div, #' . $tax . 'div-hide { display: none !important; }
        .editor-post-taxonomies__hierarchical-terms-list.taxonomy-' . $tax . ' { display: none !important; }
    </style>';
});

// ==============================
// REST support + minimal taxonomy registration for testing
// Comment out the registration callback if your theme/plugins already handle it.
// ==============================

// Ensure the taxonomy is REST-enabled even if registered elsewhere.
add_filter('register_taxonomy_args', function ($args, $taxonomy) {
    if ($taxonomy !== BUA_TAXONOMY) {
        return $args;
    }

    $args['show_in_rest'] = true;
    if (empty($args['rest_base'])) {
        $args['rest_base'] = BUA_TAXONOMY;
    }
    if (empty($args['rest_controller_class'])) {
        $args['rest_controller_class'] = 'WP_REST_Terms_Controller';
    }

    return $args;
}, 10, 2);

// Register the taxonomy if nothing else has done so.
add_action('init', function () {
    if (taxonomy_exists(BUA_TAXONOMY)) {
        return;
    }

    register_taxonomy(BUA_TAXONOMY, ['post'], [
        'label'                => 'Branches',
        'public'               => true,
        'hierarchical'         => false,
        'rewrite'              => ['slug' => 'branch', 'with_front' => false],
        'show_admin_column'    => true,
        'show_in_rest'         => true,
        'rest_base'            => BUA_TAXONOMY,
        'rest_controller_class'=> 'WP_REST_Terms_Controller',
    ]);
}, 5);
