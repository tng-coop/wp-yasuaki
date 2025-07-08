// view.js

// Inject Bootstrap CSS from esm.sh
const bsCss = document.createElement('link');
bsCss.rel = 'stylesheet';
bsCss.href = 'https://esm.sh/bootstrap@5.3.2/dist/css/bootstrap.min.css';
document.head.appendChild(bsCss);

// Import Bootstrap JS bundle (includes Popper)
import 'https://esm.sh/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js';
import { getProcessedOfficeData } from './office-data.js';

window.addEventListener('DOMContentLoaded', () => {
  const container = document.getElementById('kanagawa-office-list');
  if (!container) return;

  // Clear and prepare container
  container.innerHTML = '';
  container.classList.add('my-4');

  let data = getProcessedOfficeData();

  // Sort by regionName, then by numeric id
  data.sort((a, b) => {
    if (a.regionName < b.regionName) return -1;
    if (a.regionName > b.regionName) return  1;
    return a.id - b.id;
  });

  // Group by region
  let currentRegion = null;
  let rowWrapper = null;

  data.forEach(d => {
    if (d.regionName !== currentRegion) {
      currentRegion = d.regionName;
      // Region heading
      const heading = document.createElement('h2');
      heading.textContent = currentRegion;
      heading.classList.add('mt-5', 'mb-3');
      container.appendChild(heading);

      // New row wrapper for this region
      rowWrapper = document.createElement('div');
      rowWrapper.className = 'row row-cols-1 row-cols-md-2 g-4';
      container.appendChild(rowWrapper);
    }

    // Create card column
    const col = document.createElement('div');
    col.className = 'col';

    const card = document.createElement('div');
    card.className = 'card h-100 shadow-sm';

    const body = document.createElement('div');
    body.className = 'card-body';

    // Title and subtitle
    const title = document.createElement('h5');
    title.className = 'card-title';
    title.textContent = d.office;
    body.appendChild(title);

    const sub = document.createElement('h6');
    sub.className = 'card-subtitle mb-2 text-muted';
    sub.textContent = `ID: ${d.id2}`;
    body.appendChild(sub);

    // Contact info list
    const infoList = document.createElement('ul');
    infoList.className = 'list-unstyled mb-3';

    const addItem = (label, value, isLink=false, prefix='') => {
      if (!value) return;
      const li = document.createElement('li');
      li.className = 'mb-1';
      if (isLink) {
        const a = document.createElement('a');
        a.href = value.startsWith('http') ? value : `${prefix}${value}`;
        a.textContent = value;
        a.target = '_blank';
        li.innerHTML = `<strong>${label}:</strong> `;
        li.appendChild(a);
      } else {
        li.innerHTML = `<strong>${label}:</strong> ${value}`;
      }
      infoList.appendChild(li);
    };

    addItem('Address',  d.address);
    addItem('Tel',      d.tel);
    addItem('Fax',      d.fax);
    addItem('Email',    d.email, true, 'mailto:');
    addItem('Website',  d.url,   true);
    body.appendChild(infoList);

    // Content / description
    if (d.content) {
      const desc = document.createElement('p');
      desc.className = 'card-text mb-3';
      desc.textContent = d.content;
      body.appendChild(desc);
    }

    // Badges for category & tags
    const badgeContainer = document.createElement('div');
    if (d.category) {
      const catBadge = document.createElement('span');
      catBadge.className = 'badge bg-primary me-1';
      catBadge.textContent = d.category;
      badgeContainer.appendChild(catBadge);
    }
    if (Array.isArray(d.tags)) {
      d.tags.forEach(tag => {
        const tagBadge = document.createElement('span');
        tagBadge.className = 'badge bg-secondary me-1';
        tagBadge.textContent = tag;
        badgeContainer.appendChild(tagBadge);
      });
    }
    body.appendChild(badgeContainer);

    // Footer with coordinates and region number
    const footer = document.createElement('div');
    footer.className = 'card-footer text-muted small';
    footer.innerHTML = `
      Region No.: ${d.regionNumber}<br/>
      Coordinates: ${d.lat || '–'}, ${d.lon || '–'}
    `;
    card.appendChild(body);
    card.appendChild(footer);
    col.appendChild(card);
    rowWrapper.appendChild(col);
  });
});
