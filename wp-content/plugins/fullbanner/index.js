(function (wp) {
  const { registerBlockType } = wp.blocks;
  const { createElement: el, Fragment } = wp.element;
  const { __ } = wp.i18n;
  const { InnerBlocks, InspectorControls } = wp.blockEditor;
  const { PanelBody, TextControl } = wp.components;

  registerBlockType('fullbanner/slot-a', {
    title: __('Fullbanner: Base', 'fullbanner'),
    parent: ['fullbanner/hello'],
    icon: 'align-wide',
    supports: { html: false, reusable: false },
    edit: () =>
      el('div',
        { className: 'fullbanner-slot-a' },
        el(InnerBlocks, {
          templateLock: false,
          renderAppender: InnerBlocks.ButtonBlockAppender,
        })
      ),
    save: () => el(InnerBlocks.Content),
  });

  registerBlockType('fullbanner/slot-b', {
    title: __('Fullbanner: Overlay', 'fullbanner'),
    parent: ['fullbanner/hello'],
    icon: 'cover-image',
    supports: { html: false, reusable: false },
    edit: () =>
      el('div',
        { className: 'fullbanner-slot-b' },
        el(InnerBlocks, {
          templateLock: false,
          renderAppender: InnerBlocks.ButtonBlockAppender,
        })
      ),
    save: () => el(InnerBlocks.Content),
  });

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

        // Editor preview container
        el(
          'div',
          { className: 'fullbanner', style: height ? { height } : undefined },
          el('div', { className: 'pane a' },
            el(InnerBlocks,
              {
                template: TEMPLATE,
                allowedBlocks: ['fullbanner/slot-a', 'fullbanner/slot-b'],
                templateLock: 'all',
              }
            )
          ),
          el('div', { className: 'overlay' },
            el('div', { className: 'overlay-inner' },
              el('div', { className: 'fullbanner-overlay-preview' })
            )
          )
        )
      );
    },

    save: () => el(InnerBlocks.Content),
  });
})(window.wp);
