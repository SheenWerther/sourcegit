(function () {
  const md = window.markdownit({
    html: false,
    linkify: true,
    highlight: function (code, lang) {
      if (lang && window.hljs.getLanguage(lang)) {
        try {
          return window.hljs.highlight(code, { language: lang, ignoreIllegals: true }).value;
        } catch (e) { /* 退回无高亮 */ }
      }
      return '';
    }
  });

  const defaultFence = md.renderer.rules.fence;
  md.renderer.rules.fence = function (tokens, idx, options, env, self) {
    const token = tokens[idx];
    if (token.info.trim() === 'mermaid')
      return '<pre class="mermaid">' + md.utils.escapeHtml(token.content) + '</pre>';
    return defaultFence(tokens, idx, options, env, self);
  };

  mermaid.initialize({ startOnLoad: false, theme: 'default', securityLevel: 'strict' });

  const mathOptions = {
    delimiters: [
      { left: '$$', right: '$$', display: true },
      { left: '\\[', right: '\\]', display: true },
      { left: '$', right: '$', display: false },
      { left: '\\(', right: '\\)', display: false }
    ],
    ignoredTags: ['script', 'noscript', 'style', 'textarea', 'pre', 'code'],
    throwOnError: false
  };

  // 由宿主调用：渲染 Markdown。baseHref 是 .md 所在目录，用来解析相对路径图片。
  window.renderMarkdown = async function (text, baseHref) {
    const scrollY = window.scrollY;

    let base = document.querySelector('base');
    if (!base) {
      base = document.createElement('base');
      document.head.appendChild(base);
    }
    base.href = baseHref || '';

    const content = document.getElementById('content');
    content.innerHTML = md.render(text);
    renderMathInElement(content, mathOptions);

    const nodes = content.querySelectorAll('pre.mermaid');
    if (nodes.length > 0) {
      try {
        await mermaid.run({ nodes: nodes });
      } catch (e) { /* 图表语法错误时保留源码 */ }
    }

    window.scrollTo(0, scrollY);
  };

  // 点链接不在预览里跳转，交给宿主用系统浏览器打开
  document.addEventListener('click', function (e) {
    const a = e.target.closest('a');
    if (!a) return;
    e.preventDefault();
    const href = a.getAttribute('href') || '';
    if (/^https?:\/\//i.test(href) && window.chrome && window.chrome.webview)
      window.chrome.webview.postMessage(href);
  });
})();
