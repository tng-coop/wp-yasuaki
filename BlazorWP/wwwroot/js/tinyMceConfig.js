// Classic script for TinyMCE.Blazor to read via JsConfSrc
window.myTinyMceConfig = {
  promotion: false,
  branding: false,
  statusbar: true,
  resize: 'vertical',
  plugins: 'code media table fullscreen',
  toolbar: 'undo redo | bold italic | table | code mediaLibraryButton customButton showInfoButton fullscreen',
  mediaSource: null,
  setup: function (editor) {
    editor.ui.registry.addButton('customButton', {
      text: 'Alert',
      onAction: () => alert('Hello from TinyMCE!')
    });
    editor.ui.registry.addButton('showInfoButton', {
      text: 'Info',
      onAction: () => {
        const endpoint = localStorage.getItem('wpEndpoint') || '(none)';
        const token = localStorage.getItem('jwtToken') || '(none)';
        alert(`Endpoint: ${endpoint}\nJWT: ${token}`);
      }
    });

    function openMediaDialog(items, totalPages) {
      let page = 1;

      function itemHtml(i) {
        const thumb = (i.media_details && i.media_details.sizes && i.media_details.sizes.thumbnail)
          ? i.media_details.sizes.thumbnail.source_url
          : i.source_url;
        let desc;
        if (i.mime_type === 'application/pdf' || (i.media_type && i.media_type !== 'image')) {
          const text = (i.title && i.title.rendered) ? i.title.rendered : 'Download PDF';
          desc = encodeURIComponent(`<a href="${i.source_url}" target="_blank">${text}</a>`);
        } else {
          let src = i.source_url;
          try {
            const srcUrl = new URL(src);
            const mediaSource = getMediaSource();
            if (mediaSource) {
              const mediaOrigin = new URL(mediaSource).origin;
              if (srcUrl.origin === mediaOrigin) {
                src = srcUrl.pathname + srcUrl.search + srcUrl.hash;
              }
            }
          } catch (err) { }
          desc = encodeURIComponent(`<img src="${src}" />`);
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
      const token = localStorage.getItem('jwtToken');
      const url = source.replace(/\/?$/, '') + `/wp-json/wp/v2/media?per_page=100&page=${page}`;
      try {
        const res = await fetch(url, {
          headers: token ? { 'Authorization': 'Bearer ' + token } : {}
        });
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
        const result = await fetchMedia(1);
        openMediaDialog(result.items, result.totalPages);
      }
    });
  }
};

window.setTinyMediaSource = function (url) {
  window.myTinyMceConfig.mediaSource = url || null;
};

window.setTinyEditorContent = function (html) {
  if (window.tinymce && tinymce.get('articleEditor')) {
    tinymce.get('articleEditor').setContent(html || '');
  }
};

window.getTinyEditorContent = function () {
  if (window.tinymce && tinymce.get('articleEditor')) {
    return tinymce.get('articleEditor').getContent();
  }
  return '';
};

window.getTinyEditorContentLength = function () {
  if (window.tinymce && tinymce.get('articleEditor')) {
    return tinymce.get('articleEditor').getContent().length;
  }
  return 0;
};

window.registerTinyEditorCallbacks = function (dotNetHelper) {
  if (window.tinymce && tinymce.get('articleEditor')) {
    const editor = tinymce.get('articleEditor');
    editor.on('blur', function () {
      dotNetHelper.invokeMethodAsync('OnEditorBlur');
    });
    const changeHandler = function () {
      dotNetHelper.invokeMethodAsync('OnEditorDirty');
      editor.off('change', changeHandler);
    };
    editor.on('change', changeHandler);
  }
};
