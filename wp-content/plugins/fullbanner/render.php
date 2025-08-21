<?php
if ( ! defined( 'ABSPATH' ) ) { exit; }

$area_a = '';
$area_b = '';

if ( isset( $block ) && ! empty( $block->inner_blocks ) ) {
  foreach ( $block->inner_blocks as $ib ) {
    // Detect child block by name
    if ( isset($ib->name) && $ib->name === 'fullbanner/slot-a' ) {
      $area_a .= render_block( $ib->parsed_block );
    }
    if ( isset($ib->name) && $ib->name === 'fullbanner/slot-b' ) {
      $area_b .= render_block( $ib->parsed_block );
    }
  }
}

$height = isset( $attributes['height'] ) ? (string) $attributes['height'] : '';
?>
<fullbanner-hello
  border-color="green"
  <?php if ( $height !== '' ) : ?>
    style="height:<?php echo esc_attr( $height ); ?>;"
  <?php endif; ?>
>
  <div slot="a">
    <?php echo $area_a; // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>
  </div>

  <div slot="b">
    <?php echo $area_b; // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?>
  </div>
</fullbanner-hello>
