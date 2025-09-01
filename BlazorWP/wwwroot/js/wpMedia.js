export function initMediaPage(iframeEl, overlayEl) {
  console.log("↪ wpMedia.initMediaPage called");

  // 1) hide the overlay once the iframe really loads
  iframeEl.addEventListener("load", () => {
    try {
      const doc = iframeEl.contentDocument || iframeEl.contentWindow.document;
      const style = doc.createElement("style");
      style.textContent = `
          #wpadminbar, #adminmenumain, #adminmenuwrap, #wpfooter, .wrap > h1 {
            display: none!important;
          }
          html, body, #wpbody-content, .wrap {
            margin:0!important;
            padding:0!important;
            height:100%!important;
          }
        `;
      doc.head.appendChild(style);
    } catch (e) {
      console.warn("Failed to inject CSS:", e);
    }

    // hide the yellow overlay
    overlayEl.style.display = "none";

    // measure distance from top of PAGE to top of iframe
    const rect = iframeEl.getBoundingClientRect();
    const pageOffsetTop = rect.top + window.scrollY;
    console.log(`Distance from top of page to iframe top: ${pageOffsetTop}px`);
  });

  // 2) wire up resizing for both iframe and overlay
  function adjustMedia() {
    if (!iframeEl) return;

    // measure iframe position and size
    const rect = iframeEl.getBoundingClientRect();
    const pageOffsetTop = rect.top + window.scrollY;
    const pageOffsetLeft = rect.left + window.scrollX;

    // compute new height to fill remaining viewport
    const h = window.innerHeight - pageOffsetTop;

    // resize the iframe
    iframeEl.style.height = h + "px";

    // position and size the overlay to match the iframe
    overlayEl.style.position = "absolute";
    overlayEl.style.top = pageOffsetTop + "px";
    overlayEl.style.left = pageOffsetLeft + "px";
    overlayEl.style.width = rect.width + "px";
    overlayEl.style.height = h + "px";

    // console.log("↪ resized iframe to", h, "and overlay to", {
    //   top: overlayEl.style.top,
    //   left: overlayEl.style.left,
    //   width: overlayEl.style.width,
    //   height: overlayEl.style.height
    // });
  }

  // listen for window resizes and adjust accordingly
  window.addEventListener("resize", adjustMedia);
  // initial sizing
  adjustMedia();
}
