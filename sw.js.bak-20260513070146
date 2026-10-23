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

async function networkFirst(request) {
    const cache = await caches.open(RUNTIME_CACHE_NAME);

    try {
        const response = await fetch(request);
        cache.put(request, response.clone());
        return response;
    } catch (error) {
        const cached = await cache.match(request);
        if (cached) {
            return cached;
        }

        if (request.mode === 'navigate' || request.destination === 'document') {
            return (await caches.open(STATIC_CACHE_NAME)).match('/index.html');
        }

        throw error;
    }
}

async function staleWhileRevalidate(request) {
    const cache = await caches.open(RUNTIME_CACHE_NAME);
    const cached = await cache.match(request);

    const networkPromise = fetch(request)
        .then((response) => {
            cache.put(request, response.clone());
            return response;
        })
        .catch(() => null);

    if (cached) {
        return cached;
    }

    const networkResponse = await networkPromise;
    if (networkResponse) {
        return networkResponse;
    }

    throw new Error(`Unable to resolve request: ${request.url}`);
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
                .filter((cacheName) => cacheName !== STATIC_CACHE_NAME && cacheName !== RUNTIME_CACHE_NAME)
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

    event.respondWith(staleWhileRevalidate(event.request));
});
