// ============================================================
// GitLab OAuth Token Generator V3 - Application Logic
// ============================================================
// All OAuth operations happen client-side. Token exchange calls
// go directly from the browser to the GitLab instance.
// Data is persisted in localStorage.
// ============================================================

const STORAGE_KEYS = {
    SETTINGS: 'gitlab_oauth_settings',
    TOKENS: 'gitlab_oauth_tokens',
    PENDING_STATE: 'gitlab_oauth_pending_state'
};

// ---- Initialization ----

document.addEventListener('DOMContentLoaded', () => {
    loadSettingsIntoForms();
    setRedirectUri();
    checkForOAuthCallback();
    updateTokenBadge();
    renderTokensList();
    refreshJsonViewer();
    checkRefreshSection();
});

// ---- Tab Navigation ----

function switchTab(tabName) {
    document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
    document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
    document.querySelector(`[data-tab="${tabName}"]`).classList.add('active');
    document.getElementById(`tab-${tabName}`).classList.add('active');

    if (tabName === 'tokens') {
        renderTokensList();
        refreshJsonViewer();
    }
}

// ---- Settings Management ----

function getSettings() {
    try {
        return JSON.parse(localStorage.getItem(STORAGE_KEYS.SETTINGS) || '{}');
    } catch { return {}; }
}

function saveSettingsObj(settings) {
    localStorage.setItem(STORAGE_KEYS.SETTINGS, JSON.stringify(settings));
}

function loadSettingsIntoForms() {
    const s = getSettings();
    // Generate tab
    if (s.gitlabUrl) document.getElementById('gitlabUrl').value = s.gitlabUrl;
    if (s.clientId) document.getElementById('clientId').value = s.clientId;
    if (s.clientSecret) document.getElementById('clientSecret').value = s.clientSecret;
    // Settings tab
    if (s.gitlabUrl) document.getElementById('settingsGitlabUrl').value = s.gitlabUrl;
    if (s.clientId) document.getElementById('settingsClientId').value = s.clientId;
    if (s.clientSecret) document.getElementById('settingsClientSecret').value = s.clientSecret;
    if (s.scope) document.getElementById('settingsScope').value = s.scope;
}

function saveSettings() {
    const settings = {
        gitlabUrl: document.getElementById('gitlabUrl').value.trim(),
        clientId: document.getElementById('clientId').value.trim(),
        clientSecret: document.getElementById('clientSecret').value.trim(),
        scope: getSettings().scope || 'api'
    };
    saveSettingsObj(settings);
    showToast('Settings saved!', 'success');
}

function saveAllSettings() {
    const settings = {
        gitlabUrl: document.getElementById('settingsGitlabUrl').value.trim(),
        clientId: document.getElementById('settingsClientId').value.trim(),
        clientSecret: document.getElementById('settingsClientSecret').value.trim(),
        scope: document.getElementById('settingsScope').value.trim() || 'api'
    };
    saveSettingsObj(settings);
    loadSettingsIntoForms();
    showToast('All settings saved!', 'success');
}

function clearAllSettings() {
    if (!confirm('Clear all settings and saved tokens? This cannot be undone.')) return;
    localStorage.removeItem(STORAGE_KEYS.SETTINGS);
    localStorage.removeItem(STORAGE_KEYS.TOKENS);
    localStorage.removeItem(STORAGE_KEYS.PENDING_STATE);
    document.querySelectorAll('input:not([readonly])').forEach(i => i.value = '');
    document.getElementById('settingsScope').value = 'api';
    updateTokenBadge();
    renderTokensList();
    refreshJsonViewer();
    showToast('All data cleared.', 'info');
}

function setRedirectUri() {
    const redirectUri = window.location.origin + window.location.pathname;
    document.getElementById('redirectUri').value = redirectUri;
    const settingsEl = document.getElementById('settingsRedirectUri');
    if (settingsEl) settingsEl.textContent = redirectUri;
}

// ---- OAuth Flow ----

function startOAuthFlow() {
    const gitlabUrl = document.getElementById('gitlabUrl').value.trim();
    const clientId = document.getElementById('clientId').value.trim();
    const clientSecret = document.getElementById('clientSecret').value.trim();
    const scope = getSettings().scope || 'api';
    const redirectUri = document.getElementById('redirectUri').value;

    if (!gitlabUrl) { showToast('GitLab URL is required.', 'error'); return; }
    if (!clientId) { showToast('Application ID is required.', 'error'); return; }
    if (!clientSecret) { showToast('Application Secret is required.', 'error'); return; }

    // Save settings for after redirect
    saveSettings();

    // Generate a random state parameter for CSRF protection
    const state = crypto.randomUUID ? crypto.randomUUID() : Math.random().toString(36).substring(2);
    localStorage.setItem(STORAGE_KEYS.PENDING_STATE, JSON.stringify({
        state,
        gitlabUrl,
        clientId,
        clientSecret,
        redirectUri,
        scope,
        timestamp: Date.now()
    }));

    const authorizeUrl = `${gitlabUrl.replace(/\/+$/, '')}/oauth/authorize` +
        `?client_id=${encodeURIComponent(clientId)}` +
        `&redirect_uri=${encodeURIComponent(redirectUri)}` +
        `&response_type=code` +
        `&scope=${encodeURIComponent(scope)}` +
        `&state=${encodeURIComponent(state)}`;

    window.location.href = authorizeUrl;
}

function checkForOAuthCallback() {
    const params = new URLSearchParams(window.location.search);
    const code = params.get('code');
    const state = params.get('state');
    const error = params.get('error');
    const errorDesc = params.get('error_description');

    if (error) {
        // Clean URL
        window.history.replaceState({}, '', window.location.pathname);
        showToast(`OAuth error: ${error} - ${errorDesc || ''}`, 'error');
        return;
    }

    if (!code) return;

    // Clean URL immediately
    window.history.replaceState({}, '', window.location.pathname);

    // Retrieve pending state
    let pending;
    try {
        pending = JSON.parse(localStorage.getItem(STORAGE_KEYS.PENDING_STATE));
    } catch { pending = null; }

    if (!pending) {
        showToast('No pending OAuth state found. Please try again.', 'error');
        return;
    }

    // Verify state parameter
    if (state && pending.state && state !== pending.state) {
        showToast('OAuth state mismatch. Possible CSRF attack. Please try again.', 'error');
        localStorage.removeItem(STORAGE_KEYS.PENDING_STATE);
        return;
    }

    localStorage.removeItem(STORAGE_KEYS.PENDING_STATE);

    // Show exchange section and start token exchange
    exchangeCodeForToken(code, pending);
}

async function exchangeCodeForToken(code, config) {
    const exchangeSection = document.getElementById('exchangeSection');
    exchangeSection.style.display = 'block';
    exchangeSection.scrollIntoView({ behavior: 'smooth' });

    const log = document.getElementById('exchangeLog');
    log.innerHTML = '';

    appendLog(log, `Authorization code received: ${code.substring(0, 10)}...`, 'success');
    setStepState('step1', 'completed');
    setStepState('step2', 'active');

    appendLog(log, `Exchanging code for access token...`, 'info');
    appendLog(log, `POST ${config.gitlabUrl}/oauth/token`, 'info');

    try {
        const tokenUrl = `${config.gitlabUrl.replace(/\/+$/, '')}/oauth/token`;
        const body = new URLSearchParams({
            client_id: config.clientId,
            client_secret: config.clientSecret,
            code: code,
            grant_type: 'authorization_code',
            redirect_uri: config.redirectUri
        });

        const response = await fetch(tokenUrl, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: body.toString()
        });

        const data = await response.json();

        if (!response.ok || !data.access_token) {
            setStepState('step2', 'error');
            appendLog(log, `Token exchange failed: ${JSON.stringify(data)}`, 'error');
            showToast('Token exchange failed. Check the log for details.', 'error');
            return;
        }

        setStepState('step2', 'completed');
        appendLog(log, `Access token received: ${data.access_token.substring(0, 15)}...`, 'success');

        // Validate token
        setStepState('step3', 'active');
        appendLog(log, `Validating token via GET /api/v4/user...`, 'info');

        let userData = null;
        try {
            const userResp = await fetch(`${config.gitlabUrl.replace(/\/+$/, '')}/api/v4/user`, {
                headers: { 'Authorization': `Bearer ${data.access_token}` }
            });
            if (userResp.ok) {
                userData = await userResp.json();
                appendLog(log, `Authenticated as: ${userData.name} (@${userData.username})`, 'success');
                setStepState('step3', 'completed');
            } else {
                appendLog(log, `Validation returned HTTP ${userResp.status} (token may still work)`, 'error');
                setStepState('step3', 'error');
            }
        } catch (e) {
            appendLog(log, `Validation network error: ${e.message}`, 'error');
            setStepState('step3', 'error');
        }

        // Display result
        displayTokenResult(data, userData, config.gitlabUrl);

        // Save token
        saveToken({
            gitlabDomain: config.gitlabUrl,
            applicationId: config.clientId,
            applicationSecret: config.clientSecret,
            redirectUri: config.redirectUri,
            accessToken: data.access_token,
            refreshToken: data.refresh_token || '',
            tokenType: data.token_type || 'Bearer',
            scope: data.scope || 'api',
            expiresIn: data.expires_in || 0,
            createdAtUtc: new Date().toISOString(),
            user: userData ? { name: userData.name, username: userData.username, email: userData.email } : null
        });

        updateTokenBadge();
        checkRefreshSection();
        showToast('Token generated and saved successfully!', 'success');

    } catch (err) {
        setStepState('step2', 'error');
        appendLog(log, `Network error: ${err.message}`, 'error');
        showToast(`Network error: ${err.message}`, 'error');
    }
}

async function exchangeManualCode() {
    const input = document.getElementById('manualCode').value.trim();
    if (!input) { showToast('Please enter a code or URL.', 'error'); return; }

    let code = input;
    // Try to extract code from URL
    try {
        const url = new URL(input);
        const urlCode = url.searchParams.get('code');
        if (urlCode) code = urlCode;
    } catch { /* treat as raw code */ }

    const settings = getSettings();
    if (!settings.gitlabUrl || !settings.clientId || !settings.clientSecret) {
        showToast('Please fill in GitLab URL, Application ID, and Secret first.', 'error');
        return;
    }

    const config = {
        gitlabUrl: settings.gitlabUrl,
        clientId: settings.clientId,
        clientSecret: settings.clientSecret,
        redirectUri: document.getElementById('redirectUri').value,
        scope: settings.scope || 'api'
    };

    await exchangeCodeForToken(code, config);
}

async function refreshToken() {
    const tokens = getSavedTokens();
    const latest = tokens[tokens.length - 1];
    if (!latest || !latest.refreshToken) {
        showToast('No refresh token available.', 'error');
        return;
    }

    showToast('Refreshing token...', 'info');

    try {
        const tokenUrl = `${latest.gitlabDomain.replace(/\/+$/, '')}/oauth/token`;
        const body = new URLSearchParams({
            client_id: latest.applicationId,
            client_secret: latest.applicationSecret,
            refresh_token: latest.refreshToken,
            grant_type: 'refresh_token',
            redirect_uri: latest.redirectUri
        });

        const response = await fetch(tokenUrl, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: body.toString()
        });

        const data = await response.json();

        if (!response.ok || !data.access_token) {
            showToast(`Refresh failed: ${data.error_description || data.error || 'Unknown error'}. Re-authorize using the Generate tab.`, 'error');
            return;
        }

        // Validate
        let userData = null;
        try {
            const userResp = await fetch(`${latest.gitlabDomain.replace(/\/+$/, '')}/api/v4/user`, {
                headers: { 'Authorization': `Bearer ${data.access_token}` }
            });
            if (userResp.ok) userData = await userResp.json();
        } catch { /* ignore */ }

        displayTokenResult(data, userData, latest.gitlabDomain);

        saveToken({
            gitlabDomain: latest.gitlabDomain,
            applicationId: latest.applicationId,
            applicationSecret: latest.applicationSecret,
            redirectUri: latest.redirectUri,
            accessToken: data.access_token,
            refreshToken: data.refresh_token || latest.refreshToken,
            tokenType: data.token_type || 'Bearer',
            scope: data.scope || 'api',
            expiresIn: data.expires_in || 0,
            createdAtUtc: new Date().toISOString(),
            user: userData ? { name: userData.name, username: userData.username, email: userData.email } : null
        });

        updateTokenBadge();
        checkRefreshSection();
        showToast('Token refreshed successfully!', 'success');

    } catch (err) {
        showToast(`Refresh error: ${err.message}`, 'error');
    }
}

// ---- Display Helpers ----

function displayTokenResult(data, userData, gitlabUrl) {
    const resultSection = document.getElementById('tokenResult');
    resultSection.style.display = 'block';

    document.getElementById('resultAccessToken').textContent = data.access_token;
    document.getElementById('resultTokenType').textContent = data.token_type || 'Bearer';
    document.getElementById('resultScope').textContent = data.scope || 'api';

    const expiresIn = data.expires_in || 0;
    let expiresHuman = `${expiresIn} seconds`;
    if (expiresIn >= 86400) expiresHuman += ` (${Math.floor(expiresIn/86400)}d ${Math.floor(expiresIn%86400/3600)}h)`;
    else if (expiresIn >= 3600) expiresHuman += ` (${Math.floor(expiresIn/3600)}h ${Math.floor(expiresIn%3600/60)}m)`;
    else if (expiresIn > 0) expiresHuman += ` (${Math.floor(expiresIn/60)}m)`;
    document.getElementById('resultExpiresIn').textContent = expiresHuman;

    const expiresAt = expiresIn > 0 ? new Date(Date.now() + expiresIn * 1000) : null;
    document.getElementById('resultExpiresAt').textContent = expiresAt ? expiresAt.toISOString().replace('T', ' ').substring(0, 19) + ' UTC' : 'N/A';

    document.getElementById('resultRefreshToken').textContent = data.refresh_token ? 'Available (saved)' : 'Not available';

    if (userData) {
        document.getElementById('resultUserRow').style.display = 'flex';
        document.getElementById('resultUser').textContent = `${userData.name} (@${userData.username}) - ${userData.email}`;
    }

    // Update instructions tab
    document.getElementById('instructionToken').style.display = 'inline-flex';
    document.getElementById('instructionTokenValue').textContent = data.access_token;
    document.getElementById('instructionUrl').style.display = 'inline-flex';
    document.getElementById('instructionUrlValue').textContent = gitlabUrl;

    resultSection.scrollIntoView({ behavior: 'smooth' });
}

function setStepState(stepId, state) {
    const el = document.getElementById(stepId);
    el.className = `step ${state}`;
}

function appendLog(container, message, type) {
    const line = document.createElement('div');
    line.className = `log-${type}`;
    const timestamp = new Date().toLocaleTimeString();
    line.textContent = `[${timestamp}] ${message}`;
    container.appendChild(line);
    container.scrollTop = container.scrollHeight;
}

// ---- Token Storage ----

function getSavedTokens() {
    try {
        return JSON.parse(localStorage.getItem(STORAGE_KEYS.TOKENS) || '[]');
    } catch { return []; }
}

function saveToken(tokenData) {
    const tokens = getSavedTokens();
    tokens.push(tokenData);
    localStorage.setItem(STORAGE_KEYS.TOKENS, JSON.stringify(tokens, null, 2));
    renderTokensList();
    refreshJsonViewer();
}

function deleteToken(index) {
    if (!confirm('Delete this token?')) return;
    const tokens = getSavedTokens();
    tokens.splice(index, 1);
    localStorage.setItem(STORAGE_KEYS.TOKENS, JSON.stringify(tokens, null, 2));
    updateTokenBadge();
    renderTokensList();
    refreshJsonViewer();
    checkRefreshSection();
    showToast('Token deleted.', 'info');
}

function clearSavedTokens() {
    if (!confirm('Delete all saved tokens? This cannot be undone.')) return;
    localStorage.removeItem(STORAGE_KEYS.TOKENS);
    updateTokenBadge();
    renderTokensList();
    refreshJsonViewer();
    checkRefreshSection();
    showToast('All tokens cleared.', 'info');
}

function updateTokenBadge() {
    const count = getSavedTokens().length;
    const badge = document.getElementById('tokenBadge');
    if (count > 0) {
        badge.style.display = 'inline';
        badge.textContent = count;
    } else {
        badge.style.display = 'none';
    }
}

function checkRefreshSection() {
    const tokens = getSavedTokens();
    const section = document.getElementById('refreshSection');
    const latest = tokens.length > 0 ? tokens[tokens.length - 1] : null;

    if (latest && latest.refreshToken) {
        section.style.display = 'block';
        const created = new Date(latest.createdAtUtc);
        const expiresAt = latest.expiresIn > 0 ? new Date(created.getTime() + latest.expiresIn * 1000) : null;
        const isExpired = expiresAt ? new Date() > expiresAt : false;
        const status = isExpired ? '<span style="color:var(--danger);font-weight:600">EXPIRED</span>' : '<span style="color:var(--success);font-weight:600">Valid</span>';

        document.getElementById('refreshInfo').innerHTML = `
            <div style="font-size:0.88rem;margin-bottom:12px;">
                <p><strong>GitLab:</strong> ${escapeHtml(latest.gitlabDomain)}</p>
                <p><strong>Created:</strong> ${created.toISOString().replace('T',' ').substring(0,19)} UTC</p>
                <p><strong>Status:</strong> ${status}</p>
                ${latest.user ? `<p><strong>User:</strong> ${escapeHtml(latest.user.name)} (@${escapeHtml(latest.user.username)})</p>` : ''}
            </div>
        `;
    } else {
        section.style.display = 'none';
    }
}

// ---- Token List Rendering ----

function renderTokensList() {
    const tokens = getSavedTokens();
    const container = document.getElementById('tokensList');
    const noTokens = document.getElementById('noTokensMessage');

    if (tokens.length === 0) {
        container.innerHTML = '';
        noTokens.style.display = 'block';
        return;
    }

    noTokens.style.display = 'none';
    container.innerHTML = tokens.map((t, i) => {
        const created = new Date(t.createdAtUtc);
        const expiresAt = t.expiresIn > 0 ? new Date(created.getTime() + t.expiresIn * 1000) : null;
        const isExpired = expiresAt ? new Date() > expiresAt : false;
        const maskedToken = t.accessToken.length > 20
            ? t.accessToken.substring(0, 20) + '****'
            : t.accessToken;

        return `
        <div class="token-card">
            <div class="token-card-header">
                <h3>${escapeHtml(t.gitlabDomain)}</h3>
                <span class="token-status ${isExpired ? 'expired' : 'valid'}">${isExpired ? 'Expired' : 'Valid'}</span>
            </div>
            <div class="token-card-details">
                <div class="token-card-detail">
                    <span class="label">Access Token</span>
                    <span><code>${escapeHtml(maskedToken)}</code></span>
                </div>
                <div class="token-card-detail">
                    <span class="label">Scope</span>
                    <span>${escapeHtml(t.scope || 'api')}</span>
                </div>
                <div class="token-card-detail">
                    <span class="label">Created</span>
                    <span>${created.toISOString().replace('T',' ').substring(0,19)} UTC</span>
                </div>
                <div class="token-card-detail">
                    <span class="label">Expires</span>
                    <span>${expiresAt ? expiresAt.toISOString().replace('T',' ').substring(0,19) + ' UTC' : 'N/A'}</span>
                </div>
                ${t.user ? `
                <div class="token-card-detail">
                    <span class="label">User</span>
                    <span>${escapeHtml(t.user.name)} (@${escapeHtml(t.user.username)})</span>
                </div>` : ''}
                <div class="token-card-detail">
                    <span class="label">Refresh Token</span>
                    <span>${t.refreshToken ? 'Available' : 'None'}</span>
                </div>
            </div>
            <div class="token-card-actions">
                <button class="btn btn-secondary btn-sm" onclick="copyText('${escapeJs(t.accessToken)}')">📋 Copy Token</button>
                <button class="btn btn-secondary btn-sm" onclick="useTokenInForm(${i})">🔄 Use Settings</button>
                <button class="btn btn-danger btn-sm" onclick="deleteToken(${i})">🗑️ Delete</button>
            </div>
        </div>`;
    }).join('');
}

function useTokenInForm(index) {
    const tokens = getSavedTokens();
    const t = tokens[index];
    if (!t) return;
    document.getElementById('gitlabUrl').value = t.gitlabDomain;
    document.getElementById('clientId').value = t.applicationId;
    document.getElementById('clientSecret').value = t.applicationSecret;
    switchTab('generate');
    showToast('Settings loaded from saved token.', 'success');
}

// ---- JSON Viewer ----

function refreshJsonViewer() {
    const viewer = document.getElementById('jsonViewer');
    const tokens = getSavedTokens();
    const settings = getSettings();

    const data = {
        settings: {
            gitlabUrl: settings.gitlabUrl || '',
            clientId: settings.clientId || '',
            scope: settings.scope || 'api',
            redirectUri: window.location.origin + window.location.pathname
        },
        tokens: tokens.map(t => ({
            gitlabDomain: t.gitlabDomain,
            accessToken: t.accessToken ? t.accessToken.substring(0, 15) + '...[masked]' : '',
            tokenType: t.tokenType,
            scope: t.scope,
            expiresIn: t.expiresIn,
            refreshToken: t.refreshToken ? '[present]' : '[none]',
            createdAtUtc: t.createdAtUtc,
            user: t.user || null
        }))
    };

    viewer.querySelector('code').textContent = JSON.stringify(data, null, 2);
}

function copyJsonViewer() {
    const tokens = getSavedTokens();
    const json = JSON.stringify(tokens, null, 2);
    navigator.clipboard.writeText(json).then(() => {
        showToast('JSON copied to clipboard (includes full tokens).', 'success');
    }).catch(() => {
        showToast('Failed to copy. Select and copy manually.', 'error');
    });
}

function exportTokenJson() {
    const tokens = getSavedTokens();
    if (tokens.length === 0) { showToast('No tokens to export.', 'error'); return; }
    const json = JSON.stringify(tokens, null, 2);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'gitlab_oauth_tokens.json';
    a.click();
    URL.revokeObjectURL(url);
    showToast('Tokens exported.', 'success');
}

function importTokenJson(event) {
    const file = event.target.files[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = (e) => {
        try {
            const imported = JSON.parse(e.target.result);
            const arr = Array.isArray(imported) ? imported : [imported];
            const existing = getSavedTokens();
            const merged = [...existing, ...arr];
            localStorage.setItem(STORAGE_KEYS.TOKENS, JSON.stringify(merged, null, 2));
            updateTokenBadge();
            renderTokensList();
            refreshJsonViewer();
            checkRefreshSection();
            showToast(`Imported ${arr.length} token(s).`, 'success');
        } catch (err) {
            showToast(`Import failed: ${err.message}`, 'error');
        }
    };
    reader.readAsText(file);
    event.target.value = '';
}

// ---- Utility Functions ----

function togglePassword(inputId, btn) {
    const input = document.getElementById(inputId);
    if (input.type === 'password') {
        input.type = 'text';
        btn.textContent = '🔒';
    } else {
        input.type = 'password';
        btn.textContent = '👁️';
    }
}

function copyToClipboard(elementId) {
    const text = document.getElementById(elementId).textContent;
    copyText(text);
}

function copyText(text) {
    navigator.clipboard.writeText(text).then(() => {
        showToast('Copied to clipboard!', 'success');
    }).catch(() => {
        // Fallback
        const ta = document.createElement('textarea');
        ta.value = text;
        document.body.appendChild(ta);
        ta.select();
        document.execCommand('copy');
        document.body.removeChild(ta);
        showToast('Copied to clipboard!', 'success');
    });
}

function showToast(message, type = 'info') {
    const toast = document.getElementById('toast');
    toast.textContent = message;
    toast.className = `toast ${type} show`;
    setTimeout(() => { toast.className = 'toast'; }, 3500);
}

function escapeHtml(str) {
    if (!str) return '';
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
}

function escapeJs(str) {
    if (!str) return '';
    return str.replace(/\\/g, '\\\\').replace(/'/g, "\\'").replace(/"/g, '\\"');
}
