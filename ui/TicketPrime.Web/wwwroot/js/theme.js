'use strict';

/**
 * TicketPrime – Gerenciamento de tema (Dark Mode)
 * Persiste a preferência do usuário no localStorage e aplica via atributo data-theme.
 */

window.ticketPrimeTheme = (function () {

    const STORAGE_KEY = 'tp-theme';
    const ATTR_NAME   = 'data-theme';

    /** Retorna o tema atual ('light' ou 'dark') */
    function getTheme() {
        return document.documentElement.getAttribute(ATTR_NAME) || 'light';
    }

    /** Aplica o tema no <html> e persiste no localStorage */
    function setTheme(theme) {
        if (theme === 'dark') {
            document.documentElement.setAttribute(ATTR_NAME, 'dark');
        } else {
            document.documentElement.removeAttribute(ATTR_NAME);
        }
        try {
            localStorage.setItem(STORAGE_KEY, theme);
        } catch (_) { /* localStorage indisponível */ }
    }

    /** Alterna entre light/dark */
    function toggleTheme() {
        const current = getTheme();
        const next = current === 'dark' ? 'light' : 'dark';
        setTheme(next);
        return next;
    }

    /** Inicializa o tema a partir do localStorage (fallback: light) */
    function initTheme() {
        let theme = 'light';
        try {
            theme = localStorage.getItem(STORAGE_KEY) || 'light';
        } catch (_) { /* localStorage indisponível */ }
        setTheme(theme);
        return theme;
    }

    return { getTheme, setTheme, toggleTheme, initTheme };
})();
