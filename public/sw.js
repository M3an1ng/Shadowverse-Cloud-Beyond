const STATIC_CACHE_NAME = 'svb-static-v1';
const RUNTIME_CACHE_NAME = 'svb-runtime-v1';
const APP_SHELL = [
    '/',
    '/index.html',
    '/manifest.webmanifest',
    '/favicon.jpg',
    '/pwa-icon-192.png',
    '/pwa-icon-512.png'
];

function shouldHandleRequest(request) {
    if (request.method !== 'GET') {
        return false;
    }

    const url = new URL(request.url);
    if (url.origin !== self.location.origin) {
        return false;
    }

    if (url.pathname.startsWith('/api/')
        || url.pathname.startsWith('/gamehub')
        || url.pathname.startsWith('/loghub')) {
        return false;
    }

    return request.mode === 'navigate'
        || request.destination === 'document'
        || request.destination === 'style'
        || request.destination === 'script'
        || request.destination === 'image'
        || request.destination === 'font';
}

async function readCachedResponse(cacheName, request) {
    try {
        const cached = await (await caches.open(cacheName)).match(request);
        // 历史缓存可能已被错误页污染，不把失败响应当作离线资源。
        if (cached && !cached.ok) throw new Error(`缓存包含失败响应：${cached.status}`);
        return cached;
    } catch (error) {
        console.error('PWA: 读取离线缓存失败。', error);
        return undefined;
    }
}

async function cacheNetworkResponse(request, response) {
    if (!response.ok) return;
    try {
        const cache = await caches.open(RUNTIME_CACHE_NAME);
        await cache.put(request, response.clone());
    } catch (error) {
        // 缓存写入不是网络读取的成功条件；保留真实响应，同时报告存储错误。
        console.error('PWA: 保存离线资源失败。', error);
    }
}

async function networkFirst(request) {
    let response;
    try {
        response = await fetch(request);
    } catch (error) {
        const cached = await readCachedResponse(RUNTIME_CACHE_NAME, request);
        if (cached) return cached;
        const shell = await readCachedResponse(STATIC_CACHE_NAME, '/index.html');
        if (shell) return shell;
        throw error;
    }
    await cacheNetworkResponse(request, response);
    return response;
}

async function staleWhileRevalidate(event) {
    const request = event.request;
    const networkPromise = fetch(request).then(async response => {
        await cacheNetworkResponse(request, response);
        return response;
    });
    // 返回缓存后，刷新仍属于本次请求的生命周期，不能依赖 worker 偶然存活。
    event.waitUntil(networkPromise.then(() => undefined, error => {
        console.warn('PWA: 离线资源的网络刷新失败。', error);
    }));
    const cached = await readCachedResponse(RUNTIME_CACHE_NAME, request);
    return cached || networkPromise;
}

self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(STATIC_CACHE_NAME).then((cache) => cache.addAll(APP_SHELL))
    );
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys().then((cacheNames) => Promise.all(
            cacheNames
                .filter((cacheName) => cacheName.startsWith('svb-')
                    && cacheName !== STATIC_CACHE_NAME && cacheName !== RUNTIME_CACHE_NAME)
                .map((cacheName) => caches.delete(cacheName))
        )).then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', (event) => {
    if (!shouldHandleRequest(event.request)) {
        return;
    }

    if (event.request.mode === 'navigate' || event.request.destination === 'document') {
        event.respondWith(networkFirst(event.request));
        return;
    }

    event.respondWith(staleWhileRevalidate(event));
});
