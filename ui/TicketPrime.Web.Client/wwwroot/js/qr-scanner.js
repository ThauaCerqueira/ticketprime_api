// ═══════════════════════════════════════════════════════════════════════
//  TicketPrime — QR Code Scanner Module
//  ─────────────────────────────────────────────────────────────────────
//  Escaneia QR Codes usando a câmera do dispositivo e a biblioteca jsQR
//  (carregada dinamicamente via CDN). O código é decodificado no próprio
//  navegador — nenhuma imagem é enviada ao servidor.
//
//  Dependências:
//    - jsQR.js (carregado dinamicamente de CDN, ~15KB minified)
//    - navigator.mediaDevices.getUserMedia (WebRTC)
//
//  Uso no Blazor:
//    1. Inicializar:  await TicketPrimeQrScanner.start(videoId, callback)
//    2. Parar:        TicketPrimeQrScanner.stop()
//    3. Estado:       TicketPrimeQrScanner.isRunning()
//
//  Referência: https://github.com/cozmo/jsQR
// ═══════════════════════════════════════════════════════════════════════

window.TicketPrimeQrScanner = (() => {
    'use strict';

    let mediaStream = null;
    let animationFrameId = null;
    let isScanning = false;
    let videoElement = null;
    let canvasElement = null;
    let canvasContext = null;
    let jsqrLoaded = false;
    let dotNetRef = null;
    let callbackMethod = null;

    /** Carrega a biblioteca jsQR dinamicamente via CDN. */
    function loadJsQR() {
        return new Promise((resolve, reject) => {
            if (typeof jsQR !== 'undefined') {
                jsqrLoaded = true;
                resolve();
                return;
            }
            const script = document.createElement('script');
            script.src = 'https://cdn.jsdelivr.net/npm/jsqr@1.4.0/dist/jsQR.min.js';
            script.integrity = 'sha256-7Q5dDOUZL6hMfwBJFLOxWEiO+hI40Z01eMx6lOOuNMA=';
            script.crossOrigin = 'anonymous';
            script.onload = () => {
                jsqrLoaded = true;
                resolve();
            };
            script.onerror = () => reject(new Error('Falha ao carregar jsQR. Verifique sua conexão com a internet.'));
            document.head.appendChild(script);
        });
    }

    /**
     * Inicia o scanner de QR Code.
     * @param {string} videoElementId - ID do elemento <video>
     * @param {string} canvasElementId - ID do elemento <canvas> (oculto)
     * @param {object} dotNetRef - DotNetObjectReference do Blazor (para callback)
     * @param {string} callbackMethod - Nome do método no .NET a ser chamado
     * @param {object} options - Opções
     * @param {number} options.scanInterval - Intervalo entre scans (ms). Padrão: 300
     */
    async function start(videoElementId, canvasElementId, dotNetRef, callbackMethod, options = {}) {
        if (isScanning) {
            console.log('[QRScanner] Já está escaneando.');
            return;
        }

        const scanInterval = options.scanInterval || 300;

        try {
            await loadJsQR();

            videoElement = document.getElementById(videoElementId);
            canvasElement = document.getElementById(canvasElementId);

            if (!videoElement || !canvasElement) {
                throw new Error(`Elementos #${videoElementId} ou #${canvasElementId} não encontrados.`);
            }

            canvasContext = canvasElement.getContext('2d', { willReadFrequently: true });
    window.__tp_qr_dotNetRef = dotNetRef;
    window.__tp_qr_callbackMethod = callbackMethod;
            // Solicita permissão e inicia a câmera
            mediaStream = await navigator.mediaDevices.getUserMedia({
                video: {
                    facingMode: 'environment',  // Câmera traseira (padrão para QR Code)
                    width: { ideal: 640 },
                    height: { ideal: 480 }
                },
                audio: false
            });

            videoElement.srcObject = mediaStream;
            videoElement.setAttribute('playsinline', 'true');
            videoElement.setAttribute('autoplay', 'true');
            await videoElement.play();

            isScanning = true;
            let lastScanTime = 0;

            /** Loop principal: captura frames do vídeo e tenta decodificar. */
            function scanFrame(timestamp) {
                if (!isScanning) return;

                // Controle de taxa de scan (evita processar todos os frames)
                if (timestamp - lastScanTime >= scanInterval) {
                    lastScanTime = timestamp;

                    if (videoElement.readyState === videoElement.HAVE_ENOUGH_DATA) {
                        // Ajusta canvas para o tamanho real do vídeo
                        canvasElement.width = videoElement.videoWidth;
                        canvasElement.height = videoElement.videoHeight;

                        // Desenha o frame atual no canvas
                        canvasContext.drawImage(videoElement, 0, 0, canvasElement.width, canvasElement.height);

                        // Tenta decodificar
                        const imageData = canvasContext.getImageData(0, 0, canvasElement.width, canvasElement.height);
                        const code = jsQR(imageData.data, imageData.width, imageData.height, {
                            inversionAttempts: 'dontInvert'
                        });

                        if (code && code.data) {
                            const codigo = code.data.trim();
                            const ref = window.__tp_qr_dotNetRef;
                            const method = window.__tp_qr_callbackMethod;
                            if (codigo.length > 0 && ref && method) {
                                // Pausa o scan para evitar múltiplas detecções
                                isScanning = false;
                                ref.invokeMethodAsync(method, codigo);
                                return; // Não continua o loop
                            }
                        }
                    }
                }

                animationFrameId = requestAnimationFrame(scanFrame);
            }

            animationFrameId = requestAnimationFrame(scanFrame);

            return { success: true };

        } catch (err) {
            console.error('[QRScanner] Erro:', err);
            await stop();
            throw err;
        }
    }

    /** Para o scanner e libera a câmera. */
    async function stop() {
        isScanning = false;

        if (animationFrameId) {
            cancelAnimationFrame(animationFrameId);
            animationFrameId = null;
        }

        if (mediaStream) {
            mediaStream.getTracks().forEach(track => track.stop());
            mediaStream = null;
        }

        if (videoElement) {
            videoElement.srcObject = null;
        }

        videoElement = null;
        canvasElement = null;
        canvasContext = null;
        window.__tp_qr_dotNetRef = null;
        window.__tp_qr_callbackMethod = null;

        return { success: true };
    }

    /** Retorna se o scanner está rodando. */
    function isRunning() {
        return isScanning;
    }

    return { start, stop, isRunning };
})();
