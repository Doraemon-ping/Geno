const token = localStorage.getItem("token");
        let allTrees = [];

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

        function initNav() {
            if (!token) return;
            document.getElementById("myTreesNav").style.display = "inline";
            document.getElementById("profileNav").style.display = "inline";
            document.getElementById("loginNav").style.display = "none";
            document.getElementById("registerNav").style.display = "none";
            document.getElementById("logoutBtn").style.display = "inline";
            document.getElementById("identityTag").innerHTML = "当前身份：<b>已登录族人</b>";
            document.getElementById("logoutBtn").addEventListener("click", () => {
                localStorage.removeItem("token");
                location.reload();
            });
        }

        async function tryLoadProfile() {
            if (!token) return;
            try {
                const result = await requestJson("/api/Account/profile", { headers: authHeaders() });
                if (result.data.roleType === 0) {
                    document.getElementById("auditNav").style.display = "inline";
                }
            } catch {
                localStorage.removeItem("token");
            }
        }

        function renderTrees(list) {
            document.getElementById("treeCount").textContent = list.length;
            document.getElementById("poemCount").textContent = list.reduce((sum, item) => sum + (item.poems?.length || 0), 0);

            const grid = document.getElementById("treeGrid");
            if (!list.length) {
                grid.innerHTML = '<div class="empty">未找到符合条件的公开卷宗。</div>';
                return;
            }

            grid.innerHTML = list.map(tree => `
                <div class="tree-card">
                    <div class="tree-cover">
                        <span>公开卷宗</span>
                        <strong>${(tree.treeName || "赵氏家谱").slice(0, 6)}</strong>
                    </div>
                    <div class="tree-body">
                        <h3>${tree.treeName}</h3>
                        <p>${tree.description || "当前卷宗已开放目录浏览，登录后可进入详情页查看字辈内容。"}</p>
                        <div class="tree-meta">
                            始祖：${tree.ancestorName || "未载"}<br>
                            地区：${tree.region || "未标注"}<br>
                            字辈数：${tree.poems?.length || 0}
                        </div>
                    </div>
                    <div class="tree-footer">
                        <span style="font-size:12px;color:#999">赵氏公开谱系</span>
                        <a class="link-button" href="${token ? `tree-detail.html?id=${tree.treeID}` : `login.html?redirect=${encodeURIComponent(`tree-detail.html?id=${tree.treeID}`)}`}">
                            ${token ? "查阅" : "登录后查阅"}
                        </a>
                    </div>
                </div>
            `).join("");
        }

        function fillRegionFilter(list) {
            const regionFilter = document.getElementById("regionFilter");
            const regions = [...new Set(list.map(item => item.region).filter(Boolean))];
            regionFilter.innerHTML = '<option value="">全部地区</option>' + regions.map(region => `<option value="${region}">${region}</option>`).join("");
        }

        function applyFilters() {
            const keyword = document.getElementById("searchInput").value.trim().toLowerCase();
            const region = document.getElementById("regionFilter").value;
            const filtered = allTrees.filter(tree => {
                const hitKeyword = !keyword || [tree.treeName, tree.ancestorName, tree.region, tree.description].filter(Boolean).some(value => String(value).toLowerCase().includes(keyword));
                const hitRegion = !region || tree.region === region;
                return hitKeyword && hitRegion;
            });
            renderTrees(filtered);
        }

        async function loadTrees() {
            try {
                const result = await requestJson("/api/GenoTree/GetAll", { headers: authHeaders() });
                allTrees = Array.isArray(result.data) ? result.data.filter(item => item.isPublic) : [];
                fillRegionFilter(allTrees);
                renderTrees(allTrees);
            } catch (error) {
                document.getElementById("treeGrid").innerHTML = `<div class="empty">${error.message}</div>`;
            }
        }

        document.getElementById("searchInput").addEventListener("input", applyFilters);
        document.getElementById("regionFilter").addEventListener("change", applyFilters);

        initNav();
        tryLoadProfile();
        loadTrees();
