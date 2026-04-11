const token = localStorage.getItem("token");
        const params = new URLSearchParams(location.search);
        const treeId = params.get("treeId") || params.get("id");
        const viewport = document.getElementById("viewport");
        const stage = document.getElementById("stage");
        const detailContent = document.getElementById("detailContent");

        const state = {
            graph: null,
            scale: 1,
            offsetX: 0,
            offsetY: 0,
            dragging: false,
            dragStartX: 0,
            dragStartY: 0,
            selectedNodeId: ""
        };

        if (!treeId) {
            location.href = "public.html";
        }

        document.getElementById("backLink").href = `tree-detail.html?id=${encodeURIComponent(treeId)}`;
        document.getElementById("detailLink").href = `tree-detail.html?id=${encodeURIComponent(treeId)}`;

        if (!token) {
            document.getElementById("logoutBtn").textContent = "返回首页";
        }

        function authHeaders() {
            return token ? { Authorization: `Bearer ${token}` } : {};
        }

        function esc(value) {
            return String(value ?? "")
                .replace(/&/g, "&amp;")
                .replace(/</g, "&lt;")
                .replace(/>/g, "&gt;")
                .replace(/"/g, "&quot;")
                .replace(/'/g, "&#39;");
        }

        function setMessage(text) {
            document.getElementById("message").textContent = text || "";
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

        async function req(url, options = {}) {
            const response = await fetch(url, options);
            const contentType = response.headers.get("content-type") || "";
            const payload = contentType.includes("application/json") ? await response.json() : await response.text();
            if (!response.ok) {
                throw new Error(typeof payload === "string" ? payload : payload?.message || "请求失败");
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
            state.scale = Math.max(0.18, Math.min(scaleX, scaleY, 1));
            state.offsetX = (viewportWidth - state.graph.width * state.scale) / 2;
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

        function renderDetail(node) {
            if (!node) {
                detailContent.innerHTML = "点击左侧节点查看详细信息。";
                return;
            }

            if (node.kind === "member") {
                detailContent.innerHTML = `
                    <div>节点类型：成员</div>
                    <div>姓名：${esc(node.label)}</div>
                    <div>说明：${esc(node.subtitle)}</div>
                    <div>成员 ID：${esc(node.memberId || "")}</div>
                    <div>代际：第 ${esc(node.generation)} 代</div>
                `;
                return;
            }

            detailContent.innerHTML = `
                <div>节点类型：婚姻单元</div>
                <div>婚姻类型：${esc(node.label)}</div>
                <div>说明：${esc(node.subtitle)}</div>
                <div>单元 ID：${esc(node.unionId || "")}</div>
                <div>所在层级：第 ${esc(node.generation)} 代之间</div>
            `;
        }

        function renderGraph(graph) {
            state.graph = graph;
            document.getElementById("pageTitle").textContent = `${graph.treeName} · 婚姻单元树`;
            document.getElementById("pageSubtitle").textContent = "以成员、婚姻单元和子女关系组成图结构，适合家谱树的整支脉络浏览、分房定位和婚姻关系展示。";
            document.getElementById("memberCount").textContent = String(graph.memberCount || 0);
            document.getElementById("unionCount").textContent = String(graph.unionCount || 0);
            document.getElementById("generationCount").textContent = String(graph.generationCount || 0);
            document.getElementById("stageSize").textContent = `${Math.round(graph.width)} x ${Math.round(graph.height)}`;

            stage.setAttribute("viewBox", `0 0 ${graph.width} ${graph.height}`);
            stage.setAttribute("width", graph.width);
            stage.setAttribute("height", graph.height);

            const nodesById = new Map(graph.nodes.map(node => [node.id, node]));

            const edgesMarkup = graph.edges.map(edge => {
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

            const nodesMarkup = graph.nodes.map(node => {
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

            stage.innerHTML = `${edgesMarkup}${nodesMarkup}`;

            stage.querySelectorAll(".node").forEach(element => {
                element.addEventListener("click", event => {
                    event.stopPropagation();
                    state.selectedNodeId = element.getAttribute("data-id") || "";
                    renderDetail(nodesById.get(state.selectedNodeId));
                    renderGraph(state.graph);
                });
            });

            fitToViewport();
        }

        function buildSvgMarkup() {
            if (!state.graph) {
                throw new Error("当前还没有可下载的图数据");
            }

            return `<?xml version="1.0" encoding="UTF-8"?>\n${stage.outerHTML}`;
        }

        function downloadSvg() {
            try {
                const blob = new Blob([buildSvgMarkup()], { type: "image/svg+xml;charset=utf-8" });
                downloadBlob(blob, `${safeFileName(state.graph?.treeName)}-union-tree.svg`);
            } catch (error) {
                setMessage(error.message);
            }
        }

        async function downloadPng() {
            try {
                const markup = buildSvgMarkup();
                const blob = new Blob([markup], { type: "image/svg+xml;charset=utf-8" });
                const url = URL.createObjectURL(blob);
                const image = new Image();
                image.decoding = "async";

                await new Promise((resolve, reject) => {
                    image.onload = resolve;
                    image.onerror = () => reject(new Error("婚姻图转换 PNG 失败"));
                    image.src = url;
                });

                const canvas = document.createElement("canvas");
                canvas.width = Math.ceil(state.graph.width);
                canvas.height = Math.ceil(state.graph.height);
                const context = canvas.getContext("2d");
                context.fillStyle = "#fdfaf2";
                context.fillRect(0, 0, canvas.width, canvas.height);
                context.drawImage(image, 0, 0);

                const pngBlob = await new Promise(resolve => canvas.toBlob(resolve, "image/png"));
                URL.revokeObjectURL(url);
                if (!pngBlob) {
                    throw new Error("PNG 导出失败");
                }

                downloadBlob(pngBlob, `${safeFileName(state.graph?.treeName)}-union-tree.png`);
            } catch (error) {
                setMessage(error.message);
            }
        }

        async function loadGraph() {
            setMessage("正在读取婚姻单元树数据...");
            const result = await req(`/api/Union/graph/${treeId}`, { headers: authHeaders() });
            renderGraph(result.data || {});
            setMessage("");
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
                renderDetail(null);
                renderGraph(state.graph);
            }
        });

        document.getElementById("fitBtn").addEventListener("click", fitToViewport);
        document.getElementById("zoomInBtn").addEventListener("click", () => zoomBy(1.16));
        document.getElementById("zoomOutBtn").addEventListener("click", () => zoomBy(0.86));
        document.getElementById("downloadSvgBtn").addEventListener("click", downloadSvg);
        document.getElementById("downloadPngBtn").addEventListener("click", downloadPng);
        document.getElementById("logoutBtn").addEventListener("click", () => {
            localStorage.removeItem("token");
            location.href = "index.html";
        });

        window.addEventListener("resize", () => {
            if (state.graph) {
                fitToViewport();
            }
        });

        loadGraph().catch(error => {
            setMessage(error.message);
            stage.innerHTML = "";
            detailContent.innerHTML = "加载失败，请稍后重试。";
        });
