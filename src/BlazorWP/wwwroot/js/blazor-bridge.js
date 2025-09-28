// wwwroot/js/blazor-bridge.js
window.BlazorBridge = {
    init(dotnetRef) {
        //console.log('BlazorBridge initialized');
        this._ref = dotnetRef;
    },
    restored(html) {

        //console.log('BlazorBridge.restored called, content length', html.length);
        this._ref?.invokeMethodAsync('OnDraftRestored', html);
    },
    report(dirty) {
        //console.log('BlazorBridge.report called, dirty =', dirty);
        if (this._ref) {
            this._ref.invokeMethodAsync('OnEditorDirtyChanged', dirty);
        }
    },
    // NEW: set/clear dirty from Blazor
    setDirty(editorId, dirty) {
        const ed = editorId ? tinymce.get(editorId) : tinymce.activeEditor;
        if (!ed) {
            console.warn('[BlazorDraftBridge] editor not found:', editorId);
            return;
        }

        if (typeof ed.setDirty === 'function') {
            ed.setDirty(!!dirty);      // TinyMCE 6+
        }
    },
    setReadOnly(editorId, ro) {
        const ed = editorId ? tinymce.get(editorId) : tinymce.activeEditor;
        if (!ed) { console.warn('[BlazorBridge] editor not found:', editorId); return; }
        const mode = ro ? 'readonly' : 'design';
        if (ed.mode && typeof ed.mode.set === 'function') ed.mode.set(mode);
        else if (typeof ed.setMode === 'function') ed.setMode(mode);
        this._isReadOnly = !!ro;
    },
    isReadOnly() { return !!this._isReadOnly; },
    // Notify Blazor that a save was triggered, passing the HTML
    onSave(html) {
        // Use the stored DotNetObjectReference from init()
        this._ref?.invokeMethodAsync('OnTinySave', html);
    },

    // Optional: notify Blazor that "Cancel" was used in the save plugin
    onCancel() {
        this._ref?.invokeMethodAsync('OnTinyCancel');
    },
    pickMedia(opts) {
        return new Promise((resolve) => {
            this._pendingPick = resolve;
            this._ref?.invokeMethodAsync('OpenMediaPicker', opts || {});
        });
    },
    finishPick(html) {
        if (this._pendingPick) {
            this._pendingPick(html || null);
            this._pendingPick = null;
        }
    }

};
