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

// Function to initialize video switching
export default function initHeroVideoConfig() {
  const container = document.getElementById('hero-video');

  if (!container) {
    return;
  }

  // Flag to keep track of which video should be shown next
  let useGoat = true; // Start with Waterfall (useGoat = false means Waterfall)

  // Create video element
  const videoEl = document.createElement('video');
  videoEl.autoplay = true;
  videoEl.loop = false;  // No auto-looping, will switch videos manually
  videoEl.muted = true;
  videoEl.style.position = 'absolute';
  videoEl.style.top = '50%';
  videoEl.style.left = '50%';
  videoEl.style.transform = 'translate(-50%, -50%)';
  videoEl.style.width = '30vh';  // Full-screen or adjust to the size you need
  videoEl.style.height = '30vh'; // Full-screen or adjust to the size you need
  videoEl.style.objectFit = 'cover';
  videoEl.style.pointerEvents = 'none';
  videoEl.style.zIndex = '-1';

  // Function to start the video based on the flag
  async function playNextVideo() {
    // Choose the dataset based on the flag (useGoat = true -> Goat, useGoat = false -> Waterfall)
    const selectedData = useGoat ? goatData : waterfallData;
    
    // Pick the best video URL (from either goat or waterfall data)
    const videoUrl = pickBestVideoUrl(selectedData.video_files);

    // Preload and set the video source
    await preloadVideo(videoEl, videoUrl);

    // Reset the video element (important to make sure it plays from the start)
    videoEl.currentTime = 0;

    // Add the video element to the container (only once)
    if (!container.contains(videoEl)) {
      container.appendChild(videoEl);
    }

    // Play the video
    try {
      await videoEl.play();
    } catch (e) {
      console.warn('Error playing video:', e);
    }
  }

  // Handle video switching: when one video ends, play the next
  videoEl.addEventListener('ended', () => {
    // Switch between Goat and Waterfall videos
    useGoat = !useGoat;  // Toggle between Goat and Waterfall

    // Play the next video (either Goat or Waterfall)
    playNextVideo();
  });

  // Start by playing the first video (Waterfall)
  playNextVideo();
}

// Auto-initialize when the DOM is ready
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initHeroVideoConfig);
} else {
  initHeroVideoConfig();
}
