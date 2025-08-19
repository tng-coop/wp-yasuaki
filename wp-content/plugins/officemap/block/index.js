// WP 6.5+ Script Module (ESM)
import { registerBlockType } from '@wordpress/blocks';
import { __ } from '@wordpress/i18n';
import ServerSideRender from '@wordpress/server-side-render';
import { createElement as el } from '@wordpress/element';

registerBlockType('officemap/nonce', {
  title: __('OfficeMap (Nonce Starter)', 'officemap'),
  icon: 'location',
  category: 'widgets',

  // Show the PHP-rendered markup inside the editor.
  edit: (props) =>
    el(ServerSideRender, {
      block: 'officemap/nonce',
      attributes: props.attributes,
    }),

  // Dynamic block is rendered by PHP on the frontend.
  save: () => null,
});
