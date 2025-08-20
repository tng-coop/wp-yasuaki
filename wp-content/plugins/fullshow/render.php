<?php
if ( ! defined( 'ABSPATH' ) ) { exit; }
?>
<fullshow-hello border-color="green">
  <div slot="a" class="fullshow-text">hello</div>
  <div slot="b">
    <?php for ( $i = 0; $i < 40; $i++ ) : ?>
      <p>hello</p>
    <?php endfor; ?>
  </div>
</fullshow-hello>
