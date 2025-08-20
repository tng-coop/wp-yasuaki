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

<script type="module">
  import Split from 'https://esm.sh/split.js@1.6.0';
  const container = document.currentScript.previousElementSibling;
  if (container) {
    const left  = container.querySelector('.fullshow-left');
    const right = container.querySelector('.fullshow-right');
    if (left && right) {
      Split([left, right], {
        sizes: [50, 50],
        minSize: 0,
        gutterSize: 24,   // equals the two removed 12px borders
        snapOffset: 0,
        cursor: 'col-resize',
        direction: 'horizontal'
      });
    }
  }
</script>
