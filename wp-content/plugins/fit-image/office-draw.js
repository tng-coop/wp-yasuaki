(function () {
  // Reusable renderer (can be called again after data updates)
  async function renderOffices(el, points) {
    if (!el) return;
    await customElements.whenDefined('fit-image');
    if (!Array.isArray(points) || points.length === 0) return;

    const ptsLL = points.map(p => ({ lon: p.lon, lat: p.lat, label: p.title }));
    const opts = {
      grid: 'hex',          // enables nearest-unused hex snapping
      marker: 'auto',
      tooltip: 'html',
      unitPct: 0.05,
      strokeW: 3,
      stroke: '#fff',
      fill: 'rgba(83,182,221,0.25)'
    };

    const draw = () => el.setOverlayPointsLonLat(ptsLL, opts);

    // Draw now if metrics are ready, else wait for the first layout tick
    (el._nW > 0 && el._nH > 0)
      ? draw()
      : el.addEventListener('fit-image:viewport-transform', function once() {
          el.removeEventListener('fit-image:viewport-transform', once);
          draw();
        }, { once: true });
  }

  // Initial draw once DOM is ready
  window.addEventListener('DOMContentLoaded', () => {
    const el = document.querySelector('fit-image');
    renderOffices(el, window.officePoints);
  });

  // Optional: allow re-draws without reload
  // Call: window.updateOfficePoints(newArray);
  window.updateOfficePoints = function (nextPoints) {
    window.officePoints = Array.isArray(nextPoints) ? nextPoints : [];
    const el = document.querySelector('fit-image');
    renderOffices(el, window.officePoints);
  };
})();
