using System.Text.Json;

namespace PageArc.Services;

public static class ReaderEnhancementScript
{
    public static string Build(string? language)
    {
        var strings = ResolveStrings(language);
        return $$"""
        (() => {
          if (window.__pagearcV095Installed) return true;
          window.__pagearcV095Installed = true;
          const t = {{JsonSerializer.Serialize(strings)}};
          const css = `
            html { text-rendering: optimizeLegibility; font-kerning: normal; }
            :lang(zh), :lang(ja), [lang^="zh"], [lang^="ja"] {
              line-break: strict !important;
              word-break: normal !important;
              overflow-wrap: break-word !important;
              hanging-punctuation: first allow-end last;
              font-variant-east-asian: proportional-width;
            }
            ruby { ruby-position: over; ruby-align: space-around; }
            rt { font-size: .52em; line-height: 1; text-align: center; }
            math, svg { max-inline-size: 100%; }
            table { display: block; overflow-x: auto; max-width: 100%; }
            html.pagearc-vertical-book body,
            html.pagearc-vertical-book body * { overflow-wrap: normal !important; word-break: normal !important; }
            html.pagearc-vertical-book body { text-orientation: mixed; line-break: strict; }
            [style*="writing-mode: vertical"], [style*="writing-mode:vertical"] { max-width: none !important; }
            #pagearc-footnote-layer, #pagearc-image-viewer { position: fixed; inset: 0; z-index: 2147483000; }
            #pagearc-footnote-layer { background: rgba(0,0,0,.18); }
            .pagearc-footnote-card { position: fixed; width: min(420px, calc(100vw - 32px)); max-height: 50vh; overflow: auto;
              box-sizing: border-box; padding: 16px; border: 1px solid rgba(117,117,117,.32); border-radius: 10px;
              background: color-mix(in srgb, Canvas 96%, transparent); color: CanvasText; box-shadow: 0 10px 34px rgba(0,0,0,.22); }
            .pagearc-footnote-head { display:flex; gap:8px; align-items:center; justify-content:flex-end; margin-bottom:8px; }
            .pagearc-mini-button { border: 1px solid rgba(117,117,117,.28); border-radius: 6px; padding: 5px 10px; background: color-mix(in srgb, Canvas 92%, transparent); color: CanvasText; cursor:pointer; }
            #pagearc-image-viewer { background: rgba(18,18,18,.92); color:#fff; touch-action:none; overflow:hidden; }
            .pagearc-image-stage { position:absolute; inset:52px 0 0; overflow:hidden; }
            .pagearc-image-stage img { position:absolute; left:50%; top:50%; max-width:none !important; max-height:none !important; user-select:none; -webkit-user-drag:none; transform-origin:center center; cursor:grab; }
            .pagearc-image-stage img:active { cursor:grabbing; }
            .pagearc-image-tools { position:absolute; left:50%; top:10px; transform:translateX(-50%); display:flex; gap:6px; align-items:center;
              padding:5px; border-radius:9px; background:rgba(35,35,35,.76); backdrop-filter:blur(14px); }
            .pagearc-image-tools button { min-width:34px; height:32px; border:0; border-radius:6px; color:#fff; background:transparent; cursor:pointer; padding:0 10px; }
            .pagearc-image-tools button:hover { background:rgba(255,255,255,.12); }
          `;
          const style = document.createElement('style');
          style.id = 'pagearc-v095-style';
          style.textContent = css;
          document.head.appendChild(style);

          const writingMode = getComputedStyle(document.body).writingMode || getComputedStyle(document.documentElement).writingMode || '';
          if (writingMode.startsWith('vertical')) document.documentElement.classList.add('pagearc-vertical-book');

          const stripUnsafe = root => {
            root.querySelectorAll('script,iframe,object,embed,form,input,textarea,select,button').forEach(node => node.remove());
            root.querySelectorAll('*').forEach(node => {
              [...node.attributes].forEach(attr => { if (/^on/i.test(attr.name)) node.removeAttribute(attr.name); });
              node.removeAttribute('id');
            });
          };

          const closeFootnote = () => document.getElementById('pagearc-footnote-layer')?.remove();
          const showFootnote = (anchor, target) => {
            closeFootnote();
            const layer = document.createElement('div');
            layer.id = 'pagearc-footnote-layer';
            const card = document.createElement('div');
            card.className = 'pagearc-footnote-card';
            const head = document.createElement('div');
            head.className = 'pagearc-footnote-head';
            const jump = document.createElement('button');
            jump.className = 'pagearc-mini-button';
            jump.textContent = t.jump;
            const close = document.createElement('button');
            close.className = 'pagearc-mini-button';
            close.textContent = '×';
            head.append(jump, close);
            const body = target.cloneNode(true);
            stripUnsafe(body);
            card.append(head, body);
            layer.appendChild(card);
            document.body.appendChild(layer);
            const rect = anchor.getBoundingClientRect();
            requestAnimationFrame(() => {
              const margin = 16;
              const r = card.getBoundingClientRect();
              const left = Math.max(margin, Math.min(innerWidth - r.width - margin, rect.left));
              const preferredTop = rect.bottom + 10;
              const top = preferredTop + r.height <= innerHeight - margin ? preferredTop : Math.max(margin, rect.top - r.height - 10);
              card.style.left = `${left}px`; card.style.top = `${top}px`;
            });
            layer.addEventListener('click', e => { if (e.target === layer) closeFootnote(); });
            close.addEventListener('click', closeFootnote);
            jump.addEventListener('click', () => {
              closeFootnote();
              target.scrollIntoView({block:'center', inline:'center', behavior:'smooth'});
              setTimeout(() => window.chrome?.webview?.postMessage('progress:' + (window.__pagearc?.progress?.() ?? 0).toFixed(6)), 180);
            });
          };

          const resolveFootnote = anchor => {
            const type = `${anchor.getAttribute('epub:type') || ''} ${anchor.getAttribute('role') || ''}`.toLowerCase();
            const href = anchor.getAttribute('href') || '';
            const explicit = type.includes('noteref') || type.includes('doc-noteref');
            if (!href.includes('#') && !explicit) return null;
            let id = href.includes('#') ? href.substring(href.indexOf('#') + 1) : '';
            try { id = decodeURIComponent(id); } catch {}
            if (!id) return null;
            const target = document.getElementById(id);
            if (!target) return null;
            const targetType = `${target.getAttribute('epub:type') || ''} ${target.getAttribute('role') || ''}`.toLowerCase();
            if (!explicit && !targetType.includes('footnote') && !targetType.includes('doc-footnote') && !target.closest('aside')) return null;
            return target;
          };

          const closeImageViewer = () => document.getElementById('pagearc-image-viewer')?.remove();
          const showImageViewer = sourceImage => {
            closeImageViewer();
            const src = sourceImage.currentSrc || sourceImage.src;
            if (!src) return;
            const viewer = document.createElement('div'); viewer.id = 'pagearc-image-viewer';
            const tools = document.createElement('div'); tools.className = 'pagearc-image-tools';
            const makeButton = (text, title) => { const b=document.createElement('button'); b.textContent=text; b.title=title; tools.appendChild(b); return b; };
            const zoomOut = makeButton('−', t.zoomOut), zoomIn = makeButton('+', t.zoomIn), fit = makeButton(t.fit, t.fit), original = makeButton('100%', t.original), save = makeButton(t.save, t.save), close = makeButton('×', t.close);
            const stage = document.createElement('div'); stage.className = 'pagearc-image-stage';
            const image = document.createElement('img'); image.src = src; image.alt = sourceImage.alt || '';
            stage.appendChild(image); viewer.append(tools, stage); document.body.appendChild(viewer);
            let scale = 1, tx = 0, ty = 0, dragging = false, lastX = 0, lastY = 0;
            const apply = () => image.style.transform = `translate(calc(-50% + ${tx}px), calc(-50% + ${ty}px)) scale(${scale})`;
            const fitImage = () => {
              const w = Math.max(1, image.naturalWidth), h = Math.max(1, image.naturalHeight);
              const availableW = Math.max(1, stage.clientWidth - 48), availableH = Math.max(1, stage.clientHeight - 48);
              scale = Math.min(1, availableW / w, availableH / h); tx = 0; ty = 0; apply();
            };
            image.addEventListener('load', fitImage, {once:true});
            zoomIn.onclick = () => { scale=Math.min(8, scale*1.2); apply(); };
            zoomOut.onclick = () => { scale=Math.max(.1, scale/1.2); apply(); };
            fit.onclick = fitImage; original.onclick = () => { scale=1; tx=0; ty=0; apply(); }; close.onclick = closeImageViewer;
            stage.addEventListener('wheel', e => { e.preventDefault(); scale=Math.max(.1,Math.min(8,scale*(e.deltaY<0?1.12:.89))); apply(); }, {passive:false});
            image.addEventListener('pointerdown', e => { dragging=true; lastX=e.clientX; lastY=e.clientY; image.setPointerCapture(e.pointerId); });
            image.addEventListener('pointermove', e => { if(!dragging)return; tx+=e.clientX-lastX; ty+=e.clientY-lastY; lastX=e.clientX; lastY=e.clientY; apply(); });
            image.addEventListener('pointerup', e => { dragging=false; try{image.releasePointerCapture(e.pointerId);}catch{} });
            image.addEventListener('dblclick', fitImage);
            save.onclick = async () => {
              try {
                const response = await fetch(src); const blob = await response.blob(); const reader = new FileReader();
                reader.onload = () => window.chrome?.webview?.postMessage(JSON.stringify({type:'pagearc-image-save', dataUrl:reader.result, name:(sourceImage.alt || 'ebook-image')}));
                reader.readAsDataURL(blob);
              } catch {
                window.chrome?.webview?.postMessage(JSON.stringify({type:'pagearc-image-save', source:src, name:(sourceImage.alt || 'ebook-image')}));
              }
            };
          };

          document.addEventListener('click', e => {
            const anchor = e.target?.closest?.('a');
            if (anchor) { const target = resolveFootnote(anchor); if (target) { e.preventDefault(); e.stopPropagation(); showFootnote(anchor, target); return; } }
            const image = e.target?.closest?.('img');
            if (image && (image.naturalWidth >= 48 || image.naturalHeight >= 48)) { e.preventDefault(); e.stopPropagation(); showImageViewer(image); }
          }, true);
          document.addEventListener('keydown', e => { if (e.key === 'Escape') { closeFootnote(); closeImageViewer(); } });
          return true;
        })()
        """;
    }

    private static object ResolveStrings(string? language)
    {
        if (language?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true)
            return new { jump = "跳转", fit = "适合窗口", original = "原始大小", save = "保存", close = "关闭", zoomIn = "放大", zoomOut = "缩小" };
        if (language?.StartsWith("ja", StringComparison.OrdinalIgnoreCase) == true)
            return new { jump = "移動", fit = "ウィンドウに合わせる", original = "元のサイズ", save = "保存", close = "閉じる", zoomIn = "拡大", zoomOut = "縮小" };
        return new { jump = "Go to note", fit = "Fit", original = "Original size", save = "Save", close = "Close", zoomIn = "Zoom in", zoomOut = "Zoom out" };
    }
}
