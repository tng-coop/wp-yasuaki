// file: index.js
(function (wp) {
  const { registerBlockType } = wp.blocks;
  const { createElement: el, Fragment } = wp.element;
  const { __ } = wp.i18n;
  const { InnerBlocks, InspectorControls } = wp.blockEditor;
  const { PanelBody, TextControl } = wp.components;

  // ---------------------------
  // Child: Slot A (base layer)
  // ---------------------------
  registerBlockType('fullbanner/slot-a', {
    title: __('Fullbanner: Base', 'fullbanner'),
    parent: ['fullbanner/hello'],
    icon: 'align-wide',
    supports: { html: false, reusable: false },
    edit: () =>
      el('div', { slot: 'a' },
        el(InnerBlocks, {
          templateLock: false,
          renderAppender: InnerBlocks.ButtonBlockAppender,
        })
      ),
    save: () => el(InnerBlocks.Content),
  });

  // ---------------------------
  // Child: Slot B (overlay)
  // ---------------------------
  registerBlockType('fullbanner/slot-b', {
    title: __('Fullbanner: Overlay', 'fullbanner'),
    parent: ['fullbanner/hello'],
    icon: 'cover-image',
    supports: { html: false, reusable: false },
    edit: () =>
      el('div', { slot: 'b' },
        el(InnerBlocks, {
          templateLock: false,
          renderAppender: InnerBlocks.ButtonBlockAppender,
        })
      ),
    save: () => el(InnerBlocks.Content),
  });

  // ---------------------------
  // Parent block
  // ---------------------------
  const TEMPLATE = [
    ['fullbanner/slot-a'],
    ['fullbanner/slot-b'],
  ];

  registerBlockType('fullbanner/hello', {
    edit: ({ attributes, setAttributes }) => {
      const { height } = attributes;

      return el(
        Fragment,
        {},
        el(
          InspectorControls,
          {},
          el(
            PanelBody,
            { title: __('Banner Settings', 'fullbanner'), initialOpen: true },
            el(TextControl, {
              label: __('Height (e.g. 400px or 50vh)', 'fullbanner'),
              value: height || '',
              onChange: (val) => setAttributes({ height: val }),
              placeholder: 'e.g. 600px or 60vh',
              help: __('Leave empty to let content define height.', 'fullbanner'),
            })
          )
        ),

        // Host web component
        el(
          'fullbanner-hello',
          { style: height ? { height } : undefined, 'border-color': 'green' },
          // Render child blocks; each child’s edit() provides its slot wrapper
          el(InnerBlocks, {
            template: TEMPLATE,
            allowedBlocks: ['fullbanner/slot-a', 'fullbanner/slot-b'],
            templateLock: 'all', // keep two fixed regions; change to false if you want them removable
          })
        )
      );
    },

    // Save the children so PHP can split them
    save: () => el(InnerBlocks.Content),
  });
})(window.wp);
