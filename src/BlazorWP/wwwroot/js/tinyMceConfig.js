// Classic script for TinyMCE.Blazor to read via JsConfSrc
window.myTinyMceConfig = {
  promotion: false,
  branding: false,
  statusbar: true,
  resize: true,
  plugins: 'code media table fullscreen save autosave autoresize',
  toolbar: 'save | undo redo | bold italic | table | code mediaLibraryButton customButton showInfoButton fullscreen',
  autosave_restore_when_empty: true,
  mediaSource: null,
  // TinyMCE "save" plugin callback (prevents default form submit)
  save_onsavecallback: function (editor) {
    if (window.BlazorBridge?.isReadOnly && window.BlazorBridge.isReadOnly()) {
      return;
    }
    const html = editor.getContent({ format: 'html' });
    // Tell Blazor
    window.BlazorBridge?.onSave(html);
    // Clear dirty after successful handoff (optional)
    if (typeof editor.setDirty === 'function') editor.setDirty(false);
  },

  // Optional: if user cancels (save_oncancelcallback is part of the save plugin)
  save_oncancelcallback: function (editor) {
    window.BlazorBridge?.onCancel?.();
  },

  setup: function (editor) {

    // report dirty status whenever content changes
    const fire = () => window.BlazorBridge.report(editor.isDirty());

    editor.on('init Change Input Undo Redo SetContent ExecCommand', () => {
      console.log('Content changed, dirty=', editor.isDirty());
      fire();
    });

    editor.on('RestoreDraft', () => {
      const html = editor.getContent({ format: 'html' });
      window.BlazorBridge.restored(html);
      console.log('Draft restored');
      fire();
    });

    // Helper to build a correct <img> from media_details.sizes
    function buildImageTag(item) {
      const sizes = item.media_details?.sizes || {};
      const valid = Object.values(sizes).filter(sz => sz.source_url && sz.width);
      const srcset = valid.map(sz => `${sz.source_url} ${sz.width}w`).join(', ');
      const lg = sizes.large;
      if (!lg) {
        return `<img src="${item.source_url}" />`;
      }
      return (`<img loading="lazy" decoding="async"` +
        ` src="${lg.source_url}"` +
        ` srcset="${srcset}"` +
        ` sizes="(max-width: ${lg.width}px) 100vw, ${lg.width}px"` +
        ` width="${lg.width}" height="${lg.height}"` +
        ` class="attachment-large size-large"` +
        (item.alt_text ? ` alt="${item.alt_text}"` : ``) +
        ` />`);
    }

    function openMediaDialog(items, totalPages) {
      let page = 1;

      function itemHtml(i) {
        // debug: inspect available fields
        // console.log('Media Item details:', {
        //   id:           i.id,
        //   source_url:   i.source_url,
        //   mime_type:    i.mime_type,
        //   media_type:   i.media_type,
        //   alt_text:     i.alt_text,
        //   title:        i.title?.rendered,
        //   description:  i.description?.rendered,
        //   sizes:        i.media_details?.sizes
        // });

        const thumb = (i.media_details && i.media_details.sizes && i.media_details.sizes.thumbnail)
          ? i.media_details.sizes.thumbnail.source_url
          : i.source_url;
        let desc;
        if (i.mime_type === 'application/pdf' || (i.media_type && i.media_type !== 'image')) {
          // PDF: wrap our correct <img> in a link
          const linkedImg = `<a href="${i.source_url}" target="_blank">${buildImageTag(i)}</a>`;
          desc = encodeURIComponent(linkedImg);
        } else {
          // Image: use existing description/rendered HTML
          desc = encodeURIComponent(
            i.description && i.description.rendered
              ? i.description.rendered
              : `<img src="${i.source_url}" />`
          );
        }
        return `<img src="${thumb}" data-desc="${desc}" style="width:100px;height:100px;object-fit:cover;margin:4px;cursor:pointer;" />`;
      }

      const images = items.map(itemHtml).join('');

      const html = `<div id="tiny-media-grid" style="display:flex;flex-wrap:wrap;">${images}</div>` +
        `<div style="margin-top:8px;text-align:center;"><button type="button" id="tiny-media-loadmore">Add More</button></div>`;

      const dlg = editor.windowManager.open({
        title: 'Media Library',
        size: 'large',
        body: {
          type: 'panel',
          items: [{ type: 'htmlpanel', html: html }]
        },
        buttons: []
      });

      const panel = document.getElementById('tiny-media-grid');
      panel.addEventListener('click', function (e) {
        if (e.target.tagName === 'IMG') {
          const html = decodeURIComponent(e.target.getAttribute('data-desc'));
          editor.insertContent(html);
          dlg.close();
        }
      });

      const loadMoreBtn = document.getElementById('tiny-media-loadmore');
      loadMoreBtn.addEventListener('click', async function () {
        page++;
        const result = await fetchMedia(page);
        result.items.forEach(i => {
          panel.insertAdjacentHTML('beforeend', itemHtml(i));
        });
        totalPages = result.totalPages;
        if (page >= totalPages || result.items.length === 0) {
          this.disabled = true;
        }
      });
    }

    function getMediaSource() {
      return window.myTinyMceConfig.mediaSource;
    }

    async function fetchMedia(page = 1) {
      const source = getMediaSource();
      if (!source) {
        alert('No media source selected');
        return { items: [], totalPages: page };
      }
      const apiPath = `/wp-json/wp/v2/media?per_page=100&page=${page}`;
      const url = source.replace(/\/?$/, '') + apiPath;
      try {
        const res = await apiFetch(url, { method: 'GET' });
        if (!res.ok) {
          alert('Failed to load media: ' + res.status);
          return { items: [], totalPages: page };
        }
        const data = await res.json();
        const totalPages = parseInt(res.headers.get('X-WP-TotalPages') || page);
        return { items: data, totalPages };
      } catch (err) {
        alert('Error loading media: ' + err);
        return { items: [], totalPages: page };
      }
    }

    editor.ui.registry.addButton('mediaLibraryButton', {
      text: 'Media',
      onAction: async function () {
        const bookmark = editor.selection.getBookmark(2, true);
        const html = await window.BlazorBridge.pickMedia({ multi: false });
        if (html) {
          editor.focus();
          editor.selection.moveToBookmark(bookmark);
          editor.insertContent(html);
        }
      }

    });
  }
};

window.setTinyMediaSource = function (url) {
  window.myTinyMceConfig.mediaSource = url || null;
};

// Auto-localize TinyMCE UI
(function () {
  // Your app stores lang as "jp" in query/localStorage; map that to Tiny's "ja"
  const pref = (new URLSearchParams(location.search).get('lang') || localStorage.getItem('lang') || 'en').toLowerCase();
  const isJa = pref === 'jp' || pref.startsWith('ja');

  if (isJa) {
    window.myTinyMceConfig.language = 'ja';
    // Use locally hosted community language pack
    window.myTinyMceConfig.language_url = '/libman/tinymce-i18n/langs/ja.js';
  } else {
    delete window.myTinyMceConfig.language;
    delete window.myTinyMceConfig.language_url;
  }
})();
