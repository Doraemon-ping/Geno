const form = document.getElementById("registerForm");
        const message = document.getElementById("message");
        const sendCodeButton = document.getElementById("sendCodeButton");
        const submitButton = document.getElementById("submitButton");

        async function requestJson(url, options = {}) {
            const response = await fetch(url, options);
            const contentType = response.headers.get("content-type") || "";
            const payload = contentType.includes("application/json") ? await response.json() : await response.text();

            if (!response.ok) {
                const messageText = typeof payload === "string" ? payload : payload?.message;
                throw new Error(messageText || "请求失败");
            }

            return payload;
        }

        function setMessage(text, type = "") {
            message.textContent = text || "";
            message.className = type ? `message ${type}` : "message";
        }

        async function sendCode() {
            setMessage("");
            const email = document.getElementById("email").value.trim();

            if (!email) {
                setMessage("请先填写邮箱。");
                return;
            }

            try {
                sendCodeButton.disabled = true;
                await requestJson("/api/Account/send-code", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(email)
                });
                setMessage("验证码已发送，请查收邮箱。", "success");

                let remaining = 60;
                sendCodeButton.textContent = `${remaining}s 后重发`;
                const timer = setInterval(() => {
                    remaining -= 1;
                    if (remaining <= 0) {
                        clearInterval(timer);
                        sendCodeButton.disabled = false;
                        sendCodeButton.textContent = "发送验证码";
                        return;
                    }
                    sendCodeButton.textContent = `${remaining}s 后重发`;
                }, 1000);
            } catch (error) {
                sendCodeButton.disabled = false;
                sendCodeButton.textContent = "发送验证码";
                setMessage(error.message);
            }
        }

        form.addEventListener("submit", async event => {
            event.preventDefault();
            setMessage("");

            const password = document.getElementById("password").value;
            const confirmPassword = document.getElementById("confirmPassword").value;
            if (password !== confirmPassword) {
                setMessage("两次输入的密码不一致。");
                return;
            }

            const payload = {
                username: document.getElementById("username").value.trim(),
                password,
                email: document.getElementById("email").value.trim(),
                phone: document.getElementById("phone").value.trim()
            };

            const code = document.getElementById("code").value.trim();
            if (!code) {
                setMessage("请先填写邮箱验证码。");
                return;
            }

            try {
                submitButton.disabled = true;
                submitButton.textContent = "正在提交...";
                await requestJson(`/api/Account/register?code=${encodeURIComponent(code)}`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(payload)
                });
                setMessage("注册成功，正在前往登录页。", "success");
                setTimeout(() => {
                    location.href = "login.html";
                }, 800);
            } catch (error) {
                submitButton.disabled = false;
                submitButton.textContent = "提交注册";
                setMessage(error.message);
            }
        });

        sendCodeButton.addEventListener("click", sendCode);
