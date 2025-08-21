(function (wp) {
  const { registerBlockType } = wp.blocks;
  const { useBlockProps, InspectorControls } = wp.blockEditor || wp.editor;
  const { __ } = wp.i18n;
  const { PanelBody, SelectControl } = wp.components;

  const PRESET_OPTIONS = [
    { label: 'Frontpage', value: 'frontpage' },
    { label: 'About Page', value: 'aboutpage' },
    { label: 'Landing A', value: 'landingA' }
  ];

  registerBlockType('hero/video', {
    edit(props) {
      const { attributes, setAttributes } = props;
      const { configName = 'frontpage' } = attributes;

      const blockProps = useBlockProps({
        className: 'hero-video-container',
        id: 'hero-video'
      });

      return wp.element.createElement(
        wp.element.Fragment,
        null,
        wp.element.createElement(
          InspectorControls,
          null,
          wp.element.createElement(
            PanelBody,
            { title: __('Hero Video Preset', 'hero-video'), initialOpen: true },
            wp.element.createElement(SelectControl, {
              label: __('Choose preset config', 'hero-video'),
              value: configName,
              options: PRESET_OPTIONS,
              onChange: (val) => setAttributes({ configName: val })
            })
          )
        ),
        wp.element.createElement(
          'div',
          blockProps,
          wp.element.createElement(
            'p',
            { style: { position: 'absolute', margin: '8px' } },
            __('Hero Video (Preset: ', 'hero-video') + configName + ')'
          )
        )
      );
    },

    // Dynamic block: front end markup comes from render.php
    save() {
      return wp.element.createElement(
        'div',
        useBlockProps.save({ className: 'hero-video-container', id: 'hero-video' })
      );
    }
  });
})(window.wp);
