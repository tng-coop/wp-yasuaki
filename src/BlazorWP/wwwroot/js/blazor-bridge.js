// wwwroot/js/blazor-bridge.js
window.BlazorBridge = {
    init(dotnetRef) {
        console.log('BlazorBridge initialized');
        this._ref = dotnetRef;
    },
    restored(html) {

        console.log('BlazorBridge.restored called, content length', html.length);
        this._ref?.invokeMethodAsync('OnDraftRestored', html);
    },
    report(dirty) {
        console.log('BlazorBridge.report called, dirty =', dirty);
        if (this._ref) {
            this._ref.invokeMethodAsync('OnEditorDirtyChanged', dirty);
        }
    }
};
