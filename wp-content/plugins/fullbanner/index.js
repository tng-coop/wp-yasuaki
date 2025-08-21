// file: index.js
(function (wp) {
  const { registerBlockType } = wp.blocks;
  const { createElement: el, Fragment } = wp.element;
  const { __ } = wp.i18n;
  const { useBlockProps, InnerBlocks, InspectorControls } = wp.blockEditor;
  const { PanelBody, TextControl } = wp.components;

  const TEMPLATE = [
    [ 'core/group', { className: 'slot-a' }, [] ],
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
        el(
          'fullbanner-hello',
          {
            style: height ? `height:${height}` : undefined,
            'border-color': 'green',
          },
          el( InnerBlocks, { template: TEMPLATE, templateLock: false } )
        )
      );
    },

    save: ({ attributes }) => {
      const { height } = attributes;

      return el(
        'fullbanner-hello',
        {
          style: height ? `height:${height}` : undefined,
          'border-color': 'green',
        },
        el( InnerBlocks.Content )
      );
    },
  });
})(window.wp);
