// index.js (only save() changed)
( function( wp ) {
  const { registerBlockType } = wp.blocks;
  const { useBlockProps, InnerBlocks } = wp.blockEditor;
  const { __ } = wp.i18n;
  const el = wp.element.createElement;

  registerBlockType('hello-world-modern/block', {
    edit() {
      const blockProps = useBlockProps({ className: 'hello-world-modern' });
      return el(InnerBlocks, {
        placeholder: __('Add content for Pane B…', 'hello-world-modern'),
      });
    },
    save() {
      // Save a wrapper with your class (so our view.js can upgrade it),
      // and put the InnerBlocks output inside a child with slot="b".
      return el(
        'div',
        { className: 'hello-world-modern' },
        el('div', { slot: 'b' }, el(InnerBlocks.Content))
      );
    },
  });
})( window.wp );
