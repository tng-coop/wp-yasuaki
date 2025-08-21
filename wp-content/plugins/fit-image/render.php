<?php
if (!defined('ABSPATH')) { exit; }

/**
 * Minimal render callback: always shows the Kanagawa demo.
 */
$demo_img = plugins_url('kanagawa_sat_cropped_z10.png', __FILE__);
?>
<fit-image class="fit-image-large" data-fit-top>
  <img src="<?php echo esc_url($demo_img); ?>" alt="Example" />
  <svg id="svg-overlay"></svg>
</fit-image>
