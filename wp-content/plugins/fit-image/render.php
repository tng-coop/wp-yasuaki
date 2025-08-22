<?php
if (!defined('ABSPATH')) exit;

$demo_img = plugins_url('kanagawa_sat_cropped_z10.png', __FILE__);

/* Collect office points */
$q = new WP_Query([
  'post_type'      => 'office-cpt',
  'posts_per_page' => -1,
  'fields'         => 'ids',
  'no_found_rows'  => true,
]);

$points = [];
foreach ($q->posts as $pid) {
  $data  = get_post_meta($pid, 'data', true) ?: [];
  $title = get_the_title($pid);
  $lat   = $data['lat'] ?? $data['latitude'] ?? ($data['location']['lat'] ?? null);
  $lon   = $data['lon'] ?? $data['lng'] ?? $data['longitude'] ?? ($data['location']['lon'] ?? $data['location']['lng'] ?? null);
  if (is_numeric($lat) && is_numeric($lon)) {
    $points[] = ['id'=>$pid,'title'=>$title,'lat'=>(float)$lat,'lon'=>(float)$lon];
  }
}
$uid = 'fit-' . wp_generate_uuid4();
?>
<fit-image id="<?php echo esc_attr($uid); ?>" class="fit-image-large" data-fit-top>
  <img src="<?php echo esc_url($demo_img); ?>" alt="Example" />
  <svg id="svg-overlay"></svg>
</fit-image>

<script>
(async () => {
  const officePoints = <?php echo wp_json_encode($points, JSON_UNESCAPED_UNICODE|JSON_UNESCAPED_SLASHES); ?>;
  console.log('officePoints', officePoints);

  // Wait for custom element upgrade
  await customElements.whenDefined('fit-image');
  const el = document.getElementById('<?php echo esc_js($uid); ?>');
  if (!el || !officePoints.length) return;

  const ptsLL = officePoints.map(p => ({ lon: p.lon, lat: p.lat, label: p.title }));
  const opts = {
    grid: 'hex', marker: 'auto', tooltip: 'html',
    unitPct: 0.05, strokeW: 3, stroke: '#fff',
    fill: 'rgba(83,182,221,0.25)'
  };

  const draw = () => el.setOverlayPointsLonLat(ptsLL, opts);

  // If natural size known, draw immediately, else wait for first layout tick
  (el._nW > 0 && el._nH > 0)
    ? draw()
    : el.addEventListener('fit-image:viewport-transform', function once() {
        el.removeEventListener('fit-image:viewport-transform', once);
        draw();
      }, { once:true });
})();
</script>
