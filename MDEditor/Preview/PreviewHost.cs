using System.IO;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;
using Microsoft.Web.WebView2.Core;

namespace MDEditor.Preview;

/// <summary>
/// Shared plumbing for every WebView2 that renders Markdown: the main preview pane,
/// the presentation window and the print preview window.
/// </summary>
internal static class PreviewHost
{
    private static Task<CoreWebView2Environment>? _environment;

    /// <summary>
    /// One WebView2 environment (and user data folder) shared by all preview surfaces.
    /// Must be called on the UI thread.
    /// </summary>
    public static Task<CoreWebView2Environment> GetEnvironmentAsync()
    {
        return _environment ??= CreateEnvironmentAsync();
    }

    private static Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MDEditor", "WebView2");
        return CoreWebView2Environment.CreateAsync(null, userDataFolder);
    }

    /// <summary>
    /// Renders <paramref name="markdown"/> into an already-loaded preview document.
    /// When KaTeX/Mermaid are ready only the body is swapped (keeps scroll position);
    /// otherwise the whole document is reloaded. <paramref name="previewOptions"/> (dark theme /
    /// custom CSS) only affects the full-reload path, since the body-swap script never touches
    /// &lt;head&gt;/&lt;style&gt; - a theme/CSS change must force a full reload to take effect.
    /// Pass null for export-portable/print rendering (always light, no custom CSS).
    /// </summary>
    /// <returns>True when a full navigation was started, false when the body was swapped in place.</returns>
    public static async Task<bool> RenderAsync(CoreWebView2 webView, IMarkdownParser parser, string markdown, PreviewRenderOptions? previewOptions = null)
    {
        bool librariesReady;
        try
        {
            var probe = await webView.ExecuteScriptAsync(
                "typeof renderMathInElement === 'function' && typeof mermaid !== 'undefined'");
            librariesReady = probe == "true";
        }
        catch
        {
            librariesReady = false;
        }

        if (!librariesReady)
        {
            var fullHtml = await Task.Run(() => previewOptions.HasValue
                ? parser.ToHtmlDocument(markdown, previewOptions.Value)
                : parser.ToHtmlDocument(markdown));
            webView.NavigateToString(fullHtml);
            return true;
        }

        var bodyHtml = await Task.Run(() => parser.ToHtml(markdown));
        if (string.IsNullOrEmpty(bodyHtml))
            bodyHtml = "<p></p>";

        await webView.ExecuteScriptAsync(BuildBodySwapScript(bodyHtml));
        return false;
    }

    private static string BuildBodySwapScript(string bodyHtml)
    {
        var base64Html = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(bodyHtml));

        return $@"
            (async function() {{
                var body = document.body;
                var binaryString = atob('{base64Html}');
                var bytes = new Uint8Array(binaryString.length);
                for (var i = 0; i < binaryString.length; i++) {{
                    bytes[i] = binaryString.charCodeAt(i);
                }}
                var decoded = new TextDecoder('utf-8').decode(bytes);
                body.innerHTML = decoded;
                if (typeof renderMathInElement !== 'undefined') {{
                    renderMathInElement(body, {{
                        delimiters: [
                            {{left: '\\(', right: '\\)', display: false}},
                            {{left: '\\[', right: '\\]', display: true}}
                        ]
                    }});
                }}
                if (typeof mermaid !== 'undefined') {{
                    try {{ await mermaid.run(); }} catch(e) {{ console.error(e); }}
                }}
            }})();
        ";
    }

    /// <summary>
    /// Injected into the main preview on document creation. Exposes
    /// <c>window.__mdeFind</c> with search/next/prev/restore/clear, each returning
    /// <c>{count, index, capped}</c>. Matches are wrapped in &lt;mark class="mde-find"&gt;.
    /// Highlights are hidden in print media so PDF export / printing stay clean.
    /// </summary>
    public const string FindScript = @"
(function () {
    if (window.__mdeFind) return;
    var MAX = 5000, CLS = 'mde-find', CUR = 'mde-find-current';
    var marks = [], cur = -1, capped = false;

    function addStyle() {
        if (document.getElementById('mde-find-style')) return;
        var s = document.createElement('style');
        s.id = 'mde-find-style';
        s.textContent = 'mark.' + CLS + '{background:#fff3a3;color:inherit;border-radius:2px;padding:0}' +
            'mark.' + CUR + '{background:#ff9632;box-shadow:0 0 0 1px #e8751a}' +
            '@media print{mark.' + CLS + '{background:none!important;box-shadow:none!important}}';
        (document.head || document.documentElement).appendChild(s);
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', addStyle); else addStyle();

    function state() { return { count: marks.length, index: cur, capped: capped }; }

    function clear() {
        var parents = [];
        for (var i = 0; i < marks.length; i++) {
            var m = marks[i], p = m.parentNode;
            if (!p) continue;
            p.replaceChild(document.createTextNode(m.textContent), m);
            if (parents.indexOf(p) < 0) parents.push(p);
        }
        for (var j = 0; j < parents.length; j++) parents[j].normalize();
        marks = []; cur = -1; capped = false;
    }

    function skip(node) {
        for (var e = node.parentElement; e; e = e.parentElement) {
            var t = e.tagName;
            if (t === 'SCRIPT' || t === 'STYLE' || t === 'NOSCRIPT' || t === 'svg' || t === 'SVG' ||
                (e.classList && (e.classList.contains('katex') || e.classList.contains('mermaid')))) return true;
        }
        return false;
    }

    function highlight(scroll) {
        for (var i = 0; i < marks.length; i++) marks[i].classList.remove(CUR);
        if (cur < 0 || cur >= marks.length) return;
        marks[cur].classList.add(CUR);
        if (scroll) marks[cur].scrollIntoView({ block: 'center' });
    }

    function collect(q, matchCase) {
        clear();
        addStyle();
        if (!q || !document.body) return;
        var walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, {
            acceptNode: function (n) { return n.nodeValue && !skip(n) ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT; }
        });
        var nodes = [];
        while (walker.nextNode()) nodes.push(walker.currentNode);
        var needle = matchCase ? q : q.toLowerCase();
        for (var k = 0; k < nodes.length && !capped; k++) {
            var node = nodes[k], text = node.nodeValue;
            var hay = matchCase ? text : text.toLowerCase();
            var idx = hay.indexOf(needle);
            if (idx < 0) continue;
            var frag = document.createDocumentFragment(), last = 0;
            while (idx >= 0) {
                if (marks.length >= MAX) { capped = true; break; }
                if (idx > last) frag.appendChild(document.createTextNode(text.slice(last, idx)));
                var m = document.createElement('mark');
                m.className = CLS;
                m.textContent = text.substr(idx, q.length);
                frag.appendChild(m);
                marks.push(m);
                last = idx + q.length;
                idx = hay.indexOf(needle, last);
            }
            if (last < text.length) frag.appendChild(document.createTextNode(text.slice(last)));
            node.parentNode.replaceChild(frag, node);
        }
    }

    window.__mdeFind = {
        search: function (q, matchCase) {
            collect(q, matchCase);
            cur = marks.length ? 0 : -1;
            highlight(true);
            return state();
        },
        // Re-apply the last search after the preview content changed, without scrolling.
        restore: function (q, matchCase) {
            var keep = cur;
            collect(q, matchCase);
            cur = marks.length ? Math.min(Math.max(keep, 0), marks.length - 1) : -1;
            highlight(false);
            return state();
        },
        next: function () { if (marks.length) { cur = (cur + 1) % marks.length; highlight(true); } return state(); },
        prev: function () { if (marks.length) { cur = (cur - 1 + marks.length) % marks.length; highlight(true); } return state(); },
        clear: function () { clear(); return state(); }
    };
})();";
}
