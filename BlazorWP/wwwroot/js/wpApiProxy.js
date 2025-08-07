window.fetchProxy = async function(path) {
    try {
        const res = await fetch('/api/wp-proxy?path=' + encodeURIComponent(path));
        const text = await res.text();
        return res.ok ? text : res.status + ' ' + res.statusText + ': ' + text;
    } catch (err) {
        return 'Error: ' + err;
    }
};
