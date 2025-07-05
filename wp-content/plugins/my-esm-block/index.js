// index.js  (loaded via <script type="module">)

import { registerBlockType } from 'https://esm.sh/@wordpress/blocks';
import { createElement }     from 'https://esm.sh/@wordpress/element';

/*
 * If you still want translations, also:
 *   import { __ } from 'https://esm.sh/@wordpress/i18n';
 * and wrap strings with __().  Just note that this bypasses WP’s built‑in
 * translation loading because the module has its own copy of @wordpress/i18n.
 */

registerBlockType( 'my-plugin/hello-block', {
	title: 'Hello Block',
	icon:  'smiley',
	category: 'text',

	edit() {
		return createElement( 'p', null, 'Hello from the editor!' );
	},

	save() {
		return createElement( 'p', null, 'Hello from the front‑end!' );
	},
} );
