// Workflow canvas interaction layer (experimental).
//
// The contract with C#: this file owns everything transient — pan, zoom, drag-in-progress,
// hover, connection-in-progress. C# owns committed state. Only four things ever cross back: a
// node's final dropped position, a completed connection, a selection change, and the viewport
// after a pan/zoom.
//
// This is not an optimisation. Blazor Server sends every event over SignalR, so a pointermove
// handler in C# would be a network round-trip per frame.

(function () {
    "use strict";

    const states = new WeakMap();

    function stateOf(host) {
        return states.get(host);
    }

    // A pointer can vanish between events (lost capture, synthetic events, devtools). Capturing
    // is an optimisation, not a correctness requirement, so never let it throw.
    function capture(host, pointerId) {
        try { host.setPointerCapture(pointerId); } catch { /* pointer already gone */ }
    }

    function release(host, pointerId) {
        try { host.releasePointerCapture(pointerId); } catch { /* already released */ }
    }

    // Level-of-detail tiers, purely zoom-driven. Never touches Blazor — a tier change is a CSS
    // attribute write, the same DOM-only contract every other live value in this file follows.
    function lodForZoom(zoom) {
        if (zoom >= 0.8) return 0;   // full card
        if (zoom >= 0.4) return 1;   // header + first property row
        return 2;                    // colour tile only
    }

    function applyTransform(s) {
        s.world.style.transform =
            `translate(${s.panX}px, ${s.panY}px) scale(${s.zoom})`;
        // Keep the dot grid locked to the content.
        s.host.style.backgroundPosition = `${s.panX}px ${s.panY}px`;
        s.host.style.backgroundSize = `${24 * s.zoom}px ${24 * s.zoom}px`;

        const lod = String(lodForZoom(s.zoom));
        if (s.world.dataset.lod !== lod) {
            s.world.dataset.lod = lod;
            // The tier change collapses or expands every card via CSS, so their heights - and
            // therefore every edge's attachment point - just changed. C# cannot compute this: the
            // LOD is a zoom/CSS concern it has no knowledge of, so it always renders paths for the
            // full-size card and edges hang below collapsed ones. Re-derive from measured DOM.
            requestAnimationFrame(() => refreshAllEdges(s));
        }
    }

    // Recomputes every edge path from the CURRENT measured geometry. The authoritative paths come
    // from WorkflowEdgeGeometry on the server, which is right at full detail; this corrects them
    // whenever CSS has resized the cards underneath.
    function refreshAllEdges(s) {
        const layer = s.world.querySelector(".wf-edge-layer");
        if (!layer) return;

        layer.querySelectorAll("path[data-to]").forEach(path => {
            const toEl = s.world.querySelector(`.wf-node[data-node-id="${path.dataset.to}"]`);
            if (!toEl) return;

            const tx = parseFloat(toEl.dataset.x || "0");
            const ty = parseFloat(toEl.dataset.y || "0");

            let x1, y1;
            const pinOwner = path.dataset.pinOwner;

            if (pinOwner) {
                const ownerEl = s.world.querySelector(`.wf-node[data-node-id="${pinOwner}"]`);
                if (!ownerEl) return;
                x1 = parseFloat(ownerEl.dataset.x || "0") + ownerEl.offsetWidth;
                y1 = parseFloat(ownerEl.dataset.y || "0")
                     + parseFloat(path.dataset.pinFrac || "0.5") * ownerEl.offsetHeight;
            } else {
                const fromEl = s.world.querySelector(`.wf-node[data-node-id="${path.dataset.from}"]`);
                if (!fromEl) return;
                x1 = parseFloat(fromEl.dataset.x || "0") + fromEl.offsetWidth;
                y1 = parseFloat(fromEl.dataset.y || "0") + fromEl.offsetHeight / 2;
            }

            path.setAttribute("d", cubic(x1, y1, tx, ty + toEl.offsetHeight / 2));
        });
    }

    function notifyViewport(s) {
        if (!s.dotNet) return;
        s.dotNet.invokeMethodAsync("OnViewportChanged", s.panX, s.panY, s.zoom);
    }

    // Mirrors WorkflowEdgeGeometry.BezierPath. Keep the two in step.
    function cubic(x1, y1, x2, y2) {
        const dx = Math.max(60, Math.abs(x2 - x1) * 0.5);
        return `M ${x1} ${y1} C ${x1 + dx} ${y1} ${x2 - dx} ${y2} ${x2} ${y2}`;
    }

    // ---- panning ------------------------------------------------------------

    function beginPan(s, e) {
        s.panning = true;
        s.startX = e.clientX - s.panX;
        s.startY = e.clientY - s.panY;
        s.host.classList.add("wf-panning");
        capture(s.host, e.pointerId);
    }

    function movePan(s, e) {
        s.panX = e.clientX - s.startX;
        s.panY = e.clientY - s.startY;
        applyTransform(s);          // no .NET call — this is the whole point
    }

    function endPan(s, e) {
        s.panning = false;
        s.host.classList.remove("wf-panning");
        release(s.host, e.pointerId);
        notifyViewport(s);          // one call, on release
    }

    // ---- node dragging --------------------------------------------------------
    //
    // The node is moved by writing its transform directly. C# is told once, on release. A
    // pointermove that invoked .NET would be one SignalR round-trip per frame.

    function beginNodeDrag(s, e) {
        const nodeEl = e.target.closest(".wf-node");
        if (!nodeEl) return false;

        // Group drag: if the grabbed node is already part of a multi-node selection, every
        // selected node moves together - otherwise this is a plain single-node drag, unchanged.
        // .wf-node--selected is a normal Blazor parameter (IsSelected), so it always reflects the
        // server-confirmed selection by the time a drag can start.
        const selectedEls = [...s.world.querySelectorAll(".wf-node--selected")];
        const group = selectedEls.includes(nodeEl) && selectedEls.length > 1 ? selectedEls : [nodeEl];

        s.drag = {
            nodes: group.map(el => ({
                el,
                id: el.dataset.nodeId,
                startX: parseFloat(el.dataset.x || "0"),
                startY: parseFloat(el.dataset.y || "0"),
            })),
            pointerX: e.clientX,
            pointerY: e.clientY,
            additive: e.ctrlKey || e.shiftKey,
        };

        group.forEach(el => el.classList.add("wf-node--dragging"));
        capture(s.host, e.pointerId);
        return true;
    }

    function moveNodeDrag(s, e) {
        const d = s.drag;
        // Divide by zoom: a 10px pointer move at 0.5x zoom is 20px in world space. Every node in
        // the group moves by the SAME delta from its own start position, so the group's relative
        // layout is preserved exactly as dragged.
        const dx = (e.clientX - d.pointerX) / s.zoom;
        const dy = (e.clientY - d.pointerY) / s.zoom;

        for (const n of d.nodes) {
            const x = n.startX + dx;
            const y = n.startY + dy;
            n.el.style.transform = `translate(${x}px, ${y}px)`;
            n.lastX = x;
            n.lastY = y;
            redrawEdgesFor(s, n.id, x, y);
        }
    }

    function endNodeDrag(s, e) {
        const d = s.drag;
        s.drag = null;
        d.nodes.forEach(n => n.el.classList.remove("wf-node--dragging"));
        release(s.host, e.pointerId);

        const dx = e.clientX - d.pointerX;
        const dy = e.clientY - d.pointerY;
        const isRealDrag = Math.abs(dx) > MARQUEE_CLICK_THRESHOLD_PX || Math.abs(dy) > MARQUEE_CLICK_THRESHOLD_PX;

        if (!isRealDrag) {
            // A plain click on the drag handle (.wf-node__header / the channel cap), not a real
            // drag. setPointerCapture just above diverts the browser's native click synthesis
            // away from that element, so a Blazor @onclick bound there would never fire - the
            // same pointer-capture trap that killed the toolbar buttons (session 36's finding:
            // "a programmatic el.click() cannot reproduce this - only a real CDP click"; a
            // scripted verification of this exact path passed for the same reason and missed it).
            // Dispatched directly instead of depending on a synthesized click.
            if (s.dotNet) s.dotNet.invokeMethodAsync("OnHeaderClick", d.nodes[0].id, d.additive);
            return;
        }

        const snap = s.snap;
        const moves = d.nodes.map(n => {
            const x = snap ? Math.round((n.lastX ?? n.startX) / snap) * snap : (n.lastX ?? n.startX);
            const y = snap ? Math.round((n.lastY ?? n.startY) / snap) * snap : (n.lastY ?? n.startY);
            n.lastX = x;
            n.lastY = y;
            n.el.style.transform = `translate(${x}px, ${y}px)`;
            n.el.dataset.x = x;
            n.el.dataset.y = y;
            redrawEdgesFor(s, n.id, x, y);
            return n;
        });

        if (!s.dotNet) return;

        // Single-node path unchanged (OnNodeMoved); group moves go through OnNodesMoved so C#
        // updates every node's position in one graph mutation instead of one SignalR round-trip
        // and one _dirty-triggering re-render per node.
        if (moves.length === 1) {
            s.dotNet.invokeMethodAsync("OnNodeMoved", moves[0].id, moves[0].lastX, moves[0].lastY);
        } else {
            s.dotNet.invokeMethodAsync(
                "OnNodesMoved",
                moves.map(n => n.id), moves.map(n => n.lastX), moves.map(n => n.lastY));
        }
    }

    // Recompute only the paths touching this node, in JS, so a drag does not re-render Blazor.
    // The C# geometry in WorkflowEdgeGeometry is the source of truth; this mirrors it and the
    // authoritative version is re-rendered by Blazor on drop.
    function redrawEdgesFor(s, nodeId, x, y) {
        const layer = s.world.querySelector(".wf-edge-layer");
        if (!layer) return;

        // data-pin-owner is included deliberately: a Board->Channel edge starts at a pin on its
        // DEVICE, but its data-from is the board, which no drag will ever match. Selecting only on
        // from/to left every channel edge stranded when the device moved.
        layer.querySelectorAll(
            `path[data-from="${nodeId}"], path[data-to="${nodeId}"], path[data-pin-owner="${nodeId}"]`)
            .forEach(path => {
                const toEl = s.world.querySelector(`.wf-node[data-node-id="${path.dataset.to}"]`);
                if (!toEl) return;

                const tx = path.dataset.to === nodeId ? x : parseFloat(toEl.dataset.x || "0");
                const ty = path.dataset.to === nodeId ? y : parseFloat(toEl.dataset.y || "0");

                let x1, y1;
                const pinOwner = path.dataset.pinOwner;

                if (pinOwner) {
                    const ownerEl = s.world.querySelector(`.wf-node[data-node-id="${pinOwner}"]`);
                    if (!ownerEl) return;
                    const ox = pinOwner === nodeId ? x : parseFloat(ownerEl.dataset.x || "0");
                    const oy = pinOwner === nodeId ? y : parseFloat(ownerEl.dataset.y || "0");
                    x1 = ox + ownerEl.offsetWidth;
                    y1 = oy + parseFloat(path.dataset.pinFrac || "0.5") * ownerEl.offsetHeight;
                } else {
                    const fromEl = s.world.querySelector(`.wf-node[data-node-id="${path.dataset.from}"]`);
                    if (!fromEl) return;
                    const fx = path.dataset.from === nodeId ? x : parseFloat(fromEl.dataset.x || "0");
                    const fy = path.dataset.from === nodeId ? y : parseFloat(fromEl.dataset.y || "0");
                    x1 = fx + fromEl.offsetWidth;
                    y1 = fy + fromEl.offsetHeight / 2;
                }

                path.setAttribute("d", cubic(x1, y1, tx, ty + toEl.offsetHeight / 2));
            });
    }

    // ---- connection dragging -----------------------------------------------

    function beginConnect(s, e) {
        const port = e.target.closest(".wf-port");
        if (!port) return false;

        const nodeEl = port.closest(".wf-node");
        s.connect = {
            fromId: nodeEl.dataset.nodeId,
            x1: parseFloat(nodeEl.dataset.x || "0") + nodeEl.offsetWidth,
            y1: parseFloat(nodeEl.dataset.y || "0") + nodeEl.offsetHeight / 2,
        };

        const layer = s.world.querySelector(".wf-edge-layer");
        s.connect.ghost = document.createElementNS("http://www.w3.org/2000/svg", "path");
        s.connect.ghost.setAttribute("class", "wf-ghost-edge");
        layer.appendChild(s.connect.ghost);

        capture(s.host, e.pointerId);
        return true;
    }

    function moveConnect(s, e) {
        const rect = s.host.getBoundingClientRect();
        const x = (e.clientX - rect.left - s.panX) / s.zoom;
        const y = (e.clientY - rect.top - s.panY) / s.zoom;
        s.connect.ghost.setAttribute("d", cubic(s.connect.x1, s.connect.y1, x, y));
    }

    function endConnect(s, e) {
        const c = s.connect;
        s.connect = null;
        c.ghost.remove();
        release(s.host, e.pointerId);

        const dropped = document.elementFromPoint(e.clientX, e.clientY);
        const targetNode = dropped && dropped.closest(".wf-node");
        if (!targetNode) return;                       // dropped on empty space: no-op

        const toId = targetNode.dataset.nodeId;
        if (toId === c.fromId) return;

        // C# validates. JS never decides whether a connection is legal.
        if (s.dotNet) s.dotNet.invokeMethodAsync("OnConnect", c.fromId, toId);
    }

    // ---- pointer dispatch ----------------------------------------------------
    //
    // Priority order on press: port (start a connection), node header (start a drag), node body
    // (select it), empty canvas (pan / deselect).

    // ---- marquee selection ----------------------------------------------------

    function worldPoint(s, e) {
        const rect = s.host.getBoundingClientRect();
        return {
            x: (e.clientX - rect.left - s.panX) / s.zoom,
            y: (e.clientY - rect.top - s.panY) / s.zoom,
        };
    }

    function beginMarquee(s, e, additive) {
        const start = worldPoint(s, e);
        s.marquee = { startWorld: start, additive, el: document.createElement("div") };
        s.marquee.el.className = "wf-marquee";
        s.host.appendChild(s.marquee.el);
        positionMarqueeEl(s, e);
        capture(s.host, e.pointerId);
    }

    function positionMarqueeEl(s, e) {
        const hostRect = s.host.getBoundingClientRect();
        const startScreenX = s.marquee.startWorld.x * s.zoom + s.panX;
        const startScreenY = s.marquee.startWorld.y * s.zoom + s.panY;
        const curX = e.clientX - hostRect.left;
        const curY = e.clientY - hostRect.top;

        const left = Math.min(startScreenX, curX);
        const top = Math.min(startScreenY, curY);
        const width = Math.abs(curX - startScreenX);
        const height = Math.abs(curY - startScreenY);

        Object.assign(s.marquee.el.style, {
            left: `${left}px`, top: `${top}px`, width: `${width}px`, height: `${height}px`,
        });
        s.marquee.lastRect = { left, top, width, height };
    }

    // Below this, a pointerdown-and-up with negligible movement is treated as a click on empty
    // space (clear selection), not a zero-area marquee.
    const MARQUEE_CLICK_THRESHOLD_PX = 4;

    function endMarquee(s, e) {
        const m = s.marquee;
        s.marquee = null;
        release(s.host, e.pointerId);

        const rect = m.lastRect;
        const isRealDrag = rect.width > MARQUEE_CLICK_THRESHOLD_PX || rect.height > MARQUEE_CLICK_THRESHOLD_PX;
        m.el.remove();

        if (!isRealDrag) {
            if (s.dotNet) s.dotNet.invokeMethodAsync("OnSelect", null, m.additive);
            return;
        }

        // World-space box, from the same screen rect the overlay was drawn with.
        const worldLeft = (rect.left - s.panX) / s.zoom;
        const worldTop = (rect.top - s.panY) / s.zoom;
        const worldRight = worldLeft + rect.width / s.zoom;
        const worldBottom = worldTop + rect.height / s.zoom;

        const hitIds = [];
        s.world.querySelectorAll('.wf-node[data-node-id^="chn-"]').forEach(node => {
            const x = parseFloat(node.dataset.x || "0");
            const y = parseFloat(node.dataset.y || "0");
            const right = x + node.offsetWidth;
            const bottom = y + node.offsetHeight;
            const intersects = x < worldRight && right > worldLeft && y < worldBottom && bottom > worldTop;
            if (intersects) hitIds.push(node.dataset.nodeId);
        });

        if (s.dotNet) s.dotNet.invokeMethodAsync("OnMarqueeSelect", hitIds, m.additive);
    }

    // Controls that float ON the canvas are DOM children of the host, so their pointerdown
    // bubbles to this handler. Without this guard the handler fell through to beginMarquee, which
    // takes setPointerCapture on the host — that redirects the pointerup away from the button, so
    // the browser never synthesises a click and every toolbar/minimap/action-bar button looks
    // dead. It also fired OnSelect(null) on release, silently clearing the user's selection.
    //
    // Note a programmatic el.click() dispatches a click directly and therefore always "worked",
    // which is why this survived every scripted verification and only showed up under real mouse
    // input. Scripted clicking cannot test this class of bug.
    const CHROME = ".wf-toolbar-dock, .wf-minimap, .wf-selection-action-bar";

    // "online" is not a status - it is every status except offline, matching how the dashboard
    // defines its Online count. Both dim paths (the immediate re-scan on a filter click and the
    // per-tick telemetry write) must agree, or a filter appears to work then undoes itself on the
    // next hardware event.
    function matchesStatusFilter(statusName, filter) {
        if (filter === "online") return statusName !== "offline";
        return statusName === filter;
    }

    function onPointerDown(e) {
        const s = stateOf(e.currentTarget);
        if (!s) return;

        if (e.target.closest(CHROME)) return;

        if (beginConnect(s, e)) return;

        if (e.target.closest(".wf-node__header") && beginNodeDrag(s, e)) return;

        const nodeEl = e.target.closest(".wf-node");
        if (nodeEl) {
            const additive = e.ctrlKey || e.shiftKey;
            if (s.dotNet) s.dotNet.invokeMethodAsync("OnSelect", nodeEl.dataset.nodeId, additive);
            return;
        }

        // Empty canvas: middle-button or Space+drag pans (unchanged pan mechanics); a plain
        // left-drag is now a marquee instead of a pan (D12) - panning moved here deliberately,
        // since selection is the primary verb on an operating surface.
        // Middle-button and Space+drag always pan, whatever the mode, so a pan is never more than
        // one gesture away while selecting.
        if (e.button === 1 || s.spaceHeld || s.mode === "pan") {
            beginPan(s, e);
            return;
        }

        beginMarquee(s, e, e.ctrlKey || e.shiftKey);
    }

    function onPointerMove(e) {
        const s = stateOf(e.currentTarget);
        if (!s) return;
        if (s.connect) { moveConnect(s, e); return; }
        if (s.drag) { moveNodeDrag(s, e); return; }
        if (s.marquee) { positionMarqueeEl(s, e); return; }
        if (s.panning) movePan(s, e);
    }

    function onPointerUp(e) {
        const s = stateOf(e.currentTarget);
        if (!s) return;
        if (s.connect) { endConnect(s, e); return; }
        if (s.drag) { endNodeDrag(s, e); return; }
        if (s.marquee) { endMarquee(s, e); return; }
        if (s.panning) endPan(s, e);
    }

    function onWheel(e) {
        const s = stateOf(e.currentTarget);
        if (!s) return;

        // Same reasoning as onPointerDown: a scroll inside a popover (node fields, layouts) must
        // scroll that list, not zoom the canvas underneath it.
        if (e.target.closest(CHROME)) return;

        e.preventDefault();

        const rect = s.host.getBoundingClientRect();
        const px = e.clientX - rect.left;
        const py = e.clientY - rect.top;

        const factor = e.deltaY < 0 ? 1.1 : 1 / 1.1;
        const next = Math.min(s.maxZoom, Math.max(s.minZoom, s.zoom * factor));
        if (next === s.zoom) return;

        // Keep the point under the cursor stationary while zooming.
        s.panX = px - ((px - s.panX) * next) / s.zoom;
        s.panY = py - ((py - s.panY) * next) / s.zoom;
        s.zoom = next;

        applyTransform(s);

        clearTimeout(s.wheelTimer);
        s.wheelTimer = setTimeout(() => notifyViewport(s), 200);  // debounce the commit
    }

    function setText(node, role, value) {
        const el = node.querySelector(`[data-role="${role}"]`);
        if (el && el.textContent !== value) el.textContent = value;
    }

    // A channel's status drives the battery on the other end of its power edge, and the edge
    // itself. Both are found by walking the rendered DOM rather than re-querying C#.
    function applyFlowToEdges(s, e) {
        const layer = s.world.querySelector(".wf-edge-layer");
        if (!layer) return;

        // EVERY edge touching this channel, not just power wires. The line a user actually looks
        // at is the device->channel one, and restricting this to .wf-edge--power meant that line
        // never animated - the motion only existed on a Channel-Battery wire, which most layouts
        // do not even have.
        const edges = layer.querySelectorAll(
            `path[data-from="${e.nodeId}"], path[data-to="${e.nodeId}"]`);

        edges.forEach(path => {
            path.style.setProperty("--wf-status", e.statusHsl);
            path.classList.toggle("wf-edge--flowing", e.flow !== 0);
            path.classList.toggle("wf-edge--flowing-reverse", e.flow < 0);

            const otherId = path.dataset.from === e.nodeId ? path.dataset.to : path.dataset.from;
            const other = s.world.querySelector(`.wf-node[data-node-id="${otherId}"]`);
            if (!other) return;

            const battery = other.querySelector('[data-role="battery"]');
            if (!battery) return;

            battery.style.setProperty("--wf-status", e.statusHsl);
            battery.style.setProperty("--wf-soc", String(e.soc));
            battery.classList.toggle("wf-battery--charging", e.flow > 0);
            battery.classList.toggle("wf-battery--fault", e.statusName === "error");

            // Same pre-formatted text the channel cell uses, so an unknown SoC reads "--%" on the
            // battery node too rather than "0%". Before SoC was derived every channel reported 0
            // and the distinction did not exist.
            setText(other, "soc", e.socText);
        });
    }

    window.workflowCanvas = {
        init(host, dotNetRef, options) {
            if (!host || states.has(host)) return;

            const world = host.querySelector(".wf-world");
            if (!world) {
                console.error("[workflowCanvas] host has no .wf-world child");
                return;
            }

            const opts = options || {};
            const s = {
                host, world,
                dotNet: dotNetRef,
                panX: 0, panY: 0, zoom: 1,
                panning: false, startX: 0, startY: 0,
                drag: null, connect: null,
                marquee: null,
                spaceHeld: false,

                // "pan" = drag the canvas (hand), "select" = drag a marquee. Defaults to pan:
                // dragging to move the view is what every canvas does, and Phase 3 had made a
                // plain left-drag a marquee with panning hidden behind middle-button or
                // Space+drag - neither of which is discoverable, so the canvas felt immovable.
                mode: "pan",
                snap: opts.snap || 0,
                minZoom: opts.minZoom || 0.2,
                maxZoom: opts.maxZoom || 2.5,
                wheelTimer: 0,
                handlers: {},
            };

            s.handlers.down = onPointerDown;
            s.handlers.move = onPointerMove;
            s.handlers.up = onPointerUp;
            s.handlers.wheel = onWheel;
            host.classList.toggle("wf-mode-pan", s.mode === "pan");

            s.handlers.keydown = (ev) => {
                if (ev.code === "Space") { s.spaceHeld = true; return; }

                // Delete/Backspace removes the current selection, right on the canvas - the
                // counterpart to placing a node from the palette. Never fires while the user is
                // typing (a layout name, the refresh-rate spinner, a program/DBC dropdown) - only
                // an editable/form control can legitimately want Backspace for itself.
                if (ev.code === "Delete" || ev.code === "Backspace") {
                    const tag = document.activeElement?.tagName;
                    const typing = tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT"
                        || document.activeElement?.isContentEditable;
                    if (typing) return;

                    ev.preventDefault();
                    if (s.dotNet) s.dotNet.invokeMethodAsync("OnDeleteSelected");
                }
            };
            s.handlers.keyup = (ev) => { if (ev.code === "Space") s.spaceHeld = false; };

            host.addEventListener("pointerdown", s.handlers.down);
            host.addEventListener("pointermove", s.handlers.move);
            host.addEventListener("pointerup", s.handlers.up);
            host.addEventListener("pointercancel", s.handlers.up);
            host.addEventListener("wheel", s.handlers.wheel, { passive: false });
            document.addEventListener("keydown", s.handlers.keydown);
            document.addEventListener("keyup", s.handlers.keyup);

            states.set(host, s);
            applyTransform(s);
        },

        dispose(host) {
            const s = stateOf(host);
            if (!s) return;

            host.removeEventListener("pointerdown", s.handlers.down);
            host.removeEventListener("pointermove", s.handlers.move);
            host.removeEventListener("pointerup", s.handlers.up);
            host.removeEventListener("pointercancel", s.handlers.up);
            host.removeEventListener("wheel", s.handlers.wheel);
            document.removeEventListener("keydown", s.handlers.keydown);
            document.removeEventListener("keyup", s.handlers.keyup);

            clearTimeout(s.wheelTimer);
            states.delete(host);
        },

        setSnap(host, snapPx) {
            const s = stateOf(host);
            if (s) s.snap = snapPx || 0;
        },

        getViewport(host) {
            const s = stateOf(host);
            return s ? { panX: s.panX, panY: s.panY, zoom: s.zoom } : { panX: 0, panY: 0, zoom: 1 };
        },

        setViewport(host, panX, panY, zoom) {
            const s = stateOf(host);
            if (!s) return;
            s.panX = panX;
            s.panY = panY;
            s.zoom = Math.min(s.maxZoom, Math.max(s.minZoom, zoom || 1));
            applyTransform(s);
        },

        // Called after a Blazor render that can change card heights (the property picker) at a
        // constant zoom, where no LOD change fires. Cheap: one setAttribute per edge, no layout
        // thrash beyond the reads it already needs.
        setMode(host, mode) {
            const s = stateOf(host);
            if (!s) return;
            s.mode = mode === "select" ? "select" : "pan";
            s.host.classList.toggle("wf-mode-pan", s.mode === "pan");
        },

        refreshEdges(host) {
            const s = stateOf(host);
            if (s) refreshAllEdges(s);
        },

        setStatusFilter(host, statusName) {
            const s = stateOf(host);
            if (!s) return;
            s.statusFilter = statusName || null;

            // Re-scan every already-rendered channel node immediately, using the status text the
            // last telemetry tick already wrote - the next tick would eventually reapply this
            // anyway, but a filter click should not wait for the next hardware event to visibly
            // take effect.
            s.world.querySelectorAll('.wf-node[data-node-id^="chn-"]').forEach(node => {
                const status = node.querySelector('[data-role="status"]')?.textContent;
                node.classList.toggle(
                    "wf-node--dimmed", !!s.statusFilter && !matchesStatusFilter(status, s.statusFilter));
            });
        },

        fitToContent(host) {
            const s = stateOf(host);
            if (!s) return;

            const nodes = s.world.querySelectorAll(".wf-node");
            if (nodes.length === 0) {
                this.setViewport(host, 0, 0, 1);
                return;
            }

            // Measure in world coordinates by reading the inline transform we set ourselves,
            // rather than getBoundingClientRect, which is already zoomed.
            let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
            nodes.forEach(n => {
                const x = parseFloat(n.dataset.x || "0");
                const y = parseFloat(n.dataset.y || "0");
                minX = Math.min(minX, x);
                minY = Math.min(minY, y);
                maxX = Math.max(maxX, x + n.offsetWidth);
                maxY = Math.max(maxY, y + n.offsetHeight);
            });

            const pad = 40;
            const rect = s.host.getBoundingClientRect();
            const zoom = Math.min(
                s.maxZoom,
                Math.max(s.minZoom,
                    Math.min(rect.width / (maxX - minX + pad * 2),
                             rect.height / (maxY - minY + pad * 2))));

            this.setViewport(host, pad - minX * zoom, pad - minY * zoom, zoom);
            notifyViewport(stateOf(host));
        },

        // One call per coalesced tick. Everything here is a CSS custom property write or a
        // class toggle — no layout reads, no Blazor involvement, no re-render.
        applyTelemetry(host, entries, rollups) {
            const s = stateOf(host);
            if (!s || !entries) return;

            for (const e of entries) {
                const node = s.world.querySelector(`.wf-node[data-node-id="${e.nodeId}"]`);
                if (!node) continue;

                node.style.setProperty("--wf-status", e.statusHsl);
                node.classList.toggle("wf-node--active", e.flow !== 0);
                node.classList.toggle(
                    "wf-node--dimmed",
                    !!s.statusFilter && !matchesStatusFilter(e.statusName, s.statusFilter));

                // The channel battery cell. --wf-soc drives the fill height, the surface line and
                // the header icon's inner bar, all as percentages, so they stay correct at any
                // cell height. Idle/pause/interrupt/error deliberately get NO march: motion on an
                // error state reads as activity.
                const cell = node.querySelector('[data-role="cell"]');
                if (cell) {
                    // .wf-bcell declares its OWN fallback default for --wf-status (so an
                    // unrendered/untelemetered cell still has a sane colour) - CSS inheritance
                    // never reaches past that declaration, so writing --wf-status only on `node`
                    // (above) left every fill/march/surface inside the cell permanently grey
                    // regardless of the real status, while the cap (which declares no such
                    // fallback and genuinely inherits from `node`) coloured correctly. Written
                    // explicitly here for the same reason --wf-soc already is.
                    cell.style.setProperty("--wf-status", e.statusHsl);
                    cell.style.setProperty("--wf-soc", String(e.soc));
                    cell.classList.toggle("wf-bcell--soc-unknown", !e.socKnown);
                    cell.classList.toggle(
                        "wf-bcell--charging",
                        e.flow > 0 && (e.statusName === "charge" || e.statusName === "continue"));
                    cell.classList.toggle(
                        "wf-bcell--discharging", e.flow < 0 && e.statusName === "discharging");
                    cell.classList.toggle("wf-bcell--fault", e.statusName === "error");
                }

                // Idle reads as STOP on the cell face. This is a CELL-ONLY relabel: the status
                // legend and the filter bar still say "idle", and matchesStatusFilter above keys
                // off e.statusName, which is untouched.
                setText(node, "circuit-status", e.statusName === "idle" ? "STOP" : e.statusName);

                // SocText is pre-formatted server-side because it knows about the unknown case -
                // Math.round(null * 100) would have written "0%", which on a bench reads as a
                // flat cell rather than as a missing battery.
                setText(node, "soc", e.socText);
                setText(node, "battery-line", e.batteryLine || "");
                setText(node, "program-status", e.programStatusText);
                setText(node, "last-update", e.lastUpdateText);

                // Node-face measurement rows are user-configurable (spec D11): whichever keys
                // ChannelNode rendered a prop-{key} slot for get written; the rest are harmlessly
                // ignored by setText's querySelector-returns-null guard.
                if (e.properties) {
                    for (const key in e.properties) {
                        setText(node, "prop-" + key, e.properties[key]);
                    }
                }

                // The battery wired to this channel, plus the edge between them.
                applyFlowToEdges(s, e);
            }

            // Device summaries: a text write into an existing slot, never a structural change.
            // Board summaries: a board is a pin dot on its device's own card (D18-revised), not a
            // .wf-node of its own, so its color is written onto the pin element instead.
            if (rollups) {
                for (const r of rollups) {
                    const deviceNode = s.world.querySelector(`.wf-node[data-node-id="${r.nodeId}"]`);
                    if (deviceNode) {
                        setText(deviceNode, "online", r.onlineText);
                        continue;
                    }
                    const pin = s.world.querySelector(`[data-board-node-id="${r.nodeId}"]`);
                    if (pin) pin.style.setProperty("--wf-status", r.isOnline ? "122 39% 49%" : "0 0% 62%");
                }
            }
        },
    };

})();
