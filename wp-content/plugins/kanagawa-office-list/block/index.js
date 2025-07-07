( function( wp ) {
  const { registerBlockType } = wp.blocks;
  const { useBlockProps } = wp.blockEditor;
  const { __ } = wp.i18n;

  registerBlockType( 'kanagawa/office-list', {
    edit: () => {
      const blockProps = useBlockProps( {
        className: 'kanagawa-office-list-container',
        id: 'kanagawa-office-list'
      } );
      return wp.element.createElement(
        'div',
        blockProps,
        wp.element.createElement(
          'p',
          null,
          __( 'Kanagawa Office List', 'kanagawa-office-list' )
        )
      );
    },
    save: () => {
      return wp.element.createElement(
        'div',
        useBlockProps.save( {
          className: 'kanagawa-office-list-container',
          id: 'kanagawa-office-list'
        } )
      );
    }
  } );
} )( window.wp );
