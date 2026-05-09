'use strict';

/**
 * TicketPrime – Módulo de criptografia ponta a ponta (E2E)
 * Toda a criptografia é executada exclusivamente no navegador do cliente.
 *
 * Fluxo para cada foto:
 *   1. Gera chave efêmera AES-GCM-256
 *   2. Cifra o conteúdo da imagem com AES-GCM (ciphertext + IV + auth-tag)
 *   3. Deriva segredo compartilhado via ECDH (chave privada do organizador + chave pública do servidor)
 *   4. Usa segredo como chave AES-KW para empacotar (wrapKey) a chave AES de imagem
 *   5. Envia: { ciphertext, IV, chave-AES-empacotada, chave-pública-do-org, metadados }
 *
 * Descriptografia (NÃO implementada aqui – lado do servidor):
 *   // 1. Servidor usa ECDH(chavePrivadaServidor, chavePublicaOrg) → segredo
 *   // 2. AES-KW unwrapKey(segredo, chaveAES-empacotada) → chaveAES
 *   // 3. AES-GCM decrypt(chaveAES, IV, ciphertext) → imagem original
 */

window.ticketPrimeCrypto = (function () {

    // ── Estado do módulo ──────────────────────────────────────────────────────
    let _organizerKeyPair = null;   // Par ECDH P-256 do organizador (gerado ao carregar)
    let _serverPublicKey  = null;   // Chave pública do servidor (demo: gerada localmente)

    // ── Utilitários de codificação ────────────────────────────────────────────

    /** ArrayBuffer → Base64 */
    function ab2b64(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = '';
        for (let i = 0; i < bytes.byteLength; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return btoa(binary);
    }

    /** Base64 → ArrayBuffer */
    function b642ab(base64) {
        const binary = atob(base64);
        const bytes  = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }
        return bytes.buffer;
    }

    /** Hash SHA-256 de uma string → Base64 */
    async function hashString(str) {
        const data   = new TextEncoder().encode(str);
        const digest = await crypto.subtle.digest('SHA-256', data);
        return ab2b64(digest);
    }

    // ── Inicialização ─────────────────────────────────────────────────────────

    /**
     * Gera o par de chaves ECDH P-256 do organizador e o par demo do servidor.
     * Em produção: a chave pública do servidor seria buscada via /api/public-key.
     * @returns {{ organizerPublicKey: string, serverPublicKey: string }}
     */
    async function init() {
        // Par do organizador (mantido em memória durante a sessão)
        _organizerKeyPair = await crypto.subtle.generateKey(
            { name: 'ECDH', namedCurve: 'P-256' },
            true,
            ['deriveKey', 'deriveBits']
        );

        // Par demo do servidor (em produção, apenas a chave pública seria recebida)
        const serverKeyPair = await crypto.subtle.generateKey(
            { name: 'ECDH', namedCurve: 'P-256' },
            true,
            ['deriveKey', 'deriveBits']
        );
        _serverPublicKey = serverKeyPair.publicKey;

        const orgPubJwk = await crypto.subtle.exportKey('jwk', _organizerKeyPair.publicKey);
        const srvPubJwk = await crypto.subtle.exportKey('jwk', _serverPublicKey);

        console.group('[TicketPrime Crypto] Inicializado com sucesso');
        console.log('Chave pública do organizador (P-256):', orgPubJwk);
        console.log('Chave pública do servidor (demo P-256):', srvPubJwk);
        console.groupEnd();

        return {
            organizerPublicKey: JSON.stringify(orgPubJwk),
            serverPublicKey:    JSON.stringify(srvPubJwk)
        };
    }

    // ── Criptografia de imagem ────────────────────────────────────────────────

    /**
     * Cifra uma imagem com AES-GCM-256 e empacota a chave via ECDH + AES-KW.
     *
     * @param {string} imageBase64  – Bytes da imagem em Base64
     * @param {string} mimeType     – Ex.: "image/jpeg"
     * @param {string} fileName     – Nome original do arquivo
     * @param {number} fileSize     – Tamanho em bytes
     * @returns {Promise<object>}   – Pacote criptografado
     */
    async function encryptImage(imageBase64, mimeType, fileName, fileSize) {
        if (!_organizerKeyPair || !_serverPublicKey) {
            throw new Error('[TicketPrime Crypto] Módulo não inicializado. Chame init() primeiro.');
        }

        // 1. Gera chave AES-GCM-256 efêmera para esta imagem
        const aesKey = await crypto.subtle.generateKey(
            { name: 'AES-GCM', length: 256 },
            true,          // extraível para poder ser empacotada
            ['encrypt', 'decrypt']
        );

        // 2. IV aleatório de 12 bytes (recomendado para AES-GCM)
        const iv = crypto.getRandomValues(new Uint8Array(12));

        // 3. Cifra o conteúdo da imagem
        const plaintext  = b642ab(imageBase64);
        const ciphertext = await crypto.subtle.encrypt(
            { name: 'AES-GCM', iv, tagLength: 128 },   // auth-tag de 128 bits está incluído no ciphertext
            aesKey,
            plaintext
        );

        // 4. Deriva segredo compartilhado via ECDH → chave AES-KW-256
        //    ECDH P-256 produz exatamente 32 bytes (256 bits) de material de chave
        const sharedKey = await crypto.subtle.deriveKey(
            { name: 'ECDH', public: _serverPublicKey },
            _organizerKeyPair.privateKey,
            { name: 'AES-KW', length: 256 },
            false,   // não extraível; segredo fica na memória do navigador
            ['wrapKey', 'unwrapKey']
        );

        // 5. Empacota (wrap) a chave AES da imagem com AES-KW
        const wrappedKey = await crypto.subtle.wrapKey('raw', aesKey, sharedKey, 'AES-KW');

        // 6. Exporta chave pública do organizador (necessária para o servidor derivar o mesmo segredo)
        const orgPubJwk = await crypto.subtle.exportKey('jwk', _organizerKeyPair.publicKey);

        // 7. Gera hash do nome do arquivo (preserva privacidade do nome original)
        const fileNameHash = await hashString(fileName);

        console.log(`[TicketPrime Crypto] Imagem cifrada: "${fileName}" (${fileSize} bytes)`);

        return {
            ciphertextBase64:      ab2b64(ciphertext),
            ivBase64:              ab2b64(iv.buffer),
            wrappedKeyBase64:      ab2b64(wrappedKey),
            organizerPublicKeyJwk: JSON.stringify(orgPubJwk),
            fileNameHash,
            mimeType,
            fileSizeBytes: fileSize
        };
    }

    // ── Zona de arrastar & soltar ─────────────────────────────────────────────

    /**
     * Configura listeners de drag & drop em um elemento da DOM.
     * Notifica o componente Blazor via DotNetObjectReference.
     *
     * @param {object} dotNetRef – DotNetObjectReference do componente Blazor
     * @param {string} elementId – ID do elemento da zona de drop
     */
    function initDropZone(dotNetRef, elementId) {
        const el = document.getElementById(elementId);
        if (!el) {
            console.warn('[TicketPrime Crypto] Elemento da drop zone não encontrado:', elementId);
            return;
        }

        const ACCEPTED_TYPES = ['image/jpeg', 'image/png', 'image/webp'];
        const MAX_SIZE_BYTES = 5 * 1024 * 1024;   // 5 MB
        let dragCounter = 0;

        el.addEventListener('dragenter', (e) => {
            e.preventDefault();
            if (++dragCounter === 1) {
                dotNetRef.invokeMethodAsync('OnDragEnter');
            }
        });

        el.addEventListener('dragleave', (e) => {
            e.preventDefault();
            if (--dragCounter <= 0) {
                dragCounter = 0;
                dotNetRef.invokeMethodAsync('OnDragLeave');
            }
        });

        el.addEventListener('dragover', (e) => {
            e.preventDefault();
            if (e.dataTransfer) e.dataTransfer.dropEffect = 'copy';
        });

        el.addEventListener('drop', async (e) => {
            e.preventDefault();
            dragCounter = 0;
            dotNetRef.invokeMethodAsync('OnDragLeave');

            const files = e.dataTransfer ? Array.from(e.dataTransfer.files) : [];
            if (!files.length) return;

            const resultados = [];
            const erros      = [];

            for (const file of files) {
                if (!ACCEPTED_TYPES.includes(file.type)) {
                    erros.push(`"${file.name}" – tipo não suportado (apenas JPG, PNG, WebP).`);
                    continue;
                }
                if (file.size > MAX_SIZE_BYTES) {
                    erros.push(`"${file.name}" – excede 5 MB.`);
                    continue;
                }
                try {
                    const base64 = await _fileToBase64(file);
                    resultados.push({ name: file.name, size: file.size, type: file.type, base64Data: base64 });
                } catch (err) {
                    console.error('[TicketPrime Crypto] Erro ao ler arquivo:', err);
                    erros.push(`"${file.name}" – erro de leitura.`);
                }
            }

            if (erros.length > 0) {
                dotNetRef.invokeMethodAsync('OnDropErrors', erros);
            }
            if (resultados.length > 0) {
                dotNetRef.invokeMethodAsync('OnFilesDropped', resultados);
            }
        });
    }

    /** Lê um File como base64 (sem prefixo data:…;base64,) */
    function _fileToBase64(file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload  = () => {
                const res = reader.result;
                if (typeof res === 'string') {
                    const comma = res.indexOf(',');
                    resolve(comma >= 0 ? res.slice(comma + 1) : res);
                } else {
                    reject(new Error('FileReader retornou tipo inesperado'));
                }
            };
            reader.onerror = () => reject(reader.error);
            reader.readAsDataURL(file);
        });
    }

    /** Dispara um clique programático no input de arquivo */
    function clickFileInput(elementId) {
        const el = document.getElementById(elementId);
        if (el) el.click();
    }

    // ── Helpers de validação de imagem ────────────────────────────────────────

    /**
     * Calcula o SHA-256 dos bytes da imagem (Base64) e retorna como hex.
     * Usado para detectar fotos duplicadas antes da criptografia.
     *
     * @param {string} imageBase64 – Bytes da imagem em Base64 (sem prefixo data:...)
     * @returns {Promise<string>}  – SHA-256 em hexadecimal (64 chars)
     */
    async function hashImageContent(imageBase64) {
        const buffer = b642ab(imageBase64);
        const digest = await crypto.subtle.digest('SHA-256', buffer);
        return Array.from(new Uint8Array(digest))
            .map(b => b.toString(16).padStart(2, '0'))
            .join('');
    }

    /**
     * Retorna as dimensões (largura × altura) de uma imagem via elemento <img> + canvas.
     * Não desenha nada visível; usa apenas o decodificador do navegador.
     *
     * @param {string} imageBase64 – Bytes da imagem em Base64 (sem prefixo data:...)
     * @param {string} mimeType    – Ex.: "image/jpeg"
     * @returns {Promise<{ width: number, height: number }>}
     */
    function getImageDimensions(imageBase64, mimeType) {
        return new Promise((resolve, reject) => {
            const img = new Image();
            img.onload  = () => resolve({ width: img.naturalWidth, height: img.naturalHeight });
            img.onerror = () => reject(new Error('Não foi possível decodificar a imagem para verificar as dimensões.'));
            img.src     = `data:${mimeType};base64,${imageBase64}`;
        });
    }

    // ── API pública ───────────────────────────────────────────────────────────
    return { init, encryptImage, initDropZone, clickFileInput, hashImageContent, getImageDimensions };

})();
