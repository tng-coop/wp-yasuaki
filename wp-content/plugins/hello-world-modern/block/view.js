// Minimal front-end enhancement; loads on the site side.
(function () {
  const blocks = document.querySelectorAll('.wp-block-hello-world-modern-block, .hello-world-modern');
  blocks.forEach((el) => {
    // Example enhancement: add an accessible label once on load
    if (!el.dataset.hwInit) {
      el.setAttribute('aria-label', 'Hello World block');
      el.dataset.hwInit = '1';
    }
  });
  // Optional: console signal for debugging
  if (blocks.length) {
    // eslint-disable-next-line no-console
    console.log('Hello World (Modern): view module initialized on', blocks.length, 'node(s).');
  }
})();
