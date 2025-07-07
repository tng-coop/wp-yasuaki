import { getProcessedOfficeData } from '../../kanagawa-office-map/block/office-data.js';

window.addEventListener( 'DOMContentLoaded', () => {
  const container = document.getElementById( 'kanagawa-office-list' );
  if ( ! container ) return;

  const data = getProcessedOfficeData();

  const table = document.createElement( 'table' );
  table.className = 'kanagawa-office-list-table';

  const thead = document.createElement( 'thead' );
  const headerRow = document.createElement( 'tr' );
  [ 'ID', 'Name', 'Address', 'Tel', 'Category', 'Region' ].forEach( text => {
    const th = document.createElement( 'th' );
    th.textContent = text;
    headerRow.appendChild( th );
  } );
  thead.appendChild( headerRow );
  table.appendChild( thead );

  const tbody = document.createElement( 'tbody' );
  data.forEach( d => {
    const row = document.createElement( 'tr' );
    row.id = \`office-\${d.id}\`;
    [ d.id2, d.office, d.address, d.tel, d.category, d.regionName ].forEach( val => {
      const td = document.createElement( 'td' );
      td.textContent = val;
      row.appendChild( td );
    } );
    tbody.appendChild( row );
  } );
  table.appendChild( tbody );

  container.appendChild( table );
} );
