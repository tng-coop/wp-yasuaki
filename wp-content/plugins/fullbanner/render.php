<?php
if ( ! defined( 'ABSPATH' ) ) { exit; }

$area_a = '';

if ( isset( $block ) && ! empty( $block->inner_blocks ) ) {
  foreach ( $block->inner_blocks as $ib ) {
    $cls = isset( $ib->attributes['className'] ) ? (string) $ib->attributes['className'] : '';
    if ( strpos( $cls, 'slot-a' ) !== false ) {
      $area_a .= render_block( $ib->parsed_block );
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
  <?php if ( trim( $area_a ) !== '' ) : ?>
    <div slot="a"><?php echo $area_a; // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?></div>
  <?php endif; ?>
</fullbanner-hello>
