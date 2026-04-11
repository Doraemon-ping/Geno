const form = document.getElementById("resetForm");
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
                await requestJson("/api/Account/forgot-password-code", {
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

            const email = document.getElementById("email").value.trim();
            const code = document.getElementById("code").value.trim();
            const password = document.getElementById("password").value;
            const confirmPassword = document.getElementById("confirmPassword").value;

            if (password !== confirmPassword) {
                setMessage("两次输入的新密码不一致。");
                return;
            }

            try {
                submitButton.disabled = true;
                submitButton.textContent = "正在提交...";
                await requestJson("/api/Account/reset-password", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({
                        email,
                        code,
                        newPassword: password
                    })
                });
                setMessage("密码已重置，正在返回登录页。", "success");
                setTimeout(() => {
                    location.href = "login.html";
                }, 800);
            } catch (error) {
                submitButton.disabled = false;
                submitButton.textContent = "确认重置";
                setMessage(error.message);
            }
        });

        sendCodeButton.addEventListener("click", sendCode);
