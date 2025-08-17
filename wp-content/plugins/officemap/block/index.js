(function (wp) {
  const { registerBlockType } = wp.blocks;
  const { __ } = wp.i18n;
  const ServerSideRender = wp.serverSideRender;
  const el = wp.element.createElement;

  registerBlockType('officemap/nonce', {
    title: __('OfficeMap (Nonce Starter)', 'officemap'),
    icon: 'location',
    category: 'widgets',

    // Show the PHP-rendered markup inside the editor.
    edit: (props) =>
      el(ServerSideRender, {
        block: 'officemap/nonce',
        attributes: props.attributes,
      }),

    // Dynamic block is rendered by PHP on the frontend.
    save: () => null,
  });
})(window.wp);
