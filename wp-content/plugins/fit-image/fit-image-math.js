export function scalesForBox(nW, nH, W, H) {
  if (!nW || !nH || !W || !H) return { sContain: 1, sCover: 1 };
  const sContain = Math.min(W / nW, H / nH);
  const sCover   = Math.max(W / nW, H / nH);
  return { sContain, sCover };
}

export function scaleForT(t, nW, nH, W, H) {
  const { sContain, sCover } = scalesForBox(nW, nH, W, H);
  const ratio = (sCover / sContain) || 1;
  return sContain * Math.pow(ratio, Math.max(0, Math.min(1, t)));
}

export function tForScale(s, nW, nH, W, H) {
  const { sContain, sCover } = scalesForBox(nW, nH, W, H);
  const ratio = (sCover / sContain) || 1;
  if (ratio <= 0 || s <= 0) return 0;
  const sClamped = Math.max(sContain, Math.min(sCover, s));
  return Math.log(sClamped / sContain) / Math.log(ratio || 1.0000001);
}

export function metrics(t, nW, nH, getBoxRect) {
  const { width: W, height: H } = getBoxRect();
  const s  = scaleForT(t, nW, nH, W, H);
  const cw = nW * s;
  const ch = nH * s;
  return { W, H, s, cw, ch };
}
