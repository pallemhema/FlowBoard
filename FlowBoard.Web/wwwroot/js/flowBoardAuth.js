window.flowBoardAuth = {
  // ============================================================
  // LOGIN
  // ============================================================
  login: async function (email, password) {
    const response = await fetch("/auth/login", {
      method: "POST",
      credentials: "include",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        email: email,
        password: password,
      }),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => null);

      console.log("LOGIN HTTP STATUS:", response.status);
      console.log("LOGIN ERROR OBJECT:", error);
      console.log("LOGIN ERROR MESSAGE:", error?.message);
      console.log("LOGIN ERROR CODE:", error?.code);

      return {
        authentication: null,
        errorMessage: error?.message || "Invalid email or password.",
      };
    }

    return {
      authentication: await response.json(),
      errorMessage: null,
    };
  },

  // ============================================================
  // REFRESH
  // ============================================================
  refresh: async function () {
    const response = await fetch("/auth/refresh", {
      method: "POST",
      credentials: "include",
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
    const response = await fetch("/auth/logout", {
      method: "POST",
      credentials: "include",
    });

    return response.ok;
  },
};
