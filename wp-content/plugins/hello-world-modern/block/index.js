// hello-world-modern/block/index.js
( function( wp ) {
  const { registerBlockType } = wp.blocks;
  const { useBlockProps }     = wp.blockEditor;
  const { __ }                = wp.i18n;
  const el                    = wp.element.createElement;

  registerBlockType('hello-world-modern/block', {
    edit() {
      const blockProps = useBlockProps({ className: 'hello-world-modern' });
      return el('p', blockProps, __('Hello from the editor 👋', 'hello-world-modern'));
    },
    save() {
      return el(
        'p',
        wp.blockEditor.useBlockProps.save({ className: 'hello-world-modern' }),
        __('Hello World!', 'hello-world-modern')
      );
    },
  });
})( window.wp );
