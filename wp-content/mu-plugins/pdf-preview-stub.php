<?php
/**
 * MU-Plugin: PDF + Previews Bundle Handler
 * Location: wp-content/mu-plugins/pdf_preview_bundle_mu_plugin.php
 *
 * Accepts one PDF plus previews labeled 'full','thumbnail','medium','large',
 * moves all into the same YYYY/MM folder, and injects metadata exactly
 * as set-meta-1064.php would.
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

// Simple logger to wp-content/uploads/fix-meta.log
function mp_log($msg) {
    $log = WP_CONTENT_DIR . '/uploads/fix-meta.log';
    file_put_contents($log, '['.date('Y-m-d H:i:s').'] '.$msg."\n", FILE_APPEND);
}

function mp_handle_pdf_with_previews(WP_REST_Request $request) {
    require_once ABSPATH.'wp-admin/includes/file.php';
    require_once ABSPATH.'wp-admin/includes/media.php';
    require_once ABSPATH.'wp-admin/includes/image.php';

    $files    = $request->get_file_params();
    $dims_map = json_decode($request->get_param('dimensions'), true) ?: [];

    // 1) Upload PDF
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

    // 2) Compute paths
    $upload_dir = wp_upload_dir();
    $relative   = ltrim(str_replace($upload_dir['basedir'], '', $move_pdf['file']), '/\\');
    $year_month = dirname($relative);
    $baseurl    = trailingslashit($upload_dir['baseurl']).$year_month.'/';

    // 3) Create attachment post
    $attach_id = wp_insert_attachment([
        'post_mime_type'=> $move_pdf['type'],
        'post_title'    => sanitize_file_name(pathinfo($move_pdf['file'], PATHINFO_FILENAME)),
        'post_status'   => 'inherit',
    ], $move_pdf['file']);
    mp_log('Attachment ID: '.$attach_id);

    // 4) Set _wp_attached_file
    update_post_meta($attach_id, '_wp_attached_file', $relative);
    mp_log('_wp_attached_file set: '.$relative);

    // 5) Prepare metadata
    $meta = [
        'file'       => '',
        'width'      => null,
        'height'     => null,
        'sizes'      => [],
        'image_meta' => [],
    ];

    // 6) Process previews
    if(!empty($files['previews']['tmp_name'])) {
        // ensure uploads to same folder
        add_filter('upload_dir', function($dirs) use($year_month) {
            $dirs['subdir'] = '/'.$year_month;
            $dirs['path']   = $dirs['basedir'].'/'.$year_month;
            $dirs['url']    = $dirs['baseurl'].'/'.$year_month;
            return $dirs;
        });

        foreach($files['previews']['tmp_name'] as $size_name => $tmp_path) {
            $one = [
                'name'     => $files['previews']['name'][$size_name],
                'type'     => $files['previews']['type'][$size_name],
                'tmp_name' => $tmp_path,
                'error'    => $files['previews']['error'][$size_name],
                'size'     => $files['previews']['size'][$size_name],
            ];
            $moved = wp_handle_upload($one, ['test_form'=>false]);
            if(isset($moved['error'])) {
                mp_log("Error uploading preview {$size_name}: {$moved['error']}");
                continue;
            }
            mp_log("Preview {$size_name} uploaded: {$moved['file']}");

            // dimensions: client or fallback
            $w = $dims_map[$size_name]['width'] ?? null;
            $h = $dims_map[$size_name]['height'] ?? null;
            if(is_null($w)||is_null($h)) {
                $info = getimagesize($moved['file']);
                if($info) list($w,$h) = $info;
            }

            // set base from 'full'
            if($size_name==='full') {
                $meta['file']   = basename($moved['file']);
                $meta['width']  = $w;
                $meta['height'] = $h;
            }

            // inject
            $meta['sizes'][$size_name] = [
                'file'      => basename($moved['file']),
                'width'     => $w,
                'height'    => $h,
                'mime_type' => $moved['type'],
            ];
        }
        remove_filter('upload_dir','__return_false');
    }

    // 7) Persist metadata
    delete_post_meta($attach_id, '_wp_attachment_metadata');
    update_post_meta($attach_id, '_wp_attachment_metadata', $meta);
    mp_log('_wp_attachment_metadata written with sizes: '.implode(',', array_keys($meta['sizes'])));

    // 8) Update GUID
    $guid = $baseurl.basename($move_pdf['file']);
    wp_update_post(['ID'=>$attach_id,'guid'=>$guid]);
    mp_log("GUID updated: {$guid}");
    mp_log('=== Completed metadata injection ===');

    return rest_ensure_response(['attachment_id'=>$attach_id,'metadata'=>$meta]);
}