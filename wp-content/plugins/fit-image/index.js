( function ( blocks, element, blockEditor ) {
  const { registerBlockType } = blocks;
  const { createElement: el } = element;
  const { useBlockProps } = blockEditor;

  registerBlockType('fit/image', {
    edit: () =>
      el('div', { ...useBlockProps({ className: 'fit-image-large' }) },
        // simple editor preview; the real front-end is in render.php
        el('fit-image', { className: 'fit-image-large', 'data-fit-top': true },
          el('img', { src: wp.i18n.__('kanagawa_sat_cropped_z10.png', 'fit-image'), alt: 'Example' }),
          el('svg', { id: 'svg-overlay' })
        )
      ),
    save: () => null // dynamic render only
  });
} )( window.wp.blocks, window.wp.element, window.wp.blockEditor );
