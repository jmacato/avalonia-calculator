namespace Calculator.BrowserHost;

internal static class TelemetryDashboard
{
    public const string Html = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <title>Calculator browser telemetry</title>
          <style>
            :root { color-scheme: dark; font: 14px/1.45 ui-monospace, SFMono-Regular, Menlo, monospace; }
            * { box-sizing: border-box; }
            body { margin: 0; color: #dce6df; background: #101412; }
            header { position: sticky; top: 0; z-index: 2; padding: 14px 18px; border-bottom: 1px solid #344039; background: #151b18ee; backdrop-filter: blur(12px); }
            h1 { margin: 0 0 4px; font: 650 18px/1.2 system-ui, sans-serif; }
            .subtle { color: #8fa097; }
            main { display: grid; grid-template-columns: minmax(300px, 430px) minmax(600px, 1fr); min-height: calc(100vh - 70px); }
            #sessions { border-right: 1px solid #344039; }
            button { display: block; width: 100%; padding: 12px 16px; border: 0; border-bottom: 1px solid #28322d; color: inherit; background: transparent; text-align: left; cursor: pointer; }
            button:hover, button.selected { background: #202a25; }
            .row { display: flex; align-items: baseline; justify-content: space-between; gap: 12px; }
            .status { font-weight: 750; }
            .healthy { color: #82d18a; }
            .starting, .backgrounded, .inactive { color: #e5c66d; }
            .bad { color: #ff8178; }
            #detail { min-width: 0; padding: 18px; overflow: auto; }
            .metrics { display: grid; grid-template-columns: repeat(auto-fit, minmax(190px, 1fr)); gap: 8px; margin-bottom: 18px; }
            .metric { min-height: 66px; padding: 10px 12px; border: 1px solid #344039; border-radius: 8px; background: #171e1a; }
            .metric strong { display: block; margin-top: 4px; font-size: 16px; overflow-wrap: anywhere; }
            table { width: 100%; border-collapse: collapse; white-space: nowrap; }
            th, td { padding: 7px 9px; border-bottom: 1px solid #28322d; text-align: right; }
            th:first-child, td:first-child, .left { text-align: left; }
            section { margin: 0 0 22px; }
            h2 { margin: 0 0 9px; font: 650 16px/1.2 system-ui, sans-serif; }
            code { white-space: pre-wrap; overflow-wrap: anywhere; }
            @media (max-width: 900px) { main { display: block; } #sessions { border-right: 0; } }
          </style>
        </head>
        <body>
          <header><h1>Calculator browser telemetry</h1><div class="subtle" id="health">Connecting…</div></header>
          <main><nav id="sessions"></nav><article id="detail"><span class="subtle">Waiting for a telemetry-enabled Calculator session.</span></article></main>
          <script>
            const sessionsNode = document.querySelector('#sessions');
            const detailNode = document.querySelector('#detail');
            const healthNode = document.querySelector('#health');
            let selected = '';
            const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
            const age = value => !Number.isFinite(value) ? '—' : value < 1000 ? `${value.toFixed(0)} ms` : `${(value / 1000).toFixed(1)} s`;
            const bytes = value => !value ? '0' : `${(value / 1048576).toFixed(1)} MiB`;
            const statusClass = value => value === 'healthy' ? 'healthy' : ['starting','backgrounded','inactive'].includes(value) ? value : 'bad';
            async function json(path) { const response = await fetch(path, {cache:'no-store'}); if (!response.ok) throw new Error(`${response.status} ${response.statusText}`); return response.json(); }
            function metric(label, value) { return `<div class="metric"><span class="subtle">${esc(label)}</span><strong>${esc(value)}</strong></div>`; }
            function renderSessions(items) {
              sessionsNode.innerHTML = items.map(item => `<button data-id="${esc(item.sessionId)}" class="${item.sessionId === selected ? 'selected' : ''}">
                <div class="row"><strong>${esc(item.runId || item.sessionId.slice(0, 8))}</strong><span class="status ${statusClass(item.status)}">${esc(item.status)}</span></div>
                <div class="subtle">${esc(item.remoteAddress)} · UI ${age(item.uiAgeMs)} · worker ${age(item.workerAgeMs)}</div>
              </button>`).join('');
              sessionsNode.querySelectorAll('button').forEach(button => button.onclick = () => { selected = button.dataset.id; void refreshDetail(); });
            }
            function renderDetail(data) {
              const s = data.summary, u = s.latest || {}, events = data.events.slice(-80).reverse(), timeline = data.timeline.slice(-180).reverse();
              detailNode.innerHTML = `<section><h2>${esc(s.runId || s.sessionId)}</h2><code class="subtle">${esc(data.client?.userAgent || '')}</code></section>
                <div class="metrics">
                  ${metric('Status', s.status)} ${metric('Signals', s.signals.join(', ') || 'none')}
                  ${metric('UI heartbeat age', age(s.uiAgeMs))} ${metric('Probe worker age', age(s.workerAgeMs))}
                  ${metric('Frame rate', `${(u.frameRate || 0).toFixed(1)} fps`)} ${metric('Max frame gap', age(u.maxFrameGapMs || 0))}
                  ${metric('Pointer moves / interval', u.pointerMoveSinceLast || 0)} ${metric('Presents / interval', u.canvasPresentSinceLast || 0)}
                  ${metric('Frame receive / present / drop', `${u.canvasFrameReceivedTotal || 0} / ${u.canvasFramePresentedTotal || 0} / ${u.canvasFrameDroppedTotal || 0}`)} ${metric('Frame mailbox pending', u.canvasFramePending || 0)}
                  ${metric('Input queue depth / high-water', `${u.inputQueueDepth || 0} / ${u.inputQueueHighWater || 0}`)} ${metric('Input retry / shed', `${u.inputQueueRetries || 0} / ${u.inputQueueShed || 0}`)}
                  ${metric('Wasm linear memory', u.wasmMemoryMaxBytes ? `${bytes(u.wasmMemoryBytes)} / ${bytes(u.wasmMemoryMaxBytes)}` : bytes(u.wasmMemoryBytes))} ${metric('Wasm growth / 10 s', bytes(s.wasmGrowthBytes10s))}
                  ${metric('Managed live heap', bytes(u.managedHeapBytes))} ${metric('Managed growth / 10 s', bytes(s.managedGrowthBytes10s))}
                  ${metric('Managed allocated total', bytes(u.managedAllocatedBytes))} ${metric('GC collections', `${u.managedGen0Collections || 0}/${u.managedGen1Collections || 0}/${u.managedGen2Collections || 0}`)}
                  ${metric('Managed dispatcher / sample age', `${age(u.managedDispatcherAgeMs)} / ${age(u.managedSampleAgeMs)}`)} ${metric('Canvas backing size', `${u.canvasWidth || 0} × ${u.canvasHeight || 0}`)}
                  ${metric('Native dispatcher thread / stage / seq', `${u.nativeDispatcherThreadId || 0} / ${u.nativeDispatcherStage || 0} / ${u.nativeDispatcherStageSequence || 0}`)} ${metric('Native input / UI-command depth', `${u.nativeDispatcherInputDepth || 0} / ${u.nativeDispatcherUiCommandDepth || 0}`)}
                  ${metric('Native event read / write', `${u.nativeDispatcherEventRead || 0} / ${u.nativeDispatcherEventWrite || 0}`)} ${metric('Native UI-command read / write / scheduled', `${u.nativeDispatcherUiCommandRead || 0} / ${u.nativeDispatcherUiCommandWrite || 0} / ${u.nativeDispatcherUiCommandDrainScheduled || 0}`)}
                  ${metric('Native dump requested / completed', `${u.nativeDispatcherDumpRequested || 0} / ${u.nativeDispatcherDumpCompleted || 0}`)} ${metric('MainPage count / root / last', `${u.mainPageCount || 0} / ${u.rootMainPageId || 0} / ${u.lastNavigatedMainPageId || 0}`)}
                  ${metric('Native dispatcher fault stage / type', `${u.nativeDispatcherStage || 0} / ${u.nativeDispatcherFaultType || 0}`)} ${metric('Native fault HResult / exits', `${u.nativeDispatcherFaultHResult || 0} / ${u.nativeDispatcherLoopExitCount || 0}`)}
                  ${metric('Graph request / worker / complete', `${u.graphRequestedGeneration || 0} / ${u.graphWorkerGeneration || 0} / ${u.graphCompletedGeneration || 0}`)} ${metric('Graph publish / commit', `${u.graphPublishedGeneration || 0} / ${u.graphCommittedGeneration || 0}`)}
                  ${metric('Graph worker / status', `${u.graphWorkerActive || 0} / ${u.graphCompletedStatus || 0} / ${u.graphCommitStatus || 0}`)} ${metric('Graph settle / render', `${u.graphSettlementRequestCount || 0} / ${u.graphRenderCount || 0}`)}
                  ${metric('Graph renderers active / made / disposed', `${u.graphRendererActiveCount || 0} / ${u.graphRendererCreatedCount || 0} / ${u.graphRendererDisposedCount || 0}`)}
                  ${metric('Converter stage / thread / UI', `${u.converterStage || 0} / ${u.converterStageThreadId || 0} / ${u.converterUiThreadId || 0}`)} ${metric('Converter request / complete / fail', `${u.converterRequestCount || 0} / ${u.converterCompletionCount || 0} / ${u.converterFailureCount || 0} (${u.converterFailureKind || 0})`)}
                  ${metric('Viewport / inner height', `${(u.visualViewportHeight || 0).toFixed(0)} / ${u.innerHeight || 0}`)} ${metric('Focused element', u.activeElement || 'none')}
                </div>
                <section><h2>Events</h2><table><thead><tr><th>Received</th><th class="left">Kind</th><th class="left">Detail</th></tr></thead><tbody>${events.map(e => `<tr><td>${esc(new Date(e.receivedAt).toLocaleTimeString())}</td><td class="left">${esc(e.kind)}</td><td class="left">${esc(e.detail)}</td></tr>`).join('')}</tbody></table></section>
                <section><h2>Recent samples</h2><table><thead><tr><th>Time</th><th>Status age</th><th>fps</th><th>gap</th><th>ptr down/move/up</th><th>present rx/tx/drop</th><th>input depth/high/retry/shed</th><th>Wasm</th><th>managed age</th><th>native thread/stage/seq</th><th>native event/UI depth</th><th>graph req/work/done/commit</th><th>renderers active/made/gone</th></tr></thead><tbody>${timeline.map(t => `<tr><td>${esc(new Date(t.receivedAt).toLocaleTimeString())}</td><td>${age(t.uiAgeMs)}</td><td>${t.frameRate.toFixed(1)}</td><td>${age(t.maxFrameGapMs)}</td><td>${t.pointerDownTotal || 0}/${t.pointerMoveTotal || 0}/${t.pointerUpTotal || 0}</td><td>${t.canvasFrameReceivedTotal || 0}/${t.canvasFramePresentedTotal || 0}/${t.canvasFrameDroppedTotal || 0}</td><td>${t.inputQueueDepth || 0}/${t.inputQueueHighWater || 0}/${t.inputQueueRetries || 0}/${t.inputQueueShed || 0}</td><td>${bytes(t.wasmMemoryBytes)}</td><td>${age(t.managedDispatcherAgeMs)}</td><td>${t.nativeDispatcherThreadId || 0}/${t.nativeDispatcherStage || 0}/${t.nativeDispatcherStageSequence || 0}</td><td>${t.nativeDispatcherInputDepth || 0}/${t.nativeDispatcherUiCommandDepth || 0}</td><td>${t.graphRequestedGeneration || 0}/${t.graphWorkerGeneration || 0}/${t.graphCompletedGeneration || 0}/${t.graphCommittedGeneration || 0}</td><td>${t.graphRendererActiveCount || 0}/${t.graphRendererCreatedCount || 0}/${t.graphRendererDisposedCount || 0}</td></tr>`).join('')}</tbody></table></section>`;
            }
            async function refreshDetail() { if (!selected) return; try { renderDetail(await json(`/telemetry/v1/sessions/${encodeURIComponent(selected)}`)); } catch (error) { healthNode.textContent = error; } }
            async function refresh() {
              try {
                const [health, sessions] = await Promise.all([json('/telemetry/v1/health'), json('/telemetry/v1/sessions')]);
                healthNode.textContent = `Live · ${sessions.length} session(s) · log ${health.logPath}`;
                if (!selected && sessions.length) selected = sessions[0].sessionId;
                renderSessions(sessions);
                await refreshDetail();
              } catch (error) { healthNode.textContent = `Disconnected: ${error}`; }
            }
            setInterval(refresh, 1000); void refresh();
          </script>
        </body>
        </html>
        """;
}
