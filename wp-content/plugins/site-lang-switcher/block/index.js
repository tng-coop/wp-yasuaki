;(function waitForWP() {
  // How often (ms) to check for wp.*
  const CHECK_INTERVAL = 50;

  // If wp.blocks is available, register your block and stop polling
  if (
    window.wp &&
    window.wp.blocks &&
    window.wp.element &&
    window.wp.i18n &&
    window.wp.blockEditor
  ) {
    const { registerBlockType } = window.wp.blocks;
    const { useBlockProps }     = window.wp.blockEditor;
    const { __ }                = window.wp.i18n;

    registerBlockType( 'sls/site-lang-switcher', {
      edit() {
        return window.wp.element.createElement(
          'div',
          { ...useBlockProps(), className: 'sls-preview' },
          __( '🌐  EN / JP language switcher (front-end only)', 'site-lang-switcher' )
        );
      },
      save() {
        return window.wp.element.createElement(
          'div',
          { id: 'site-lang-switcher' },
          [
            window.wp.element.createElement(
              'span',
              { key: 'icon', style: { fontSize: '1.2rem' } },
              '🌐'
            ),
            window.wp.element.createElement(
              'div',
              { key: 'toggle', className: 'lang-toggle' },
              [
                window.wp.element.createElement(
                  'button',
                  { key: 'en', id: 'btn-en', className: 'lang-btn', type: 'button' },
                  'EN'
                ),
                window.wp.element.createElement(
                  'button',
                  { key: 'jp', id: 'btn-jp', className: 'lang-btn', type: 'button' },
                  'JP'
                )
              ]
            )
          ]
        );
      }
    } );
  } else {
    // Try again in 50ms
	console.log( 'Waiting for wp.blocks...' );
    setTimeout( waitForWP, CHECK_INTERVAL );
  }
})();
