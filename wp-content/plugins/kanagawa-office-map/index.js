( function( wp ) {
  const { registerBlockType } = wp.blocks;
  const { useBlockProps } = wp.blockEditor;
  const { __ } = wp.i18n;

  registerBlockType( 'kanagawa/office-map', {
    edit: () => {
      const blockProps = useBlockProps({
        className: 'kanagawa-office-map-container',
        id: 'kanagawa-office-map'
      });
      return wp.element.createElement(
        'div',
        blockProps,
        wp.element.createElement(
          'p',
          null,
          __( 'Kanagawa Office Map', 'kanagawa-office-map' )
        )
      );
    },
    save: () => {
      return wp.element.createElement(
        'div',
        useBlockProps.save({
          className: 'kanagawa-office-map-container',
          id: 'kanagawa-office-map'
        })
      );
    }
  } );
} )( window.wp );