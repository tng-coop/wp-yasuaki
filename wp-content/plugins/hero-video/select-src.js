// Pure selection helpers. No DOM. No fetch.

/**
 * Pick best file dynamically from Pexels catalog files.
 * Prefers <= target_w, else smallest > target_w within maxOvershoot.
 * @param {Array} files catalog.video_files
 * @param {number} target_w pixels
 * @param {string[]} preferMimes e.g. ['video/mp4','video/webm']
 * @param {number} maxOvershoot e.g. 1.33
 * @returns {{link:string,width:number,height:number|null,file_type:string}|null}
 */
export function pickBestFile(files, target_w, preferMimes = ['video/mp4'], maxOvershoot = 1.33) {
  if (!Array.isArray(files) || files.length === 0) return null;

  const pref = preferMimes.map(s => String(s).toLowerCase());
  const norm = files
    .filter(f => f && f.link && Number.isFinite(+f.width))
    .map(f => ({
      link: f.link,
      width: +f.width,
      height: Number.isFinite(+f.height) ? +f.height : null,
      file_type: String(f.file_type || 'video/mp4').toLowerCase(),
    }));
  if (!norm.length) return null;

  const prefIndex = t => {
    const i = pref.indexOf(t);
    return i === -1 ? Number.MAX_SAFE_INTEGER : i;
  };

  // sort by mime pref then width asc
  norm.sort((a, b) => {
    const ap = prefIndex(a.file_type), bp = prefIndex(b.file_type);
    return ap !== bp ? ap - bp : a.width - b.width;
  });

  const unders = norm.filter(f => f.width <= target_w).sort((a, b) => b.width - a.width);
  if (unders.length) return unders[0];

  const overs = norm.filter(f => f.width > target_w).sort((a, b) => a.width - b.width);
  if (overs.length) {
    const bestOver = overs[0];
    if (bestOver.width <= Math.round(target_w * maxOvershoot)) return bestOver;
    // too large: narrowest overall
    norm.sort((a, b) => a.width - b.width);
    return norm[0];
  }
  return null;
}

/**
 * Convenience: choose from full catalog JSON using viewport.
 * @param {object} catalog result of /pexels-proxy/v1/video?id=…
 * @param {number} cssW window.innerWidth
 * @param {number} dpr window.devicePixelRatio
 * @param {string[]} preferMimes
 */
export function chooseFromCatalog(catalog, cssW, dpr, preferMimes = ['video/mp4']) {
  const target_w = Math.max(320, Math.min(3840, Math.round(cssW * Math.max(1, Math.round(dpr || 1)))));
  const best = pickBestFile(catalog?.video_files || [], target_w, preferMimes);
  return best ? { ...best, target_w } : null;
}
