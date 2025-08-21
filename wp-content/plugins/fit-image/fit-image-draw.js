// === fit-image-draw.js (D3 v7.9.0) ===
import * as d3 from "https://esm.sh/d3@7.9.0";

/* utils */
const key = (x, y) => `${x},${y}`;
const sqrDist = (x1, y1, x2, y2) => { const dx = x1 - x2, dy = y1 - y2; return dx*dx + dy*dy; };
function ensureRoot(svg) {
  return svg.selectAll("g#overlay-root")
    .data([null])
    .join(enter => enter.append("g").attr("id", "overlay-root"));
}
function ensureHtmlTip() {
  return d3.select(document.body).selectAll("div.d3-tip")
    .data([0])
    .join("div")
    .attr("class", "d3-tip")
    .style("position", "fixed")
    .style("pointer-events", "none")
    .style("padding", "4px 6px")
    .style("background", "#111")
    .style("color", "#fff")
    .style("border-radius", "4px")
    .style("font", "12px/1.2 system-ui, -apple-system, Segoe UI, Roboto")
    .style("z-index", "9999")
    .style("opacity", 0);
}

/* marker shapes (centered at 0,0) */
function hexPath(r){ const pts=[]; for(let i=0;i<6;i++){const a=-Math.PI/2+i*(Math.PI/3); pts.push([r*Math.cos(a), r*Math.sin(a)]);} return `M${pts.map(p=>p.join(",")).join("L")}Z`; }
function squarePath(a){ const h=a/2; return `M${-h},${-h}L${h},${-h}L${h},${h}L${-h},${h}Z`; }
function triPath(a){ const h=Math.sqrt(3)/2*a; const v1=[0,-2*h/3], v2=[-a/2,h/3], v3=[a/2,h/3]; return `M${v1}L${v2}L${v3}Z`; }

/* lattice centers */
function centersHex(nW,nH,r,strokeW){ const w=Math.sqrt(3)*r, v=1.5*r, mX=(Math.sqrt(3)/2)*r+strokeW/2, mY=r+strokeW/2, out=[];
  for(let row=0,y=mY; y<=nH-mY; row++, y+=v){ const x0=mX+((row%2)?w/2:0); for(let x=x0; x<=nW-mX; x+=w) out.push([x,y]); } return out; }
function centersSquare(nW,nH,cell,strokeW){ const half=cell/2, m=half+strokeW/2, out=[];
  for(let y=m; y<=nH-m+1e-6; y+=cell) for(let x=m; x<=nW-m+1e-6; x+=cell) out.push([x,y]); return out; }
function centersBrick(nW,nH,cell,strokeW){ const half=cell/2, m=half+strokeW/2, out=[];
  for(let row=0,y=m; y<=nH-m+1e-6; row++, y+=cell){ const x0=m+((row%2)?half:0); for(let x=x0; x<=nW-m+1e-6; x+=cell) out.push([x,y]); } return out; }
function centersTri(nW,nH,a,strokeW){ const h=Math.sqrt(3)/2*a, R=a/Math.sqrt(3), m=R+strokeW/2, out=[];
  for(let row=0,y=m; y<=nH-m+1e-6; row++, y+=h){ const x0=m+a/2+((row%2)?a/2:0); for(let x=x0; x<=nW-m+1e-6; x+=a) out.push([x,y]); } return out; }

/**
 * Snap points to nearest *unused* lattice site and draw markers.
 * opts:
 *  - grid: 'hex'|'square'|'brick'|'tri' (default 'hex')
 *  - marker: 'auto'|'hex'|'square'|'triangle'|'circle'
 *  - unitPct, strokeW, fill, stroke
 *  - tooltip: 'none'|'native'|'html' (default 'native')
 *  - getLabel: (d,i) => string
 */
export function drawPointsToNearestGrid(svgEl, opts = {}) {
  if (!svgEl) return { snapped: [] };
  const {
    nW, nH,
    points = [],
    grid = "hex",
    unitPct = 0.05,
    strokeW = 3,
    fill = "rgba(83, 182, 221, 0.2)",
    stroke = "#fff",
    marker = "auto",
    tooltip = "native",
    getLabel = (d,i) => (d?.datum?.label ?? `#${i+1}`)
  } = opts;

  if (!(nW > 0 && nH > 0) || !points.length) return { snapped: [] };

  const svg  = d3.select(svgEl);
  const root = ensureRoot(svg);
  const unit = Math.min(nW, nH) * unitPct;

  let centers;
  if (grid === "square") centers = centersSquare(nW, nH, 2*unit, strokeW);
  else if (grid === "brick") centers = centersBrick(nW, nH, 2*unit, strokeW);
  else if (grid === "tri") centers = centersTri(nW, nH, 2*unit, strokeW);
  else centers = centersHex(nW, nH, unit, strokeW);

  // nearest-unused assignment
  const taken = new Set();
  const snapped = [];
  for (const p of points) {
    let best = null, bestD2 = Infinity;
    for (const [cx, cy] of centers) {
      const k = key(cx, cy);
      if (taken.has(k)) continue;
      const d2 = sqrDist(cx, cy, p.x, p.y);
      if (d2 < bestD2) { bestD2 = d2; best = { x: cx, y: cy, key: k }; }
    }
    if (best) {
      taken.add(best.key);
      snapped.push({ x: best.x, y: best.y, src: { x: p.x, y: p.y }, datum: p.datum });
    }
  }

  // marker type + sizes
  const markerType = (marker === "auto")
    ? (grid === "hex" ? "hex" : grid === "square" ? "square" : grid === "tri" ? "triangle" : "circle")
    : marker;
  const mark = { hexR: unit, squareA: 1.5*unit, triA: 1.8*unit, circR: 0.9*unit };

  // JOIN
  const nodes = root.selectAll("g.marker").data(snapped, d => key(d.x,d.y));
  const enter = nodes.enter().append("g")
    .attr("class","marker")
    .style("cursor","default")
    .on("pointerdown", e => e.stopPropagation());

  enter.each(function(){
    const g = d3.select(this);
    if (markerType === "hex") g.append("path").attr("d", hexPath(mark.hexR));
    else if (markerType === "square") g.append("path").attr("d", squarePath(mark.squareA));
    else if (markerType === "triangle") g.append("path").attr("d", triPath(mark.triA));
    else g.append("circle").attr("r", mark.circR);
  });

  enter.merge(nodes).select("path,circle")
    .attr("fill", fill)
    .attr("stroke", stroke)
    .attr("stroke-width", strokeW);

  enter.merge(nodes).attr("transform", d => `translate(${d.x},${d.y})`);

  // tooltips
  if (tooltip === "native") {
    // ensure <title> exists/updated; remove HTML handlers
    enter.append("title").text(getLabel);
    enter.merge(nodes).select("title").text(getLabel);
    enter.merge(nodes)
      .on("pointerenter.tooltip", null)
      .on("pointermove.tooltip",  null)
      .on("pointerleave.tooltip", null);
    d3.selectAll("div.d3-tip").style("opacity", 0);
  } else if (tooltip === "html") {
    const tip = ensureHtmlTip();
    // remove native titles to avoid double-tooltips
    enter.merge(nodes).select("title").remove();
    enter.merge(nodes)
      .on("pointerenter.tooltip", (e,d) => { tip.style("opacity",1).text(getLabel(d)); })
      .on("pointermove.tooltip",  (e)    => { tip.style("left",(e.clientX+12)+"px").style("top",(e.clientY+12)+"px"); })
      .on("pointerleave.tooltip", ()     => { tip.style("opacity",0); });
  } else {
    // none: remove titles and handlers
    enter.merge(nodes).select("title").remove();
    enter.merge(nodes)
      .on("pointerenter.tooltip", null)
      .on("pointermove.tooltip",  null)
      .on("pointerleave.tooltip", null);
    d3.selectAll("div.d3-tip").style("opacity", 0);
  }

  nodes.exit().remove();
  return { snapped };
}

export function clearOverlay(svgEl){
  if (!svgEl) return;
  const svg = d3.select(svgEl);
  svg.select("g#overlay-root")?.remove();
  d3.selectAll("div.d3-tip").style("opacity", 0);
}

