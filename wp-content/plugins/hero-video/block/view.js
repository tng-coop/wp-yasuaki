// hero-video/block/view.js

const CACHE = 'hero-video-cache-v3';

// ---------- utils ----------
function clamp(n, min, max) { return Math.max(min, Math.min(max, n)); }

function targetWidth() {
  const dpr = Math.max(1, Math.round(window.devicePixelRatio || 1));
  const w   = clamp(window.innerWidth || 1280, 320, 3840);
  return { cssW: w, dpr, px: w * dpr };
}

// convert ".../pexels-proxy/v1/video" -> ".../pexels-proxy/v1"
// and map the legacy route to MU if needed
function apiRootFromVideo(apiBase) {
  if (/\/pexels-proxy\/v1\/video$/.test(apiBase)) return apiBase.replace(/\/video$/, '');
  if (/\/hero-video\/v1\/pexels$/.test(apiBase))  return apiBase.replace(/\/hero-video\/v1\/pexels$/, '/pexels-proxy/v1');
  return apiBase.replace(/\/video$/, '');
}

async function getBestSrc(apiBase, id) {
  const root = apiRootFromVideo(apiBase);
  const { cssW, dpr } = targetWidth();
  const url = `${root}/video-src?id=${encodeURIComponent(id)}&w=${cssW}&dpr=${dpr}&types=video/mp4&json=1`;
  const res = await fetch(url, { cache: 'no-store' });
  if (!res.ok) throw new Error(`Failed to get best src (${res.status})`);
  const data = await res.json();
  if (!data || !data.src) throw new Error('Proxy returned no src');
  return data.src;
}

async function preloadVideo(el, src) {
  const cache = await caches.open(CACHE);
  let response = await cache.match(src);
  if (!response) {
    response = await fetch(src);
    if (!res.ok) throw new Error(`Failed to fetch ${src}`);
    await cache.put(src, response.clone());
  }
  const blob = await response.blob();
  const url  = URL.createObjectURL(blob);
  if (el._blobUrl) URL.revokeObjectURL(el._blobUrl);
  el._blobUrl = url;
  el.src = url;
  el.load();
}

function debounce(fn, ms) {
  let t; return function(...args){ clearTimeout(t); t = setTimeout(() => fn.apply(this, args), ms); };
}

// ---------- per-instance player ----------
const inited = new WeakSet();

async function initHeroVideoInstance(container) {
  if (!container || inited.has(container)) return;
  inited.add(container);

  const goatId      = container.dataset.goatId;
  const waterfallId = container.dataset.waterfallId;
  const apiBase     = container.dataset.api;

  if (!apiBase || (!goatId && !waterfallId)) {
    console.warn('Hero Video: missing data-api or ids on container', container);
    return;
  }

  // per-instance state
  const state = {
    curr: 0,
    next: 1,
    switching: false,
    handler: null,
    videos: [document.createElement('video'), document.createElement('video')]
  };

  // set common <video> props; IMPORTANT: disable transition initially
  state.videos.forEach(v => {
    v.autoplay = true;
    v.muted = true;
    v.playsInline = true;
    v.preload = 'auto';
    v.style.opacity = '0';
    v.style.position = 'absolute';
    v.style.top = '50%';
    v.style.left = '50%';
    v.style.transform = 'translate(-50%, -50%)';
    v.style.minWidth = '100%';
    v.style.minHeight = '100%';
    v.style.objectFit = 'cover';
    v.style.pointerEvents = 'none';
    v.style.transition = 'none';            // ← ensure first reveal is NOT animated
    v.setAttribute('aria-hidden', 'true');
  });

  container.appendChild(state.videos[0]);
  container.appendChild(state.videos[1]);

  // instance-local helpers using state
  const setupSwitch = () => {
    const { videos } = state;
    if (state.handler) videos[state.curr].removeEventListener('timeupdate', state.handler);
    state.handler = async () => {
      const v = videos[state.curr];
      if (state.switching) return;
      if (v.duration && (v.duration - v.currentTime) <= 0.5) {
        v.removeEventListener('timeupdate', state.handler);
        await switchVideos();
      }
    };
    videos[state.curr].addEventListener('timeupdate', state.handler);
  };

  const switchVideos = async () => {
    if (state.switching) return;
    state.switching = true;
    const cV = state.videos[state.curr];
    const nV = state.videos[state.next];
    nV.currentTime = 0;
    try { await nV.play(); } catch (e) { console.warn('Error playing next video', e); state.switching = false; return; }
    nV.style.opacity = '1';
    cV.style.opacity = '0';
    cV.pause();
    [state.curr, state.next] = [state.next, state.curr];
    setupSwitch();
    state.switching = false;
  };

  // fetch optimized sources and preload
  const [srcA, srcB] = await Promise.all([
    getBestSrc(apiBase, waterfallId),
    getBestSrc(apiBase, goatId)
  ]);

  await Promise.all([
    preloadVideo(state.videos[0], srcA),
    preloadVideo(state.videos[1], srcB)
  ]);

  // show first instantly, then arm transitions (double rAF)
  state.videos[state.curr].style.opacity = '1';
  requestAnimationFrame(() => requestAnimationFrame(() => {
    state.videos.forEach(v => { v.style.transition = 'opacity 5s ease'; }); // re-enable animation AFTER first paint
  }));

  try { await state.videos[state.curr].play(); } catch (e) {
    console.warn('Playback error', e);
  }
  setupSwitch();

  // optional: re-pick on big resizes
  let lastBucket = Math.round((window.innerWidth || 0) / 320);
  const onResize = async () => {
    const bucket = Math.round((window.innerWidth || 0) / 320);
    if (bucket === lastBucket) return;
    lastBucket = bucket;
    try {
      const [nsA, nsB] = await Promise.all([
        getBestSrc(apiBase, waterfallId),
        getBestSrc(apiBase, goatId)
      ]);
      await Promise.all([
        preloadVideo(state.videos[state.next], nsA),
        preloadVideo(state.videos[state.curr], nsB)
      ]);
    } catch (e) {
      console.warn('Resize repick failed', e);
    }
  };
  window.addEventListener('resize', debounce(onResize, 400));
}

// ---------- public API / auto-boot ----------
export default async function initHeroVideoConfig(container) {
  if (container) {
    await initHeroVideoInstance(container).catch(console.error);
    return;
  }
  const nodes = document.querySelectorAll('.hero-video-container, #hero-video');
  if (!nodes.length) return;
  for (const el of nodes) {
    await initHeroVideoInstance(el).catch(console.error);
  }
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', () => { initHeroVideoConfig().catch(console.error); });
} else {
  initHeroVideoConfig().catch(console.error);
}
