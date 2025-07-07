// view.js
const link = document.createElement('link');
link.rel = 'stylesheet';
link.href = 'https://esm.sh/leaflet@1.9.3/dist/leaflet.css';
document.head.appendChild(link);

import * as L from 'https://esm.sh/leaflet@1.9.3?bundle';
import * as d3 from 'https://esm.sh/d3@7?bundle';
import { hexbin as d3Hexbin } from 'https://esm.sh/d3-hexbin?bundle';
import { getProcessedOfficeData } from './office-data.js';
console.log("aa")
export default function initOfficeMap() {
  const container = document.getElementById('kanagawa-office-map');
  if (!container) return;

  const rawData = getProcessedOfficeData();
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

  map.getContainer().style.pointerEvents = 'none';
  map.getPanes().overlayPane.style.pointerEvents = 'all';

  L.tileLayer('https://cyberjapandata.gsi.go.jp/xyz/seamlessphoto/{z}/{x}/{y}.jpg', { maxZoom: 18 }).addTo(map);
  L.control.attribution({ prefix: false }).addAttribution('出典：国土地理院').addTo(map);

  const extremes = [
    { name: 'Northernmost', coords: [35.6518, 139.1418] },
    { name: 'Southernmost', coords: [35.1389, 139.6264] },
    { name: 'Easternmost', coords: [35.5229, 139.7762] },
    { name: 'Westernmost', coords: [35.2606, 139.0045] }
  ];
  const markers = extremes.map(ext => L.marker(ext.coords, {
    icon: L.divIcon({ html: `<div class="label-text">${ext.name}</div>`, iconAnchor: [0, -10] })
  }));
  const group = L.featureGroup(markers);
  map.fitBounds(group.getBounds(), { padding: [0, 0], animate: false });

  L.svg().addTo(map);
  const svg = d3.select(map.getPanes().overlayPane).select('svg');
  const g = svg.append('g').attr('class', 'hexbin-layer');

  function updateGrid() {
    g.selectAll('*').remove();
    const R = 20;
    const bounds = group.getBounds();
    const sw = map.latLngToLayerPoint(bounds.getSouthWest());
    const ne = map.latLngToLayerPoint(bounds.getNorthEast());
    const xMin = sw.x, yMin = ne.y;
    const width = ne.x - sw.x, height = sw.y - ne.y;

    // // draw boundaries
    // g.append('rect').attr('class', 'boundary')
    //   .attr('x', xMin).attr('y', yMin)
    //   .attr('width', width).attr('height', height);

    // generate hex grid cells
    const horiz = Math.sqrt(3) * R, vert = 1.5 * R;
    const cells = [];
    for (let j = 0; ; j++) {
      const cy = yMin + R + j * vert;
      if (cy > yMin + height - R) break;
      const xOff = (j % 2) * (horiz / 2);
      for (let i = 0; ; i++) {
        const cx = xMin + R + i * horiz + xOff;
        if (cx > xMin + width - R) break;
        cells.push({ cx, cy, used: false });
      }
    }

    // place one office per cell
    const points = rawData.map(d => map.latLngToLayerPoint([d.lat, d.lon]));
    const hexgen = d3Hexbin().radius(R);
    points.forEach((pt, idx) => {
      let best = { dist: Infinity, cell: null };
      cells.forEach(c => {
        if (!c.used) {
          const dx = c.cx - pt.x, dy = c.cy - pt.y;
          const d2 = dx * dx + dy * dy;
          if (d2 < best.dist) best = { dist: d2, cell: c };
        }
      });
      if (best.cell) {
        best.cell.used = true;
        const cell = g.append('g').attr('transform', `translate(${best.cell.cx},${best.cell.cy})`);
        cell.append('path').attr('class', 'hexbin').attr('d', hexgen.hexagon());
        cell.append('text').attr('class', 'hex-label')
          .attr('dy', '.35em').attr('text-anchor', 'middle')
          .text(rawData[idx].id2);
      }
    });
  }

  updateGrid();
  map.on('moveend zoomend', updateGrid);
  window.addEventListener('resize', () => {
    map.invalidateSize(false);
    updateGrid();
  });
}

// Auto-init
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initOfficeMap);
} else {
  initOfficeMap();
}