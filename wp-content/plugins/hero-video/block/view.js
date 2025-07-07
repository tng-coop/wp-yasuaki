// hero-video/block/view.js

import goatData from './goat.js';
import waterfallData from './waterfall.js';

const CACHE = 'hero-video-cache-v1';
let curr = 0,
    next = 1,
    switching = false,
    handler;

// Pick optimal video URL based on screen resolution
function pickBestVideoUrl(files) {
  const maxW = window.innerWidth * (window.devicePixelRatio || 1);
  const candidates = files
    .map(f => ({ link: f.link, width: f.width }))
    .filter(f => f.width <= maxW)
    .sort((a, b) => b.width - a.width);

  if (candidates.length) {
    return candidates[0].link;
  }
  // Fallback to the smallest available
  return files
    .map(f => ({ link: f.link, width: f.width }))
    .sort((a, b) => a.width - b.width)[0].link;
}

// Preload via Cache API & blob URL
async function preloadVideo(el, src) {
  const cache = await caches.open(CACHE);
  let response = await cache.match(src);

  if (!response) {
    response = await fetch(src);
    if (!response.ok) {
      throw new Error(`Failed to fetch ${src}`);
    }
    await cache.put(src, response.clone());
  }

  const blob = await response.blob();
  const url  = URL.createObjectURL(blob);
  if (el._blobUrl) {
    URL.revokeObjectURL(el._blobUrl);
  }
  el._blobUrl = url;
  el.src      = url;
  el.load();
}

// Set up timeupdate listener to trigger crossfade just before end
function setupSwitch(videos) {
  if (handler) {
    videos[curr].removeEventListener('timeupdate', handler);
  }

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
  try {
    await nV.play();
  } catch (e) {
    console.warn('Error playing next video', e);
    switching = false;
    return;
  }

  // Fade in/out
  nV.style.opacity = '1';
  cV.style.opacity = '0';
  cV.pause();

  [curr, next] = [next, curr];
  setupSwitch(videos);
  switching = false;
}

// Create a digital clock overlay
function createClock() {
  const clock = document.createElement('div');
  clock.className = 'hero-video-clock';
  function update() {
    clock.textContent = new Date().toLocaleTimeString();
  }
  update();
  setInterval(update, 1000);
  document.body.appendChild(clock);
}

export default async function initHeroVideoConfig() {
  const container = document.getElementById('hero-video');
  if (!container) return;

  // 1. Clock overlay
  createClock();

  // 2. Create two video elements for crossfade
  const videos = [
    document.createElement('video'),
    document.createElement('video')
  ];

  videos.forEach(v => {
    v.autoplay    = true;
    v.muted       = true;
    v.playsInline = true;
    v.preload     = 'auto';
    Object.assign(v.style, {
      position:      'absolute',
      top:           '50%',
      left:          '50%',
      transform:     'translate(-50%, -50%)',
      minWidth:      '100%',
      minHeight:     '100%',
      objectFit:     'cover',
      pointerEvents: 'none',
      zIndex:        '-1',
      opacity:       '0',    // hidden initially
      transition:    'none', // disable for initial reveal
    });
  });

  // 3. Insert both videos into the container
  container.appendChild(videos[0]);
  container.appendChild(videos[1]);

  // 4. Preload Goat & Waterfall videos
  await Promise.all([
    preloadVideo(videos[0], pickBestVideoUrl(waterfallData.video_files)),
    preloadVideo(videos[1], pickBestVideoUrl(goatData.video_files)),
  ]);

  // 5. Reveal first video instantly (no fade)
  videos[curr].style.opacity = '1';

  // 6. After two paint frames, enable the 5s transition for crossfades
  requestAnimationFrame(() => {
    requestAnimationFrame(() => {
      videos.forEach(v => {
        v.style.transition = 'opacity 5s ease';
      });
    });
  });

  // 7. Start playback & set up the looping crossfade
  try {
    await videos[curr].play();
  } catch (e) {
    console.warn('Playback error', e);
  }
  setupSwitch(videos);
}

// Auto-initialize on DOM ready
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', () => {
    initHeroVideoConfig().catch(console.error);
  });
} else {
  initHeroVideoConfig().catch(console.error);
}
