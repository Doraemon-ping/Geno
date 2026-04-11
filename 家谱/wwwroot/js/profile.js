const token = localStorage.getItem("token");
        const state = {
            profile: null,
            avatarImage: null,
            avatarScale: 1,
            avatarOffsetX: 0,
            avatarOffsetY: 0,
            avatarDragging: false,
            avatarDragStart: null
        };

        if (!token) {
            location.href = "login.html?redirect=profile.html";
        }

        function authHeaders(json = false) {
            const headers = { Authorization: `Bearer ${token}` };
            if (json) {
                headers["Content-Type"] = "application/json";
            }
            return headers;
        }

        async function requestJson(url, options = {}) {
            const response = await fetch(url, options);
            const contentType = response.headers.get("content-type") || "";
            const payload = contentType.includes("application/json") ? await response.json() : await response.text();

            if (!response.ok) {
                const message = typeof payload === "string" ? payload : payload?.message || payload?.Message;
                throw new Error(message || "请求失败");
            }

            return payload;
        }

        function escapeHtml(value) {
            return String(value ?? "")
                .replace(/&/g, "&amp;")
                .replace(/</g, "&lt;")
                .replace(/>/g, "&gt;")
                .replace(/"/g, "&quot;")
                .replace(/'/g, "&#39;");
        }

        function updateAvatarPreview(name, avatarUrl) {
            const preview = document.getElementById("avatarPreview");
            const displayName = document.getElementById("profileDisplayName");
            const safeName = String(name || "用户").trim();
            const safeUrl = String(avatarUrl || "").trim();
            displayName.textContent = safeName;
            preview.textContent = safeName ? safeName[0] : "用";
            if (safeUrl) {
                preview.innerHTML = `<img src="${escapeHtml(safeUrl)}" alt="${escapeHtml(safeName)}" onerror="this.parentElement.textContent='${escapeHtml(safeName ? safeName[0] : "用")}'">`;
            }
        }

        function drawAvatarCrop() {
            const canvas = document.getElementById("avatarCropCanvas");
            const context = canvas.getContext("2d");
            const image = state.avatarImage;
            context.clearRect(0, 0, canvas.width, canvas.height);
            context.fillStyle = "#f4eadc";
            context.fillRect(0, 0, canvas.width, canvas.height);
            if (!image) return;

            const base = Math.max(canvas.width / image.width, canvas.height / image.height);
            const scale = base * state.avatarScale;
            const width = image.width * scale;
            const height = image.height * scale;
            const x = (canvas.width - width) / 2 + state.avatarOffsetX;
            const y = (canvas.height - height) / 2 + state.avatarOffsetY;

            context.save();
            context.beginPath();
            context.arc(canvas.width / 2, canvas.height / 2, canvas.width / 2, 0, Math.PI * 2);
            context.clip();
            context.drawImage(image, x, y, width, height);
            context.restore();
        }

        function openAvatarCrop(file) {
            if (!file || !file.type.startsWith("image/")) {
                setMessage("profileMessage", "请选择图片文件");
                return;
            }

            const reader = new FileReader();
            reader.onload = () => {
                const image = new Image();
                image.onload = () => {
                    state.avatarImage = image;
                    state.avatarScale = 1;
                    state.avatarOffsetX = 0;
                    state.avatarOffsetY = 0;
                    document.getElementById("avatarZoom").value = "1";
                    document.getElementById("avatarCropModal").classList.add("show");
                    drawAvatarCrop();
                };
                image.src = reader.result;
            };
            reader.readAsDataURL(file);
        }

        async function uploadCroppedAvatar() {
            const canvas = document.getElementById("avatarCropCanvas");
            if (!state.avatarImage) return;
            canvas.toBlob(async blob => {
                if (!blob) {
                    setMessage("profileMessage", "头像裁剪失败，请重新选择图片");
                    return;
                }

                const formData = new FormData();
                formData.append("file", blob, "avatar.webp");
                try {
                    const result = await requestJson("/api/Account/avatar/upload", {
                        method: "POST",
                        headers: authHeaders(),
                        body: formData
                    });
                    const data = result.data || result.Data || {};
                    const avatarUrl = data.avatarUrl || data.AvatarUrl || "";
                    document.getElementById("avatarUrl").value = avatarUrl;
                    updateAvatarPreview(document.getElementById("username").value, avatarUrl);
                    document.getElementById("avatarCropModal").classList.remove("show");
                    setMessage("profileMessage", "头像上传成功", true);
                } catch (error) {
                    setMessage("profileMessage", error.message);
                }
            }, "image/webp", 0.92);
        }

        function openAvatarCrop(file) {
            if (!file || !file.type.startsWith("image/")) {
                setMessage("profileMessage", "请选择图片文件");
                return;
            }

            const reader = new FileReader();
            reader.onload = () => {
                const image = new Image();
                image.onload = () => {
                    state.avatarImage = image;
                    state.avatarScale = 1;
                    state.avatarOffsetX = 0;
                    state.avatarOffsetY = 0;
                    document.getElementById("avatarZoom").value = "1";
                    document.getElementById("avatarCropModal").classList.add("show");
                    drawAvatarCrop();
                };
                image.src = reader.result;
            };
            reader.readAsDataURL(file);
        }

        async function uploadCroppedAvatar() {
            const canvas = document.getElementById("avatarCropCanvas");
            if (!state.avatarImage) return;
            canvas.toBlob(async blob => {
                if (!blob) {
                    setMessage("profileMessage", "头像裁剪失败，请重新选择图片");
                    return;
                }

                const formData = new FormData();
                formData.append("file", blob, "avatar.webp");
                try {
                    const result = await requestJson("/api/Account/avatar/upload", {
                        method: "POST",
                        headers: authHeaders(),
                        body: formData
                    });
                    const data = result.data || result.Data || {};
                    const avatarUrl = data.avatarUrl || data.AvatarUrl || "";
                    document.getElementById("avatarUrl").value = avatarUrl;
                    updateAvatarPreview(document.getElementById("username").value, avatarUrl);
                    document.getElementById("avatarCropModal").classList.remove("show");
                    setMessage("profileMessage", "头像上传成功", true);
                } catch (error) {
                    setMessage("profileMessage", error.message);
                }
            }, "image/webp", 0.92);
        }

        function openAvatarCrop(file) {
            if (!file || !file.type.startsWith("image/")) {
                setMessage("profileMessage", "\u8bf7\u9009\u62e9\u56fe\u7247\u6587\u4ef6");
                return;
            }

            const reader = new FileReader();
            reader.onload = () => {
                const image = new Image();
                image.onload = () => {
                    state.avatarImage = image;
                    state.avatarScale = 1;
                    state.avatarOffsetX = 0;
                    state.avatarOffsetY = 0;
                    document.getElementById("avatarZoom").value = "1";
                    document.getElementById("avatarCropModal").classList.add("show");
                    drawAvatarCrop();
                };
                image.src = reader.result;
            };
            reader.readAsDataURL(file);
        }

        async function uploadCroppedAvatar() {
            const canvas = document.getElementById("avatarCropCanvas");
            if (!state.avatarImage) return;
            canvas.toBlob(async blob => {
                if (!blob) {
                    setMessage("profileMessage", "\u5934\u50cf\u88c1\u526a\u5931\u8d25\uff0c\u8bf7\u91cd\u65b0\u9009\u62e9\u56fe\u7247");
                    return;
                }

                const formData = new FormData();
                formData.append("file", blob, "avatar.webp");
                try {
                    const result = await requestJson("/api/Account/avatar/upload", {
                        method: "POST",
                        headers: authHeaders(),
                        body: formData
                    });
                    const data = result.data || result.Data || {};
                    const avatarUrl = data.avatarUrl || data.AvatarUrl || "";
                    document.getElementById("avatarUrl").value = avatarUrl;
                    updateAvatarPreview(document.getElementById("username").value, avatarUrl);
                    document.getElementById("avatarCropModal").classList.remove("show");
                    setMessage("profileMessage", "\u5934\u50cf\u4e0a\u4f20\u6210\u529f", true);
                } catch (error) {
                    setMessage("profileMessage", error.message);
                }
            }, "image/webp", 0.92);
        }

        function toObject(value) {
            if (value == null) {
                return null;
            }

            if (typeof value === "string") {
                try {
                    return JSON.parse(value);
                } catch {
                    return { value };
                }
            }

            return value;
        }

        function getValue(source, ...names) {
            const data = toObject(source);
            if (!data || typeof data !== "object") {
                return undefined;
            }

            for (const name of names) {
                if (String(name).includes(".")) {
                    const parts = String(name).split(".");
                    let current = data;
                    let matched = true;
                    for (const part of parts) {
                        if (current && typeof current === "object") {
                            const directKey = Object.keys(current).find(key => key.toLowerCase() === part.toLowerCase());
                            if (directKey) {
                                current = current[directKey];
                                continue;
                            }
                        }
                        matched = false;
                        break;
                    }

                    if (matched) {
                        return current;
                    }
                }

                if (Object.prototype.hasOwnProperty.call(data, name)) {
                    return data[name];
                }

                const matchedKey = Object.keys(data).find(key => key.toLowerCase() === String(name).toLowerCase());
                if (matchedKey) {
                    return data[matchedKey];
                }
            }

            return undefined;
        }

        function roleText(roleType) {
            const value = Number(roleType);
            if (value === 0) return "超级管理员";
            if (value === 1) return "管理员";
            if (value === 2) return "修谱员";
            return "访客";
        }

        function treeRoleText(roleType) {
            const value = Number(roleType);
            if (value === 1) return "树管理员";
            if (value === 2) return "修谱员";
            return "普通成员";
        }

        function genderText(gender) {
            const value = Number(gender);
            if (value === 1) return "男";
            if (value === 2) return "女";
            return "未知";
        }

        function boolText(value) {
            return value ? "是" : "否";
        }

        function actionTitle(actionCode) {
            const map = {
                "ApplyAdmin": "申请系统权限",
                "Tree.ApplyRole": "申请树内权限",
                "Tree.Create": "新建家谱树",
                "Tree.Update": "修改家谱树",
                "Tree.Delete": "删除家谱树",
                "Poem.Create": "新增字辈",
                "Poem.Update": "修改字辈",
                "Poem.Delete": "删除字辈",
                "Member.Create": "新增树成员",
                "Member.Update": "修改树成员",
                "Member.Delete": "删除树成员",
                "Union.Create": "新增婚姻单元",
                "Union.Delete": "删除婚姻单元",
                "UnionMember.Add": "新增家庭子女关联",
                "UnionMember.Delete": "删除家庭子女关联"
            };
            return map[actionCode] || actionCode || "未命名任务";
        }

        function statusClass(status) {
            if (status === "审核通过") return "status-badge status-approved";
            if (status === "审核驳回") return "status-badge status-rejected";
            return "status-badge";
        }

        function renderDetailGrid(items) {
            const validItems = items.filter(item => item && item.value != null && item.value !== "");
            if (!validItems.length) {
                return "<div class=\"detail-value\">暂无数据</div>";
            }

            return `
                <div class="detail-grid">
                    ${validItems.map(item => `
                        <div class="detail-item">
                            <span class="detail-label">${escapeHtml(item.label)}</span>
                            <div class="detail-value">${escapeHtml(item.value)}</div>
                        </div>
                    `).join("")}
                </div>
            `;
        }

        function renderSection(title, items) {
            return `
                <div class="detail-section">
                    <strong>${escapeHtml(title)}</strong>
                    ${renderDetailGrid(items)}
                </div>
            `;
        }

        function renderGenericObject(title, source) {
            const data = toObject(source);
            if (!data || typeof data !== "object") {
                return "";
            }

            const labels = {
                treeName: "家谱树名称",
                ancestorName: "始迁祖",
                region: "所属地区",
                description: "简介",
                username: "用户名",
                email: "邮箱",
                phone: "电话",
                roleName: "角色",
                generationNum: "世代序号",
                word: "字辈",
                meaning: "释义",
                fullName: "成员姓名",
                firstName: "名",
                lastName: "姓",
                birthDateRaw: "生卒信息",
                biography: "人物简介",
                newRole: "申请角色",
                isPublic: "是否公开",
                isDeleted: "是否删除"
            };

            const items = Object.entries(data).map(([key, value]) => {
                let displayValue = value;
                if (key === "roleType") displayValue = roleText(value);
                if (key === "newRole") displayValue = key.toLowerCase().includes("role") ? roleText(value) : value;
                if (key === "isPublic" || key === "isDeleted") displayValue = boolText(Boolean(value));
                return {
                    label: labels[key] || key,
                    value: typeof displayValue === "object" ? JSON.stringify(displayValue) : displayValue
                };
            });

            return renderSection(title, items);
        }

        function renderTaskDetail(task) {
            const change = toObject(task.changeData) || {};
            const tree = toObject(task.treeSummary) || {};
            const target = toObject(task.targetSummary) || {};
            const submitter = toObject(task.submitter) || {};
            const sections = [];

            if (task.actionCode === "ApplyAdmin") {
                sections.push(renderSection("申请用户", [
                    { label: "用户名", value: getValue(target, "username", "Username") || getValue(submitter, "username", "Username") || task.submitterName },
                    { label: "当前角色", value: getValue(target, "roleName", "RoleName") || getValue(submitter, "roleName", "RoleName") },
                    { label: "邮箱", value: getValue(target, "email", "Email") || getValue(submitter, "email", "Email") },
                    { label: "电话", value: getValue(target, "phone", "Phone") || getValue(submitter, "phone", "Phone") }
                ]));
                sections.push(renderSection("申请内容", [
                    { label: "目标角色", value: roleText(getValue(change, "newRole", "NewRole")) },
                    { label: "申请理由", value: task.reason || "未填写" }
                ]));
            } else if (task.actionCode === "Tree.ApplyRole") {
                sections.push(renderSection("目标家谱树", [
                    { label: "家谱树名称", value: getValue(tree, "treeName", "TreeName") },
                    { label: "始迁祖", value: getValue(tree, "ancestorName", "AncestorName") },
                    { label: "所属地区", value: getValue(tree, "region", "Region") },
                    { label: "是否公开", value: getValue(tree, "isPublic", "IsPublic") == null ? "" : boolText(Boolean(getValue(tree, "isPublic", "IsPublic"))) }
                ]));
                sections.push(renderSection("申请对象", [
                    { label: "用户名", value: getValue(target, "username", "Username") },
                    { label: "邮箱", value: getValue(target, "email", "Email") },
                    { label: "电话", value: getValue(target, "phone", "Phone") },
                    { label: "申请权限", value: treeRoleText(getValue(change, "newRole", "NewRole")) }
                ]));
                sections.push(renderSection("申请说明", [
                    { label: "申请理由", value: task.reason || "未填写" }
                ]));
            } else if (task.actionCode === "Tree.Create" || task.actionCode === "Tree.Update" || task.actionCode === "Tree.Delete") {
                sections.push(renderSection("家谱树信息", [
                    { label: "家谱树名称", value: getValue(tree, "treeName", "TreeName") || getValue(change, "treeName", "TreeName") },
                    { label: "始迁祖", value: getValue(tree, "ancestorName", "AncestorName") || getValue(change, "ancestorName", "AncestorName") },
                    { label: "所属地区", value: getValue(tree, "region", "Region") || getValue(change, "region", "Region") },
                    { label: "是否公开", value: getValue(tree, "isPublic", "IsPublic") != null ? boolText(Boolean(getValue(tree, "isPublic", "IsPublic"))) : (getValue(change, "isPublic", "IsPublic") != null ? boolText(Boolean(getValue(change, "isPublic", "IsPublic"))) : "") },
                    { label: "简介", value: getValue(tree, "description", "Description") || getValue(change, "description", "Description") }
                ]));
                if (task.reason) {
                    sections.push(renderSection("提交说明", [
                        { label: "申请理由", value: task.reason }
                    ]));
                }
            } else if (task.actionCode === "Poem.Create" || task.actionCode === "Poem.Update" || task.actionCode === "Poem.Delete") {
                sections.push(renderSection("字辈信息", [
                    { label: "所属家谱树", value: getValue(tree, "treeName", "TreeName") },
                    { label: "世代序号", value: getValue(target, "generationNum", "GenerationNum") || getValue(change, "generationNum", "GenerationNum") },
                    { label: "字辈", value: getValue(target, "word", "Word") || getValue(change, "word", "Word") },
                    { label: "释义", value: getValue(target, "meaning", "Meaning") || getValue(change, "meaning", "Meaning") }
                ]));
                if (task.reason) {
                    sections.push(renderSection("提交说明", [
                        { label: "申请理由", value: task.reason }
                    ]));
                }
            } else if (task.actionCode === "Member.Create" || task.actionCode === "Member.Update" || task.actionCode === "Member.Delete") {
                sections.push(renderSection("成员信息", [
                    { label: "成员姓名", value: getValue(target, "fullName", "FullName") || `${getValue(change, "lastName", "LastName") || ""}${getValue(change, "firstName", "FirstName") || ""}` },
                    { label: "所属家谱树", value: getValue(tree, "treeName", "TreeName") },
                    { label: "世代序号", value: getValue(target, "generationNum", "GenerationNum") || getValue(change, "generationNum", "GenerationNum") },
                    { label: "性别", value: genderText(getValue(target, "gender", "Gender") ?? getValue(change, "gender", "Gender")) },
                    { label: "生卒信息", value: getValue(target, "birthDateRaw", "BirthDateRaw") || getValue(change, "birthDateRaw", "BirthDateRaw") },
                    { label: "人物简介", value: getValue(target, "biography", "Biography") || getValue(change, "biography", "Biography") }
                ]));
                if (task.reason) {
                    sections.push(renderSection("提交说明", [
                        { label: "申请理由", value: task.reason }
                    ]));
                }
            } else if (task.actionCode === "Union.Create" || task.actionCode === "Union.Delete") {
                sections.push(renderSection("婚姻单元信息", [
                    { label: "所属家谱树", value: getValue(tree, "treeName", "TreeName") },
                    { label: "伴侣 1", value: getValue(target, "partner1Name", "Partner1Name") || getValue(change, "partner1Name", "Partner1Name") || getValue(change, "partner1Id", "Partner1Id") },
                    { label: "伴侣 2", value: getValue(target, "partner2Name", "Partner2Name") || getValue(change, "partner2Name", "Partner2Name") || getValue(change, "partner2Id", "Partner2Id") },
                    { label: "婚姻类型", value: getValue(target, "unionTypeName", "UnionTypeName") || getValue(change, "unionTypeName", "UnionTypeName") },
                    { label: "排序", value: getValue(target, "sortOrder", "SortOrder") || getValue(change, "sortOrder", "SortOrder") },
                    { label: "结婚日期", value: getValue(target, "marriageDate", "MarriageDate") || getValue(change, "marriageDate", "MarriageDate") }
                ]));
                if (task.reason) {
                    sections.push(renderSection("提交说明", [
                        { label: "申请理由", value: task.reason }
                    ]));
                }
            } else if (task.actionCode === "UnionMember.Add" || task.actionCode === "UnionMember.Delete") {
                sections.push(renderSection("家庭子女关联", [
                    { label: "所属家谱树", value: getValue(tree, "treeName", "TreeName") },
                    { label: "伴侣 1", value: getValue(change, "partner1.fullName", "partner1Name", "Partner1Name") || getValue(change, "partner1Name", "Partner1Name") },
                    { label: "伴侣 2", value: getValue(change, "partner2.fullName", "partner2Name", "Partner2Name") || getValue(change, "partner2Name", "Partner2Name") },
                    { label: "子女成员", value: getValue(change, "childName", "ChildName") || getValue(target, "fullName", "FullName") },
                    { label: "关系类型", value: getValue(change, "relTypeName", "RelTypeName") },
                    { label: "排行", value: getValue(change, "childOrder", "ChildOrder") }
                ]));
                if (task.reason) {
                    sections.push(renderSection("提交说明", [
                        { label: "申请理由", value: task.reason }
                    ]));
                }
            } else {
                sections.push(renderGenericObject("任务内容", target || change));
            }

            if (task.reviewNotes) {
                sections.push(renderSection("审核备注", [
                    { label: "备注内容", value: task.reviewNotes }
                ]));
            }

            return sections.join("");
        }

        function renderTaskCard(task) {
            return `
                <article class="task-card">
                    <div class="task-header">
                        <strong>${escapeHtml(actionTitle(task.actionCode))}</strong>
                        <span class="${statusClass(task.status)}">${escapeHtml(task.status || "未知状态")}</span>
                    </div>
                    <div class="task-meta">
                        <span>提交人：${escapeHtml(task.submitterName || "未知用户")}</span>
                        <span>创建时间：${escapeHtml(task.createTime || "未记录")}</span>
                        <span>处理人：${escapeHtml(task.reviewName || "待处理")}</span>
                        <span>处理时间：${escapeHtml(task.processTime || "待处理")}</span>
                    </div>
                    ${renderTaskDetail(task)}
                </article>
            `;
        }

        function setMessage(id, text, success = false) {
            const element = document.getElementById(id);
            element.textContent = text || "";
            element.className = success ? "message success" : "message";
        }

        async function loadProfile() {
            const result = await requestJson("/api/Account/profile", { headers: authHeaders() });
            const profile = result.data || result.Data;
            state.profile = profile;
            if (!document.getElementById("myApplicationsEntry")) {
                const row = document.querySelector(".button-row");
                if (row) {
                    const link = document.createElement("a");
                    link.id = "myApplicationsEntry";
                    link.className = "ghost-button";
                    link.href = "my-applications.html";
                    link.textContent = "我的申请";
                    row.appendChild(link);
                }
            }

            document.getElementById("heroTitle").textContent = `${profile.username} · 宗务总览`;
            document.getElementById("heroSubtitle").textContent = `当前角色为 ${profile.roleName}，你可以在此维护资料、申请权限，并查看审核记录。`;
            document.getElementById("username").value = profile.username || "";
            document.getElementById("avatarUrl").value = profile.avatarUrl || profile.AvatarUrl || "";
            updateAvatarPreview(profile.username, profile.avatarUrl || profile.AvatarUrl || "");
            document.getElementById("roleName").value = profile.roleName || "";
            document.getElementById("userId").value = profile.userId || "";
            document.getElementById("email").value = profile.email || "";
            document.getElementById("phone").value = profile.phone || "";
            document.getElementById("roleNameSummary").textContent = profile.roleName || "--";

            if (Number(profile.roleType) <= 1) {
                document.getElementById("auditNav").style.display = "inline";
                document.getElementById("auditEntry").style.display = "inline-flex";
            }
        }

        async function loadMyTasks() {
            try {
                const result = await requestJson("/api/Task/my-tasks", { headers: authHeaders() });
                const tasks = Array.isArray(result.data || result.Data) ? (result.data || result.Data) : [];
                document.getElementById("taskCountSummary").textContent = String(tasks.length);
                document.getElementById("pendingCountSummary").textContent = String(tasks.filter(item => item.status === "待审核").length);
                document.getElementById("taskList").innerHTML = tasks.length
                    ? tasks.map(renderTaskCard).join("")
                    : '<div class="empty">你目前还没有审核记录。</div>';
            } catch (error) {
                document.getElementById("taskList").innerHTML = `<div class="empty">${escapeHtml(error.message)}</div>`;
            }
        }

        async function loadReviewTasks() {
            if (!state.profile || Number(state.profile.roleType) > 1) {
                return;
            }

            try {
                const result = await requestJson("/api/Task/get-all", {
                    method: "POST",
                    headers: authHeaders()
                });
                const tasks = Array.isArray(result.data || result.Data) ? (result.data || result.Data) : [];
                document.getElementById("reviewSection").style.display = "block";
                document.getElementById("reviewTaskList").innerHTML = tasks.length
                    ? tasks.slice(0, 3).map(renderTaskCard).join("")
                    : '<div class="empty">当前没有你可处理的待办任务。</div>';
            } catch (error) {
                document.getElementById("reviewSection").style.display = "block";
                document.getElementById("reviewTaskList").innerHTML = `<div class="empty">${escapeHtml(error.message)}</div>`;
            }
        }

        async function saveProfile() {
            setMessage("profileMessage", "");
            const payload = {
                username: document.getElementById("username").value.trim(),
                email: document.getElementById("email").value.trim(),
                phone: document.getElementById("phone").value.trim(),
                avatarUrl: document.getElementById("avatarUrl").value.trim()
            };

            try {
                await requestJson("/api/Account/update-profile", {
                    method: "PUT",
                    headers: authHeaders(true),
                    body: JSON.stringify(payload)
                });
                updateAvatarPreview(payload.username, payload.avatarUrl);
                setMessage("profileMessage", "资料保存成功。", true);
            } catch (error) {
                setMessage("profileMessage", error.message);
            }
        }

        async function submitApply() {
            setMessage("applyMessage", "");
            const payload = {
                newRole: Number(document.getElementById("targetRole").value),
                reason: document.getElementById("applyReason").value.trim()
            };

            if (!payload.reason) {
                setMessage("applyMessage", "请先填写申请理由。");
                return;
            }

            try {
                await requestJson("/api/Apply/apply-admin", {
                    method: "POST",
                    headers: authHeaders(true),
                    body: JSON.stringify(payload)
                });
                setMessage("applyMessage", "申请已提交，请等待审核。", true);
                document.getElementById("applyReason").value = "";
                await loadMyTasks();
            } catch (error) {
                setMessage("applyMessage", error.message);
            }
        }

        async function loadMyTasks() {
            try {
                const [allResult, pendingResult] = await Promise.all([
                    requestJson("/api/Task/my-submissions?page=1&pageSize=1", { headers: authHeaders() }),
                    requestJson("/api/Task/my-submissions?page=1&pageSize=1&status=0", { headers: authHeaders() })
                ]);

                const allData = allResult.data || allResult.Data || {};
                const pendingData = pendingResult.data || pendingResult.Data || {};
                const totalCount = Number(allData.totalCount || allData.TotalCount || 0);
                const pendingCount = Number(pendingData.totalCount || pendingData.TotalCount || 0);

                document.getElementById("taskCountSummary").textContent = String(totalCount);
                document.getElementById("pendingCountSummary").textContent = String(pendingCount);
                document.getElementById("taskList").innerHTML = `
                    <article class="task-card">
                        <div class="task-header">
                            <strong>我的申请</strong>
                            <span class="status-badge">${totalCount} 条记录</span>
                        </div>
                        <div class="task-meta">
                            <span>待审核申请：${pendingCount} 条</span>
                            <span>你提交过的系统权限、树权限和内容变更申请已集中到独立页面。</span>
                        </div>
                        <div class="button-row">
                            <a class="ghost-button" href="my-applications.html">打开我的申请</a>
                        </div>
                    </article>
                `;
            } catch (error) {
                document.getElementById("taskList").innerHTML = `<div class="empty">${escapeHtml(error.message)}</div>`;
            }
        }

        function ensureProfileLink(id, text, href, containerSelector) {
            if (document.getElementById(id)) {
                return document.getElementById(id);
            }

            const container = document.querySelector(containerSelector);
            if (!container) {
                return null;
            }

            const link = document.createElement("a");
            link.id = id;
            link.className = "ghost-button";
            link.href = href;
            link.textContent = text;
            container.appendChild(link);
            return link;
        }

        const __originalLoadProfile = loadProfile;
        loadProfile = async function () {
            await __originalLoadProfile();

            ensureProfileLink("myApplicationsEntry", "我的申请", "my-applications.html", ".button-row");

            if (state.profile && Number(state.profile.roleType) <= 1) {
                const auditNav = document.getElementById("auditNav");
                const auditEntry = document.getElementById("auditEntry");
                if (auditNav) {
                    auditNav.href = "review-tasks.html";
                    auditNav.textContent = "审核任务";
                }
                if (auditEntry) {
                    auditEntry.href = "review-tasks.html";
                    auditEntry.textContent = "进入审核任务";
                    auditEntry.style.display = "inline-flex";
                }

                ensureProfileLink("dataLogsEntry", "数据库日志", "data-logs.html", ".content-grid .button-row:last-child");
            }
        };

        loadReviewTasks = async function () {
            if (!state.profile || Number(state.profile.roleType) > 1) {
                return;
            }

            let pendingCount = 0;
            try {
                const result = await requestJson("/api/Task/get-all", {
                    method: "POST",
                    headers: authHeaders()
                });
                const tasks = Array.isArray(result.data || result.Data) ? (result.data || result.Data) : [];
                pendingCount = tasks.length;
            } catch {
                pendingCount = 0;
            }

            document.getElementById("reviewSection").style.display = "block";
            document.getElementById("reviewTaskList").innerHTML = `
                <article class="task-card">
                    <div class="task-header">
                        <strong>审核工作台</strong>
                        <span class="status-badge">${pendingCount} 条待办</span>
                    </div>
                    <div class="task-meta">
                        <span>审核任务与数据库日志已拆分为两个独立页面。</span>
                        <span>你可以分别进入审核任务页和数据库日志页进行处理与检索。</span>
                    </div>
                    <div class="button-row">
                        <a class="ghost-button" href="review-tasks.html">打开审核任务</a>
                        <a class="ghost-button" href="data-logs.html">打开数据库日志</a>
                    </div>
                </article>
            `;
        };

        const avatarInput = document.getElementById("avatarUrl");
        document.getElementById("username").addEventListener("input", () => updateAvatarPreview(document.getElementById("username").value, avatarInput?.value || ""));
        document.getElementById("avatarPreview").addEventListener("click", () => document.getElementById("avatarFileInput").click());
        document.getElementById("chooseAvatarBtn").addEventListener("click", () => document.getElementById("avatarFileInput").click());
        document.getElementById("avatarFileInput").addEventListener("change", event => openAvatarCrop(event.target.files?.[0]));
        document.getElementById("avatarZoom").addEventListener("input", event => {
            state.avatarScale = Number(event.target.value) || 1;
            drawAvatarCrop();
        });
        document.getElementById("avatarCropCanvas").addEventListener("pointerdown", event => {
            state.avatarDragging = true;
            state.avatarDragStart = { x: event.clientX, y: event.clientY, ox: state.avatarOffsetX, oy: state.avatarOffsetY };
            event.currentTarget.setPointerCapture(event.pointerId);
        });
        document.getElementById("avatarCropCanvas").addEventListener("pointermove", event => {
            if (!state.avatarDragging || !state.avatarDragStart) return;
            state.avatarOffsetX = state.avatarDragStart.ox + event.clientX - state.avatarDragStart.x;
            state.avatarOffsetY = state.avatarDragStart.oy + event.clientY - state.avatarDragStart.y;
            drawAvatarCrop();
        });
        document.getElementById("avatarCropCanvas").addEventListener("pointerup", () => {
            state.avatarDragging = false;
            state.avatarDragStart = null;
        });
        document.getElementById("uploadAvatarBtn").addEventListener("click", uploadCroppedAvatar);
        document.getElementById("cancelAvatarBtn").addEventListener("click", () => document.getElementById("avatarCropModal").classList.remove("show"));
        document.getElementById("saveProfileBtn").addEventListener("click", saveProfile);
        document.getElementById("applyBtn").addEventListener("click", submitApply);
        document.getElementById("logoutBtn").addEventListener("click", () => {
            localStorage.removeItem("token");
            location.href = "index.html";
        });

        loadProfile()
            .then(async () => {
                await loadMyTasks();
                await loadReviewTasks();
            })
            .catch(() => {
                localStorage.removeItem("token");
                location.href = "login.html?redirect=profile.html";
            });
