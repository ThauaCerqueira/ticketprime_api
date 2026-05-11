'use strict';

/**
 * TicketPrime – Persistência de sessão no localStorage
 * Armazena refresh token + dados básicos do usuário para
 * reautenticação automática após refresh (F5) do Blazor Server.
 *
 * Chaves:
 *   tp-refresh-token → string (token de refresh)
 *   tp-user-info     → JSON { Cpf, Nome, Perfil }
 */

window.ticketPrimeSession = (function () {

    const KEYS = {
        REFRESH_TOKEN: 'tp-refresh-token',
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

    /** Remove todos os dados de sessão do localStorage */
    function clear() {
        try {
            localStorage.removeItem(KEYS.REFRESH_TOKEN);
            localStorage.removeItem(KEYS.USER_INFO);
        } catch (_) { /* localStorage indisponível */ }
    }

    return { setItem, getItem, removeItem, clear, KEYS };
})();
