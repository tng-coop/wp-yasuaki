<?php
return [
  'dependencies' => [
    'wp-element',
    'wp-dom-ready',
    'wp-blocks',
    'wp-i18n',
    // any other handles your view.js/officedata.js import
  ],
  'version'      => filemtime( __DIR__ . '/view.js' ),
];
