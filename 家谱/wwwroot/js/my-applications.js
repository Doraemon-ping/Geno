const token = localStorage.getItem("token");
    const state = { page: 1, pageSize: 10, totalCount: 0, totalPages: 0, pendingCount: 0 };
    if (!token) location.href = "login.html?redirect=my-applications.html";

    function authHeaders() { return { Authorization: `Bearer ${token}` }; }
    function escapeHtml(value) { return String(value ?? "").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/\"/g,"&quot;").replace(/'/g,"&#39;"); }
    async function requestJson(url, options = {}) { const response = await fetch(url, options); const type = response.headers.get("content-type") || ""; const payload = type.includes("application/json") ? await response.json() : await response.text(); if (!response.ok) throw new Error(typeof payload === "string" ? payload : (payload?.message || payload?.Message || "请求失败")); return payload; }
    function toObject(value) { if (value == null) return null; if (typeof value === "string") { try { return JSON.parse(value); } catch { return { value }; } } return value; }
    function getValue(source, ...names) {
      const data = toObject(source);
      if (!data || typeof data !== "object") return undefined;
      for (const name of names) {
        if (Object.prototype.hasOwnProperty.call(data, name)) return data[name];
        const matched = Object.keys(data).find(key => key.toLowerCase() === String(name).toLowerCase());
        if (matched) return data[matched];
      }
      return undefined;
    }
    function roleText(roleType) { const value = Number(roleType); if (value === 0) return "超级管理员"; if (value === 1) return "管理员"; if (value === 2) return "修谱员"; return "访客"; }
    function treeRoleText(roleType) { const value = Number(roleType); if (value === 1) return "树管理员"; if (value === 2) return "修谱员"; return "普通成员"; }
    function genderText(gender) { const value = Number(gender); if (value === 1) return "男"; if (value === 2) return "女"; return "未知"; }
    function boolText(value) { return value ? "是" : "否"; }
    function actionTitle(actionCode) {
      const map = {
        "ApplyAdmin":"申请系统权限","Tree.ApplyRole":"申请树内权限","Tree.Create":"新建家谱树","Tree.Update":"修改家谱树","Tree.Delete":"删除家谱树",
        "Poem.Create":"新增字辈","Poem.Update":"修改字辈","Poem.Delete":"删除字辈","Member.Create":"新增树成员","Member.Update":"修改树成员","Member.Delete":"删除树成员",
        "Union.Create":"新增婚姻单元","Union.Delete":"删除婚姻单元","UnionMember.Add":"新增家庭子女关联","UnionMember.Delete":"删除家庭子女关联"
      };
      return map[actionCode] || actionCode || "未命名任务";
    }
    function statusClass(status) { if (status === "审核通过") return "status-badge status-approved"; if (status === "审核驳回") return "status-badge status-rejected"; return "status-badge"; }
    function renderDetailGrid(items) {
      const valid = items.filter(item => item && item.value != null && item.value !== "");
      if (!valid.length) return '<div class="detail-value">暂无数据</div>';
      return `<div class="detail-grid">${valid.map(item => `<div><span class="detail-label">${escapeHtml(item.label)}</span><div class="detail-value">${escapeHtml(item.value)}</div></div>`).join("")}</div>`;
    }
    function renderSection(title, items) { return `<div class="detail-section"><strong>${escapeHtml(title)}</strong>${renderDetailGrid(items)}</div>`; }
    function renderTaskDetail(task) {
      const change = toObject(task.changeData) || {};
      const tree = toObject(task.treeSummary) || {};
      const target = toObject(task.targetSummary) || {};
      const sections = [];
      if (task.actionCode === "ApplyAdmin") {
        sections.push(renderSection("申请内容", [
          { label: "目标角色", value: roleText(getValue(change, "newRole", "NewRole")) },
          { label: "申请理由", value: task.reason || "未填写" },
          { label: "申请用户", value: getValue(target, "username", "Username") || task.submitterName }
        ]));
      } else if (task.actionCode === "Tree.ApplyRole") {
        sections.push(renderSection("树权限申请", [
          { label: "家谱树", value: getValue(tree, "treeName", "TreeName") },
          { label: "申请角色", value: treeRoleText(getValue(change, "newRole", "NewRole")) },
          { label: "申请理由", value: task.reason || "未填写" }
        ]));
      } else if (task.actionCode.startsWith("Tree.")) {
        sections.push(renderSection("家谱树信息", [
          { label: "家谱树名称", value: getValue(tree, "treeName", "TreeName") || getValue(change, "treeName", "TreeName") },
          { label: "始祖", value: getValue(tree, "ancestorName", "AncestorName") || getValue(change, "ancestorName", "AncestorName") },
          { label: "地区", value: getValue(tree, "region", "Region") || getValue(change, "region", "Region") },
          { label: "是否公开", value: getValue(tree, "isPublic", "IsPublic") != null ? boolText(Boolean(getValue(tree, "isPublic", "IsPublic"))) : "" },
          { label: "说明", value: task.reason || getValue(change, "description", "Description") }
        ]));
      } else if (task.actionCode.startsWith("Poem.")) {
        sections.push(renderSection("字辈信息", [
          { label: "家谱树", value: getValue(tree, "treeName", "TreeName") },
          { label: "代数", value: getValue(target, "generationNum", "GenerationNum") || getValue(change, "generationNum", "GenerationNum") },
          { label: "字辈", value: getValue(target, "word", "Word") || getValue(change, "word", "Word") },
          { label: "释义", value: getValue(target, "meaning", "Meaning") || getValue(change, "meaning", "Meaning") }
        ]));
      } else if (task.actionCode.startsWith("Member.")) {
        sections.push(renderSection("成员信息", [
          { label: "成员姓名", value: getValue(target, "fullName", "FullName") || `${getValue(change, "lastName", "LastName") || ""}${getValue(change, "firstName", "FirstName") || ""}` },
          { label: "家谱树", value: getValue(tree, "treeName", "TreeName") },
          { label: "代数", value: getValue(target, "generationNum", "GenerationNum") || getValue(change, "generationNum", "GenerationNum") },
          { label: "性别", value: genderText(getValue(target, "gender", "Gender") ?? getValue(change, "gender", "Gender")) },
          { label: "出生信息", value: getValue(target, "birthDateRaw", "BirthDateRaw") || getValue(change, "birthDateRaw", "BirthDateRaw") },
          { label: "成员简介", value: getValue(target, "biography", "Biography") || getValue(change, "biography", "Biography") }
        ]));
      } else if (task.actionCode.startsWith("Union")) {
        sections.push(renderSection("婚姻单元信息", [
          { label: "家谱树", value: getValue(tree, "treeName", "TreeName") },
          { label: "伴侣 1", value: getValue(target, "partner1Name", "Partner1Name") || getValue(change, "partner1Name", "Partner1Name") },
          { label: "伴侣 2", value: getValue(target, "partner2Name", "Partner2Name") || getValue(change, "partner2Name", "Partner2Name") },
          { label: "婚姻类型", value: getValue(target, "unionTypeName", "UnionTypeName") || getValue(change, "unionTypeName", "UnionTypeName") },
          { label: "说明", value: task.reason || getValue(change, "relTypeName", "RelTypeName") }
        ]));
      }
      if (task.reviewNotes) sections.push(renderSection("审核备注", [{ label: "备注内容", value: task.reviewNotes }]));
      return sections.join("");
    }
    function renderTaskCard(task) {
      return `<article class="task-card">
        <div class="task-header">
          <strong>${escapeHtml(actionTitle(task.actionCode))}</strong>
          <span class="${statusClass(task.status)}">${escapeHtml(task.status || "未知状态")}</span>
        </div>
        <div class="task-meta">
          <span>提交时间：${escapeHtml(task.createTime || "未记录")}</span>
          <span>处理人：${escapeHtml(task.reviewName || "待处理")}</span>
          <span>处理时间：${escapeHtml(task.processTime || "待处理")}</span>
        </div>
        ${renderTaskDetail(task)}
      </article>`;
    }
    async function loadPendingCount() {
      const result = await requestJson("/api/Task/my-submissions?page=1&pageSize=1&status=0", { headers: authHeaders() });
      const data = result.data || result.Data || {};
      state.pendingCount = Number(data.totalCount || data.TotalCount || 0);
      document.getElementById("pendingCount").textContent = String(state.pendingCount);
    }
    async function loadApplications() {
      document.getElementById("message").textContent = "正在读取申请记录...";
      const result = await requestJson(`/api/Task/my-submissions?page=${state.page}&pageSize=${state.pageSize}`, { headers: authHeaders() });
      const data = result.data || result.Data || {};
      const items = Array.isArray(data.items || data.Items) ? (data.items || data.Items) : [];
      state.totalCount = Number(data.totalCount || data.TotalCount || 0);
      state.totalPages = Number(data.totalPages || data.TotalPages || 0);
      state.page = Number(data.page || data.Page || state.page);
      document.getElementById("message").textContent = "";
      document.getElementById("totalCount").textContent = String(state.totalCount);
      document.getElementById("pageInfo").textContent = `第 ${state.page} / ${Math.max(state.totalPages, 1)} 页`;
      document.getElementById("pagerText").textContent = `共 ${state.totalCount} 条记录`;
      document.getElementById("prevBtn").disabled = state.page <= 1;
      document.getElementById("nextBtn").disabled = state.page >= Math.max(state.totalPages, 1);
      document.getElementById("taskList").innerHTML = items.length ? items.map(renderTaskCard).join("") : '<div class="empty">你当前还没有提交过申请。</div>';
    }
    document.getElementById("prevBtn").addEventListener("click", async () => { if (state.page > 1) { state.page--; await loadApplications(); } });
    document.getElementById("nextBtn").addEventListener("click", async () => { if (state.page < Math.max(state.totalPages, 1)) { state.page++; await loadApplications(); } });
    document.getElementById("logoutBtn").addEventListener("click", () => { localStorage.removeItem("token"); location.href = "index.html"; });
    Promise.all([loadApplications(), loadPendingCount()]).catch(error => { document.getElementById("message").textContent = error.message; document.getElementById("taskList").innerHTML = `<div class="empty">${escapeHtml(error.message)}</div>`; });
