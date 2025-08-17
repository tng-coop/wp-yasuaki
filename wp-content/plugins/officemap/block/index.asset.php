<?php
return [
  'dependencies' => [
    'wp-blocks',
    'wp-element',
    'wp-i18n',
    'wp-block-editor',
    'wp-server-side-render' // added for editor preview
  ],
  'version' => filemtime(__DIR__ . '/index.js'),
];
