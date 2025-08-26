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
// Print the scaler script once.
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

      // reset transform for a correct measurement
      inner.style.transform = 'translate(-50%,-50%)';
      var boxH   = box.getBoundingClientRect().height;
      var innerH = inner.getBoundingClientRect().height;

      if (boxH > 0 && innerH > 0) {
        var s = Math.min(1, boxH / innerH); // fill height, never upscale
        inner.style.transform = 'translate(-50%,-50%) scale(' + s + ')';
      }
    }

    function fitAll() {
      document.querySelectorAll('.fullbanner').forEach(fitBox);
    }

    // initial
    if (document.readyState === 'complete') fitAll();
    else window.addEventListener('load', fitAll, { once: true });

    // window resize
    window.addEventListener('resize', fitAll);

    // image and media loads inside overlay
    document.addEventListener('load', function (e) {
      var el = e.target;
      if (!el || !el.closest) return;
      if (el.closest('.overlay[data-scale="fit-height"]')) fitAll();
    }, true);
  })();
  </script>
<?php endif; ?>
