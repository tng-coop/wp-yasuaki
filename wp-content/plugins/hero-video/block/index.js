( function( wp ) {
    var registerBlockType = wp.blocks.registerBlockType;
    var useBlockProps = wp.blockEditor.useBlockProps;
    var __ = wp.i18n.__;

    registerBlockType( 'hero/video', {
        edit: function() {
            // Apply the same container classes and ID so the editor shows the 100% × 300px box
            var blockProps = useBlockProps({ className: 'hero-video-container', id: 'hero-video' });
            return wp.element.createElement(
                'div',
                blockProps,
                wp.element.createElement(
                    'p',
                    null,
                    __( 'Select Video: Goat or Waterfall', 'hero-video' )
                )
            );
        },

        save: function() {
            return wp.element.createElement(
                'div',
                useBlockProps.save({ className: 'hero-video-container', id: 'hero-video' }),
            );
        }
    } );

} )( window.wp );
