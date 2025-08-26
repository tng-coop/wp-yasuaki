<?php
if ( ! defined( 'ABSPATH' ) ) { exit; }

$area_a = '';
$area_b = '';

if ( isset( $block ) && ! empty( $block->inner_blocks ) ) {
  foreach ( $block->inner_blocks as $ib ) {
    if ( isset( $ib->name ) && $ib->name === 'fullbanner/slot-a' ) {
      $area_a .= render_block( $ib->parsed_block );
    }
    if ( isset( $ib->name ) && $ib->name === 'fullbanner/slot-b' ) {
      $area_b .= render_block( $ib->parsed_block );
    }
  }
}

$height = isset( $attributes['height'] ) ? (string) $attributes['height'] : '';
$style  = $height !== '' ? 'height:' . esc_attr( $height ) . ';' : '';
?>
<div class="fullbanner" style="<?php echo esc_attr( $style ); ?>">
  <div class="pane a">
    <?php echo $area_a; // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>
  </div>

  <div class="overlay" data-scale="fit-height">
    <div class="overlay-inner">
      <?php echo $area_b; // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>
    </div>
  </div>
</div>

<?php
// Print the scaler once per page.
static $fb_scaler_printed = false;
if ( ! $fb_scaler_printed ) :
  $fb_scaler_printed = true; ?>
  <script>
  (function () {
    function fitBox(box) {
      var overlay = box.querySelector('.overlay[data-scale="fit-height"]');
      if (!overlay) return;
      var inner = overlay.querySelector('.overlay-inner');
      if (!inner) return;

      // anchor at left edge, vertical center
      inner.style.left = '0';
      inner.style.top = '50%';
      inner.style.transformOrigin = 'left center';

      // reset for accurate measurement (no X translation)
      inner.style.transform = 'translateY(-50%)';

      var boxH   = box.getBoundingClientRect().height;
      var innerH = inner.getBoundingClientRect().height;

      if (boxH > 0 && innerH > 0) {
        var s = Math.min(1, boxH / innerH); // fit height, do not upscale
        inner.style.transform = 'translateY(-50%) scale(' + s + ')';
      }
    }

    function fitAll() {
      document.querySelectorAll('.fullbanner').forEach(fitBox);
    }

    if (document.readyState === 'complete') fitAll();
    else window.addEventListener('load', fitAll, { once: true });

    window.addEventListener('resize', fitAll);

    document.addEventListener('load', function (e) {
      if (e.target && e.target.closest && e.target.closest('.overlay[data-scale="fit-height"]')) {
        fitAll();
      }
    }, true);
  })();
  </script>
<?php endif; ?>
