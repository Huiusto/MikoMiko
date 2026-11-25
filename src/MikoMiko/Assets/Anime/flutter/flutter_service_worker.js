'use strict';
const MANIFEST = 'flutter-app-manifest';
const TEMP = 'flutter-temp-cache';
const CACHE_NAME = 'flutter-app-cache';

const RESOURCES = {"assets/AssetManifest.bin": "1d04f63610f6f3800d3e476acc968367",
"assets/AssetManifest.bin.json": "62132359f4dfdc7388009c0ddd4621bf",
"assets/AssetManifest.json": "99b46dbdf78bc11c0095a500360a5915",
"assets/assets/covers/c01.jpg": "8d006d5e384198a3f1a3e572ede9c7e2",
"assets/assets/covers/c02.jpg": "0cead8eb2928d72a6d9058447afe1cde",
"assets/assets/covers/c03.png": "da1884214c2f00b0922acaee90b5dd0b",
"assets/assets/covers/c04.jpg": "90f2167b037747985a5cb87f4e589330",
"assets/assets/covers/c05.png": "d8b2d5078b01457109a72a10910f9bd4",
"assets/assets/covers/c06.jpg": "f6a46f35a707c73374bb97185532a338",
"assets/assets/covers/c07.jpg": "901bb29aad298d8233138156c4195098",
"assets/assets/covers/c08.jpg": "65ef4637121b2cead048ee65a51ecd1a",
"assets/assets/covers/c09.png": "8ad0ae88275bc9fa72b0128d3b966cd2",
"assets/assets/covers/c10.png": "ddb801d691af73c67d1f9cce74439938",
"assets/assets/covers/c11.jpg": "c57cae0b04fa876d423abaf8ed81cb95",
"assets/assets/covers/c12.jpg": "57274f8e8ab484667b8237f7a978e215",
"assets/assets/covers/c13.jpg": "e12c233697b6f9089ab2cf955272265c",
"assets/assets/covers/c14.jpg": "b6c8181e11574f3aaa68c1148f7bd61c",
"assets/assets/covers/c15.jpg": "2edf3b25a7c15aae7c2f4c5e8ab2fe98",
"assets/assets/covers/c16.jpg": "e93cebf90062a5e60ebdc0727e86702c",
"assets/assets/covers/c17.jpg": "9165583b920fb7faa3baa4428f05bb64",
"assets/assets/fonts/NotoSansSC.ttf": "504abdda545478632820c606a577b4a3",
"assets/assets/images/home_banner.png": "d31172cf8226e01e7889904d2f43e2fb",
"assets/assets/rules/manmankan.json": "1490dd6815845556f7c7a8788a5d79cf",
"assets/assets/rules/mhys.json": "20a34495e5f1da7b3fcb844342ff4629",
"assets/assets/rules/milace.json": "b7de52765ba5360008e234da74affa62",
"assets/assets/rules/silidm.json": "52348357a4eaed40cca94214fd221f95",
"assets/assets/rules/superpix.json": "3e176c47c97d3fd0c06816866ae517f0",
"assets/assets/rules/vcinema.json": "e75c30268610c45432875620d411ce4c",
"assets/assets/rules/yhdmtv.json": "9161826de64d9aac71c2c8501dbc35da",
"assets/assets/rules/zhuidm.json": "ed82918ed3c0dc5d718a056d63555721",
"assets/FontManifest.json": "baad8af0dd50df0d99f8a887a51c26bb",
"assets/fonts/MaterialIcons-Regular.otf": "755d630723e4ff36f1d81eadc1dcb18b",
"assets/NOTICES": "223e4cd6c09e677bd226cc7160c823e0",
"assets/shaders/ink_sparkle.frag": "ecc85a2e95f5e9f53123dcaf8cb9b6ce",
"canvaskit/canvaskit.js": "140ccb7d34d0a55065fbd422b843add6",
"canvaskit/canvaskit.js.symbols": "58832fbed59e00d2190aa295c4d70360",
"canvaskit/canvaskit.wasm": "07b9f5853202304d3b0749d9306573cc",
"canvaskit/chromium/canvaskit.js": "5e27aae346eee469027c80af0751d53d",
"canvaskit/chromium/canvaskit.js.symbols": "193deaca1a1424049326d4a91ad1d88d",
"canvaskit/chromium/canvaskit.wasm": "24c77e750a7fa6d474198905249ff506",
"canvaskit/skwasm.js": "1ef3ea3a0fec4569e5d531da25f34095",
"canvaskit/skwasm.js.symbols": "0088242d10d7e7d6d2649d1fe1bda7c1",
"canvaskit/skwasm.wasm": "264db41426307cfc7fa44b95a7772109",
"canvaskit/skwasm_heavy.js": "413f5b2b2d9345f37de148e2544f584f",
"canvaskit/skwasm_heavy.js.symbols": "3c01ec03b5de6d62c34e17014d1decd3",
"canvaskit/skwasm_heavy.wasm": "8034ad26ba2485dab2fd49bdd786837b",
"favicon.png": "5dcef449791fa27946b3d35ad8803796",
"flutter.js": "888483df48293866f9f41d3d9274a779",
"flutter_bootstrap.js": "f1f916646f9927f507168a2f56c55e33",
"icons/Icon-192.png": "ac9a721a12bbc803b44f645561ecb1e1",
"icons/Icon-512.png": "96e752610906ba2a93c65f8abe1645f1",
"icons/Icon-maskable-192.png": "c457ef57daa1d16f64b27b786ec2ea3c",
"icons/Icon-maskable-512.png": "301a7604d45b3e739efc881eb04896ea",
"index.html": "9c77303adf2db18de5f5eab5019553b0",
"/": "9c77303adf2db18de5f5eab5019553b0",
"main.dart.js": "06ef8f18a73df2311e26af8184351de9",
"manifest.json": "7768e28276a279a38097c4c719872d8a",
"version.json": "39eb3df9c8a389bcc0fbe07cb14da0eb"};
// The application shell files that are downloaded before a service worker can
// start.
const CORE = ["main.dart.js",
"index.html",
"flutter_bootstrap.js",
"assets/AssetManifest.bin.json",
"assets/FontManifest.json"];

// During install, the TEMP cache is populated with the application shell files.
self.addEventListener("install", (event) => {
  self.skipWaiting();
  return event.waitUntil(
    caches.open(TEMP).then((cache) => {
      return cache.addAll(
        CORE.map((value) => new Request(value, {'cache': 'reload'})));
    })
  );
});
// During activate, the cache is populated with the temp files downloaded in
// install. If this service worker is upgrading from one with a saved
// MANIFEST, then use this to retain unchanged resource files.
self.addEventListener("activate", function(event) {
  return event.waitUntil(async function() {
    try {
      var contentCache = await caches.open(CACHE_NAME);
      var tempCache = await caches.open(TEMP);
      var manifestCache = await caches.open(MANIFEST);
      var manifest = await manifestCache.match('manifest');
      // When there is no prior manifest, clear the entire cache.
      if (!manifest) {
        await caches.delete(CACHE_NAME);
        contentCache = await caches.open(CACHE_NAME);
        for (var request of await tempCache.keys()) {
          var response = await tempCache.match(request);
          await contentCache.put(request, response);
        }
        await caches.delete(TEMP);
        // Save the manifest to make future upgrades efficient.
        await manifestCache.put('manifest', new Response(JSON.stringify(RESOURCES)));
        // Claim client to enable caching on first launch
        self.clients.claim();
        return;
      }
      var oldManifest = await manifest.json();
      var origin = self.location.origin;
      for (var request of await contentCache.keys()) {
        var key = request.url.substring(origin.length + 1);
        if (key == "") {
          key = "/";
        }
        // If a resource from the old manifest is not in the new cache, or if
        // the MD5 sum has changed, delete it. Otherwise the resource is left
        // in the cache and can be reused by the new service worker.
        if (!RESOURCES[key] || RESOURCES[key] != oldManifest[key]) {
          await contentCache.delete(request);
        }
      }
      // Populate the cache with the app shell TEMP files, potentially overwriting
      // cache files preserved above.
      for (var request of await tempCache.keys()) {
        var response = await tempCache.match(request);
        await contentCache.put(request, response);
      }
      await caches.delete(TEMP);
      // Save the manifest to make future upgrades efficient.
      await manifestCache.put('manifest', new Response(JSON.stringify(RESOURCES)));
      // Claim client to enable caching on first launch
      self.clients.claim();
      return;
    } catch (err) {
      // On an unhandled exception the state of the cache cannot be guaranteed.
      console.error('Failed to upgrade service worker: ' + err);
      await caches.delete(CACHE_NAME);
      await caches.delete(TEMP);
      await caches.delete(MANIFEST);
    }
  }());
});
// The fetch handler redirects requests for RESOURCE files to the service
// worker cache.
self.addEventListener("fetch", (event) => {
  if (event.request.method !== 'GET') {
    return;
  }
  var origin = self.location.origin;
  var key = event.request.url.substring(origin.length + 1);
  // Redirect URLs to the index.html
  if (key.indexOf('?v=') != -1) {
    key = key.split('?v=')[0];
  }
  if (event.request.url == origin || event.request.url.startsWith(origin + '/#') || key == '') {
    key = '/';
  }
  // If the URL is not the RESOURCE list then return to signal that the
  // browser should take over.
  if (!RESOURCES[key]) {
    return;
  }
  // If the URL is the index.html, perform an online-first request.
  if (key == '/') {
    return onlineFirst(event);
  }
  event.respondWith(caches.open(CACHE_NAME)
    .then((cache) =>  {
      return cache.match(event.request).then((response) => {
        // Either respond with the cached resource, or perform a fetch and
        // lazily populate the cache only if the resource was successfully fetched.
        return response || fetch(event.request).then((response) => {
          if (response && Boolean(response.ok)) {
            cache.put(event.request, response.clone());
          }
          return response;
        });
      })
    })
  );
});
self.addEventListener('message', (event) => {
  // SkipWaiting can be used to immediately activate a waiting service worker.
  // This will also require a page refresh triggered by the main worker.
  if (event.data === 'skipWaiting') {
    self.skipWaiting();
    return;
  }
  if (event.data === 'downloadOffline') {
    downloadOffline();
    return;
  }
});
// Download offline will check the RESOURCES for all files not in the cache
// and populate them.
async function downloadOffline() {
  var resources = [];
  var contentCache = await caches.open(CACHE_NAME);
  var currentContent = {};
  for (var request of await contentCache.keys()) {
    var key = request.url.substring(origin.length + 1);
    if (key == "") {
      key = "/";
    }
    currentContent[key] = true;
  }
  for (var resourceKey of Object.keys(RESOURCES)) {
    if (!currentContent[resourceKey]) {
      resources.push(resourceKey);
    }
  }
  return contentCache.addAll(resources);
}
// Attempt to download the resource online before falling back to
// the offline cache.
function onlineFirst(event) {
  return event.respondWith(
    fetch(event.request).then((response) => {
      return caches.open(CACHE_NAME).then((cache) => {
        cache.put(event.request, response.clone());
        return response;
      });
    }).catch((error) => {
      return caches.open(CACHE_NAME).then((cache) => {
        return cache.match(event.request).then((response) => {
          if (response != null) {
            return response;
          }
          throw error;
        });
      });
    })
  );
}
