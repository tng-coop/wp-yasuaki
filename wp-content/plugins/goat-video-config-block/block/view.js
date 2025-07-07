console.log('view.js loaded');
/**
 * view.js
 * Populates the <pre> with only the goat video JSON on the front-end.
 */
export default function initGoatVideoConfig() {
  const container = document.getElementById( 'goat-video-config' );
  if (
    ! container ||
    ! window.pageDetailsData ||
    ! pageDetailsData.videoData ||
    ! pageDetailsData.videoData[ '30646036' ]
  ) {
    return;
  }

  // Pretty-print just the goat video files array
  container.textContent = JSON.stringify(
    pageDetailsData.videoData[ '30646036' ],
    null,
    2
  );
}
// Auto-initialize when the DOM is ready
if ( document.readyState === 'loading' ) {
  document.addEventListener( 'DOMContentLoaded', initGoatVideoConfig );
} else {
  initGoatVideoConfig();
}
