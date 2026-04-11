const token = localStorage.getItem("token");

        function authHeaders() {
            return token ? { Authorization: `Bearer ${token}` } : {};
        }

        async function requestJson(url, options = {}) {
            const response = await fetch(url, options);
            const contentType = response.headers.get("content-type") || "";
            const payload = contentType.includes("application/json") ? await response.json() : await response.text();

            if (!response.ok) {
                const message = typeof payload === "string" ? payload : payload?.message;
                throw new Error(message || "请求失败");
            }

            return payload;
        }

        async function loadProfile() {
            if (!token) {
                return;
            }

            try {
                const result = await requestJson("/api/Account/profile", { headers: authHeaders() });
                const user = result.data;
                document.getElementById("myTreesNav").style.display = "inline";
                document.getElementById("profileNav").style.display = "inline";
                document.getElementById("userName").style.display = "inline";
                document.getElementById("logoutBtn").style.display = "inline";
                document.getElementById("loginNav").style.display = "none";
                document.getElementById("registerNav").style.display = "none";
                document.getElementById("userName").textContent = user.username;

                if (user.roleType <= 1) {
                    document.getElementById("auditNav").style.display = "inline";
                }
            } catch {
                localStorage.removeItem("token");
            }
        }

        async function loadPublicTrees() {
            try {
                const result = await requestJson("/api/GenoTree/GetAll");
                const list = Array.isArray(result.data) ? result.data : [];
                const trees = list.filter(item => item.isPublic).slice(0, 5);
                const container = document.getElementById("publicTrees");

                if (!trees.length) {
                    container.innerHTML = '<div class="empty">暂无公开卷宗。</div>';
                    return;
                }

                container.innerHTML = trees.map(tree => `
                    <div class="public-item">
                        <div>
                            <div class="item-title">${tree.treeName}</div>
                            <div class="item-meta">始祖：${tree.ancestorName || "不详"} · 地区：${tree.region || "未知"} · 字辈数：${tree.poems?.length || 0}</div>
                        </div>
                        <a class="link-button" href="${token ? `tree-detail.html?id=${tree.treeID}` : `login.html?redirect=${encodeURIComponent(`tree-detail.html?id=${tree.treeID}`)}`}">
                            ${token ? "查阅" : "登录后查阅"}
                        </a>
                    </div>
                `).join("");
            } catch (error) {
                document.getElementById("publicTrees").innerHTML = `<div class="empty">${error.message}</div>`;
            }
        }

        function escapeHtml(value) {
            return String(value ?? "")
                .replace(/&/g, "&amp;")
                .replace(/</g, "&lt;")
                .replace(/>/g, "&gt;")
                .replace(/"/g, "&quot;")
                .replace(/'/g, "&#39;");
        }

        function formatDate(value) {
            if (!value) return "";
            const date = new Date(value);
            return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleDateString("zh-CN");
        }

        async function loadAnnouncements() {
            const container = document.getElementById("homeAnnouncements");
            if (!container) return;

            try {
                const result = await requestJson("/api/Announcement/public?page=1&pageSize=3");
                const data = result.data || result.Data || {};
                const items = Array.isArray(data.items || data.Items) ? (data.items || data.Items) : [];

                if (!items.length) {
                    container.innerHTML = '<p>暂无系统公告。</p>';
                    return;
                }

                container.innerHTML = items.map(item => {
                    const title = item.title || item.Title || "未命名公告";
                    const category = item.category || item.Category || "系统公告";
                    const publishedAt = item.publishedAt || item.PublishedAt;
                    const pinned = Boolean(item.isPinned ?? item.IsPinned);
                    return `<p>${pinned ? "置顶" : "公告"} <b>${escapeHtml(formatDate(publishedAt))}</b>：<a href="announcements.html">${escapeHtml(`[${category}] ${title}`)}</a></p>`;
                }).join("");
            } catch (error) {
                container.innerHTML = `<p>${escapeHtml(error.message)}</p>`;
            }
        }

        function goToMyTrees() {
            location.href = token ? "my-trees.html" : "login.html?redirect=my-trees.html";
        }

        function goToProfile() {
            location.href = token ? "profile.html" : "login.html?redirect=profile.html";
        }

        document.getElementById("logoutBtn").addEventListener("click", () => {
            localStorage.removeItem("token");
            location.href = "index.html";
        });

        loadProfile();
        loadPublicTrees();
        loadAnnouncements();
