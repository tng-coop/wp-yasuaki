<?php
error_log('index.asset.php');
return [
  'dependencies' => [
    'wp-blocks',
    'wp-element',
    'wp-i18n',
    'wp-block-editor',
    'wp-dom-ready',
  ],
  'version'      => filemtime( __DIR__ . '/index.js' ),
];
