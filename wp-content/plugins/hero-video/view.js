// === view.js (buildless ESM, blob mode restored, no <link rel="preload">) ===

// Toggle between blob mode and URL mode
const STRICT_BLOB_MODE = true;

// --- optional CacheStorage for blobs ---
const CACHE = 'hero-video-cache-v3';

// --- metadata cache ---
const META_CACHE = 'hero-video-meta-v1';
const META_TTL_MS = 24 * 60 * 60 * 1000; // 1 day

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

// ---- utils ----
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

async function getBestSrc(apiBase, id) {
  const root = apiRootFromVideo(apiBase);
  const { cssW, dpr } = targetWidth();
  const url = `${root}/video-src?id=${encodeURIComponent(id)}&w=${cssW}&dpr=${dpr}&types=video/mp4&json=1`;

  let cache;
  try { cache = await caches.open(META_CACHE); } catch {
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
    if ((Date.now() - ts) > META_TTL_MS) {
      fetch(url, { cache: 'no-store' })
        .then(r => r.ok ? r.json() : Promise.reject(r))
        .then(json => cache.put(req, makeMetaResponse(json)))
        .catch(()=>{});
    }
    if (!payload?.src) throw new Error('Proxy returned no src (cached)');
    return payload.src;
  }

  const res = await fetch(url, { cache: 'no-store' });
  if (!res.ok) throw new Error(`Failed to get best src (${res.status})`);
  const data = await res.json();
  if (!data?.src) throw new Error('Proxy returned no src');
  try { await cache.put(req, makeMetaResponse(data)); } catch {}
  return data.src;
}

// ---- blob helpers ----
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

  if (!resp.ok && resp.status !== 206) {
    throw new Error(`Blob fetch failed: ${resp.status}`);
  }
  return await resp.blob();
}

function revokeObjURL(el) {
  const prev = el?.dataset?.objUrl;
  if (prev) {
    try { URL.revokeObjectURL(prev); } catch {}
    el.dataset.objUrl = '';
  }
}

async function setVideoFromBlob(videoEl, url) {
  const blob = await fetchVideoBlob(url);
  const obj = URL.createObjectURL(blob);
  revokeObjURL(videoEl);
  videoEl.src = obj;
  videoEl.dataset.objUrl = obj;

  if (videoEl.readyState < 1) {
    await new Promise((resolve) => {
      const onLoaded = () => { videoEl.removeEventListener('loadedmetadata', onLoaded); resolve(); };
      videoEl.addEventListener('loadedmetadata', onLoaded, { once: true });
      setTimeout(resolve, 1200);
    });
  }
}

async function setVideoFromURL(videoEl, url) {
  revokeObjURL(videoEl);
  if (videoEl.src !== url) videoEl.src = url;
}

// ---- misc ----
function debounce(fn, ms) { let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; }

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

  state.videos.forEach(v => {
    v.autoplay = true; v.muted = true; v.playsInline = true; v.preload = 'auto';
    v.style.opacity = '0'; v.style.position = 'absolute';
    v.style.top = '50%'; v.style.left = '50%'; v.style.transform = 'translate(-50%, -50%)';
    v.style.minWidth = '100%'; v.style.minHeight = '100%'; v.style.objectFit = 'cover'; v.style.pointerEvents = 'none';
    v.style.transition = 'none';
    v.setAttribute('aria-hidden', 'true');
    container.appendChild(v);
  });

  const nextIdIndex = (i) => (i + 1) % ids.length;

  async function preloadIntoHidden() {
    const upcomingIdx = nextIdIndex(state.idx);
    const src = await getBestSrc(apiBase, ids[upcomingIdx]);
    if (STRICT_BLOB_MODE) {
      await setVideoFromBlob(state.videos[state.next], src);
    } else {
      await setVideoFromURL(state.videos[state.next], src);
    }
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
    nV.currentTime = 0;

    try { await nV.play(); } catch (e) { console.warn('play error', e); state.switching=false; return; }

    nV.style.opacity = '1';
    cV.style.opacity = '0';
    cV.pause();

    if (STRICT_BLOB_MODE) revokeObjURL(cV);

    state.idx = nextIdIndex(state.idx);
    [state.curr, state.next] = [state.next, state.curr];
    setupSwitch();
    state.switching = false;

    try { await preloadIntoHidden(); } catch (e) { console.warn('preload fail', e); }
  }

  const firstSrc = await getBestSrc(apiBase, ids[state.idx]);
  if (STRICT_BLOB_MODE) {
    await setVideoFromBlob(state.videos[state.curr], firstSrc);
  } else {
    await setVideoFromURL(state.videos[state.curr], firstSrc);
  }
  state.videos[state.curr].style.opacity = '1';

  if (transitionSec > 0) {
    requestAnimationFrame(() => requestAnimationFrame(() => {
      state.videos.forEach(v => { v.style.transition = `opacity ${transitionSec}s ease`; });
    }));
  }

  try { await state.videos[state.curr].play(); } catch (e) { console.warn('playback error', e); }

  try { await preloadIntoHidden(); } catch (e) {}
  setupSwitch();

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
