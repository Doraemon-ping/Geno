const token = localStorage.getItem("token");

        if (!token) {
            location.href = "login.html?redirect=" + encodeURIComponent("admin-audit.html");
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
                    { label: "伴侣 1", value: getValue(change, "partner1.fullName", "partner1Name", "Partner1Name") },
                    { label: "伴侣 2", value: getValue(change, "partner2.fullName", "partner2Name", "Partner2Name") },
                    { label: "子女成员", value: getValue(change, "childName", "ChildName") || getValue(target, "fullName", "FullName") },
                    { label: "关系类型", value: getValue(change, "relTypeName", "RelTypeName") },
                    { label: "排行", value: getValue(change, "childOrder", "ChildOrder") }
                ]));
                if (task.reason) {
                    sections.push(renderSection("提交说明", [
                        { label: "申请理由", value: task.reason }
                    ]));
                }
            }

            if (task.reviewNotes) {
                sections.push(renderSection("审核备注", [
                    { label: "备注内容", value: task.reviewNotes }
                ]));
            }

            return sections.join("");
        }

        function renderTask(task) {
            return `
                <article class="task-card">
                    <div class="top-row">
                        <strong>${escapeHtml(actionTitle(task.actionCode))}</strong>
                        <span class="${statusClass(task.status)}">${escapeHtml(task.status || "未知状态")}</span>
                    </div>
                    <div class="meta">
                        <span>提交人：${escapeHtml(task.submitterName || "未知用户")}</span>
                        <span>提交时间：${escapeHtml(task.createTime || "未记录")}</span>
                        <span>申请理由：${escapeHtml(task.reason || "未填写")}</span>
                    </div>
                    ${renderTaskDetail(task)}
                    <div class="field">
                        <label for="note-${task.taskId}">审核备注</label>
                        <textarea id="note-${task.taskId}" placeholder="可填写通过说明或驳回原因"></textarea>
                    </div>
                    <div class="button-row">
                        <button class="action-button" onclick="processTask('${task.taskId}', 1)">通过并执行</button>
                        <button class="danger-button" onclick="processTask('${task.taskId}', 2)">驳回</button>
                    </div>
                </article>
            `;
        }

        function tableLabel(tableName) {
            const map = {
                "Sys_Users": "系统用户",
                "Sys_Review_Tasks": "审核任务",
                "Geno_Trees": "家谱树",
                "Geno_Generation_Poems": "字辈",
                "Geno_Tree_Permissions": "树权限",
                "Geno_Members": "树成员",
                "Geno_Unions": "婚姻单元",
                "Geno_Union_Members": "家庭子女关联"
            };
            return map[tableName] || tableName;
        }

        function opTypeLabel(opType) {
            const value = (opType || "").toUpperCase();
            if (value === "CREATE") return "新增";
            if (value === "UPDATE") return "修改";
            if (value === "DELETE") return "删除";
            return opType || "未知操作";
        }

        function snapshotItems(tableName, rawData) {
            const data = toObject(rawData) || {};
            switch (tableName) {
                case "Sys_Users":
                    return [
                        { label: "用户名", value: getValue(data, "username", "Username") },
                        { label: "邮箱", value: getValue(data, "email", "Email") },
                        { label: "电话", value: getValue(data, "phone", "Phone") },
                        { label: "角色", value: getValue(data, "roleName", "RoleName") || roleText(getValue(data, "roleType", "RoleType")) },
                        { label: "状态", value: Number(getValue(data, "userStatus", "UserStatus")) === 1 ? "启用" : (getValue(data, "userStatus", "UserStatus") == null ? "" : "禁用") }
                    ];
                case "Sys_Review_Tasks":
                    return [
                        { label: "任务类型", value: actionTitle(getValue(data, "actionCode", "ActionCode")) },
                        { label: "任务状态", value: Number(getValue(data, "status", "Status")) === 0 ? "待审核" : Number(getValue(data, "status", "Status")) === 1 ? "审核通过" : Number(getValue(data, "status", "Status")) === 2 ? "审核驳回" : (getValue(data, "status", "Status") == null ? "" : "已撤回") },
                        { label: "提交人 ID", value: getValue(data, "submitterID", "SubmitterID") },
                        { label: "审核人 ID", value: getValue(data, "reviewerID", "ReviewerID") },
                        { label: "申请理由", value: getValue(data, "applyReason", "ApplyReason") },
                        { label: "审核备注", value: getValue(data, "reviewNotes", "ReviewNotes") }
                    ];
                case "Geno_Trees":
                    return [
                        { label: "家谱树名称", value: getValue(data, "treeName", "TreeName") },
                        { label: "始迁祖", value: getValue(data, "ancestorName", "AncestorName") },
                        { label: "所属地区", value: getValue(data, "region", "Region") },
                        { label: "是否公开", value: getValue(data, "isPublic", "IsPublic") == null ? "" : boolText(Boolean(getValue(data, "isPublic", "IsPublic"))) },
                        { label: "是否删除", value: getValue(data, "isDel", "IsDel", "isDeleted", "IsDeleted") == null ? "" : boolText(Boolean(getValue(data, "isDel", "IsDel", "isDeleted", "IsDeleted"))) }
                    ];
                case "Geno_Generation_Poems":
                    return [
                        { label: "世代序号", value: getValue(data, "generationNum", "GenerationNum") },
                        { label: "字辈", value: getValue(data, "word", "Word") },
                        { label: "释义", value: getValue(data, "meaning", "Meaning") },
                        { label: "是否删除", value: getValue(data, "isDel", "IsDel", "isDeleted", "IsDeleted") == null ? "" : boolText(Boolean(getValue(data, "isDel", "IsDel", "isDeleted", "IsDeleted"))) }
                    ];
                case "Geno_Tree_Permissions":
                    return [
                        { label: "家谱树", value: getValue(data, "treeName", "TreeName") || getValue(data, "treeID", "TreeID", "treeId", "TreeId") },
                        { label: "授权用户", value: getValue(data, "username", "Username", "userName", "UserName") || getValue(data, "userID", "UserID", "userId", "UserId") },
                        { label: "角色", value: treeRoleText(getValue(data, "roleType", "RoleType")) },
                        { label: "是否有效", value: getValue(data, "isActive", "IsActive") == null ? "" : boolText(Boolean(getValue(data, "isActive", "IsActive"))) }
                    ];
                case "Geno_Members":
                    return [
                        { label: "成员姓名", value: getValue(data, "fullName", "FullName") || `${getValue(data, "lastName", "LastName") || ""}${getValue(data, "firstName", "FirstName") || ""}` },
                        { label: "世代序号", value: getValue(data, "generationNum", "GenerationNum") },
                        { label: "性别", value: genderText(getValue(data, "gender", "Gender")) },
                        { label: "生卒信息", value: getValue(data, "birthDateRaw", "BirthDateRaw") },
                        { label: "人物简介", value: getValue(data, "biography", "Biography") }
                    ];
                case "Geno_Unions":
                    return [
                        { label: "伴侣 1", value: getValue(data, "partner1Name", "Partner1Name") },
                        { label: "伴侣 2", value: getValue(data, "partner2Name", "Partner2Name") },
                        { label: "婚姻类型", value: getValue(data, "unionTypeName", "UnionTypeName") },
                        { label: "排序", value: getValue(data, "sortOrder", "SortOrder") },
                        { label: "结婚日期", value: getValue(data, "marriageDate", "MarriageDate") },
                        { label: "是否删除", value: getValue(data, "isDel", "IsDel") == null ? "" : boolText(Boolean(getValue(data, "isDel", "IsDel"))) }
                    ];
                case "Geno_Union_Members":
                    return [
                        { label: "伴侣 1", value: getValue(data, "partner1Name", "Partner1Name") },
                        { label: "伴侣 2", value: getValue(data, "partner2Name", "Partner2Name") },
                        { label: "子女成员", value: getValue(data, "childName", "ChildName") },
                        { label: "关系类型", value: getValue(data, "relTypeName", "RelTypeName") },
                        { label: "排行", value: getValue(data, "childOrder", "ChildOrder") },
                        { label: "是否删除", value: getValue(data, "isDel", "IsDel") == null ? "" : boolText(Boolean(getValue(data, "isDel", "IsDel"))) }
                    ];
                default:
                    return Object.entries(data).map(([key, value]) => ({
                        label: key,
                        value: typeof value === "object" ? JSON.stringify(value) : value
                    }));
            }
        }

        function renderSnapshot(title, tableName, data) {
            const items = snapshotItems(tableName, data).filter(item => item.value != null && item.value !== "");
            return `
                <div class="snapshot-box">
                    <strong>${escapeHtml(title)}</strong>
                    ${
                        items.length
                            ? `
                                <div class="snapshot-grid">
                                    ${items.map(item => `
                                        <div class="snapshot-item">
                                            <span class="snapshot-label">${escapeHtml(item.label)}</span>
                                            <div class="snapshot-value">${escapeHtml(item.value)}</div>
                                        </div>
                                    `).join("")}
                                </div>
                            `
                            : '<div class="snapshot-value">暂无数据</div>'
                    }
                </div>
            `;
        }

        function renderLog(log) {
            return `
                <article class="log-card">
                    <div class="top-row">
                        <strong>${escapeHtml(tableLabel(log.targetTable))} · ${escapeHtml(opTypeLabel(log.opType))}</strong>
                        <span class="status-badge">${escapeHtml(log.createdAt || "未记录")}</span>
                    </div>
                    <div class="meta">
                        <span>操作人：${escapeHtml(log.operatorName || "系统")}</span>
                        <span>目标表：${escapeHtml(tableLabel(log.targetTable))}</span>
                        <span>目标记录：${escapeHtml(log.targetId || "")}</span>
                        <span>关联任务：${escapeHtml(log.actionName || "无")}</span>
                    </div>
                    <div class="two-column">
                        ${renderSnapshot("变更前", log.targetTable, log.beforeData)}
                        ${renderSnapshot("变更后", log.targetTable, log.afterData)}
                    </div>
                </article>
            `;
        }

        function updateStats(tasks, logs) {
            document.getElementById("pendingCount").textContent = String(tasks.length);
            document.getElementById("applyCount").textContent = String(tasks.filter(item =>
                item.actionCode === "ApplyAdmin" || item.actionCode === "Tree.ApplyRole"
            ).length);
            document.getElementById("contentCount").textContent = String(tasks.filter(item =>
                item.actionCode !== "ApplyAdmin" && item.actionCode !== "Tree.ApplyRole"
            ).length);
            document.getElementById("logCount").textContent = String(logs.length);
        }

        async function loadTasks() {
            const result = await requestJson("/api/Task/get-all", {
                method: "POST",
                headers: authHeaders()
            });
            const tasks = Array.isArray(result.data || result.Data) ? (result.data || result.Data) : [];

            document.getElementById("taskList").innerHTML = tasks.length
                ? tasks.map(renderTask).join("")
                : '<div class="empty">当前没有你可处理的待审核任务。</div>';

            return tasks;
        }

        async function loadLogs() {
            const result = await requestJson("/api/DataLog/list?take=80", {
                headers: authHeaders()
            });
            const logs = Array.isArray(result.data || result.Data) ? (result.data || result.Data) : [];

            document.getElementById("logList").innerHTML = logs.length
                ? logs.map(renderLog).join("")
                : '<div class="empty">当前没有数据库操作日志。</div>';

            return logs;
        }

        async function processTask(taskId, action) {
            const notes = document.getElementById(`note-${taskId}`)?.value?.trim() || "";
            try {
                await requestJson("/api/Task/process", {
                    method: "POST",
                    headers: authHeaders(true),
                    body: JSON.stringify({ taskId, action, notes })
                });

                const [tasks, logs] = await Promise.all([loadTasks(), loadLogs()]);
                updateStats(tasks, logs);
            } catch (error) {
                alert(error.message);
            }
        }

        document.getElementById("logoutButton").addEventListener("click", () => {
            localStorage.removeItem("token");
            location.href = "index.html";
        });

        Promise.all([loadTasks(), loadLogs()])
            .then(([tasks, logs]) => updateStats(tasks, logs))
            .catch(error => {
                document.getElementById("taskList").innerHTML = `<div class="error">${escapeHtml(error.message)}</div>`;
                document.getElementById("logList").innerHTML = `<div class="error">${escapeHtml(error.message)}</div>`;
            });

        window.processTask = processTask;
