window.flowBoardAuth = {

    // ============================================================
    // LOGIN
    // ============================================================
    login: async function (email, password) {

        const response = await fetch('/auth/login', {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                email: email,
                password: password
            })
        });

        if (!response.ok) {
            return null;
        }

        return await response.json();
    },


    // ============================================================
    // REGISTER
    // Browser → FlowBoard.Web /auth/register
    // ============================================================
    register: async function (request) {

        const response = await fetch('/auth/register', {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(request)
        });

        if (!response.ok) {
            return null;
        }

        return await response.json();
    },


    // ============================================================
    // REFRESH
    // ============================================================
    refresh: async function () {

        const response = await fetch('/auth/refresh', {
            method: 'POST',
            credentials: 'include'
        });

        if (!response.ok) {
            return null;
        }

        return await response.json();
    },


    // ============================================================
    // LOGOUT
    // ============================================================
    logout: async function () {

        const response = await fetch('/auth/logout', {
            method: 'POST',
            credentials: 'include'
        });

        return response.ok;
    }
};