window.BlazorSave = {
  init(dotnetRef) {
    // TinyMCE will call this when Ctrl/Cmd+S is pressed (save plugin)
    window.__tinySaveToBlazor = () => dotnetRef.invokeMethodAsync('OnEditorSave');
  }
};
