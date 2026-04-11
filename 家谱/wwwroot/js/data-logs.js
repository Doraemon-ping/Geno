const token = localStorage.getItem("token");
    if (!token) {
      location.href = "login.html?redirect=data-logs.html";
    }

    const state = {
      page: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0
    };

    function authHeaders() {
      return { Authorization: `Bearer ${token}` };
    }

    async function requestJson(url, options = {}) {
      const response = await fetch(url, options);
      const type = response.headers.get("content-type") || "";
      const payload = type.includes("application/json") ? await response.json() : await response.text();
      if (!response.ok) {
        throw new Error(typeof payload === "string" ? payload : (payload?.message || payload?.Message || "请求失败"));
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
      if (value == null) return null;
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
      if (!data || typeof data !== "object") return undefined;

      for (const name of names) {
        if (Object.prototype.hasOwnProperty.call(data, name)) {
          return data[name];
        }

        const matched = Object.keys(data).find(key => key.toLowerCase() === String(name).toLowerCase());
        if (matched) {
          return data[matched];
        }
      }

      return undefined;
    }

    function isGuidLike(value) {
      return typeof value === "string" && /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value.trim());
    }

    function shouldHideKey(key) {
      const normalized = String(key || "").toLowerCase().replace(/[_\s-]/g, "");
      return normalized.endsWith("id") && normalized !== "taskid";
    }

    function normalizeValue(value) {
      if (value == null || value === "") return "";
      if (typeof value === "boolean") return value ? "是" : "否";
      if (typeof value === "object") return "";
      return String(value);
    }

    function safeField(label, value, allowTaskId = false) {
      const normalizedLabel = String(label || "").toLowerCase().replace(/[\s_-]/g, "");
      if (!allowTaskId && normalizedLabel.endsWith("id")) {
        return null;
      }

      const normalizedValue = normalizeValue(value);
      if (!normalizedValue) {
        return null;
      }

      if (!allowTaskId && isGuidLike(normalizedValue)) {
        return null;
      }

      return { label, value: normalizedValue };
    }

    function fieldRows(items) {
      const valid = items.filter(Boolean);
      if (!valid.length) {
        return '<div class="empty" style="padding:18px;">暂无可展示内容</div>';
      }

      return `<div class="detail-grid">
        ${valid.map(item => `
          <div class="detail-item">
            <span>${escapeHtml(item.label)}</span>
            <div>${escapeHtml(item.value)}</div>
          </div>`).join("")}
      </div>`;
    }

    function tableLabel(value) {
      const map = {
        Sys_Review_Tasks: "审核任务",
        Geno_Trees: "家谱树",
        Geno_Generation_Poems: "字辈",
        Geno_Members: "树成员",
        Geno_Unions: "婚姻单元",
        Geno_Union_Members: "家庭成员关联",
        Geno_Tree_Permissions: "树权限",
        Geno_Events: "历史事件",
        Geno_Event_Participants: "事件参与者",
        Sys_Media_Files: "媒体文件"
      };
      return map[value] || value || "未知表";
    }

    function boolText(value) {
      return value ? "是" : "否";
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

    function unionTypeText(type) {
      const value = Number(type);
      if (value === 1) return "正式结婚";
      if (value === 2) return "事实婚姻";
      if (value === 3) return "离异";
      return "";
    }

    function relationTypeText(type) {
      const value = Number(type);
      if (value === 1) return "亲生";
      if (value === 2) return "收养";
      if (value === 3) return "继子";
      if (value === 4) return "过继";
      return "";
    }

    function mapSnapshot(table, snapshot) {
      const data = toObject(snapshot) || {};

      if (table === "Sys_Review_Tasks") {
        return [
          safeField("任务编号", getValue(data, "taskId", "TaskID"), true),
          safeField("任务类型", getValue(data, "actionName", "ActionName") || getValue(data, "actionCode", "ActionCode")),
          safeField("状态", getValue(data, "status", "Status")),
          safeField("申请理由", getValue(data, "applyReason", "ApplyReason")),
          safeField("审核备注", getValue(data, "reviewNotes", "ReviewNotes")),
          safeField("创建时间", getValue(data, "createdAt", "CreatedAt")),
          safeField("处理时间", getValue(data, "processedAt", "ProcessedAt"))
        ];
      }

      if (table === "Geno_Trees") {
        return [
          safeField("家谱树名称", getValue(data, "treeName", "TreeName")),
          safeField("始祖", getValue(data, "ancestorName", "AncestorName")),
          safeField("地区", getValue(data, "region", "Region")),
          safeField("是否公开", getValue(data, "isPublic", "IsPublic") == null ? "" : boolText(Boolean(getValue(data, "isPublic", "IsPublic")))),
          safeField("说明", getValue(data, "description", "Description")),
          safeField("逻辑删除", getValue(data, "isDeleted", "isDel", "IsDel") == null ? "" : boolText(Boolean(getValue(data, "isDeleted", "isDel", "IsDel"))))
        ];
      }

      if (table === "Geno_Generation_Poems") {
        return [
          safeField("代数", getValue(data, "generationNum", "GenerationNum")),
          safeField("字辈", getValue(data, "word", "Word")),
          safeField("释义", getValue(data, "meaning", "Meaning")),
          safeField("逻辑删除", getValue(data, "isDeleted", "isDel", "IsDel") == null ? "" : boolText(Boolean(getValue(data, "isDeleted", "isDel", "IsDel"))))
        ];
      }

      if (table === "Geno_Members") {
        const fullName = getValue(data, "fullName", "FullName")
          || `${getValue(data, "lastName", "LastName") || ""}${getValue(data, "firstName", "FirstName") || ""}`;

        return [
          safeField("成员姓名", fullName),
          safeField("代数", getValue(data, "generationNum", "GenerationNum")),
          safeField("性别", getValue(data, "genderText", "GenderText") || getValue(data, "gender", "Gender")),
          safeField("出生信息", getValue(data, "birthDateRaw", "BirthDateRaw")),
          safeField("成员简介", getValue(data, "biography", "Biography")),
          safeField("逻辑删除", getValue(data, "isDeleted", "isDel", "IsDel") == null ? "" : boolText(Boolean(getValue(data, "isDeleted", "isDel", "IsDel"))))
        ];
      }

      if (table === "Geno_Unions") {
        return [
          safeField("伴侣 1", getValue(data, "partner1Name", "Partner1Name")),
          safeField("伴侣 2", getValue(data, "partner2Name", "Partner2Name")),
          safeField("婚姻类型", getValue(data, "unionTypeName", "UnionTypeName") || unionTypeText(getValue(data, "unionType", "UnionType"))),
          safeField("排序", getValue(data, "sortOrder", "SortOrder")),
          safeField("结婚日期", getValue(data, "marriageDate", "MarriageDate")),
          safeField("逻辑删除", getValue(data, "isDeleted", "isDel", "IsDel") == null ? "" : boolText(Boolean(getValue(data, "isDeleted", "isDel", "IsDel"))))
        ];
      }

      if (table === "Geno_Union_Members") {
        return [
          safeField("子女成员", getValue(data, "childName", "ChildName")),
          safeField("关系类型", getValue(data, "relTypeName", "RelTypeName") || relationTypeText(getValue(data, "relType", "RelType"))),
          safeField("排行", getValue(data, "childOrder", "ChildOrder")),
          safeField("逻辑删除", getValue(data, "isDeleted", "isDel", "IsDel") == null ? "" : boolText(Boolean(getValue(data, "isDeleted", "isDel", "IsDel"))))
        ];
      }

      if (table === "Geno_Tree_Permissions") {
        return [
          safeField("家谱树", getValue(data, "treeName", "TreeName")),
          safeField("授权用户", getValue(data, "username", "Username")),
          safeField("角色", getValue(data, "roleName", "RoleName") || treeRoleText(getValue(data, "roleType", "RoleType"))),
          safeField("是否激活", getValue(data, "isActive", "IsActive") == null ? "" : boolText(Boolean(getValue(data, "isActive", "IsActive"))))
        ];
      }

      if (table === "Geno_Events") {
        return [
          safeField("事件标题", getValue(data, "eventTitle", "EventTitle")),
          safeField("事件类型", getValue(data, "eventTypeName", "EventTypeName") || getValue(data, "eventType", "EventType")),
          safeField("所属范围", getValue(data, "isGlobal", "IsGlobal") == null ? "" : (Boolean(getValue(data, "isGlobal", "IsGlobal")) ? "社会历史事件" : "树内历史事件")),
          safeField("事件日期", getValue(data, "eventDate", "EventDate") || getValue(data, "dateRaw", "DateRaw")),
          safeField("详细描述", getValue(data, "description", "Description")),
          safeField("参与成员", getValue(data, "participantSummary", "ParticipantSummary")),
          safeField("附件资料", getValue(data, "mediaSummary", "MediaSummary")),
          safeField("逻辑删除", getValue(data, "isDeleted", "isDel", "IsDel") == null ? "" : boolText(Boolean(getValue(data, "isDeleted", "isDel", "IsDel"))))
        ];
      }

      if (table === "Geno_Event_Participants") {
        return [
          safeField("事件标题", getValue(data, "eventTitle", "EventTitle")),
          safeField("参与成员", getValue(data, "memberName", "MemberName")),
          safeField("身份备注", getValue(data, "roleDescription", "RoleDescription")),
          safeField("逻辑删除", getValue(data, "isDeleted", "isDel", "IsDel") == null ? "" : boolText(Boolean(getValue(data, "isDeleted", "isDel", "IsDel"))))
        ];
      }

      if (table === "Sys_Media_Files") {
        return [
          safeField("文件名称", getValue(data, "fileName", "FileName")),
          safeField("文件类型", getValue(data, "mimeType", "MimeType") || getValue(data, "fileExt", "FileExt")),
          safeField("说明", getValue(data, "caption", "Caption")),
          safeField("状态", getValue(data, "statusName", "StatusName") || getValue(data, "status", "Status")),
          safeField("排序", getValue(data, "sortOrder", "SortOrder")),
          safeField("访问路径", getValue(data, "publicUrl", "PublicUrl")),
          safeField("逻辑删除", getValue(data, "isDeleted", "isDel", "IsDel") == null ? "" : boolText(Boolean(getValue(data, "isDeleted", "isDel", "IsDel"))))
        ];
      }

      return Object.entries(data)
        .filter(([key, value]) => !shouldHideKey(key) && typeof value !== "object")
        .map(([key, value]) => safeField(key, value));
    }

    function formatDateRange() {
      const from = document.getElementById("createdFromInput").value;
      const to = document.getElementById("createdToInput").value;
      if (from && to) return `${from} 至 ${to}`;
      if (from) return `${from} 起`;
      if (to) return `截止 ${to}`;
      return "全部";
    }

    function buildQuery() {
      const params = new URLSearchParams();
      params.set("page", String(state.page));
      params.set("pageSize", String(state.pageSize));

      const createdFrom = document.getElementById("createdFromInput").value;
      const createdTo = document.getElementById("createdToInput").value;
      const values = {
        keyword: document.getElementById("keywordInput").value.trim(),
        targetTable: document.getElementById("tableFilter").value,
        opType: document.getElementById("opTypeFilter").value,
        createdFrom: createdFrom ? `${createdFrom}T00:00:00` : "",
        createdTo: createdTo ? `${createdTo}T23:59:59` : ""
      };

      Object.entries(values).forEach(([key, value]) => {
        if (value) params.set(key, value);
      });

      return params.toString();
    }

    function renderLog(log) {
      const beforeRows = fieldRows(mapSnapshot(log.targetTable, log.beforeData));
      const afterRows = fieldRows(mapSnapshot(log.targetTable, log.afterData));
      const actionName = log.actionName || log.actionCode || "无关联任务";
      const taskIdText = log.taskId ? String(log.taskId) : "无";

      return `
        <article class="log-card">
          <div class="log-head">
            <div>
              <strong>${escapeHtml(tableLabel(log.targetTable))} · ${escapeHtml(log.opType || "UNKNOWN")}</strong>
              <p>这条记录展示的是业务变更结果，不直接暴露内部对象 ID。</p>
            </div>
            <div class="badges">
              <span class="badge">${escapeHtml(log.createdAt || "未记录时间")}</span>
              <span class="badge">${escapeHtml(actionName)}</span>
            </div>
          </div>

          <div class="meta-grid">
            <div class="meta-item">
              <span>操作人</span>
              <strong>${escapeHtml(log.operatorName || "系统")}</strong>
            </div>
            <div class="meta-item">
              <span>目标表</span>
              <strong>${escapeHtml(tableLabel(log.targetTable))}</strong>
            </div>
            <div class="meta-item">
              <span>关联任务</span>
              <strong>${escapeHtml(actionName)}</strong>
            </div>
            <div class="meta-item">
              <span>任务 ID</span>
              <strong>${escapeHtml(taskIdText)}</strong>
            </div>
          </div>

          <div class="snapshot-grid">
            <section class="snapshot">
              <strong>变更前</strong>
              ${beforeRows}
            </section>
            <section class="snapshot">
              <strong>变更后</strong>
              ${afterRows}
            </section>
          </div>
        </article>`;
    }

    function updateSummary() {
      document.getElementById("totalCount").textContent = String(state.totalCount);
      document.getElementById("pageInfo").textContent = `第 ${state.page} / ${Math.max(state.totalPages, 1)} 页`;
      document.getElementById("pageSizeInfo").textContent = String(state.pageSize);
      document.getElementById("scopeInfo").textContent = formatDateRange();
      document.getElementById("pagerText").textContent = `共 ${state.totalCount} 条记录，当前第 ${state.page} 页`;
      document.getElementById("prevBtn").disabled = state.page <= 1;
      document.getElementById("nextBtn").disabled = state.page >= Math.max(state.totalPages, 1);
    }

    async function loadLogs() {
      document.getElementById("message").textContent = "正在读取数据库日志...";
      const result = await requestJson(`/api/DataLog/query?${buildQuery()}`, {
        headers: authHeaders()
      });

      const data = result.data || result.Data || {};
      const items = Array.isArray(data.items || data.Items) ? (data.items || data.Items) : [];
      state.totalCount = Number(data.totalCount || data.TotalCount || 0);
      state.totalPages = Number(data.totalPages || data.TotalPages || 0);
      state.page = Number(data.page || data.Page || state.page);

      updateSummary();
      document.getElementById("message").textContent = "";
      document.getElementById("logList").innerHTML = items.length
        ? items.map(renderLog).join("")
        : '<div class="empty">当前筛选条件下没有匹配的数据库日志。</div>';
    }

    document.getElementById("searchBtn").addEventListener("click", async () => {
      state.page = 1;
      state.pageSize = Number(document.getElementById("pageSizeSelect").value) || 20;
      await loadLogs();
    });

    document.getElementById("resetBtn").addEventListener("click", async () => {
      document.getElementById("keywordInput").value = "";
      document.getElementById("tableFilter").value = "";
      document.getElementById("opTypeFilter").value = "";
      document.getElementById("createdFromInput").value = "";
      document.getElementById("createdToInput").value = "";
      document.getElementById("pageSizeSelect").value = "20";
      state.page = 1;
      state.pageSize = 20;
      await loadLogs();
    });

    document.getElementById("pageSizeSelect").addEventListener("change", async () => {
      state.page = 1;
      state.pageSize = Number(document.getElementById("pageSizeSelect").value) || 20;
      await loadLogs();
    });

    document.getElementById("prevBtn").addEventListener("click", async () => {
      if (state.page <= 1) return;
      state.page -= 1;
      await loadLogs();
    });

    document.getElementById("nextBtn").addEventListener("click", async () => {
      if (state.page >= Math.max(state.totalPages, 1)) return;
      state.page += 1;
      await loadLogs();
    });

    document.getElementById("logoutBtn").addEventListener("click", () => {
      localStorage.removeItem("token");
      location.href = "index.html";
    });

    loadLogs().catch(error => {
      document.getElementById("message").textContent = error.message;
      document.getElementById("logList").innerHTML = `<div class="empty">${escapeHtml(error.message)}</div>`;
    });
