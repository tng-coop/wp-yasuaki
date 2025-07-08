// load Leaflet CSS dynamically
const link = document.createElement('link');
link.rel = 'stylesheet';
link.href = 'https://esm.sh/leaflet@1.9.3/dist/leaflet.css';
document.head.appendChild(link);

import * as L from 'https://esm.sh/leaflet@1.9.3?bundle';
import * as d3 from 'https://esm.sh/d3@7?bundle';
import { hexbin as d3Hexbin } from 'https://esm.sh/d3-hexbin?bundle';
import { getProcessedOfficeData } from './office-data.js';

export default function initOfficeMap() {
  const container = document.getElementById('kanagawa-office-map');
  if (!container) return;

  const rawData = getProcessedOfficeData();

  // 1) Initialize Leaflet
  const map = L.map('kanagawa-office-map', {
    attributionControl: false,
    maxZoom: 18,
    zoomSnap: 0,
    zoomDelta: 0.25,
    wheelPxPerZoomLevel: 60,
    dragging: false,
    touchZoom: false,
    scrollWheelZoom: false,
    doubleClickZoom: false,
    boxZoom: false,
    keyboard: false,
    tap: false,
    zoomControl: false,
  });

  // allow pointer events only on the overlay pane
  map.getContainer().style.pointerEvents = 'auto';
  map.getPanes().overlayPane.style.pointerEvents = 'all';

  L.tileLayer(
    'https://cyberjapandata.gsi.go.jp/xyz/seamlessphoto/{z}/{x}/{y}.jpg',
    { maxZoom: 18 }
  ).addTo(map);

  L.control.attribution({ prefix: false })
   .addAttribution('出典：国土地理院')
   .addTo(map);

  // 2) Fit to extremes
  const extremes = [
    { name: 'Northernmost', coords: [35.6518, 139.1418] },
    { name: 'Southernmost', coords: [35.1389, 139.6264] },
    { name: 'Easternmost',  coords: [35.5229, 139.7762] },
    { name: 'Westernmost',  coords: [35.2606, 139.0045] }
  ];
  const markers = extremes.map(ext =>
    L.marker(ext.coords, {
      icon: L.divIcon({
        html: `<div class="label-text">${ext.name}</div>`,
        iconAnchor: [0, -10]
      })
    })
  );
  const group = L.featureGroup(markers);
  map.fitBounds(group.getBounds(), { padding: [0,0], animate: false });

  // redraw on map events
  map.on('moveend zoomend resize', updateGrid);
  map.once('load', updateGrid);
  map.fire('load');

  function updateGrid() {
    // 1) clear old overlay
    d3.select(map.getPanes().overlayPane).select('svg').remove();

    // 2) compute pane bounds
    const tl = map.latLngToLayerPoint(map.getBounds().getNorthWest());
    const br = map.latLngToLayerPoint(map.getBounds().getSouthEast());
    const paneW = br.x - tl.x;
    const paneH = br.y - tl.y;

    // 3) derive the pixel‐bounds of our extremes rectangle
    const bounds = group.getBounds();
    const sw = map.latLngToLayerPoint(bounds.getSouthWest());
    const ne = map.latLngToLayerPoint(bounds.getNorthEast());
    const xMin = sw.x, yMin = ne.y;
    const w    = ne.x - sw.x, h    = sw.y - ne.y;

    // 4) resize only the height of the map container to fit that boundary,
    //    then bail so Leaflet emits 'resize' and calls updateGrid() again
    const targetH = Math.round(h);
    if (container.offsetHeight !== targetH) {
      container.style.height = `${targetH}px`;
      map.invalidateSize(false);
      return;
    }

    // 5) append SVG & G
    const overlay = d3.select(map.getPanes().overlayPane)
                      .append('svg')
                        .attr('class','leaflet-zoom-hide')
                        .attr('width',  paneW)
                        .attr('height', paneH)
                        .style('left',  `${tl.x}px`)
                        .style('top',   `${tl.y}px`);
    const g = overlay.append('g')
                     .attr('class','hexbin-layer')
                     .attr('transform', `translate(${-tl.x},${-tl.y})`);

    // // 6) draw the red boundary
    // g.append('rect')
    //   .attr('class','boundary')
    //   .attr('x',      xMin)
    //   .attr('y',      yMin)
    //   .attr('width',  w)
    //   .attr('height', h);

    // 7) build hex‐grid cell centers
    const R     = 20;
    const horiz = Math.sqrt(3) * R;
    const vert  = 1.5 * R;
    const cells = [];
    for (let j = 0; ; j++) {
      const cy = yMin + R + j * vert;
      if (cy > yMin + h - R) break;
      const xOff = (j % 2) * (horiz / 2);
      for (let i = 0; ; i++) {
        const cx = xMin + R + i * horiz + xOff;
        if (cx > xMin + w - R) break;
        cells.push({ cx, cy, used: false });
      }
    }
    const hexgen = d3Hexbin().radius(R);

    // 8) place one hex per office & attach click
    rawData.forEach((d) => {
      const pt = map.latLngToLayerPoint([d.lat, d.lon]);
      let best = { dist: Infinity, cell: null };
      cells.forEach(c => {
        if (!c.used) {
          const dx = c.cx - pt.x,
                dy = c.cy - pt.y,
                d2 = dx*dx + dy*dy;
          if (d2 < best.dist) best = { dist: d2, cell: c };
        }
      });
      if (!best.cell) return;
      best.cell.used = true;

      // hexagon
      g.append('path')
        .attr('class', 'hexbin')
        .attr('d', hexgen.hexagon())
        .attr('transform', `translate(${best.cell.cx},${best.cell.cy})`)
        .style('pointer-events', 'all')
        .on('click', () => {
          const el = document.getElementById(`tile-${d.id}`);
          if (!el) return console.warn(`No element with ID tile-${d.id}`);

          const rect      = el.getBoundingClientRect();
          const absoluteY = rect.top + window.scrollY;
          const mapHeight = container.getBoundingClientRect().height;
          const offsetY   = absoluteY - mapHeight;

          window.scrollTo({ top: offsetY, behavior: 'smooth' });
        });

      // label
      g.append('text')
        .attr('class', 'hex-label')
        .attr('transform', `translate(${best.cell.cx},${best.cell.cy})`)
        .attr('dy', '.35em')
        .attr('text-anchor', 'middle')
        .text(d.id2);
    });
  }
}

// auto-init
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initOfficeMap);
} else {
  initOfficeMap();
}
