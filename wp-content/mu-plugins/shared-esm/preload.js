// Shared preload utilities

const DEFAULT_CACHE = 'shared-media-cache-v1';

export async function preloadToBlobURL(url, { cacheName = DEFAULT_CACHE } = {}) {
  const cache = await caches.open(cacheName);
  let res = await cache.match(url);
  if (!res) {
    const net = await fetch(url, { cache: 'no-store' });
    if (!net.ok) throw new Error(`Fetch failed: ${net.status} for ${url}`);
    await cache.put(url, net.clone());
    res = net;
  }
  const blob = await res.blob();
  return URL.createObjectURL(blob);
}

export function swapVideoSrc(videoEl, blobUrl) {
  if (videoEl._blobUrl) URL.revokeObjectURL(videoEl._blobUrl);
  videoEl._blobUrl = blobUrl;
  videoEl.src = blobUrl;
  videoEl.load();
}

export async function preloadAndSwap(videoEl, src, { cacheName = DEFAULT_CACHE } = {}) {
  const blobUrl = await preloadToBlobURL(src, { cacheName });
  swapVideoSrc(videoEl, blobUrl);
  return blobUrl;
}
