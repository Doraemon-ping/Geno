const token = localStorage.getItem("token");
const state = { page: 1, pageSize: 10, totalPages: 1, profile: null, editingId: "" };

function authHeaders(json = false) {
    const headers = {};
    if (token) headers.Authorization = `Bearer ${token}`;
    if (json) headers["Content-Type"] = "application/json";
    return headers;
}

async function requestJson(url, options = {}) {
    const response = await fetch(url, options);
    const contentType = response.headers.get("content-type") || "";
    const payload = contentType.includes("application/json") ? await response.json() : await response.text();
    if (!response.ok) {
        throw new Error((payload && (payload.message || payload.Message)) || payload || "请求失败");
    }
    return payload;
}

function dataOf(result) {
    return result?.data || result?.Data || result;
}

function esc(value) {
    return String(value ?? "").replace(/[&<>"']/g, ch => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[ch]));
}

function fmt(value) {
    if (!value) return "未发布";
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString("zh-CN", { hour12: false });
}

function isAdmin() {
    return state.profile && Number(state.profile.roleType ?? state.profile.RoleType) <= 1;
}

function setMessage(text = "", ok = false) {
    const el = document.getElementById("adminMessage");
    el.textContent = text;
    el.classList.toggle("success", ok);
}

async function loadProfile() {
    if (!token) return;
    try {
        const result = await requestJson("/api/Account/profile", { headers: authHeaders() });
        state.profile = dataOf(result);
        document.getElementById("profileNav").style.display = "inline";
        document.getElementById("logoutBtn").style.display = "inline";
        document.getElementById("loginNav").style.display = "none";
        document.getElementById("adminPanel").classList.toggle("hidden", !isAdmin());
    } catch {
        localStorage.removeItem("token");
    }
}

async function loadAnnouncements() {
    const keyword = document.getElementById("keywordInput").value.trim();
    const url = isAdmin()
        ? `/api/Announcement/query?page=${state.page}&pageSize=${state.pageSize}&publicOnly=false&keyword=${encodeURIComponent(keyword)}`
        : `/api/Announcement/public?page=${state.page}&pageSize=${state.pageSize}&keyword=${encodeURIComponent(keyword)}`;
    const result = await requestJson(url, { headers: authHeaders() });
    const page = dataOf(result);
    const items = Array.isArray(page.items || page.Items) ? (page.items || page.Items) : [];
    state.totalPages = Math.max(Number(page.totalPages || page.TotalPages || 1), 1);
    document.getElementById("pageInfo").textContent = `第 ${state.page} / ${state.totalPages} 页`;
    document.getElementById("prevBtn").disabled = state.page <= 1;
    document.getElementById("nextBtn").disabled = state.page >= state.totalPages;
    document.getElementById("announcementList").innerHTML = items.length
        ? items.map(renderAnnouncement).join("")
        : '<div class="empty">暂无公告。</div>';
}

function renderAnnouncement(item) {
    const id = item.announcementId || item.AnnouncementId;
    const title = item.title || item.Title || "未命名公告";
    const content = item.content || item.Content || "";
    const category = item.category || item.Category || "系统公告";
    const status = Number(item.status ?? item.Status ?? 1);
    const pinned = Boolean(item.isPinned ?? item.IsPinned);
    const creator = item.creatorName || item.CreatorName || "系统管理员";
    const publishedAt = item.publishedAt || item.PublishedAt;
    const actions = isAdmin()
        ? `<div class="actions"><button class="ghost-button" type="button" onclick="editAnnouncement('${id}')">编辑</button><button class="danger-button" type="button" onclick="deleteAnnouncement('${id}')">删除</button></div>`
        : "";
    return `<article class="notice-card ${pinned ? "pinned" : ""}">
        <div class="notice-top">
            <div>
                <h3 class="notice-title">${esc(title)}</h3>
                <div class="notice-meta">
                    <span class="badge">${esc(category)}</span>
                    ${pinned ? '<span class="badge">置顶</span>' : ""}
                    ${isAdmin() ? `<span class="badge">${status === 1 ? "已发布" : "草稿"}</span>` : ""}
                    <span>${esc(fmt(publishedAt))}</span>
                    <span>发布人：${esc(creator)}</span>
                </div>
            </div>
            ${actions}
        </div>
        <div class="notice-content">${esc(content)}</div>
    </article>`;
}

function readPayload() {
    return {
        title: document.getElementById("titleInput").value.trim(),
        content: document.getElementById("contentInput").value.trim(),
        category: document.getElementById("categoryInput").value.trim() || "系统公告",
        isPinned: document.getElementById("pinnedInput").checked,
        publishNow: document.getElementById("statusInput").value === "1",
        status: Number(document.getElementById("statusInput").value)
    };
}

function resetEditor() {
    state.editingId = "";
    document.getElementById("editorTitle").textContent = "发布公告";
    document.getElementById("titleInput").value = "";
    document.getElementById("contentInput").value = "";
    document.getElementById("categoryInput").value = "系统公告";
    document.getElementById("statusInput").value = "1";
    document.getElementById("pinnedInput").checked = false;
    document.getElementById("deleteBtn").classList.add("hidden");
    setMessage();
}

async function saveAnnouncement() {
    if (!isAdmin()) return;
    const payload = readPayload();
    if (!payload.title || !payload.content) {
        setMessage("请填写公告标题和内容");
        return;
    }
    const url = state.editingId ? `/api/Announcement/Update/${encodeURIComponent(state.editingId)}` : "/api/Announcement/Add";
    const method = state.editingId ? "PUT" : "POST";
    try {
        await requestJson(url, { method, headers: authHeaders(true), body: JSON.stringify(payload) });
        setMessage("公告已保存", true);
        resetEditor();
        await loadAnnouncements();
    } catch (error) {
        setMessage(error.message);
    }
}

async function editAnnouncement(id) {
    const cards = Array.from(document.querySelectorAll(".notice-card"));
    const targetButton = cards.find(card => card.querySelector(`[onclick="editAnnouncement('${id}')"]`));
    if (!targetButton) return;
    const title = targetButton.querySelector(".notice-title")?.textContent || "";
    const content = targetButton.querySelector(".notice-content")?.textContent || "";
    const badges = Array.from(targetButton.querySelectorAll(".badge")).map(x => x.textContent || "");
    state.editingId = id;
    document.getElementById("editorTitle").textContent = "编辑公告";
    document.getElementById("titleInput").value = title;
    document.getElementById("contentInput").value = content;
    document.getElementById("categoryInput").value = badges[0] || "系统公告";
    document.getElementById("pinnedInput").checked = badges.includes("置顶");
    document.getElementById("statusInput").value = badges.includes("草稿") ? "0" : "1";
    document.getElementById("deleteBtn").classList.remove("hidden");
    window.scrollTo({ top: 0, behavior: "smooth" });
}

async function deleteAnnouncement(id = state.editingId) {
    if (!isAdmin() || !id || !confirm("确定删除这条公告吗？")) return;
    try {
        await requestJson(`/api/Announcement/Del/${encodeURIComponent(id)}`, { method: "DELETE", headers: authHeaders() });
        resetEditor();
        await loadAnnouncements();
    } catch (error) {
        setMessage(error.message);
    }
}

document.getElementById("saveBtn").addEventListener("click", saveAnnouncement);
document.getElementById("deleteBtn").addEventListener("click", () => deleteAnnouncement());
document.getElementById("resetBtn").addEventListener("click", resetEditor);
document.getElementById("searchBtn").addEventListener("click", () => { state.page = 1; loadAnnouncements(); });
document.getElementById("keywordInput").addEventListener("keydown", event => {
    if (event.key === "Enter") {
        state.page = 1;
        loadAnnouncements();
    }
});
document.getElementById("prevBtn").addEventListener("click", () => { if (state.page > 1) { state.page--; loadAnnouncements(); } });
document.getElementById("nextBtn").addEventListener("click", () => { if (state.page < state.totalPages) { state.page++; loadAnnouncements(); } });
document.getElementById("logoutBtn").addEventListener("click", () => {
    localStorage.removeItem("token");
    location.href = "index.html";
});

window.editAnnouncement = editAnnouncement;
window.deleteAnnouncement = deleteAnnouncement;

loadProfile().then(loadAnnouncements).catch(error => {
    document.getElementById("announcementList").innerHTML = `<div class="empty">${esc(error.message)}</div>`;
});
