// panzoom-engine.js
import { metrics, tForScale } from "./fit-image-math.js";

export class PanZoomEngine {
  constructor({
    getBoxRect, getNaturalSize, getState, setState, applyLayout,
    wheelStep = 0.001, dblTapMs = 280, dblTapSlop = 24
  }) {
    this.getBoxRect = getBoxRect;
    this.getNaturalSize = getNaturalSize;
    this.getState = getState;
    this.setState = setState;
    this.applyLayout = applyLayout;

    this.WHEEL_STEP = wheelStep;
    this.DOUBLE_TAP_MS = dblTapMs;
    this.DOUBLE_TAP_SLOP = dblTapSlop;

    this.pointers = new Map();
    this.pinching = false;
    this.pinchStartDist = 0;
    this.pinchAnchorX = 0;
    this.pinchAnchorY = 0;

    this.dragging = false;
    this.startPointerX = 0;
    this.startPointerY = 0;
    this.startPosX = 50;
    this.startPosY = 50;

    this.lastTapTime = 0;
    this.lastTapX = 0;
    this.lastTapY = 0;
  }

  // utility
  _trackPointer(e) {
    const rect = this.getBoxRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;
    this.pointers.set(e.pointerId, { x, y });
    return { x, y };
  }
  _untrackPointer(e) { this.pointers.delete(e.pointerId); }
  _distanceAndMidpoint() {
    const it = Array.from(this.pointers.values());
    if (it.length < 2) return null;
    const [a, b] = it;
    const dx = b.x - a.x, dy = b.y - a.y;
    return { dist: Math.hypot(dx, dy), mx: (a.x + b.x)/2, my: (a.y + b.y)/2 };
  }

  _setZoomWithAnchor(tNext, cx = null, cy = null) {
    const { nW, nH } = this.getNaturalSize();
    if (!nW || !nH) return;

    const { t, posX, posY } = this.getState();
    const { width: W, height: H } = this.getBoxRect();
    const { s: s0, cw: cw0, ch: ch0 } = metrics(t, nW, nH, this.getBoxRect);

    const _cx = cx ?? W / 2, _cy = cy ?? H / 2;
    const x0 = (W - cw0) * (posX / 100);
    const y0 = (H - ch0) * (posY / 100);
    const imgX = s0 ? (_cx - x0) / s0 : 0;
    const imgY = s0 ? (_cy - y0) / s0 : 0;

    const { s: s1, cw: cw1, ch: ch1 } = metrics(tNext, nW, nH, this.getBoxRect);
    const x1 = _cx - imgX * s1;
    const y1 = _cy - imgY * s1;

    const nextPosX = (W !== cw1) ? (x1 * 100) / (W - cw1) : 50;
    const nextPosY = (H !== ch1) ? (y1 * 100) / (H - ch1) : 50;

    this.setState({
      t: Math.max(0, Math.min(1, tNext)),
      posX: Math.max(0, Math.min(100, nextPosX)),
      posY: Math.max(0, Math.min(100, nextPosY)),
      hasInteracted: true
    });
    this.applyLayout();
  }

  toggleMode() {
    const { mode } = this.getState();
    const target = (mode === "cover") ? 0 : 1;
    this._setZoomWithAnchor(target);
  }

  onWheel = (e) => {
    const { nW, nH } = this.getNaturalSize();
    if (!nW || !nH) return;
    e.preventDefault();

    const rect = this.getBoxRect();
    const cx = e.clientX - rect.left, cy = e.clientY - rect.top;

    const { t } = this.getState();
    const speed = this.WHEEL_STEP * (e.ctrlKey ? 2 : 1);
    const dt = -e.deltaY * speed;
    const tNext = Math.max(0, Math.min(1, t + dt));
    if (tNext === t) return;

    this._setZoomWithAnchor(tNext, cx, cy);
  };

  onPointerDown = (e) => {
    // double-tap
    const now = performance.now();
    const rect = this.getBoxRect();
    const tapX = e.clientX - rect.left, tapY = e.clientY - rect.top;
    if (now - this.lastTapTime < this.DOUBLE_TAP_MS &&
        Math.hypot(tapX - this.lastTapX, tapY - this.lastTapY) < this.DOUBLE_TAP_SLOP) {
      e.preventDefault();
      this.toggleMode();
      this.lastTapTime = 0;
      return;
    }
    this.lastTapTime = now; this.lastTapX = tapX; this.lastTapY = tapY;

    // tracking
    this._trackPointer(e);

    // pinch start?
    if (this.pointers.size === 2) {
      const dmp = this._distanceAndMidpoint();
      this.pinching = true;
      this.pinchStartDist = dmp?.dist || 1;
      this.pinchAnchorX = dmp?.mx || 0;
      this.pinchAnchorY = dmp?.my || 0;
      this.dragging = false;
      this.setState({ hasInteracted: true });
      e.preventDefault();
      return;
    }

    // pan start only if we have slack
    const { nW, nH } = this.getNaturalSize();
    const { t, posX, posY } = this.getState();
    const { W, H, cw, ch } = metrics(t, nW, nH, this.getBoxRect);
    if (Math.abs(W - cw) <= 0.5 && Math.abs(H - ch) <= 0.5) return;

    this.dragging = true;
    this.startPointerX = e.clientX;
    this.startPointerY = e.clientY;
    this.startPosX = posX;
    this.startPosY = posY;
    this.setState({ hasInteracted: true });
    e.target.setPointerCapture?.(e.pointerId);
    e.preventDefault();
  };

  onPointerMove = (e) => {
    this._trackPointer(e);

    // pinch-to-zoom
    if (this.pinching && this.pointers.size >= 2) {
      const dmp = this._distanceAndMidpoint();
      if (!dmp) return;
      const { dist, mx, my } = dmp;

      const { nW, nH } = this.getNaturalSize();
      const { t } = this.getState();
      const { W, H, s: s0 } = metrics(t, nW, nH, this.getBoxRect);

      const scaleFactor = (dist || 1) / (this.pinchStartDist || 1);
      const s1 = s0 * scaleFactor;
      // convert scale → t
      const tNext = tForScale(s1, nW, nH, W, H);
      this._setZoomWithAnchor(tNext, mx, my);
      e.preventDefault();
      return;
    }

    // pan
    if (!this.dragging) return;
    const { nW, nH } = this.getNaturalSize();
    const { t } = this.getState();
    const { W, H, cw, ch } = metrics(t, nW, nH, this.getBoxRect);
    const dx = e.clientX - this.startPointerX;
    const dy = e.clientY - this.startPointerY;

    const denomX = (W - cw);
    const denomY = (H - ch);
    const nextX = denomX !== 0 ? this.startPosX + (dx * 100) / denomX : 50;
    const nextY = denomY !== 0 ? this.startPosY + (dy * 100) / denomY : 50;

    this.setState({
      posX: Math.max(0, Math.min(100, nextX)),
      posY: Math.max(0, Math.min(100, nextY))
    });
    this.applyLayout();
    e.preventDefault();
  };

  onPointerUp = (e) => {
    this._untrackPointer(e);
    if (this.pinching && this.pointers.size < 2) this.pinching = false;
    if (!this.dragging) return;
    this.dragging = false;
    e.target.releasePointerCapture?.(e.pointerId);
  };
}
