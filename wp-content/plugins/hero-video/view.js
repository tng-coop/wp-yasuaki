// Catalog-only client. Selects best file via separate ESM module.
import { chooseFromCatalog } from './select-src.js';

const STRICT_BLOB_MODE = true;
const CACHE = 'hero-video-cache-v3';

// --- catalog cache with TTL for /video responses ---
const META_CACHE = 'hero-video-catalog-v1';
const META_TTL_MS = 24 * 60 * 60 * 1000;

function clamp(n, min, max) { return Math.max(min, Math.min(max, n)); }
function viewport() {
  const dpr = Math.max(1, Math.round(window.devicePixelRatio || 1));
  const w   = clamp(window.innerWidth || 1280, 320, 3840);
  return { cssW: w, dpr };
}

function makeMetaResponse(json) {
  return new Response(JSON.stringify({ __ts: Date.now(), payload: json }), {
    headers: { 'Content-Type': 'application/json' }
  });
}
async function readMetaResponse(res) {
  const data = await res.clone().json().catch(() => null);
  return (data && data.__ts && 'payload' in data)
    ? { ts: data.__ts, payload: data.payload }
    : { ts: Date.now(), payload: data };
}

async function fetchCatalogWithTTL(url) {
  if (!('caches' in window)) {
    const r = await fetch(url, { cache: 'no-store' });
    if (!r.ok) throw new Error(`HTTP ${r.status}`);
    return r.json();
    }
  const cache = await caches.open(META_CACHE);
  const req = new Request(url, { method: 'GET' });
  const cached = await cache.match(req);
  if (cached) {
    const { ts, payload } = await readMetaResponse(cached);
    if ((Date.now() - ts) > META_TTL_MS) {
      fetch(url, { cache: 'no-store' })
        .then(r => r.ok ? r.json() : Promise.reject(r))
        .then(json => cache.put(req, makeMetaResponse(json)))
        .catch(()=>{});
    }
    return payload;
  }
  const res = await fetch(url, { cache: 'no-store' });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  const json = await res.json();
  try { await cache.put(req, makeMetaResponse(json)); } catch {}
  return json;
}

async function waitPlayable(video) {
  if ('requestVideoFrameCallback' in video) {
    await new Promise((resolve) => {
      let done = false;
      const finish = () => { if (!done) { done = true; resolve(); } };
      try { video.play().catch(()=>{}); } catch {}
      video.requestVideoFrameCallback(() => finish());
      setTimeout(finish, 1500);
    });
    return;
  }
  if (video.readyState < HTMLMediaElement.HAVE_FUTURE_DATA) {
    await new Promise((resolve) => {
      const h = () => { video.removeEventListener('canplay', h); resolve(); };
      video.addEventListener('canplay', h, { once: true });
      try { video.play().catch(()=>{}); } catch {}
      setTimeout(resolve, 1500);
    });
  }
}

async function fetchVideoBlob(url) {
  let resp;
  if ('caches' in window) {
    try {
      const cache = await caches.open(CACHE);
      resp = await cache.match(url);
      if (!resp) {
        resp = await fetch(url, { cache: 'no-store', mode: 'cors' });
        try { await cache.put(url, resp.clone()); } catch {}
      }
    } catch {
      resp = await fetch(url, { cache: 'no-store', mode: 'cors' });
    }
  } else {
    resp = await fetch(url, { cache: 'no-store', mode: 'cors' });
  }
  if (!resp.ok && resp.status !== 206) throw new Error(`Blob fetch failed: ${resp.status}`);
  return await resp.blob();
}

function revokeObjURL(el) {
  const prev = el?.dataset?.objUrl;
  if (prev) { try { URL.revokeObjectURL(prev); } catch {} el.dataset.objUrl = ''; }
}
async function setVideoFromBlob(videoEl, url) {
  const blob = await fetchVideoBlob(url);
  const obj = URL.createObjectURL(blob);
  revokeObjURL(videoEl);
  videoEl.src = obj;
  videoEl.dataset.objUrl = obj;
  await waitPlayable(videoEl);
}
async function setVideoFromURL(videoEl, url) {
  revokeObjURL(videoEl);
  if (videoEl.src !== url) videoEl.src = url;
  await waitPlayable(videoEl);
}

function debounce(fn, ms) { let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; }
const inited = new WeakSet();

async function initHeroVideoInstance(container) {
  if (!container || inited.has(container)) return;
  inited.add(container);

  const apiBase = container.dataset.api;               // e.g. /wp-json/pexels-proxy/v1/video
  let config = {};
  try { config = JSON.parse(container.dataset.config || '{}'); } catch {}
  const ids = Array.isArray(config.pexelVideos) ? config.pexelVideos.map(x=>+x).filter(Boolean) : [];
  const transitionSec = Number.isFinite(+config.transition) ? +config.transition : 3;
  if (!apiBase || ids.length === 0) return;

  const state = {
    curr: 0, next: 1, idx: 0, switching: false, handler: null,
    videos: [document.createElement('video'), document.createElement('video')]
  };

  state.videos.forEach(v => {
    v.autoplay = true; v.muted = true; v.playsInline = true; v.preload = 'auto';
    v.style.opacity = '0'; v.style.position = 'absolute';
    v.style.top = '50%'; v.style.left = '50%'; v.style.transform = 'translate(-50%, -50%)';
    v.style.minWidth = '100%'; v.style.minHeight = '100%'; v.style.objectFit = 'cover'; v.style.pointerEvents = 'none';
    v.style.transition = 'none'; v.style.willChange = 'opacity';
    v.setAttribute('aria-hidden', 'true');
    container.appendChild(v);
  });

  const nextIdIndex = (i) => (i + 1) % ids.length;

  async function loadCatalog(id) {
    const url = `${apiBase}?id=${encodeURIComponent(id)}`;
    return await fetchCatalogWithTTL(url);
  }

  async function getBestURL(id) {
    const { cssW, dpr } = viewport();
    const catalog = await loadCatalog(id);
    const best = chooseFromCatalog(catalog, cssW, dpr, ['video/mp4']);
    if (!best) throw new Error('no suitable file');
    return best.link;
  }

  async function preloadIntoHidden() {
    const upcomingIdx = nextIdIndex(state.idx);
    const src = await getBestURL(ids[upcomingIdx]);
    if (STRICT_BLOB_MODE) { await setVideoFromBlob(state.videos[state.next], src); }
    else { await setVideoFromURL(state.videos[state.next], src); }
  }

  function setupSwitch() {
    if (state.handler) state.videos[state.curr].removeEventListener('timeupdate', state.handler);
    state.handler = async () => {
      const v = state.videos[state.curr];
      if (state.switching) return;
      if (v.duration && (v.duration - v.currentTime) <= 0.5) {
        v.removeEventListener('timeupdate', state.handler);
        await switchVideos();
      }
    };
    state.videos[state.curr].addEventListener('timeupdate', state.handler);
  }

  async function switchVideos() {
    if (state.switching) return;
    state.switching = true;

    const cV = state.videos[state.curr];
    const nV = state.videos[state.next];

    cV.style.zIndex = '1';
    nV.style.zIndex = '2';

    nV.currentTime = 0;
    try { await nV.play(); } catch { state.switching=false; return; }
    await waitPlayable(nV);

    nV.style.opacity = '1';

    const finish = () => {
      nV.removeEventListener('transitionend', finish);
      cV.style.opacity = '0';
      cV.pause();
      if (STRICT_BLOB_MODE) revokeObjURL(cV);
      state.idx = (state.idx + 1) % ids.length;
      [state.curr, state.next] = [state.next, state.curr];
      setupSwitch();
      state.switching = false;
      preloadIntoHidden().catch(()=>{});
    };

    const dur = parseFloat(getComputedStyle(nV).transitionDuration) || 0;
    if (dur <= 0) finish(); else nV.addEventListener('transitionend', finish, { once: true });
  }

  // initial
  const firstSrc = await getBestURL(ids[state.idx]);
  if (STRICT_BLOB_MODE) { await setVideoFromBlob(state.videos[state.curr], firstSrc); }
  else { await setVideoFromURL(state.videos[state.curr], firstSrc); }
  state.videos[state.curr].style.opacity = '1';

  if (transitionSec > 0) {
    requestAnimationFrame(() => requestAnimationFrame(() => {
      state.videos.forEach(v => { v.style.transition = `opacity ${transitionSec}s ease`; });
    }));
  }

  try { await state.videos[state.curr].play(); } catch {}

  try { await preloadIntoHidden(); } catch {}
  setupSwitch();

  // resize bucketed
  let lastBucket = Math.round((window.innerWidth||0)/320);
  const onResize = async () => {
    const bucket = Math.round((window.innerWidth||0)/320);
    if (bucket===lastBucket) return;
    lastBucket = bucket;
    try { await preloadIntoHidden(); } catch {}
  };
  window.addEventListener('resize', debounce(onResize, 400));

  window.addEventListener('beforeunload', () => {
    if (!STRICT_BLOB_MODE) return;
    state.videos.forEach(revokeObjURL);
  }, { once: true });
}

export default async function initHeroVideoConfig(container) {
  if (container) { await initHeroVideoInstance(container).catch(console.error); return; }
  const nodes = document.querySelectorAll('.hero-video-container');
  for (const el of nodes) await initHeroVideoInstance(el).catch(console.error);
}
if (document.readyState==='loading') {
  document.addEventListener('DOMContentLoaded', ()=>{ initHeroVideoConfig().catch(console.error); });
} else {
  initHeroVideoConfig().catch(console.error);
}
