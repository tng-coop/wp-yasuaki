<?php
/**
 * Plugin Name: Address Manager (CPT + Fields)
 * Description: Adds an Address post type with editable fields in WP Admin.
 * Version: 1.2.1
 */

if ( ! defined('ABSPATH') ) exit;

add_action('init', function () {
    register_post_type('address', [
        'labels' => [
            'name'               => 'Addresses',
            'singular_name'      => 'Address',
            'add_new_item'       => 'Add New Address',
            'edit_item'          => 'Edit Address',
            'new_item'           => 'New Address',
            'view_item'          => 'View Address',
            'search_items'       => 'Search Addresses',
        ],
        'public'             => false,
        'show_ui'            => true,
        'show_in_menu'       => true,
        'menu_position'      => 20,
        'menu_icon'          => 'dashicons-location',
        'capability_type'    => 'post',
        // ✅ Keep custom-fields so REST meta behaves like in your working setup
        'supports'           => ['title','revisions','custom-fields'], // Title = Office
        'show_in_rest'       => true,
        'has_archive'        => false,
        'map_meta_cap'       => true,
    ]);
});

/** Meta fields */
function address_meta_fields_def() {
    return [
        'csv_id'   => ['label' => 'ID',     'type' => 'string'],
        'csv_id2'  => ['label' => 'ID2',    'type' => 'string'],
        'address'  => ['label' => 'Address','type' => 'string'],
        'tel'      => ['label' => 'TEL',    'type' => 'string'],
        'fax'      => ['label' => 'FAX',    'type' => 'string'],
        'email'    => ['label' => 'Email',  'type' => 'string'],
        'url'      => ['label' => 'URL',    'type' => 'string'],
        'work'     => ['label' => 'Work',   'type' => 'string'],
    ];
}

add_action('init', function () {
    foreach (address_meta_fields_def() as $key => $schema) {
        register_post_meta('address', $key, [
            'type'          => $schema['type'],
            'single'        => true,
            'show_in_rest'  => true, // expose in REST
            'auth_callback' => function() { return current_user_can('edit_posts'); },
            'sanitize_callback' => function($value) use ($key) {
                if (!is_string($value)) $value = '';
                switch ($key) {
                    case 'email':   return sanitize_email($value);
                    case 'url':     return esc_url_raw($value);
                    case 'address':
                    case 'work':    return sanitize_textarea_field($value);
                    default:        return sanitize_text_field($value);
                }
            },
        ]);
    }
});

/** Admin metabox (your single, clean UI) */
add_action('add_meta_boxes', function () {
    add_meta_box(
        'address_fields_box',
        'Address Details',
        'render_address_fields_box',
        'address',
        'normal',
        'high'
    );
});

function render_address_fields_box($post) {
    wp_nonce_field('save_address_fields', 'address_fields_nonce');
    $get = fn($k,$d='') => get_post_meta($post->ID, $k, true) ?: $d;

    $fields = [
        'csv_id'  => ['label' => 'ID',     'placeholder' => 'K03 / C02 など'],
        'csv_id2' => ['label' => 'ID2',    'placeholder' => '川南 / 医協 など'],
        'address' => ['label' => 'Address','placeholder' => '〒... 神奈川県...'],
        'tel'     => ['label' => 'TEL',    'placeholder' => '044-123-4567'],
        'fax'     => ['label' => 'FAX',    'placeholder' => '044-123-4567'],
        'email'   => ['label' => 'Email',  'placeholder' => 'name@example.com'],
        'url'     => ['label' => 'URL',    'placeholder' => 'https://example.com'],
        'work'    => ['label' => 'Work',   'placeholder' => '事業内容など'],
    ];
    ?>
    <style>
        .address-grid { display:grid; grid-template-columns:1fr 1fr; gap:12px; }
        .address-grid .full { grid-column:1 / -1; }
        .address-field label { display:block; font-weight:600; margin-bottom:4px; }
        .address-field input, .address-field textarea { width:100%; max-width:100%; }
        .muted { color:#666; font-size:12px; margin-bottom:8px; }
    </style>

    <p class="muted">※ 「Office（オフィス名）」は上部のタイトル欄に入力してください。</p>

    <div class="address-grid">
        <?php foreach ($fields as $key => $meta): ?>
            <div class="address-field <?php echo in_array($key, ['address','work']) ? 'full' : '' ?>">
                <label for="<?php echo esc_attr($key); ?>"><?php echo esc_html($meta['label']); ?></label>
                <?php if (in_array($key, ['address','work'], true)): ?>
                    <textarea id="<?php echo esc_attr($key); ?>"
                              name="<?php echo esc_attr($key); ?>"
                              rows="3"
                              placeholder="<?php echo esc_attr($meta['placeholder']); ?>"><?php echo esc_textarea($get($key)); ?></textarea>
                <?php else: ?>
                    <input type="<?php echo $key === 'email' ? 'email' : ($key === 'url' ? 'url' : 'text'); ?>"
                           id="<?php echo esc_attr($key); ?>"
                           name="<?php echo esc_attr($key); ?>"
                           placeholder="<?php echo esc_attr($meta['placeholder']); ?>"
                           value="<?php echo esc_attr($get($key)); ?>" />
                <?php endif; ?>
            </div>
        <?php endforeach; ?>
    </div>
    <?php
}

/** Save handler */
add_action('save_post_address', function ($post_id, $post, $update) {
    if ( ! isset($_POST['address_fields_nonce']) || ! wp_verify_nonce($_POST['address_fields_nonce'], 'save_address_fields') ) return;
    if ( defined('DOING_AUTOSAVE') && DOING_AUTOSAVE ) return;
    if ( ! current_user_can('edit_post', $post_id) ) return;

    foreach (array_keys(address_meta_fields_def()) as $key) {
        if (!isset($_POST[$key])) continue;
        $val = $_POST[$key];
        switch ($key) {
            case 'email':  $val = sanitize_email($val); break;
            case 'url':    $val = esc_url_raw($val);    break;
            case 'address':
            case 'work':   $val = sanitize_textarea_field($val); break;
            default:       $val = sanitize_text_field($val); break;
        }
        update_post_meta($post_id, $key, $val);
    }
}, 10, 3);

/** Admin list columns */
add_filter('manage_address_posts_columns', function ($cols) {
    $new = [];
    $new['cb']     = $cols['cb'];
    $new['csv_id'] = __('ID');
    $new['csv_id2']= __('ID2');
    $new['title']  = __('Office');
    $new['tel']    = __('TEL');
    $new['email']  = __('Email');
    $new['address']= __('Address');
    return $new;
});
add_action('manage_address_posts_custom_column', function ($col, $post_id) {
    switch ($col) {
        case 'csv_id':
        case 'csv_id2':
        case 'tel':
        case 'email':
        case 'address':
            echo esc_html(get_post_meta($post_id, $col, true));
            break;
    }
}, 10, 2);
add_filter('manage_edit-address_sortable_columns', function($cols){
    $cols['csv_id']  = 'csv_id';
    $cols['csv_id2'] = 'csv_id2';
    return $cols;
});

/** 🔒 Hide the generic "Custom Fields" metabox so fields don't show twice */
add_action('admin_menu', function () {
    remove_meta_box('postcustom', 'address', 'normal'); // Classic editor
});
add_action('enqueue_block_editor_assets', function() {
    $screen = function_exists('get_current_screen') ? get_current_screen() : null;
    if ($screen && $screen->post_type === 'address') {
        // Hide the Custom Fields panel area in block editor, if user enabled it
        wp_add_inline_style(
            'wp-edit-post',
            '.edit-post-meta-boxes-area { display:none !important; }'
        );
    }
});
