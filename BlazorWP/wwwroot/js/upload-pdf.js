import * as pdfjsLib from '../libman/pdfjs-dist/build/pdf.mjs';
// resolve the worker based on this module’s own URL
pdfjsLib.GlobalWorkerOptions.workerSrc =
  new URL('../libman/pdfjs-dist/build/pdf.worker.mjs', import.meta.url).href;

// resolve your CMap directory the same way
const cMapBaseUrl =
  new URL('../libman/pdfjs-dist/cmaps/', import.meta.url).href;
const cMapPacked = true;
const { getDocument } = pdfjsLib;
let lastDataUrl = null;

// Initialize image resizing behavior on component mount
export function initialize(imgId) {
  const outputImg = document.getElementById(imgId);
  window.addEventListener('resize', () => adjustPreview(outputImg));
  outputImg?.addEventListener('load', () => adjustPreview(outputImg));
}

// Render using a .NET stream reference for clean separation
export async function renderFirstPageFromStream(streamRef, canvasId, imgId) {
  // Acquire a ReadableStream from the .NET stream reference
  const jsStream = await streamRef.stream();
  // Convert the stream into an ArrayBuffer for PDF.js
  const arrayBuffer = await new Response(jsStream).arrayBuffer();

  const loadingTask = getDocument({ data: arrayBuffer, cMapUrl: cMapBaseUrl, cMapPacked });
  const pdf = await loadingTask.promise;
  const page = await pdf.getPage(1);
  const originalViewport = page.getViewport({ scale: 1 });
  const viewport = page.getViewport({ scale: 2 });

  const canvas = document.getElementById(canvasId);
  canvas.width = viewport.width;
  canvas.height = viewport.height;
  const ctx = canvas.getContext('2d');
  await page.render({ canvasContext: ctx, viewport }).promise;

  const outputImg = document.getElementById(imgId);
  lastDataUrl = canvas.toDataURL('image/png');
  outputImg.src = lastDataUrl;
  adjustPreview(outputImg);

  return {
    width: Math.floor(originalViewport.width),
    height: Math.floor(originalViewport.height),
    dataUrl: lastDataUrl
  };
}

export function getCurrentDataUrl() {
  return lastDataUrl;
}

// Return a resized preview data URL using the current rendered page
export async function getScaledPreview(width, height, cover) {
  if (!lastDataUrl) return null;
  const img = new Image();
  img.src = lastDataUrl;
  await img.decode();

  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const ctx = canvas.getContext('2d');

  if (cover) {
    const srcRatio = img.width / img.height;
    const dstRatio = width / height;
    let sx = 0, sy = 0, sw = img.width, sh = img.height;
    if (srcRatio > dstRatio) {
      sw = img.height * dstRatio;
      sx = (img.width - sw) / 2;
    } else {
      sh = img.width / dstRatio;
      sy = (img.height - sh) / 2;
    }
    ctx.drawImage(img, sx, sy, sw, sh, 0, 0, width, height);
  } else {
    const ratio = Math.min(width / img.width, height / img.height, 1);
    const dw = img.width * ratio;
    const dh = img.height * ratio;
    const dx = (width - dw) / 2;
    const dy = (height - dh) / 2;
    ctx.drawImage(img, 0, 0, img.width, img.height, dx, dy, dw, dh);
  }

  return canvas.toDataURL('image/jpeg');
}

function adjustPreview(outputImg) {
  if (!outputImg) return;
  const rect = outputImg.getBoundingClientRect();
  const top = rect.top + window.scrollY;
  const h = window.innerHeight - top - 20;
  outputImg.style.maxHeight = h + 'px';
}
