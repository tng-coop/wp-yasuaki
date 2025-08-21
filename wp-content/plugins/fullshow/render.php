<?php
if ( ! defined( 'ABSPATH' ) ) { exit; }

/**
 * We look for two top-level inner blocks (Groups) with className "slot-a" and "slot-b".
 * If content exists for a slot, we output it inside the corresponding slotted <div>.
 * If a slot is missing or empty, we OMIT the slotted <div> entirely so the web component's
 * default slot content is used.
 *
 * Available vars (provided by WP when using "render": "file:render.php"):
 * - $attributes (array)
 * - $content (string of rendered inner blocks)  [not used directly]
 * - $block (WP_Block): gives access to parsed inner blocks
 */

$areas = array( 'a' => '', 'b' => '' );

if ( isset( $block ) && ! empty( $block->inner_blocks ) ) {
	foreach ( $block->inner_blocks as $ib ) {
		$cls = isset( $ib->attributes['className'] ) ? (string) $ib->attributes['className'] : '';

		// Render the inner block to HTML
		$html = render_block( $ib->parsed_block );

		if ( strpos( $cls, 'slot-a' ) !== false ) {
			$areas['a'] .= $html;
		} elseif ( strpos( $cls, 'slot-b' ) !== false ) {
			$areas['b'] .= $html;
		}
	}
}
?>
<fullshow-hello border-color="green">
	<?php if ( trim( $areas['a'] ) !== '' ) : ?>
		<div slot="a"><?php echo $areas['a']; // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?></div>
	<?php endif; ?>

	<?php if ( trim( $areas['b'] ) !== '' ) : ?>
		<div slot="b"><?php echo $areas['b']; // phpcs:ignore WordPress.Security.EscapeOutput.OutputNotEscaped ?></div>
	<?php endif; ?>
</fullshow-hello>
