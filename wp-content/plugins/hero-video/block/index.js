( function( wp ) {
    var registerBlockType = wp.blocks.registerBlockType;
    var useBlockProps = wp.blockEditor.useBlockProps;
    var __ = wp.i18n.__;

    registerBlockType( 'hero/video', {  // Changed block name to hero/video
        edit: function() {
            // In the block editor, show "Hero Video JSON"
            return wp.element.createElement(
                'div',
                useBlockProps(),
                wp.element.createElement(
                    'pre',
                    null,
                    __( 'Hero Video JSON:', 'hero-video' ) + '\n' +
                    'Hero Video JSON' // Placeholder text
                )
            );
        },

        save: function() {
            // In the front-end, we rely on view.js to handle rendering
            return wp.element.createElement(
                'pre',
                useBlockProps.save({ className: 'hero-video', id: 'hero-video' }),
                'Hero Video JSON' // Placeholder text for front-end
            );
        }
    } );

} )( window.wp );
