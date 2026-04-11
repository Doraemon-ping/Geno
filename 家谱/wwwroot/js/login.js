const form = document.getElementById("loginForm");
        const message = document.getElementById("message");
        const submitButton = document.getElementById("submitButton");
        const redirect = new URLSearchParams(location.search).get("redirect");
        const existingToken = localStorage.getItem("token");

        function resolveRedirect() {
            if (!redirect) {
                return "index.html";
            }

            return redirect.startsWith("/") ? redirect.slice(1) : redirect;
        }

        async function requestJson(url, options = {}) {
            const response = await fetch(url, options);
            const payload = await response.json();

            if (!response.ok) {
                throw new Error(payload?.message || "登录失败");
            }

            return payload;
        }

        if (existingToken) {
            location.href = resolveRedirect();
        }

        form.addEventListener("submit", async event => {
            event.preventDefault();
            message.textContent = "";
            submitButton.disabled = true;
            submitButton.textContent = "正在校验身份...";

            const payload = {
                username: document.getElementById("username").value.trim(),
                password: document.getElementById("password").value
            };

            try {
                const result = await requestJson("/api/Account/login", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(payload)
                });

                if (result.code !== 200 || !result.data?.token) {
                    throw new Error(result.message || "登录失败");
                }

                localStorage.setItem("token", result.data.token);
                if (!document.getElementById("remember").checked) {
                    sessionStorage.setItem("login-ephemeral", "1");
                } else {
                    sessionStorage.removeItem("login-ephemeral");
                }

                submitButton.textContent = "登录成功，正在进入...";
                location.href = resolveRedirect();
            } catch (error) {
                message.textContent = error.message;
                submitButton.disabled = false;
                submitButton.textContent = "登录并进入";
            }
        });
