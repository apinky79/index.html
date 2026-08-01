const CACHE = "cushion-order-v8";
const ASSETS = [
  "./",
  "./index.html",
  "./print-blank.html",
  "./privacy.html",
  "./css/app.css",
  "./js/templates.js",
  "./js/storage.js",
  "./js/editor.js",
  "./js/panels.js",
  "./js/app.js",
  "./manifest.webmanifest",
  "../cushion-order-kit/form/dudgeon-purchase-order.pdf",
  "../cushion-order-kit/drawings/svg/chair/seat/t-cushion.svg",
  "../cushion-order-kit/drawings/svg/chair/back/t-back.svg",
];

self.addEventListener("install", (e) => {
  e.waitUntil(caches.open(CACHE).then((c) => c.addAll(ASSETS)).then(() => self.skipWaiting()));
});

self.addEventListener("activate", (e) => {
  e.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k)))
    ).then(() => self.clients.claim())
  );
});

self.addEventListener("fetch", (e) => {
  if (e.request.method !== "GET") return;
  e.respondWith(
    caches.match(e.request).then((cached) => cached || fetch(e.request))
  );
});
