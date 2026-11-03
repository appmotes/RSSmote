export function isApple() {
    return /Mac|iPhone|iPad|iPod/.test(globalThis.navigator.platform || globalThis.navigator.userAgent);
}

// localStorage throws instead of answering when the site may not store anything (a private window, blocked data).
export function getItem(key) {
    try { return globalThis.localStorage.getItem(key); } catch { return null; }
}

export function setItem(key, value) {
    try { globalThis.localStorage.setItem(key, value); return true; } catch { return false; }
}
