'use strict';

/**
 * TicketPrime – Persistência de sessão (APENAS dados não-sensíveis)
 *
 * ═══════════════════════════════════════════════════════════════════
 * SEGURANÇA: NÃO armazenamos mais o refresh token no localStorage.
 *
 * ANTES (vulnerável):
 *   - tp-refresh-token era armazenado aqui
 *   - XSS → atacante roubava refresh token → sessão sequestrada
 *
 * AGORA (seguro):
 *   - Refresh token DEFINIDO EXCLUSIVAMENTE como cookie httpOnly
 *     (ticketprime_refresh) pelo backend.
 *   - JavaScript NÃO CONSEGUE ler este cookie → imune a XSS.
 *   - O cookie é enviado automaticamente nas requisições fetch.
 *   - Aqui armazenamos apenas tp-user-info (nome, perfil, CPF)
 *     para exibição na UI — dados não críticos para segurança.
 *
 * Chave mantida:
 *   tp-user-info → JSON { Cpf, Nome, Perfil }
 * ═══════════════════════════════════════════════════════════════════
 */

window.ticketPrimeSession = (function () {

    const KEYS = {
        USER_INFO: 'tp-user-info'
    };

    function setItem(key, value) {
        try {
            localStorage.setItem(key, value);
        } catch (_) { /* localStorage indisponível */ }
    }

    function getItem(key) {
        try {
            return localStorage.getItem(key);
        } catch (_) {
            return null;
        }
    }

    function removeItem(key) {
        try {
            localStorage.removeItem(key);
        } catch (_) { /* localStorage indisponível */ }
    }

    /** Remove dados de session do localStorage (apenas info do usuário) */
    function clear() {
        try {
            localStorage.removeItem(KEYS.USER_INFO);
            // Limpa resquícios da versão antiga (tp-refresh-token)
            localStorage.removeItem('tp-refresh-token');
        } catch (_) { /* localStorage indisponível */ }
    }

    return { setItem, getItem, removeItem, clear, KEYS };
})();
