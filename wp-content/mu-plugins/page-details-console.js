(function(){
  if(window.bgVideoInit) return;
  window.bgVideoInit = true;

  // Initialize local lib
  if(typeof MyLib !== 'undefined') MyLib.init();

  // Log page details
  console.group("\ud83d\udcc4 Page Details");
  console.log("ID: " + pageDetailsData.pageId);
  console.log("Title: " + pageDetailsData.title);
  console.log("Slug: " + pageDetailsData.slug);
  console.log("URL: " + pageDetailsData.url);
  console.groupEnd();

  // Digital clock
  const clock = document.createElement('div');
  Object.assign(clock.style, {
    position:'fixed', left:'10px', bottom:'10px',
    background:'rgba(0,0,0,0.5)', color:'#fff',
    padding:'4px 8px', fontFamily:'monospace',
    borderRadius:'4px', zIndex:'9999'
  });
  function updateClock(){ clock.textContent = new Date().toLocaleTimeString(); }
  updateClock(); setInterval(updateClock,1000);
  document.addEventListener('DOMContentLoaded',()=>document.body.appendChild(clock));

  // Background videos: choose best variant <= screen width
  const rawData = pageDetailsData.videoData;
  document.addEventListener('DOMContentLoaded',async()=>{
    const CACHE = 'bg-video-cache-v1';
    const ids = Object.keys(rawData);
    const videos = [document.createElement('video'), document.createElement('video')];
    let curr=0, next=1, switching=false, handler=null;

// ── NEW: target your Cover block instead ──
const heroEl = document.querySelector('.js-hero-hook');
if ( ! heroEl ) return;               // bail if not present

// allow absolute children…
heroEl.style.position = 'relative';

// make sure nothing spills out
heroEl.style.overflow = 'hidden';
// size it however tall you want
heroEl.style.width  = '100%';
heroEl.style.height = '30vh';

videos.forEach(v=>{
  v.autoplay   = true;   // enable auto‐play
  v.loop       = true;   // loop seamlessly
  v.muted      = true;   // must be muted to autoplay in most browsers
  v.playsInline= true;   // required for mobile
  v.preload    = 'auto';
  Object.assign(v.style,{
    position:   'absolute',
    top:        '50%',
    left:       '50%',
    transform:  'translate(-50%, -50%)',
    minWidth:   '100%',
    minHeight:  '100%',
    objectFit:  'cover',
    pointerEvents:'none',
    zIndex:     '-1',
    opacity:    0,
    transition: 'opacity 0.5s ease'
  });
});

heroEl.prepend(videos[1], videos[0]);


    // pick URL: filter <= screen width, then highest width
    function pickUrl(files) {
      const maxW = window.innerWidth * (window.devicePixelRatio||1);
      const candidates = files
        .map(f => ({ link: f.link, type: f.file_type, width: f.width }))
        .filter(f => f.width <= maxW)
        .sort((a,b)=>b.width - a.width);
      // if none <= screen, pick smallest overall
      if(candidates.length) return candidates[0].link;
      const all = files.map(f=>({link:f.link,width:f.width}))
                        .sort((a,b)=>a.width-b.width);
      return all[0].link;
    }

    async function preload(el,src){
      const c = await caches.open(CACHE);
      let r = await c.match(src);
      if(!r){ r = await fetch(src); if(r.ok) await c.put(src,r.clone()); else throw 'Fetch '+src; }
      const b = await r.blob(), u=URL.createObjectURL(b);
      if(el._u) URL.revokeObjectURL(el._u);
      el._u=u; el.src=u; el.load();
    }

    function setup(v){
      if(handler) v.removeEventListener('timeupdate',handler);
      handler = async()=>{
        if(switching) return;
        if(v.duration - v.currentTime <= 0.5){
          v.removeEventListener('timeupdate',handler);
          await switchVideos();
        }
      };
      v.addEventListener('timeupdate',handler);
    }

    async function switchVideos(){
      if(switching) return; switching=true;
      const cV = videos[curr], nV = videos[next];
      if(handler) cV.removeEventListener('timeupdate',handler);
      nV.currentTime=0;
      try{ await nV.play(); }catch(e){console.warn(e); switching=false; return;}
      nV.style.opacity=1; cV.style.opacity=0; cV.pause();
      [curr,next]=[next,curr]; setup(videos[curr]); switching=false;
    }

    // preload chosen and start
    await Promise.all(ids.map((id,i)=>{
      const url = pickUrl(rawData[id]);
      return preload(videos[i], url);
    }));
    videos[curr].style.opacity=1;
    try{ await videos[curr].play(); }catch(e){ console.warn(e); await switchVideos(); }
    setup(videos[curr]);
  });
})();
