( function ( blocks, element, blockEditor ) {
	const { registerBlockType } = blocks;
	const { createElement: el } = element;
	const { useBlockProps } = blockEditor;

	registerBlockType( 'fullshow/hello', {
		edit: () =>
			el(
				'div',
				{ ...useBlockProps( { className: 'fullshow-box' } ) },
				el( 'div', { className: 'fullshow-text' }, 'hello' )
			),
		save: () => null // dynamic
	} );
} )( window.wp.blocks, window.wp.element, window.wp.blockEditor );
