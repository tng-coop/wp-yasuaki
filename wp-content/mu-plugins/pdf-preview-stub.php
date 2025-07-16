<?php
/**
 * MU-Plugin: PDF + Previews Bundle Handler (Step 3: Manual Metadata Injection)
 * Location: wp-content/mu-plugins/pdf_preview_bundle_mu_plugin.php
 *
 * After uploading the PDF and preview images, inject metadata
 * exactly as set-meta-1064.php does, with logging and ensuring
 * both the full preview and thumbnails are correctly recognized,
 * using getimagesize fallback where necessary.
 * Access restricted to Editors and Administrators.
 */

add_action('rest_api_init', function() {
    register_rest_route('myplugin/v1','/media/bundle',[
        'methods'             => 'POST',
        'callback'            => 'mp_handle_pdf_with_previews',
        'permission_callback' => function() {
            return current_user_can('edit_pages');
        },
    ]);
});

// Logger to wp-content/uploads/fix-meta.log
function mp_log($msg) {
    $log = WP_CONTENT_DIR . '/uploads/fix-meta.log';
    file_put_contents($log, '['.date('Y-m-d H:i:s').'] '.$msg."\n", FILE_APPEND);
}

function mp_handle_pdf_with_previews(WP_REST_Request $request) {
    // Load WP media functions
    require_once ABSPATH.'wp-admin/includes/file.php';
    require_once ABSPATH.'wp-admin/includes/media.php';
    require_once ABSPATH.'wp-admin/includes/image.php';

    $files    = $request->get_file_params();
    $dims_map = json_decode($request->get_param('dimensions'), true) ?: [];

    // Step 1: upload PDF
    if(empty($files['pdf'])) {
        return new WP_Error('no_pdf','No PDF file received',['status'=>400]);
    }
    mp_log('=== Starting metadata injection ===');
    $move_pdf = wp_handle_upload($files['pdf'], ['test_form'=>false]);
    if(isset($move_pdf['error'])) {
        mp_log('Error uploading PDF: '.$move_pdf['error']);
        return new WP_Error('upload_error',$move_pdf['error'],['status'=>500]);
    }
    mp_log('PDF uploaded: '.$move_pdf['file']);

    // Determine relative path and base URL
    $upload_dir = wp_upload_dir();
    $relative   = ltrim(str_replace($upload_dir['basedir'], '', $move_pdf['file']), '/\\');
    $year_month = dirname($relative);
    $baseurl    = trailingslashit($upload_dir['baseurl']).$year_month.'/';

    // Step 2: create attachment
    $attach_id = wp_insert_attachment([
        'post_mime_type'=> $move_pdf['type'],
        'post_title'    => sanitize_file_name(pathinfo($move_pdf['file'], PATHINFO_FILENAME)),
        'post_status'   => 'inherit',
    ], $move_pdf['file']);
    mp_log('Attachment ID: '.$attach_id);

    // Set attached file meta
    update_post_meta($attach_id, '_wp_attached_file', $relative);
    mp_log('_wp_attached_file set: '.$relative);

    // Initialize metadata
    $meta = [
        'file'       => basename($move_pdf['file']),
        'width'      => $dims_map['full']['width'] ?? null,
        'height'     => $dims_map['full']['height'] ?? null,
        'sizes'      => [],
        'image_meta' => [],
    ];

    // Map preview keys to WP size names
    $size_map = [
        'full'     => 'full',
        '150x106'  => 'thumbnail',
        '300x212'  => 'medium',
        '1024x724' => 'large',
    ];

    // Step 3: handle previews
    if(!empty($files['previews']['tmp_name'])) {
        // Ensure previews save to same folder
        add_filter('upload_dir', function($dirs) use($year_month) {
            $dirs['subdir'] = '/'.$year_month;
            $dirs['path']   = $dirs['basedir'].'/'.$year_month;
            $dirs['url']    = $dirs['baseurl'].'/'.$year_month;
            return $dirs;
        });

        foreach($files['previews']['tmp_name'] as $key => $tmp_path) {
            $one = [
                'name'     => $files['previews']['name'][$key],
                'type'     => $files['previews']['type'][$key],
                'tmp_name' => $tmp_path,
                'error'    => $files['previews']['error'][$key],
                'size'     => $files['previews']['size'][$key],
            ];
            $moved = wp_handle_upload($one, ['test_form'=>false]);
            if(isset($moved['error'])) {
                mp_log("Error uploading preview {$key}: {$moved['error']}");
                continue;
            }
            mp_log("Preview {$key} uploaded: {$moved['file']}");

            $size_name = $size_map[$key] ?? $key;
            // Determine dimensions, fallback to server-side
            $w = $dims_map[$key]['width'] ?? null;
            $h = $dims_map[$key]['height'] ?? null;
            if($w === null || $h === null) {
                $info = getimagesize($moved['file']);
                if($info) {
                    $w = $info[0];
                    $h = $info[1];
                }
            }

            $meta['sizes'][$size_name] = [
                'file'      => basename($moved['file']),
                'width'     => $w,
                'height'    => $h,
                'mime_type' => $moved['type'],
            ];
        }
        remove_all_filters('upload_dir');
    }

    // Step 4: write metadata
    delete_post_meta($attach_id, '_wp_attachment_metadata');
    update_post_meta($attach_id, '_wp_attachment_metadata', $meta);
    mp_log('_wp_attachment_metadata written with sizes: '.implode(',', array_keys($meta['sizes'])));

    // Step 5: update GUID
    $guid = $baseurl.basename($move_pdf['file']);
    wp_update_post(['ID'=>$attach_id,'guid'=>$guid]);
    mp_log("GUID updated: {$guid}");
    mp_log('=== Completed metadata injection ===');

    return rest_ensure_response(['attachment_id'=>$attach_id,'metadata'=>$meta]);
}
