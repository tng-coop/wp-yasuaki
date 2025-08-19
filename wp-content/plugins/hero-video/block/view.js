// === view.js ===
import { preloadAndSwap } from 'shared/preload';

const CACHE = 'hero-video-cache-v3';

// --- NEW: metadata cache constants ---
const META_CACHE = 'hero-video-meta-v1';
const META_TTL_MS = 24 * 60 * 60 * 1000; // 1 day

// --- NEW: helpers for wrapping cache entries ---
function makeMetaResponse(json) {
  return new Response(JSON.stringify({ __ts: Date.now(), payload: json }), {
    headers: { 'Content-Type': 'application/json' }
  });
}
async function readMetaResponse(res) {
  const data = await res.clone().json().catch(() => null);
  if (data && typeof data === 'object' && data.__ts && 'payload' in data) {
    return { ts: data.__ts, payload: data.payload };
  }
  return { ts: Date.now(), payload: data };
}
function metaRequest(url) {
  return new Request(url, { method: 'GET' });
}

// --------- utils ----------
function clamp(n, min, max) { return Math.max(min, Math.min(max, n)); }
function targetWidth() {
  const dpr = Math.max(1, Math.round(window.devicePixelRatio || 1));
  const w   = clamp(window.innerWidth || 1280, 320, 3840);
  return { cssW: w, dpr, px: w * dpr };
}
function apiRootFromVideo(apiBase) {
  if (/\/pexels-proxy\/v1\/video$/.test(apiBase)) return apiBase.replace(/\/video$/, '');
  return apiBase.replace(/\/video$/, '');
}

// --------- UPDATED getBestSrc with metadata caching ----------
async function getBestSrc(apiBase, id) {
  const root = apiRootFromVideo(apiBase);
  const { cssW, dpr } = targetWidth();
  const url = `${root}/video-src?id=${encodeURIComponent(id)}&w=${cssW}&dpr=${dpr}&types=video/mp4&json=1`;

  let cache;
  try { cache = await caches.open(META_CACHE); } catch {
    // fallback if CacheStorage unavailable
    const res = await fetch(url, { cache: 'no-store' });
    if (!res.ok) throw new Error(`Failed to get best src (${res.status})`);
    const data = await res.json();
    if (!data?.src) throw new Error('Proxy returned no src');
    return data.src;
  }

  const req = metaRequest(url);
  const cached = await cache.match(req);

  if (cached) {
    const { ts, payload } = await readMetaResponse(cached);
    // background refresh if stale
    if ((Date.now() - ts) > META_TTL_MS) {
      fetch(url, { cache: 'no-store' })
        .then(r => r.ok ? r.json() : Promise.reject(r))
        .then(json => cache.put(req, makeMetaResponse(json)))
        .catch(()=>{});
    }
    if (!payload?.src) throw new Error('Proxy returned no src (cached)');
    return payload.src;
  }

  // cache miss
  const res = await fetch(url, { cache: 'no-store' });
  if (!res.ok) throw new Error(`Failed to get best src (${res.status})`);
  const data = await res.json();
  if (!data?.src) throw new Error('Proxy returned no src');
  try { await cache.put(req, makeMetaResponse(data)); } catch {}
  return data.src;
}

function debounce(fn, ms) { let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; }

// --------- per-instance player ----------
const inited = new WeakSet();

async function initHeroVideoInstance(container) {
  if (!container || inited.has(container)) return;
  inited.add(container);

  const apiBase = container.dataset.api;
  let config = {};
  try { config = JSON.parse(container.dataset.config || '{}'); } catch {}
  const ids = Array.isArray(config.pexelVideos) ? config.pexelVideos.map(x=>+x).filter(Boolean) : [];
  const transitionSec = Number.isFinite(+config.transition) ? +config.transition : 3;
  if (!apiBase || ids.length === 0) {
    console.warn('Hero Video: missing api or ids', { apiBase, ids });
    return;
  }

  const state = {
    curr: 0,
    next: 1,
    idx: 0,
    switching: false,
    handler: null,
    videos: [document.createElement('video'), document.createElement('video')]
  };

  // base video props
  state.videos.forEach(v => {
    v.autoplay = true; v.muted = true; v.playsInline = true; v.preload = 'auto';
    v.style.opacity = '0'; v.style.position = 'absolute';
    v.style.top = '50%'; v.style.left = '50%'; v.style.transform = 'translate(-50%, -50%)';
    v.style.minWidth = '100%'; v.style.minHeight = '100%'; v.style.objectFit = 'cover'; v.style.pointerEvents = 'none';
    v.style.transition = 'none'; // first paint: no transition
    v.setAttribute('aria-hidden', 'true');
    container.appendChild(v);
  });

  const nextIdIndex = (i) => (i + 1) % ids.length;

  const preloadIntoHidden = async () => {
    const upcomingIdx = nextIdIndex(state.idx);
    const src = await getBestSrc(apiBase, ids[upcomingIdx]);
    await preloadAndSwap(state.videos[state.next], src, { cacheName: CACHE });
  };

  const setupSwitch = () => {
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
  };

  const switchVideos = async () => {
    if (state.switching) return;
    state.switching = true;

    const cV = state.videos[state.curr];
    const nV = state.videos[state.next];
    nV.currentTime = 0;

    try { await nV.play(); } catch (e) { console.warn('play error', e); state.switching=false; return; }

    nV.style.opacity = '1';
    cV.style.opacity = '0';
    cV.pause();

    state.idx = nextIdIndex(state.idx);
    [state.curr, state.next] = [state.next, state.curr];
    setupSwitch();
    state.switching = false;
    try { await preloadIntoHidden(); } catch (e) { console.warn('preload fail', e); }
  };

  // ---- initial load ----
  const firstSrc = await getBestSrc(apiBase, ids[state.idx]);
  await preloadAndSwap(state.videos[state.curr], firstSrc, { cacheName: CACHE });
  state.videos[state.curr].style.opacity = '1';

  // AFTER first paint, apply transition if > 0
  if (transitionSec > 0) {
    requestAnimationFrame(() => requestAnimationFrame(() => {
      state.videos.forEach(v => { v.style.transition = `opacity ${transitionSec}s ease`; });
    }));
  }

  try { await state.videos[state.curr].play(); } catch (e) { console.warn('playback error', e); }

  try { await preloadIntoHidden(); } catch (e) {}
  setupSwitch();

  // resize repick (optional)
  let lastBucket = Math.round((window.innerWidth||0)/320);
  const onResize = async () => {
    const bucket = Math.round((window.innerWidth||0)/320);
    if (bucket===lastBucket) return;
    lastBucket = bucket;
    try { await preloadIntoHidden(); } catch {}
  };
  window.addEventListener('resize', debounce(onResize, 400));
}

// --------- auto boot ----------
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
