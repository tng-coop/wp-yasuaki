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

  // allow clicks only on overlayPane
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
    const width  = br.x - tl.x;
    const height = br.y - tl.y;

    // 3) append SVG & G
    const overlay = d3.select(map.getPanes().overlayPane)
                      .append('svg')
                        .attr('class','leaflet-zoom-hide')
                        .attr('width',  width)
                        .attr('height', height)
                        .style('left',  `${tl.x}px`)
                        .style('top',   `${tl.y}px`);
    const g = overlay.append('g')
                     .attr('class','hexbin-layer')
                     .attr('transform', `translate(${-tl.x},${-tl.y})`);

    // 4) derive our grid bounds from the extremes group
    const bounds = group.getBounds();
    const sw = map.latLngToLayerPoint(bounds.getSouthWest());
    const ne = map.latLngToLayerPoint(bounds.getNorthEast());
    const xMin = sw.x, yMin = ne.y;
    const w    = ne.x - sw.x, h    = sw.y - ne.y;

    // 5) build hex‐grid cell centers
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

    // 6) place one hex per office & attach click
    rawData.forEach((d) => {
      // find best cell
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

      g.append('path')
        .attr('class', 'hexbin')
        .attr('d', hexgen.hexagon())
        .attr('transform', `translate(${best.cell.cx},${best.cell.cy})`)
        .style('pointer-events', 'all')
        .on('click', () => {
          const id = d.id;
          const el = document.getElementById(`tile-${id}`);
          if (!el) {
            console.warn(`No element with ID tile-${id}`);
            return;
          }

          // calculate absolute Y and apply 500px offset
          const rect = el.getBoundingClientRect();
          const absoluteY = rect.top + window.scrollY;
          const offsetY = absoluteY - 500;

          console.log(
            `tile-${id}: absoluteY=${absoluteY.toFixed(1)}px, ` +
            `scrolling to ${offsetY.toFixed(1)}px`
          );

          window.scrollTo({
            top: offsetY,
            behavior: 'smooth'
          });
        });

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
