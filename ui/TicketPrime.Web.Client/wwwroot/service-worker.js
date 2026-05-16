// ═══════════════════════════════════════════════════════════════════════════════
//  TicketPrime — Service Worker (PWA)
//
//  ANTES: Sem service worker. A aplicação não funcionava offline e não era
//  instalável como PWA. Usuários perdiam acesso se a rede caísse.
//
//  AGORA: Cache-first para assets estáticos (DLLs, CSS, imagens) e
//  network-first para dados da API. A página de erro/offline é mostrada
//  quando não há conexão.
//
//  Estratégia de cache:
//   - ASSETS (DLLs, WASM, CSS, JS, fontes): Cache First
//     → Carrega do cache Instantaneamente após a primeira visita
//     → Atualiza em background quando o service worker atualiza
//   - API (/api/): Network First com fallback para cache
//     → Sempre busca dados frescos do servidor
//     → Mostra dados em cache se offline (para navegação básica)
//   - Páginas (navegação): Network First
//     → Sempre tenta o servidor primeiro
//     → Fallback para página offline customizada
// ═══════════════════════════════════════════════════════════════════════════════

const CACHE_NAME = 'ticketprime-v1';
const STATIC_ASSETS = [
    '/',
    '/css/app.css',
    '/manifest.json'
];

// ── Asserts do Blazor WebAssembly (DLLs, etc.) ──────────────────────────────
// Estes arquivos mudam apenas quando a aplicação é republicada.
// O service worker os atualiza automaticamente na próxima visita.
const BLAZOR_ASSETS_CACHE = 'ticketprime-blazor-v1';

// ── Instalação ──────────────────────────────────────────────────────────────
self.addEventListener('install', (event) => {
    console.log('[ServiceWorker] Instalando...');
    event.waitUntil(
        caches.open(CACHE_NAME).then((cache) => {
            return cache.addAll(STATIC_ASSETS);
        })
    );
    // Força o service worker a ativar imediatamente (sem esperar fechar abas)
    self.skipWaiting();
});

// ── Ativação ────────────────────────────────────────────────────────────────
self.addEventListener('activate', (event) => {
    console.log('[ServiceWorker] Ativando...');
    event.waitUntil(
        caches.keys().then((cacheNames) => {
            return Promise.all(
                cacheNames
                    .filter((name) => name !== CACHE_NAME && name !== BLAZOR_ASSETS_CACHE)
                    .map((name) => caches.delete(name))
            );
        })
    );
    // Reivindica controle sobre todas as abas abertas
    self.clients.claim();
});

// ── Interceptação de Fetch ──────────────────────────────────────────────────
self.addEventListener('fetch', (event) => {
    const url = new URL(event.request.url);

    // ── API requests: Network First ──────────────────────────────────
    if (url.pathname.startsWith('/api/')) {
        event.respondWith(networkFirstWithFallback(event.request, '/offline'));
        return;
    }

    // ── Navegação (páginas .NET): Network First ──────────────────────
    if (event.request.mode === 'navigate') {
        event.respondWith(networkFirstWithFallback(event.request, '/offline'));
        return;
    }

    // ── Assets estáticos (DLLs, CSS, WASM): Cache First ──────────────
    if (isStaticAsset(event.request)) {
        event.respondWith(cacheFirst(event.request));
        return;
    }

    // ── Outros (imagens, fontes): Cache First ────────────────────────
    event.respondWith(cacheFirst(event.request));
});

// ═══════════════════════════════════════════════════════════════════════════════
//  Estratégias de Cache
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Cache First: tenta servir do cache; se não existir, busca na rede e armazena.
/// Ideal para assets que não mudam frequentemente (DLLs, CSS, imagens).
/// </summary>
async function cacheFirst(request) {
    const cached = await caches.match(request);
    if (cached) {
        // Atualiza o cache em background (stale-while-revalidate)
        fetchAndCache(request);
        return cached;
    }
    return fetchAndCache(request);
}

/// <summary>
/// Network First: tenta buscar da rede; se falhar, usa cache como fallback.
/// Ideal para dados da API e navegação.
/// </summary>
async function networkFirstWithFallback(request, fallbackUrl) {
    try {
        const response = await fetch(request);
        if (response.ok) {
            // Só armazena em cache respostas bem-sucedidas
            const cache = await caches.open(CACHE_NAME);
            cache.put(request, response.clone());
            return response;
        }
        throw new Error('Resposta não-ok');
    } catch (error) {
        console.log('[ServiceWorker] Rede indisponível, buscando cache:', request.url);
        const cached = await caches.match(request);
        if (cached) {
            return cached;
        }
        // Se não tem cache, retorna página offline
        if (request.mode === 'navigate') {
            const offlinePage = await caches.match(fallbackUrl);
            if (offlinePage) return offlinePage;
        }
        throw error;
    }
}

/// <summary>
/// Busca na rede e armazena em cache.
/// </summary>
async function fetchAndCache(request) {
    const response = await fetch(request);
    if (response.ok && request.method === 'GET') {
        const cache = await caches.open(CACHE_NAME);
        cache.put(request, response.clone());
    }
    return response;
}

/// <summary>
/// Verifica se a requisição é de um asset estático do Blazor.
/// </summary>
function isStaticAsset(request) {
    const url = new URL(request.url);
    const ext = url.pathname.split('.').pop()?.toLowerCase();
    return [
        'dll', 'wasm', 'pdb', 'dat', 'blat',
        'css', 'js', 'woff', 'woff2', 'ttf',
        'png', 'jpg', 'jpeg', 'gif', 'webp', 'svg', 'ico'
    ].includes(ext || '');
}
