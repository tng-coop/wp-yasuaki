// block/index.js
(function (wp) {
  const { registerBlockType } = wp.blocks;
  const { useBlockProps, InnerBlocks } = wp.blockEditor;
  const { __ } = wp.i18n;
  const el = wp.element.createElement;

  // Two groups, each starts with an editor-only heading label:
  const TEMPLATE = [
    ['core/group', { className: 'hw-pane-a' }, [
      ['core/heading', {
        level: 6,
        className: 'hw-pane-label',
        content: __('Pane A', 'hello-world-modern'),
        // lock the label so it isn't accidentally removed
        lock: { remove: true, move: false }
      }],
      // authors can add anything *after* the label
    ]],
    ['core/group', { className: 'hw-pane-b' }, [
      ['core/heading', {
        level: 6,
        className: 'hw-pane-label',
        content: __('Pane B', 'hello-world-modern'),
        lock: { remove: true, move: false }
      }],
    ]],
  ];

  registerBlockType('hello-world-modern/block', {
    edit() {
      const props = useBlockProps({ className: 'hello-world-modern' });
      return el('div', props,
        el(InnerBlocks, {
          template: TEMPLATE,
          templateLock: false,   // groups stay; content inside is flexible
        })
      );
    },

    // Save exactly what the editor produced; no slot attributes in post HTML
    save() {
      return el(
        'div',
        { className: 'hello-world-modern' },
        el(InnerBlocks.Content)
      );
    },
  });
})(window.wp);
