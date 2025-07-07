( function( wp ) {
    var registerBlockType = wp.blocks.registerBlockType;
    var useBlockProps = wp.blockEditor.useBlockProps;
    var __ = wp.i18n.__;

    registerBlockType( 'goat/video-config', {
        edit: function() {
            // In the block editor, show "Goat JSON"
            return wp.element.createElement(
                'div',
                useBlockProps(),
                wp.element.createElement(
                    'pre',
                    null,
                    __( 'Goat JSON:', 'goat-video-config' ) + '\n' +
                    'Goat JSON' // This will just display "Goat JSON" in the editor
                )
            );
        },

        save: function() {
            // In the front-end, we rely on view.js to handle rendering
            return wp.element.createElement(
                'pre',
                useBlockProps.save({ className: 'goat-video-config', id: 'goat-video-config' }),
                'Goat JSON' // This is just a placeholder; the actual JSON will be rendered by view.js
            );
        }
    } );

} )( window.wp );
