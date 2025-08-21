// file: index.js
( function ( blocks, element, blockEditor ) {
  const { registerBlockType } = blocks;
  const { createElement: el } = element;
  const { useBlockProps, InnerBlocks } = blockEditor;

  // Two containers authors can drop any blocks into
  const TEMPLATE = [
    [ 'core/group', { className: 'slot-a' }, [] ],
    [ 'core/group', { className: 'slot-b' }, [] ],
  ];

  registerBlockType( 'fullshow/hello', {
    edit: () =>
      el(
        'div',
        { ...useBlockProps( { className: 'fullshow-box' } ) },
        el( InnerBlocks, { template: TEMPLATE, templateLock: false } )
      ),

    // ✅ Serialize InnerBlocks so the editor can restore them on reload
    // (front-end remains dynamic via render.php)
    save: () =>
      el(
        'div',
        { ...useBlockProps.save( { className: 'fullshow-box' } ) },
        el( InnerBlocks.Content )
      ),
  } );
} )( window.wp.blocks, window.wp.element, window.wp.blockEditor );
