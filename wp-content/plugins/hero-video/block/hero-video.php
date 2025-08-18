// hero-video/block/view.js

const CACHE = 'hero-video-cache-v1';
let curr = 0, next = 1, switching = false, handler;

// Fetch full Pexels video JSON via WP REST proxy
async function getVideoDef(apiBase, id) {
  const res = await fetch(`${apiBase}?id=${encodeURIComponent(id)}`);
  if (!res.ok) throw new Error(`Failed to fetch Pexels video ${id}`);
  return await res.json();
}

// Pick optimal video URL based on screen resolution
function pickBestVideoUrl(files) {
  const maxW = window.innerWidth * (window.devicePixelRatio || 1);
  const candidates = files
    .map(f => ({ link: f.link, width: f.width }))
    .filter(f => f.width <= maxW)
    .sort((a, b) => b.width - a.width);
  if (candidates.length) return candidates[0].link;
  return files.map(f => ({ link: f.link, width: f.width }))
              .sort((a, b) => a.width - b.width)[0].link;
}

// Preload via Cache API & blob URL
async function preloadVideo(el, src) {
  const cache = await caches.open(CACHE);
  let response = await cache.match(src);
  if (!response) {
    response = await fetch(src);
    if (!response.ok) throw new Error(`Failed to fetch ${src}`);
    await cache.put(src, response.clone());
  }
  const blob = await response.blob();
  const url  = URL.createObjectURL(blob);
  if (el._blobUrl) URL.revokeObjectURL(el._blobUrl);
  el._blobUrl = url;
  el.src = url;
  el.load();
}

// Set up timeupdate listener to trigger crossfade just before end
function setupSwitch(videos) {
  if (handler) videos[curr].removeEventListener('timeupdate', handler);
  handler = async () => {
    const v = videos[curr];
    if (switching) return;
    if (v.duration - v.currentTime <= 0.5) {
      v.removeEventListener('timeupdate', handler);
      await switchVideos(videos);
    }
  };
  videos[curr].addEventListener('timeupdate', handler);
}

// Crossfade to the next video
async function switchVideos(videos) {
  if (switching) return;
  switching = true;
  const cV = videos[curr];
  const nV = videos[next];
  nV.currentTime = 0;
  try { await nV.play(); } catch (e) { console.warn('Error playing next video', e); switching = false; return; }
  nV.style.opacity = '1';
  cV.style.opacity = '0';
  cV.pause();
  [curr, next] = [next, curr];
  setupSwitch(videos);
  switching = false;
}

export default async function initHeroVideoConfig() {
  const container = document.getElementById('hero-video');
  if (!container) return;

  const goatId = container.dataset.goatId;
  const waterfallId = container.dataset.waterfallId;
  const apiBase = container.dataset.api;

  const videos = [document.createElement('video'), document.createElement('video')];
  videos.forEach(v => { v.autoplay = true; v.muted = true; v.playsInline = true; v.preload = 'auto'; });
  container.appendChild(videos[0]);
  container.appendChild(videos[1]);

  // Fetch definitions from server, then preload
  const [waterfallDef, goatDef] = await Promise.all([
    getVideoDef(apiBase, waterfallId),
    getVideoDef(apiBase, goatId)
  ]);

  await Promise.all([
    preloadVideo(videos[0], pickBestVideoUrl(waterfallDef.video_files)),
    preloadVideo(videos[1], pickBestVideoUrl(goatDef.video_files))
  ]);

  videos[curr].style.opacity = '1';
  requestAnimationFrame(() => requestAnimationFrame(() => {
    videos.forEach(v => { v.style.transition = 'opacity 5s ease'; });
  }));

  try { await videos[curr].play(); } catch (e) { console.warn('Playback error', e); }
  setupSwitch(videos);
}

// Auto-init
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', () => { initHeroVideoConfig().catch(console.error); });
} else {
  initHeroVideoConfig().catch(console.error);
}
