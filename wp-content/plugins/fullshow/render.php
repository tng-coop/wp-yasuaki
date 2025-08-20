<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}
?>
<div class="fullshow-box">
	<div class="fullshow-pane fullshow-left">
		<div class="fullshow-text">hello</div>
	</div>
	<div class="fullshow-pane fullshow-right">
		<?php for ( $i = 0; $i < 40; $i++ ) : ?>
			<p>hello</p>
		<?php endfor; ?>
	</div>
</div>
