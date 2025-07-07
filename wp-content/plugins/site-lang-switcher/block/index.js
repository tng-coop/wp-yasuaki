( function ( wp ) {
	const { registerBlockType } = wp.blocks;
	const { useBlockProps }   = wp.blockEditor;
	const { __ }              = wp.i18n;

	registerBlockType( 'sls/site-lang-switcher', {
		edit() {
			return wp.element.createElement(
				'div',
				{ ...useBlockProps(), className: 'sls-preview' },
				__('🌐  EN / JP language switcher (front‑end only)', 'site-lang-switcher')
			);
		},
		save() {
			// Static markup rendered on front‑end.
			return wp.element.createElement(
				'div',
				{ id: 'site-lang-switcher' },
				[
					wp.element.createElement(
						'span',
						{ key: 'icon', style: { fontSize: '1.2rem' } },
						'🌐'
					),
					wp.element.createElement(
						'div',
						{ key: 'toggle', className: 'lang-toggle' },
						[
							wp.element.createElement(
								'button',
								{ key: 'en', id: 'btn-en', className: 'lang-btn', type: 'button' },
								'EN'
							),
							wp.element.createElement(
								'button',
								{ key: 'jp', id: 'btn-jp', className: 'lang-btn', type: 'button' },
								'JP'
							)
						]
					)
				]
			);
		}
	} );
} )( window.wp );
