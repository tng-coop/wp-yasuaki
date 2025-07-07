import goatData from './goat.js';  // Importing goat.js as an ES6 module
import waterfallData from './waterfall.js';  // Importing waterfall.js as an ES6 module

console.log('view.js loaded');

// Function to pick the best video URL based on the screen width
function pickBestVideoUrl(files) {
  const maxW = window.innerWidth * (window.devicePixelRatio || 1);
  const candidates = files
    .map(f => ({ link: f.link, width: f.width }))
    .filter(f => f.width <= maxW)
    .sort((a, b) => b.width - a.width);

  if (candidates.length) return candidates[0].link;

  // If none are suitable, pick the smallest resolution
  const smallest = files
    .map(f => ({ link: f.link, width: f.width }))
    .sort((a, b) => a.width - b.width);

  return smallest[0].link;
}

// Function to preload and set up the video
async function preloadVideo(el, src) {
  const CACHE = 'bg-video-cache-v1';
  const c = await caches.open(CACHE);
  let r = await c.match(src);
  if (!r) {
    r = await fetch(src);
    if (r.ok) await c.put(src, r.clone());
    else throw 'Failed to fetch ' + src;
  }

  const b = await r.blob();
  const u = URL.createObjectURL(b);
  el.src = u;
  el.load();
  return u;
}

// Function to render the video configuration
export default function initHeroVideoConfig() {
  const container = document.getElementById('hero-video');

  if (!container) {
    return;
  }

  // Merge both goat and waterfall video data
  const allVideos = [
    ...goatData.video_files,
    ...waterfallData.video_files,
  ];

  // Pick the best video URL (from either goat or waterfall data)
  const videoUrl = pickBestVideoUrl(allVideos);

  // Create a video element
  const videoEl = document.createElement('video');
  videoEl.autoplay = true;
  videoEl.loop = true;
  videoEl.muted = true;
  videoEl.style.position = 'absolute';
  videoEl.style.top = '50%';
  videoEl.style.left = '50%';
  videoEl.style.transform = 'translate(-50%, -50%)';
  // videoEl.style.minWidth = '100%';
  // videoEl.style.minHeight = '100%';
  videoEl.style.width='20vw';
  videoEl.style.width='20vh';
  videoEl.style.objectFit = 'cover';
  videoEl.style.pointerEvents = 'none';
  videoEl.style.zIndex = '-1';

  // Preload and set the video source
  preloadVideo(videoEl, videoUrl).then(() => {
    container.appendChild(videoEl);
  });

  // Pretty-print the JSON for the video (both goat and waterfall data)
  container.textContent = JSON.stringify({ goatData, waterfallData }, null, 2);
}

// Auto-initialize when the DOM is ready
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initHeroVideoConfig);
} else {
  initHeroVideoConfig();
}
