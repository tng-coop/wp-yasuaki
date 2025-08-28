export function keys() {
  return Object.keys(window.localStorage);
}

export function itemInfo(key) {
  const value = window.localStorage.getItem(key);
  let ts = window.localStorage.getItem(key + '_timestamp');
  if (!ts && value) {
    try {
      const obj = JSON.parse(value);
      if (obj && typeof obj === 'object' && obj.lastUpdated) {
        ts = obj.lastUpdated;
      }
    } catch { }
  }
  return { value: value, lastUpdated: ts };
}

export function deleteItem(key) {
  window.localStorage.removeItem(key);
  window.localStorage.removeItem(key + '_timestamp');
}

