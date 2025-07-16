<?php
// set-meta-1064.php
// Manual metadata injection for PDF thumbnails with logging and verification

// 1) Your attachment ID
$id = 1064;

// 2) Load required WP functions
require_once ABSPATH . 'wp-admin/includes/file.php';
require_once ABSPATH . 'wp-admin/includes/media.php';
require_once ABSPATH . 'wp-admin/includes/image.php';

// 3) Setup logging using ABSPATH and wp-content/uploads
$log_dir  = ABSPATH . 'wp-content/uploads/';
if ( ! file_exists( $log_dir ) ) {
    wp_mkdir_p( $log_dir );
}
$log_file = $log_dir . 'fix-meta-1064.log';

function log_msg( $msg ) {
    // Directly write to a fixed log file path
    $log_file = WP_CONTENT_DIR . '/uploads/fix-meta-1064.log';
    $time     = date( 'Y-m-d H:i:s' );
    file_put_contents( $log_file, "[{$time}] {$msg}
", FILE_APPEND );
}

log_msg( "=== Starting metadata fix for attachment ID {$id} ===" );

// 4) Define relative path and compute absolute path
$relative_path = '2025/07/sienn_ohisama_2025.pdf';
$upload_dir    = wp_upload_dir();
$absolute_path = $upload_dir['basedir'] . '/' . $relative_path;
$baseurl       = trailingslashit( $upload_dir['baseurl'] ) . '2025/07/';

// 5) Verify file exists
if ( ! file_exists( $absolute_path ) ) {
    log_msg( "Error: File not found at {$absolute_path}" );
    wp_die( "Error: File {$relative_path} not found in uploads." );
}
log_msg( "File exists: {$absolute_path}" );

// 6) Update attached file meta
update_post_meta( $id, '_wp_attached_file', $relative_path );
log_msg( "_wp_attached_file updated to {$relative_path}" );

// 7) Build metadata array
$meta = array(
    'file'       => '2025/07/sienn_ohisama_2025-pdf.jpg',
    'width'      => 1497,
    'height'     => 1058,
    'sizes'      => array(
        'full'      => array(
            'file'      => 'sienn_ohisama_2025-pdf.jpg',
            'width'     => 1497,
            'height'    => 1058,
            'mime_type' => 'application/pdf',
        ),
        'large'     => array(
            'file'      => 'sienn_ohisama_2025-pdf-1024x724.jpg',
            'width'     => 1024,
            'height'    => 724,
            'mime_type' => 'image/jpeg',
        ),
        'medium'    => array(
            'file'      => 'sienn_ohisama_2025-pdf-300x212.jpg',
            'width'     => 300,
            'height'    => 212,
            'mime_type' => 'image/jpeg',
        ),
        'thumbnail' => array(
            'file'      => 'sienn_ohisama_2025-pdf-150x106.jpg',
            'width'     => 150,
            'height'    => 106,
            'mime_type' => 'image/jpeg',
        ),
    ),
    'image_meta' => array(),
);

// 8) Overwrite metadata
delete_post_meta( $id, '_wp_attachment_metadata' );
log_msg( "Deleted old _wp_attachment_metadata" );
update_post_meta( $id, '_wp_attachment_metadata', $meta );
log_msg( "Inserted new _wp_attachment_metadata with sizes: " . implode( ',', array_keys( $meta['sizes'] ) ) );

// 9) Update post GUID for top-level source_url
$guid_value = $baseurl . basename( $relative_path );
wp_update_post( array( 'ID' => $id, 'guid' => $guid_value ) );
log_msg( "GUID updated to {$guid_value}" );

// 10) Verification: retrieve and log saved metadata
$saved_meta = get_post_meta( $id, '_wp_attachment_metadata', true );
if ( empty( $saved_meta['sizes'] ) ) {
    log_msg( "Verification failed: sizes array missing" );
} else {
    $sizes = implode( ',', array_keys( $saved_meta['sizes'] ) );
    log_msg( "Verification OK: sizes in metadata: {$sizes}" );
}

log_msg( "=== Completed metadata fix for attachment ID {$id} ===" );
