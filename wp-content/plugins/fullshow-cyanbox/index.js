( function ( blocks, element, blockEditor ) {
  const { registerBlockType } = blocks;
  const { createElement: el } = element;
  const { useBlockProps } = blockEditor;

  registerBlockType( 'fullshow/cyanbox', {
    edit: () =>
      el(
        'div',
        { ...useBlockProps( { className: 'cyanbox' } ) },
        'Cyan Box (editor preview)'
      ),
    save: () => null // dynamic render only
  } );
} )( window.wp.blocks, window.wp.element, window.wp.blockEditor );
