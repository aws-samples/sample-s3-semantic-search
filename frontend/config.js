/**
 * Frontend configuration for SnapFind.
 *
 * apiBaseUrl resolution order:
 *  1. window.SNAPFIND_API_URL (set by env-config.js if running locally via Aspire)
 *  2. Hardcoded value below (used in production after CDK deploy)
 */
(function () {
    'use strict';

    var apiBaseUrl = (typeof window !== 'undefined' && window.SNAPFIND_API_URL)
        || 'https://dpin3r6uv9.execute-api.us-east-1.amazonaws.com';

    var config = { apiBaseUrl: apiBaseUrl };

    window.__API_BASE_URL__ = config.apiBaseUrl;
    window.APP_CONFIG = config;
})();
