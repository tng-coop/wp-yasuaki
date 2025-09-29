window.myTinyMceConfig = {
  promotion: false,
  branding: false,
  statusbar: true,
  resize: true,

  // Broad, standard feature set
  plugins: [
    'autosave save autoresize',
    'lists advlist link autolink',
    'table',
    'image imagetools media',
    'code codesample',
    'searchreplace preview',
    'charmap emoticons hr',
    'anchor',
    'wordcount',
    'visualblocks visualchars',
    'fullscreen',
    // Optional/premium if available:
    // 'a11ychecker linkchecker powerpaste spellchecker'
  ].join(' '),

  menubar: 'file edit view insert format tools table help',
  toolbar_mode: 'sliding',
  toolbar: [
    'save | undo redo | blocks | bold italic underline strikethrough removeformat',
    '| bullist numlist outdent indent | alignleft aligncenter alignright alignjustify',
    '| link anchor | table | image media | hr blockquote codesample',
    '| searchreplace preview code fullscreen | emoticons charmap | showInfoButton mediaLibraryButton'
  ].join(' '),

  // Blocks & styles
  block_formats: 'Paragraph=p; Heading 2=h2; Heading 3=h3; Heading 4=h4; Preformatted=pre',
  style_formats_merge: true,
  content_style: `
    :root { --font: system-ui, -apple-system, Segoe UI, Roboto, sans-serif; }
    body { font-family: var(--font); line-height: 1.6; }
    h2,h3,h4 { font-weight: 700; }
    img { max-width: 100%; height: auto; }
    figure image { margin: 0; }
  `,

  // Links & images
  link_context_toolbar: true,
  link_target_list: [{ title: 'Same window', value: '' }, { title: 'New window', value: '_blank' }],
  rel_list: [{ title: 'None', value: '' }, { title: 'NoReferrer', value: 'noreferrer' }, { title: 'NoOpener', value: 'noopener' }],
  default_link_target: '_blank',
  image_caption: true,
  image_advtab: true,
  imagetools_cors_hosts: ['*'], // tighten in prod

  // Paste & autosave (tune to taste)
  paste_as_text: false,
  paste_data_images: true,
  // If you have PowerPaste, configure allowed styles/cleanup here
  autosave_interval: '20s',
  autosave_retention: '30m',
  autosave_restore_when_empty: true,

  // Context & quickbars
  contextmenu: 'link table image inserttable | cell row column deletetable',

  // Save plugin callbacks
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

    // Your reporter
    const fire = () => window.BlazorBridge.report(editor.isDirty());

    // Debounced wrapper (calls fire() after 200ms of silence)
    const debouncedFire = (() => {
      let t = null;
      function call() {
        if (t) clearTimeout(t);
        t = setTimeout(fire, 200); // tweak delay as needed
      }
      call.flush = () => { if (t) { clearTimeout(t); t = null; fire(); } };
      call.cancel = () => { if (t) { clearTimeout(t); t = null; } };
      return call;
    })();

    editor.on('Input', () => {
      console.log('Content changed, dirty=', editor.isDirty());
      debouncedFire();
    });

    // Make sure the last change isn't lost when leaving the editor
    editor.on('Blur Remove', () => debouncedFire.flush());

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

    function getMediaSource() {
      return window.myTinyMceConfig.mediaSource;
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
