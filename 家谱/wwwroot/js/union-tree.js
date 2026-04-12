const token = localStorage.getItem("token");
const params = new URLSearchParams(location.search);
const treeId = params.get("treeId") || params.get("id");
const viewport = document.getElementById("viewport");
const stage = document.getElementById("stage");
const detailContent = document.getElementById("detailContent");

const state = {
    tree: null,
    access: {},
    graph: null,
    unions: [],
    searchResults: [],
    selectedNodeId: "",
    selectedNode: null,
    scale: 1,
    offsetX: 0,
    offsetY: 0,
    dragging: false,
    dragStartX: 0,
    dragStartY: 0
};

if (!treeId) {
    location.href = "public.html";
}

document.getElementById("backLink").href = `tree-detail.html?id=${encodeURIComponent(treeId)}`;
document.getElementById("detailLink").href = `tree-detail.html?id=${encodeURIComponent(treeId)}`;

if (!token) {
    document.getElementById("logoutBtn").textContent = "返回首页";
}

function authHeaders(json = false) {
    const headers = {};
    if (token) {
        headers.Authorization = `Bearer ${token}`;
    }
    if (json) {
        headers["Content-Type"] = "application/json";
    }
    return headers;
}

function get(obj, ...keys) {
    for (const key of keys) {
        if (obj && obj[key] !== undefined && obj[key] !== null) {
            return obj[key];
        }
    }

    return null;
}

function esc(value) {
    return String(value === undefined || value === null ? "" : value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function setMessage(text, ok = false) {
    const el = document.getElementById("message");
    el.textContent = text || "";
    el.className = ok ? "message success" : "message";
}

function requireLogin() {
    if (token) {
        return true;
    }

    location.href = `login.html?redirect=${encodeURIComponent(`union-tree.html?treeId=${treeId}`)}`;
    return false;
}

function canDirectEdit() {
    return Boolean(get(state.access, "canDirectEdit", "CanDirectEdit", "canEdit", "CanEdit"));
}

function canSubmitChange() {
    return Boolean(get(state.access, "canSubmitChange", "CanSubmitChange"));
}

function roleName() {
    return get(state.access, "roleName", "RoleName") || "访客";
}

function memberId(item) {
    return get(item, "memberId", "memberID", "MemberID") || "";
}

function unionId(item) {
    return get(item, "unionId", "unionID", "UnionID") || "";
}

function fullName(item) {
    return get(item, "fullName", "FullName") || `${get(item, "lastName", "LastName") || ""}${get(item, "firstName", "FirstName") || ""}` || "未命名成员";
}

function genderName(value) {
    return Number(value) === 1 ? "男" : Number(value) === 2 ? "女" : "未知";
}

function workflowMessage(result, fallback) {
    return get(get(result, "data"), "message") || get(get(result, "Data"), "Message") || get(result, "message", "Message") || fallback;
}

function workflowData(result) {
    return get(get(result, "data"), "data") || get(get(result, "Data"), "Data") || get(result, "data", "Data");
}

async function req(url, options = {}) {
    const response = await fetch(url, options);
    const contentType = response.headers.get("content-type") || "";
    const payload = contentType.includes("application/json") ? await response.json() : await response.text();
    if (!response.ok) {
        throw new Error(typeof payload === "string" ? payload : get(payload, "message", "Message") || get(get(payload, "data"), "message") || "请求失败");
    }

    return payload;
}

function nodeCenter(node) {
    return {
        x: node.x + node.width / 2,
        y: node.y + node.height / 2
    };
}

function edgePath(fromNode, toNode) {
    const start = { x: fromNode.x + fromNode.width / 2, y: fromNode.y + fromNode.height };
    const end = { x: toNode.x + toNode.width / 2, y: toNode.y };
    const midY = start.y + (end.y - start.y) / 2;
    return `M ${start.x} ${start.y} L ${start.x} ${midY} L ${end.x} ${midY} L ${end.x} ${end.y}`;
}

function updateTransform() {
    stage.style.transform = `translate(${state.offsetX}px, ${state.offsetY}px) scale(${state.scale})`;
}

function fitToViewport() {
    if (!state.graph) {
        return;
    }

    const viewportWidth = viewport.clientWidth;
    const viewportHeight = viewport.clientHeight;
    const scaleX = viewportWidth / state.graph.width;
    const scaleY = viewportHeight / state.graph.height;
    const isVeryWide = state.graph.width / Math.max(state.graph.height, 1) > 3;
    state.scale = isVeryWide
        ? Math.max(0.42, Math.min(scaleY * 0.92, 1.08))
        : Math.max(0.24, Math.min(scaleX, scaleY, 1.08));
    state.offsetX = isVeryWide ? 28 : (viewportWidth - state.graph.width * state.scale) / 2;
    state.offsetY = (viewportHeight - state.graph.height * state.scale) / 2;
    updateTransform();
}

function zoomBy(factor) {
    const nextScale = Math.min(2.5, Math.max(0.12, state.scale * factor));
    const centerX = viewport.clientWidth / 2;
    const centerY = viewport.clientHeight / 2;
    state.offsetX = centerX - ((centerX - state.offsetX) * nextScale / state.scale);
    state.offsetY = centerY - ((centerY - state.offsetY) * nextScale / state.scale);
    state.scale = nextScale;
    updateTransform();
}

function findUnion(id) {
    return state.unions.find(item => String(unionId(item)).toLowerCase() === String(id).toLowerCase()) || null;
}

function findUnionByPartnerIds(partner1Id, partner2Id) {
    return state.unions.find(item => {
        const p1 = get(item, "partner1", "Partner1") || {};
        const p2 = get(item, "partner2", "Partner2") || {};
        const ids = [memberId(p1), memberId(p2)].map(value => String(value).toLowerCase()).sort();
        const target = [partner1Id, partner2Id].map(value => String(value).toLowerCase()).sort();
        return ids[0] === target[0] && ids[1] === target[1];
    }) || null;
}

function findParentUnionForChild(id) {
    return state.unions.find(item => {
        const children = Array.isArray(get(item, "children", "Children")) ? get(item, "children", "Children") : [];
        return children.some(child => String(memberId(child)).toLowerCase() === String(id).toLowerCase());
    }) || null;
}

function setMemberTarget(kind, id, label) {
    document.getElementById(`${kind}Id`).value = id || "";
    document.getElementById(`${kind}Name`).textContent = label || "未选择";
}

function setUnionTarget(id, label) {
    document.getElementById("relationUnionId").value = id || "";
    document.getElementById("relationUnionName").textContent = label || "未选择婚姻单元";
}

function unionLabel(item) {
    if (!item) {
        return "未选择婚姻单元";
    }

    const p1 = get(item, "partner1", "Partner1") || {};
    const p2 = get(item, "partner2", "Partner2") || {};
    const type = get(item, "unionTypeName", "UnionTypeName") || "婚姻单元";
    return `${fullName(p1)} × ${fullName(p2)} · ${type}`;
}

function fillUnionForm(item) {
    if (!item) {
        return;
    }

    const p1 = get(item, "partner1", "Partner1") || {};
    const p2 = get(item, "partner2", "Partner2") || {};
    setMemberTarget("partner1", memberId(p1), fullName(p1));
    setMemberTarget("partner2", memberId(p2), fullName(p2));
    document.getElementById("unionIdInput").value = unionId(item);
    document.getElementById("unionTypeInput").value = String(get(item, "unionType", "UnionType") || 1);
    document.getElementById("sortOrderInput").value = String(get(item, "sortOrder", "SortOrder") || 1);
    document.getElementById("marriageDateInput").value = String(get(item, "marriageDate", "MarriageDate") || "").slice(0, 10);
    setUnionTarget(unionId(item), unionLabel(item));
}

function fillChildFromNode(node) {
    if (!node || !node.memberId) {
        return;
    }

    setMemberTarget("child", node.memberId, node.label);
    const parentUnion = findParentUnionForChild(node.memberId);
    if (parentUnion) {
        setUnionTarget(unionId(parentUnion), unionLabel(parentUnion));
    }
}

function renderDetail(node) {
    if (!node) {
        detailContent.innerHTML = "点击左侧节点查看详细信息，也可以在下方编排台搜索成员。";
        return;
    }

    if (node.kind === "member") {
        const parentUnion = findParentUnionForChild(node.memberId);
        detailContent.innerHTML = `
            <div>节点类型：成员</div>
            <div>姓名：${esc(node.label)}</div>
            <div>说明：${esc(node.subtitle)}</div>
            <div>代际：第 ${esc(node.generation)} 代</div>
            <div>父母单元：${esc(parentUnion ? unionLabel(parentUnion) : "未建立")}</div>
            <div class="detail-actions">
                <button class="btn-line compact" type="button" onclick="useSelectedAsPartner()">设为伴侣</button>
                <button class="btn-line compact" type="button" onclick="useSelectedAsChild()">设为子女</button>
            </div>
        `;
        return;
    }

    const union = findUnion(node.unionId);
    const children = Array.isArray(get(union, "children", "Children")) ? get(union, "children", "Children") : [];
    detailContent.innerHTML = `
        <div>节点类型：婚姻单元</div>
        <div>婚姻类型：${esc(node.label)}</div>
        <div>说明：${esc(node.subtitle)}</div>
        <div>子女数量：${children.length}</div>
        <div class="detail-actions">
            <button class="btn-line compact" type="button" onclick="useSelectedUnion()">编辑此单元</button>
        </div>
    `;
}

function renderUnionSelect() {
    const options = state.unions
        .map(item => `<option value="${esc(unionId(item))}">${esc(unionLabel(item))}</option>`)
        .join("");
    document.getElementById("unionPicker").innerHTML = `<option value="">选择已有婚姻单元</option>${options}`;
}

function renderSearchResults() {
    const box = document.getElementById("memberSearchResults");
    if (!state.searchResults.length) {
        box.innerHTML = '<div class="empty">输入姓名、出生信息或简介关键字后搜索成员。</div>';
        return;
    }

    box.innerHTML = state.searchResults.map(item => {
        const id = memberId(item);
        const name = fullName(item);
        return `
            <div class="member-result">
                <div>
                    <strong>${esc(name)}</strong>
                    <span>第 ${esc(get(item, "generationNum", "GenerationNum") === null ? "未标注" : get(item, "generationNum", "GenerationNum"))} 代 · ${esc(genderName(get(item, "gender", "Gender")))}</span>
                    <small>${esc(get(item, "birthDateRaw", "BirthDateRaw") || "未填写出生信息")}</small>
                </div>
                <div class="result-actions">
                    <button type="button" class="btn-line compact" data-pick-member="partner1" data-member-id="${esc(id)}" data-member-name="${esc(name)}">伴侣 1</button>
                    <button type="button" class="btn-line compact" data-pick-member="partner2" data-member-id="${esc(id)}" data-member-name="${esc(name)}">伴侣 2</button>
                    <button type="button" class="btn-line compact" data-pick-member="child" data-member-id="${esc(id)}" data-member-name="${esc(name)}">子女</button>
                </div>
            </div>
        `;
    }).join("");

    box.querySelectorAll("[data-pick-member]").forEach(button => {
        button.addEventListener("click", () => {
            pickMember(button.dataset.pickMember, button.dataset.memberId, button.dataset.memberName);
        });
    });
}

function renderEditorState() {
    document.getElementById("roleName").textContent = roleName();
    document.getElementById("editorPanel").classList.toggle("locked", !canSubmitChange());
    document.getElementById("editorHint").textContent = canDirectEdit()
        ? "你当前可直接编排成员、婚姻单元和子女关系，保存后立即生效。"
        : canSubmitChange()
            ? "你当前可提交编排申请，审核通过后关系会写入家谱。"
            : "你当前只有查看权限，不能提交编排变更。";
    document.querySelectorAll("[data-edit-action]").forEach(item => {
        item.disabled = !canSubmitChange();
    });
}

function renderGraph(graph) {
    state.graph = graph;
    document.getElementById("pageTitle").textContent = `${graph.treeName || "家谱"} · 图形化编排`;
    document.getElementById("pageSubtitle").textContent = "以婚姻单元为骨架，把成员、配偶、子女关系放在一张可缩放图里维护。点击节点即可带入右侧编排台。";
    document.getElementById("memberCount").textContent = String(graph.memberCount || 0);
    document.getElementById("unionCount").textContent = String(graph.unionCount || 0);
    document.getElementById("generationCount").textContent = String(graph.generationCount || 0);
    document.getElementById("stageSize").textContent = `${Math.round(graph.width || 1200)} x ${Math.round(graph.height || 800)}`;

    stage.setAttribute("viewBox", `0 0 ${graph.width || 1200} ${graph.height || 800}`);
    stage.setAttribute("width", graph.width || 1200);
    stage.setAttribute("height", graph.height || 800);

    const nodesById = new Map((graph.nodes || []).map(node => [node.id, node]));
    const edgesMarkup = (graph.edges || []).map(edge => {
        const fromNode = nodesById.get(edge.fromId);
        const toNode = nodesById.get(edge.toId);
        if (!fromNode || !toNode) {
            return "";
        }

        const labelMarkup = edge.label
            ? `<text class="edge-label" x="${(nodeCenter(fromNode).x + nodeCenter(toNode).x) / 2}" y="${(nodeCenter(fromNode).y + nodeCenter(toNode).y) / 2 - 6}" text-anchor="middle">${esc(edge.label)}</text>`
            : "";

        return `
            <g>
                <path class="edge ${esc(edge.kind)}" d="${edgePath(fromNode, toNode)}" fill="none"></path>
                ${labelMarkup}
            </g>
        `;
    }).join("");

    const nodesMarkup = (graph.nodes || []).map(node => {
        const classes = [
            "node",
            node.kind,
            node.kind === "member" && node.subtitle.includes("男") ? "male" : "",
            node.kind === "member" && node.subtitle.includes("女") ? "female" : "",
            state.selectedNodeId === node.id ? "active" : ""
        ].filter(Boolean).join(" ");
        const labelX = node.x + node.width / 2;
        const titleY = node.y + (node.kind === "union" ? 12 : 24);
        const subY = node.y + (node.kind === "union" ? 24 : 45);

        return `
            <g class="${classes}" data-id="${esc(node.id)}">
                <rect x="${node.x}" y="${node.y}" rx="${node.kind === "union" ? 10 : 12}" ry="${node.kind === "union" ? 10 : 12}" width="${node.width}" height="${node.height}"></rect>
                <text x="${labelX}" y="${titleY}" text-anchor="middle" font-size="${node.kind === "union" ? 11 : 15}" font-weight="700" fill="#6c4e32">${esc(node.label)}</text>
                <text x="${labelX}" y="${subY}" text-anchor="middle" font-size="${node.kind === "union" ? 9 : 11}" fill="#8d7662">${esc(node.subtitle)}</text>
            </g>
        `;
    }).join("");

    stage.innerHTML = `${edgesMarkup}${nodesMarkup || '<text class="empty" x="50%" y="50%" text-anchor="middle">当前还没有成员，请先新增成员。</text>'}`;
    stage.querySelectorAll(".node").forEach(element => {
        element.addEventListener("click", event => {
            event.stopPropagation();
            state.selectedNodeId = element.getAttribute("data-id") || "";
            state.selectedNode = nodesById.get(state.selectedNodeId);
            if (state.selectedNode && state.selectedNode.kind === "union") {
                fillUnionForm(findUnion(state.selectedNode.unionId));
            } else if (state.selectedNode && state.selectedNode.kind === "member") {
                fillChildFromNode(state.selectedNode);
            }
            renderDetail(state.selectedNode);
            renderGraph(state.graph);
        });
    });

    fitToViewport();
}

function buildSvgMarkup() {
    if (!state.graph) {
        throw new Error("当前还没有可下载的图数据");
    }

    const clone = stage.cloneNode(true);
    clone.setAttribute("xmlns", "http://www.w3.org/2000/svg");
    clone.removeAttribute("style");
    clone.setAttribute("viewBox", `0 0 ${state.graph.width} ${state.graph.height}`);
    clone.setAttribute("width", Math.ceil(state.graph.width));
    clone.setAttribute("height", Math.ceil(state.graph.height));

    const background = document.createElementNS("http://www.w3.org/2000/svg", "rect");
    background.setAttribute("x", "0");
    background.setAttribute("y", "0");
    background.setAttribute("width", "100%");
    background.setAttribute("height", "100%");
    background.setAttribute("fill", "#fdfaf2");
    clone.insertBefore(background, clone.firstChild);

    const style = document.createElementNS("http://www.w3.org/2000/svg", "style");
    style.textContent = `
        text { font-family: "Noto Serif SC", "KaiTi", serif; pointer-events: none; user-select: none; }
        .node.member rect { fill: #fffdfa; stroke: rgba(139, 69, 19, 0.2); stroke-width: 1.4; }
        .node.member.male rect { fill: #f4efe2; }
        .node.member.female rect { fill: #f8efe8; }
        .node.union rect { fill: #efe6d5; stroke: rgba(139, 69, 19, 0.26); stroke-width: 1.3; }
        .node.active rect { stroke: #d4af37; stroke-width: 2.4; }
        .edge { stroke-width: 2; }
        .edge.partner { stroke: rgba(125, 88, 53, 0.92); }
        .edge.child { stroke: rgba(162, 116, 67, 0.7); }
        .edge-label { font-size: 10px; fill: #866745; }
    `;
    clone.insertBefore(style, clone.firstChild);

    return `<?xml version="1.0" encoding="UTF-8"?>\n${new XMLSerializer().serializeToString(clone)}`;
}

function downloadBlob(blob, fileName) {
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

function safeFileName(value) {
    return String(value || "union-tree").replace(/[\\/:*?"<>|]+/g, "-");
}

function downloadSvg() {
    try {
        const blob = new Blob([buildSvgMarkup()], { type: "image/svg+xml;charset=utf-8" });
        downloadBlob(blob, `${safeFileName(state.graph && state.graph.treeName)}-union-tree.svg`);
    } catch (error) {
        setMessage(error.message);
    }
}

async function downloadPng() {
    try {
        const markup = buildSvgMarkup();
        const url = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(markup)}`;
        const image = new Image();
        image.decoding = "async";
        await new Promise((resolve, reject) => {
            image.onload = resolve;
            image.onerror = () => reject(new Error("婚姻图转换 PNG 失败"));
            image.src = url;
        });

        const maxExportWidth = 12000;
        const exportScale = Math.min(1, maxExportWidth / Math.max(state.graph.width, 1));
        const canvas = document.createElement("canvas");
        canvas.width = Math.ceil(state.graph.width * exportScale);
        canvas.height = Math.ceil(state.graph.height * exportScale);
        const context = canvas.getContext("2d");
        context.fillStyle = "#fdfaf2";
        context.fillRect(0, 0, canvas.width, canvas.height);
        context.drawImage(image, 0, 0, canvas.width, canvas.height);
        const pngBlob = await new Promise(resolve => canvas.toBlob(resolve, "image/png"));
        if (!pngBlob) {
            throw new Error("PNG 导出失败");
        }

        downloadBlob(pngBlob, `${safeFileName(state.graph && state.graph.treeName)}-union-tree.png`);
    } catch (error) {
        setMessage(error.message);
    }
}

async function loadTree() {
    const result = await req(`/api/GenoTree/Get/${encodeURIComponent(treeId)}`, { headers: authHeaders() });
    state.tree = result.data || result.Data || {};
    state.access = get(state.tree, "access", "Access") || {};
    renderEditorState();
}

async function loadUnions() {
    const result = await req(`/api/Union/tree/${encodeURIComponent(treeId)}`, { headers: authHeaders() });
    state.unions = Array.isArray(result.data || result.Data) ? (result.data || result.Data) : [];
    renderUnionSelect();
}

async function loadGraph(keepMessage = false) {
    if (!keepMessage) {
        setMessage("正在读取婚姻单元树数据...");
    }
    const result = await req(`/api/Union/graph/${encodeURIComponent(treeId)}`, { headers: authHeaders() });
    renderGraph(result.data || result.Data || {});
    if (!keepMessage) {
        setMessage("");
    }
}

async function reloadAll(successMessage) {
    await Promise.all([loadTree(), loadUnions()]);
    await loadGraph(true);
    if (successMessage) {
        setMessage(successMessage, true);
    }
}

async function searchMembers() {
    const keyword = document.getElementById("memberKeyword").value.trim();
    setMessage("正在搜索成员...");
    const result = await req(`/api/Member/query?treeId=${encodeURIComponent(treeId)}&page=1&pageSize=20&keyword=${encodeURIComponent(keyword)}`, { headers: authHeaders() });
    const data = result.data || result.Data || {};
    state.searchResults = Array.isArray(get(data, "items", "Items")) ? get(data, "items", "Items") : [];
    renderSearchResults();
    setMessage("");
}

function memberPayloadFromForm() {
    return {
        treeId,
        lastName: document.getElementById("newLastName").value.trim(),
        firstName: document.getElementById("newFirstName").value.trim(),
        generationNum: Number(document.getElementById("newGeneration").value) || null,
        gender: Number(document.getElementById("newGender").value),
        birthDateRaw: document.getElementById("newBirthRaw").value.trim(),
        isLiving: document.getElementById("newIsLiving").value === "true",
        biography: document.getElementById("newBiography").value.trim(),
        mediaIds: []
    };
}

async function createMember() {
    if (!requireLogin() || !canSubmitChange()) {
        return;
    }

    const payload = memberPayloadFromForm();
    if (!payload.lastName || !payload.firstName) {
        setMessage("请填写新成员的姓氏和名字。");
        return;
    }

    const result = await req("/api/Member/Add", {
        method: "POST",
        headers: authHeaders(true),
        body: JSON.stringify(payload)
    });
    const data = workflowData(result);
    const newMemberId = get(data, "memberId", "MemberId");
    if (newMemberId) {
        const name = `${payload.lastName}${payload.firstName}`;
        setMemberTarget("child", newMemberId, name);
        state.searchResults = [{ ...payload, memberId: newMemberId }];
        renderSearchResults();
    }

    await reloadAll(workflowMessage(result, "成员处理成功"));
}

function unionPayloadFromForm() {
    return {
        treeId,
        partner1Id: document.getElementById("partner1Id").value,
        partner2Id: document.getElementById("partner2Id").value,
        unionType: Number(document.getElementById("unionTypeInput").value),
        sortOrder: Number(document.getElementById("sortOrderInput").value) || 1,
        marriageDate: document.getElementById("marriageDateInput").value || null
    };
}

async function saveUnion() {
    if (!requireLogin() || !canSubmitChange()) {
        return;
    }

    const payload = unionPayloadFromForm();
    if (!payload.partner1Id || !payload.partner2Id) {
        setMessage("请先选择伴侣 1 和伴侣 2。");
        return;
    }
    if (payload.partner1Id === payload.partner2Id) {
        setMessage("婚姻单元中的两个伴侣不能是同一成员。");
        return;
    }

    const editingId = document.getElementById("unionIdInput").value;
    const result = editingId
        ? await req(`/api/Union/Update/${encodeURIComponent(editingId)}`, { method: "PUT", headers: authHeaders(true), body: JSON.stringify(payload) })
        : await req("/api/Union/Add", { method: "POST", headers: authHeaders(true), body: JSON.stringify(payload) });
    const data = workflowData(result);
    const savedUnionId = editingId || get(data, "unionId", "UnionId");
    if (savedUnionId) {
        document.getElementById("unionIdInput").value = savedUnionId;
        setUnionTarget(savedUnionId, "刚处理的婚姻单元");
    }

    await reloadAll(workflowMessage(result, "婚姻单元处理成功"));
}

async function deleteUnion() {
    if (!requireLogin() || !canSubmitChange()) {
        return;
    }

    const id = document.getElementById("unionIdInput").value || document.getElementById("relationUnionId").value;
    if (!id) {
        setMessage("请先选择要删除的婚姻单元。");
        return;
    }
    if (!confirm("确定删除这个婚姻单元吗？它下面的子女关联也会逻辑删除。")) {
        return;
    }

    const result = await req(`/api/Union/Del/${encodeURIComponent(id)}`, { method: "DELETE", headers: authHeaders() });
    document.getElementById("unionIdInput").value = "";
    setUnionTarget("", "");
    await reloadAll(workflowMessage(result, "婚姻单元处理成功"));
}

async function addChildRelation() {
    if (!requireLogin() || !canSubmitChange()) {
        return;
    }

    const payload = {
        treeId,
        unionId: document.getElementById("relationUnionId").value,
        memberId: document.getElementById("childId").value,
        relType: Number(document.getElementById("relTypeInput").value),
        childOrder: Number(document.getElementById("childOrderInput").value) || 1
    };

    if (!payload.unionId || !payload.memberId) {
        setMessage("请先选择婚姻单元和子女成员。");
        return;
    }

    const result = await req("/api/Union/member/Add", { method: "POST", headers: authHeaders(true), body: JSON.stringify(payload) });
    await reloadAll(workflowMessage(result, "家庭子女关联处理成功"));
}

async function removeChildRelation() {
    if (!requireLogin() || !canSubmitChange()) {
        return;
    }

    const selectedUnionId = document.getElementById("relationUnionId").value;
    const selectedChildId = document.getElementById("childId").value;
    if (!selectedUnionId || !selectedChildId) {
        setMessage("请先选择要移除的婚姻单元和子女成员。");
        return;
    }
    if (!confirm("确定移除这个子女关系吗？成员本身不会被删除。")) {
        return;
    }

    const result = await req(`/api/Union/member/Del?unionId=${encodeURIComponent(selectedUnionId)}&memberId=${encodeURIComponent(selectedChildId)}`, {
        method: "DELETE",
        headers: authHeaders()
    });
    await reloadAll(workflowMessage(result, "家庭子女关联处理成功"));
}

async function quickAddParents() {
    if (!requireLogin() || !canSubmitChange()) {
        return;
    }

    if (!canDirectEdit()) {
        setMessage("快速串联父母单元需要先创建婚姻单元再添加子女关系；当前身份会进入审核，建议分两步提交：先提交婚姻单元，审核通过后再添加子女。");
        return;
    }

    const payload = unionPayloadFromForm();
    const childId = document.getElementById("childId").value;
    if (!payload.partner1Id || !payload.partner2Id || !childId) {
        setMessage("请先选择父母双方和子女成员。");
        return;
    }

    let parentUnion = findUnionByPartnerIds(payload.partner1Id, payload.partner2Id);
    if (!parentUnion) {
        const unionResult = await req("/api/Union/Add", { method: "POST", headers: authHeaders(true), body: JSON.stringify(payload) });
        const data = workflowData(unionResult);
        const createdUnionId = get(data, "unionId", "UnionId");
        await loadUnions();
        parentUnion = createdUnionId ? findUnion(createdUnionId) : findUnionByPartnerIds(payload.partner1Id, payload.partner2Id);
    }

    if (!parentUnion) {
        throw new Error("父母婚姻单元创建成功后未能定位，请刷新后再添加子女关系。");
    }

    const relationPayload = {
        treeId,
        unionId: unionId(parentUnion),
        memberId: childId,
        relType: Number(document.getElementById("relTypeInput").value),
        childOrder: Number(document.getElementById("childOrderInput").value) || 1
    };
    const result = await req("/api/Union/member/Add", { method: "POST", headers: authHeaders(true), body: JSON.stringify(relationPayload) });
    await reloadAll(workflowMessage(result, "父母与子女关系已串联"));
}

function clearUnionForm() {
    document.getElementById("unionIdInput").value = "";
    setMemberTarget("partner1", "", "");
    setMemberTarget("partner2", "", "");
    setUnionTarget("", "");
    document.getElementById("unionTypeInput").value = "1";
    document.getElementById("sortOrderInput").value = "1";
    document.getElementById("marriageDateInput").value = "";
}

function clearNewMemberForm() {
    ["newLastName", "newFirstName", "newGeneration", "newBirthRaw", "newBiography"].forEach(id => {
        document.getElementById(id).value = "";
    });
    document.getElementById("newGender").value = "0";
    document.getElementById("newIsLiving").value = "true";
}

function pickMember(kind, id, name) {
    setMemberTarget(kind, id, name);
}

function useSelectedAsPartner() {
    if (!state.selectedNode || !state.selectedNode.memberId) {
        return;
    }

    const p1 = document.getElementById("partner1Id").value;
    setMemberTarget(p1 ? "partner2" : "partner1", state.selectedNode.memberId, state.selectedNode.label);
}

function useSelectedAsChild() {
    if (state.selectedNode && state.selectedNode.memberId) {
        setMemberTarget("child", state.selectedNode.memberId, state.selectedNode.label);
    }
}

function useSelectedUnion() {
    if (state.selectedNode && state.selectedNode.unionId) {
        fillUnionForm(findUnion(state.selectedNode.unionId));
    }
}

viewport.addEventListener("mousedown", event => {
    state.dragging = true;
    state.dragStartX = event.clientX - state.offsetX;
    state.dragStartY = event.clientY - state.offsetY;
    viewport.classList.add("dragging");
});

window.addEventListener("mousemove", event => {
    if (!state.dragging) {
        return;
    }

    state.offsetX = event.clientX - state.dragStartX;
    state.offsetY = event.clientY - state.dragStartY;
    updateTransform();
});

window.addEventListener("mouseup", () => {
    state.dragging = false;
    viewport.classList.remove("dragging");
});

viewport.addEventListener("wheel", event => {
    event.preventDefault();
    zoomBy(event.deltaY < 0 ? 1.12 : 0.9);
}, { passive: false });

stage.addEventListener("click", event => {
    if (event.target === stage) {
        state.selectedNodeId = "";
        state.selectedNode = null;
        renderDetail(null);
        renderGraph(state.graph);
    }
});

document.getElementById("fitBtn").addEventListener("click", fitToViewport);
document.getElementById("zoomInBtn").addEventListener("click", () => zoomBy(1.16));
document.getElementById("zoomOutBtn").addEventListener("click", () => zoomBy(0.86));
document.getElementById("downloadSvgBtn").addEventListener("click", downloadSvg);
document.getElementById("downloadPngBtn").addEventListener("click", downloadPng);
document.getElementById("searchMemberBtn").addEventListener("click", () => searchMembers().catch(error => setMessage(error.message)));
document.getElementById("createMemberBtn").addEventListener("click", () => createMember().catch(error => setMessage(error.message)));
document.getElementById("clearMemberFormBtn").addEventListener("click", clearNewMemberForm);
document.getElementById("saveUnionBtn").addEventListener("click", () => saveUnion().catch(error => setMessage(error.message)));
document.getElementById("deleteUnionBtn").addEventListener("click", () => deleteUnion().catch(error => setMessage(error.message)));
document.getElementById("clearUnionBtn").addEventListener("click", clearUnionForm);
document.getElementById("addChildBtn").addEventListener("click", () => addChildRelation().catch(error => setMessage(error.message)));
document.getElementById("removeChildBtn").addEventListener("click", () => removeChildRelation().catch(error => setMessage(error.message)));
document.getElementById("quickParentsBtn").addEventListener("click", () => quickAddParents().catch(error => setMessage(error.message)));
document.getElementById("unionPicker").addEventListener("change", event => {
    const item = findUnion(event.target.value);
    if (item) {
        fillUnionForm(item);
    }
});
document.getElementById("memberKeyword").addEventListener("keydown", event => {
    if (event.key === "Enter") {
        event.preventDefault();
        searchMembers().catch(error => setMessage(error.message));
    }
});
document.getElementById("logoutBtn").addEventListener("click", () => {
    localStorage.removeItem("token");
    location.href = "index.html";
});

window.addEventListener("resize", () => {
    if (state.graph) {
        fitToViewport();
    }
});

Promise.all([loadTree(), loadUnions()])
    .then(() => loadGraph())
    .catch(error => {
        setMessage(error.message);
        stage.innerHTML = "";
        detailContent.innerHTML = "加载失败，请稍后重试。";
    });

window.pickMember = pickMember;
window.useSelectedAsPartner = useSelectedAsPartner;
window.useSelectedAsChild = useSelectedAsChild;
window.useSelectedUnion = useSelectedUnion;
