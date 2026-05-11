// ═══════════════════════════════════════════════════════════════════════
//  Mercado Pago SDK — Card Tokenization Module
//  ─────────────────────────────────────────────────────────────────────
//  This module wraps MercadoPago.js v2 Fields for secure card tokenization
//  in Blazor Server (InteractiveServer). Card data is captured via iframes
//  that send data DIRECTLY to Mercado Pago — it NEVER touches our server.
//
//  Usage:
//    1. Initialize:  await MpModule.init(publicKey, 'cardNumberContainerId', ...)
//    2. Tokenize:    const result = await MpModule.createCardToken(cardholderName)
//    3. Cleanup:     MpModule.unmount()
//
//  Reference: https://www.mercadopago.com.br/developers/pt/docs/checkout-api/integration-test
// ═══════════════════════════════════════════════════════════════════════

window.TicketPrimeMp = (() => {
    'use strict';

    let mpInstance = null;
    const fieldIds = {
        cardNumber: 'mp-cardNumber',
        cardholderName: 'mp-cardholderName',
        cardExpirationMonth: 'mp-cardExpirationMonth',
        cardExpirationYear: 'mp-cardExpirationYear',
        securityCode: 'mp-securityCode',
    };

    /** Load the MercadoPago.js v2 SDK script dynamically. */
    function loadSdkScript() {
        return new Promise((resolve, reject) => {
            // Avoid loading twice
            if (window.MercadoPago) {
                resolve();
                return;
            }
            const script = document.createElement('script');
            script.src = 'https://sdk.mercadopago.com/js/v2';
            script.onload = () => resolve();
            script.onerror = () => reject(new Error('Falha ao carregar SDK Mercado Pago.'));
            document.head.appendChild(script);
        });
    }

    /**
     * Initialize the Mercado Pago Fields instances inside the provided container.
     * @param {string} publicKey - Mercado Pago public key
     * @param {string} containerId - ID of the div that will hold the card fields
     */
    async function init(publicKey, containerId) {
        await loadSdkScript();

        mpInstance = new MercadoPago(publicKey, {
            locale: 'pt-BR',
        });

        // Create the field containers if they don't exist
        const container = document.getElementById(containerId);
        if (!container) {
            throw new Error(`Container #${containerId} not found.`);
        }

        // Ensure child containers exist for each field
        const fieldContainers = {};
        for (const [key, id] of Object.entries(fieldIds)) {
            let el = document.getElementById(id);
            if (!el) {
                el = document.createElement('div');
                el.id = id;
                el.className = 'mp-field-container';
                container.appendChild(el);
            }
            fieldContainers[key] = el;
        }

        // Common style for all fields
        const commonStyle = {
            input: {
                'font-size': '14px',
                'font-family': "'Inter', system-ui, -apple-system, sans-serif",
                color: '#1a1a2e',
                'background-color': '#F9FAFB',
                border: '1.5px solid #E5E7EB',
                'border-radius': '8px',
                padding: '10px 12px',
                height: '42px',
                'box-sizing': 'border-box',
                outline: 'none',
                transition: 'border-color 0.15s ease',
            },
            '.input:focus': {
                'border-color': '#5B5BD6',
            },
            '.input-error': {
                'border-color': '#DC2626',
            },
        };

        // Mount cardNumber field (with iframe)
        await mpInstance.fields.create('cardNumber', {
            placeholder: 'Número do cartão',
            style: commonStyle,
        }).mount(fieldIds.cardNumber);

        // Mount cardholderName field
        await mpInstance.fields.create('cardholderName', {
            placeholder: 'Nome do titular',
            style: commonStyle,
        }).mount(fieldIds.cardholderName);

        // Mount expiration month
        await mpInstance.fields.create('cardExpirationMonth', {
            placeholder: 'Mês',
            style: { ...commonStyle, input: { ...commonStyle.input, 'text-align': 'center' } },
        }).mount(fieldIds.cardExpirationMonth);

        // Mount expiration year
        await mpInstance.fields.create('cardExpirationYear', {
            placeholder: 'Ano',
            style: { ...commonStyle, input: { ...commonStyle.input, 'text-align': 'center' } },
        }).mount(fieldIds.cardExpirationYear);

        // Mount security code (CVV)
        await mpInstance.fields.create('securityCode', {
            placeholder: 'CVV',
            style: commonStyle,
        }).mount(fieldIds.securityCode);
    }

    /**
     * Create a card token from the already-mounted fields.
     * @returns {{ token: string, lastFourDigits: string }}
     */
    async function createCardToken() {
        if (!mpInstance) {
            throw new Error('Mercado Pago não inicializado. Chame init() primeiro.');
        }

        const cardToken = await mpInstance.fields.createCardToken();

        return {
            token: cardToken.id,
            lastFourDigits: cardToken.last_four_digits,
        };
    }

    /** Unmount all fields and clean up. */
    function unmount() {
        if (mpInstance) {
            try {
                mpInstance.fields.unmount();
            } catch (e) {
                // Ignore errors on unmount
            }
            mpInstance = null;
        }

        // Clear containers
        for (const id of Object.values(fieldIds)) {
            const el = document.getElementById(id);
            if (el) {
                el.innerHTML = '';
            }
        }
    }

    return { init, createCardToken, unmount };
})();
