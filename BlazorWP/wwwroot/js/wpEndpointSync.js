let dotNetHelper = null;

function handleStorage(e) {
  if (e.key === 'wpEndpoint' && dotNetHelper) {
    dotNetHelper.invokeMethodAsync('UpdateEndpoint', e.newValue);
  }
}

export function register(dotnet) {
  dotNetHelper = dotnet;
  window.addEventListener('storage', handleStorage);
  dotnet.invokeMethodAsync('UpdateEndpoint', localStorage.getItem('wpEndpoint'));
}

export function unregister() {
  window.removeEventListener('storage', handleStorage);
  dotNetHelper = null;
}

export function set(value) {
  localStorage.setItem('wpEndpoint', value);
  if (dotNetHelper) {
    dotNetHelper.invokeMethodAsync('UpdateEndpoint', value);
  }
}
