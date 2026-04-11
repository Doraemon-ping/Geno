const token = localStorage.getItem("token");
        const modal = document.getElementById("modal");
        const message = document.getElementById("message");
        let trees = [];
        let editingId = "";

        if (!token) {
            location.href = "login.html?redirect=my-trees.html";
        }

        function authHeaders() {
            return {
                Authorization: `Bearer ${token}`,
                "Content-Type": "application/json"
            };
        }

        async function requestJson(url, options = {}) {
            const response = await fetch(url, options);
            const contentType = response.headers.get("content-type") || "";
            const payload = contentType.includes("application/json") ? await response.json() : await response.text();
            if (!response.ok) {
                const errorMessage = typeof payload === "string" ? payload : payload?.message;
                throw new Error(errorMessage || "请求失败");
            }
            return payload;
        }

        function setMessage(text, success = false) {
            message.textContent = text || "";
            message.className = success ? "message success" : "message";
        }

        function resetForm() {
            editingId = "";
            document.getElementById("modalTitle").textContent = "提交新建家谱树申请";
            document.getElementById("treeName").value = "";
            document.getElementById("ancestorName").value = "";
            document.getElementById("region").value = "";
            document.getElementById("description").value = "";
            document.getElementById("isPublic").checked = false;
        }

        function updateStats() {
            document.getElementById("treeCount").textContent = trees.length;
            document.getElementById("publicCount").textContent = trees.filter(tree => tree.isPublic).length;
            document.getElementById("poemCount").textContent = trees.reduce((sum, tree) => sum + (tree.poems?.length || 0), 0);
        }

        function workflowMessage(result, fallback) {
            return result?.data?.message || result?.message || fallback;
        }

        function renderTrees() {
            updateStats();
            const grid = document.getElementById("treeGrid");
            if (!trees.length) {
                grid.innerHTML = '<div class="empty">你还没有可访问的卷宗，点击上方按钮先提交一份新树申请。</div>';
                return;
            }

            grid.innerHTML = trees.map(tree => {
                const access = tree.access || {};
                const canEdit = Boolean(access.canEdit);
                const canSubmitChange = Boolean(access.canSubmitChange);
                const roleName = access.roleName || "访客";
                return `
                    <div class="tree-card">
                        <div class="tree-top">
                            <h3>${tree.treeName}</h3>
                            <div class="stack">
                                <span class="tag ${tree.isPublic ? "public" : "private"}">${tree.isPublic ? "公开" : "私有"}</span>
                                <span class="tag role">${roleName}</span>
                            </div>
                        </div>
                        <div class="tree-meta">
                            始祖：${tree.ancestorName || "未载"}<br>
                            地区：${tree.region || "未标注"}<br>
                            字辈数：${tree.poems?.length || 0}
                        </div>
                        <div class="tree-desc">${tree.description || "当前卷宗尚未补充说明。"}</div>
                        <div class="tree-actions">
                            <a href="tree-detail.html?id=${tree.treeID}">进入卷宗</a>
                            ${canSubmitChange ? `<button class="secondary" onclick="editTree('${tree.treeID}')">${canEdit ? "修改" : "提交修改"}</button>` : ""}
                            ${canSubmitChange ? `<button class="danger" onclick="deleteTree('${tree.treeID}')">${canEdit ? "删除" : "提交删除"}</button>` : ""}
                        </div>
                        <div class="note">
                            ${canEdit ? "你拥有这棵树的直接维护权限，修改会立即生效。" : canSubmitChange ? "你当前可参与修谱，但提交的树信息变更会先进入审核。" : "你当前只有查看权限，如需参与修谱，请先申请树内角色。"}
                        </div>
                    </div>
                `;
            }).join("");
        }

        async function loadTrees() {
            try {
                const result = await requestJson("/api/GenoTree/my-trees", {
                    headers: { Authorization: `Bearer ${token}` }
                });
                trees = Array.isArray(result.data) ? result.data : [];
                renderTrees();
            } catch (error) {
                document.getElementById("treeGrid").innerHTML = `<div class="empty">${error.message}</div>`;
            }
        }

        function openModal() {
            modal.classList.add("show");
        }

        function closeModal() {
            modal.classList.remove("show");
            resetForm();
        }

        function editTree(id) {
            const tree = trees.find(item => item.treeID === id);
            if (!tree) return;
            editingId = id;
            document.getElementById("modalTitle").textContent = "修改家谱树";
            document.getElementById("treeName").value = tree.treeName || "";
            document.getElementById("ancestorName").value = tree.ancestorName || "";
            document.getElementById("region").value = tree.region || "";
            document.getElementById("description").value = tree.description || "";
            document.getElementById("isPublic").checked = Boolean(tree.isPublic);
            openModal();
        }

        async function saveTree() {
            setMessage("");
            const payload = {
                treeName: document.getElementById("treeName").value.trim(),
                ancestorName: document.getElementById("ancestorName").value.trim(),
                region: document.getElementById("region").value.trim(),
                description: document.getElementById("description").value.trim(),
                isPublic: document.getElementById("isPublic").checked
            };

            if (!payload.treeName) {
                setMessage("请先填写卷宗名称。");
                return;
            }

            try {
                let result;
                if (editingId) {
                    result = await requestJson(`/api/GenoTree/Update/${editingId}`, {
                        method: "PUT",
                        headers: authHeaders(),
                        body: JSON.stringify(payload)
                    });
                } else {
                    result = await requestJson("/api/GenoTree/Add", {
                        method: "POST",
                        headers: authHeaders(),
                        body: JSON.stringify(payload)
                    });
                }

                setMessage(workflowMessage(result, "操作成功"), true);
                closeModal();
                loadTrees();
            } catch (error) {
                setMessage(error.message);
            }
        }

        async function deleteTree(id) {
            if (!confirm("确定删除此卷宗吗？")) return;
            try {
                const result = await requestJson(`/api/GenoTree/Del/${id}`, {
                    method: "DELETE",
                    headers: { Authorization: `Bearer ${token}` }
                });
                setMessage(workflowMessage(result, "操作成功"), true);
                loadTrees();
            } catch (error) {
                setMessage(error.message);
            }
        }

        document.getElementById("createBtn").addEventListener("click", openModal);
        document.getElementById("cancelBtn").addEventListener("click", closeModal);
        document.getElementById("saveBtn").addEventListener("click", saveTree);
        document.getElementById("logoutBtn").addEventListener("click", () => {
            localStorage.removeItem("token");
            location.href = "index.html";
        });
        modal.addEventListener("click", event => {
            if (event.target === modal) closeModal();
        });

        loadTrees();
        window.editTree = editTree;
        window.deleteTree = deleteTree;
