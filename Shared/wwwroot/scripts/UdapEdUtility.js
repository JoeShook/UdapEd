// Light/dark choice. The choice is kept in localStorage so it survives reloads, including the round trip through an
// authorization server during the authorization code flow. Monaco's theme is global, so every editor follows it.
window.udapEdTheme = {
  _key: 'udaped-theme',
  _dark: false,
  getSaved: function () {
    try { return localStorage.getItem(this._key); } catch (e) { return null; }
  },
  save: function (dark) {
    try { localStorage.setItem(this._key, dark ? 'dark' : 'light'); } catch (e) { /* storage unavailable */ }
  },
  apply: function (dark) {
    this._dark = !!dark;
    document.documentElement.style.colorScheme = this._dark ? 'dark' : 'light';
    this.applyMonaco();
  },
  applyMonaco: function () {
    if (window.monaco && window.monaco.editor) {
      window.monaco.editor.setTheme(this._dark ? 'vs-dark' : 'vs');
    }
  }
};

// Loads the Monaco editor scripts on first use instead of on every page. A host page that already includes them
// (the MAUI index.html) resolves immediately.
window.udapEdMonaco = {
  _loading: null,
  ensureLoaded: function () {
    if (window.monaco && window.monaco.editor) {
      return Promise.resolve();
    }
    if (!this._loading) {
      const base = '_content/BlazorMonaco/lib/monaco-editor/min/vs/';
      const addScript = (src) => new Promise((resolve, reject) => {
        const s = document.createElement('script');
        s.src = src;
        s.onload = () => resolve();
        s.onerror = () => reject(new Error('Could not load ' + src));
        document.body.appendChild(s);
      });
      const waitForMonaco = () => new Promise((resolve, reject) => {
        const started = Date.now();
        (function check() {
          if (window.monaco && window.monaco.editor) { resolve(); return; }
          if (Date.now() - started > 30000) { reject(new Error('Monaco did not initialise')); return; }
          setTimeout(check, 25);
        })();
      });
      this._loading = (typeof window.require === 'function' && window.require.config ? Promise.resolve() : addScript(base + 'loader.js'))
        .then(() => addScript(base + 'editor/editor.main.js'))
        .then(waitForMonaco)
        .then(() => window.udapEdTheme.applyMonaco())
        .catch((e) => { this._loading = null; throw e; });
    }
    return this._loading;
  }
};

function postToAuthorize(authorizeUrl) {
  const url = new URL(authorizeUrl);
  const form = document.createElement('form');
  form.method = 'post';
  form.action = url.origin + url.pathname;
  for (const [key, value] of url.searchParams.entries()) {
    const input = document.createElement('input');
    input.type = 'hidden';
    input.name = key;
    input.value = value;
    form.appendChild(input);
  }
  document.body.appendChild(form);
  form.submit();
}

function downloadFileFromBytes(bytes, mimeType, fileName) {
  const blob = new Blob([new Uint8Array(bytes)], { type: mimeType });
  const link = document.createElement('a');
  link.href = URL.createObjectURL(blob);
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
}

window.dpopArrow = {
  _resizeHandler: null,
  _scrollHandler: null,
  _dotnetRef: null,
  _textareaId: null,
  _searchText: null,

  getPosition: function (textareaId, searchText) {
    const textarea = document.getElementById(textareaId);
    if (!textarea) return { y: -1, visible: false };

    const idx = textarea.value.indexOf(searchText);
    if (idx < 0) return { y: -1, visible: false };

    const style = window.getComputedStyle(textarea);
    const mirror = document.createElement('div');
    const props = ['font-family','font-size','font-weight','line-height','letter-spacing',
      'word-spacing','text-indent','text-transform','padding-top','padding-right',
      'padding-bottom','padding-left','border-top-width','border-right-width',
      'border-bottom-width','border-left-width','box-sizing'];
    for (const p of props) {
      mirror.style.setProperty(p, style.getPropertyValue(p));
    }
    mirror.style.position = 'absolute';
    mirror.style.visibility = 'hidden';
    mirror.style.height = 'auto';
    mirror.style.overflow = 'hidden';
    mirror.style.whiteSpace = 'pre-wrap';
    mirror.style.wordWrap = 'break-word';
    mirror.style.width = textarea.clientWidth + 'px';

    // Measure text up to the start of the claim line
    const textBefore = textarea.value.substring(0, idx);
    const lastNewline = textBefore.lastIndexOf('\n');
    const textToLineStart = lastNewline >= 0 ? textBefore.substring(0, lastNewline + 1) : '';

    mirror.textContent = textToLineStart || '\n';
    document.body.appendChild(mirror);
    const lineTop = textToLineStart ? mirror.scrollHeight : 0;
    document.body.removeChild(mirror);

    const paddingTop = parseFloat(style.paddingTop) || 0;
    const absoluteY = lineTop + paddingTop;
    const scrollTop = textarea.scrollTop;
    const visibleY = absoluteY - scrollTop;
    const clientHeight = textarea.clientHeight;
    const visible = visibleY >= 0 && visibleY < clientHeight;

    return { y: visibleY, visible: visible };
  },

  _resizeObserver: null,

  register: function (dotnetRef, textareaId, searchText) {
    this._dotnetRef = dotnetRef;
    this._textareaId = textareaId;
    this._searchText = searchText;

    const notify = () => {
      const pos = this.getPosition(this._textareaId, this._searchText);
      this._dotnetRef.invokeMethodAsync('UpdateDPoPArrowPosition', pos.y, pos.visible);
    };

    this._resizeHandler = notify;
    this._scrollHandler = notify;
    window.addEventListener('resize', this._resizeHandler);

    const textarea = document.getElementById(textareaId);
    if (textarea) {
      textarea.addEventListener('scroll', this._scrollHandler);
      this._resizeObserver = new ResizeObserver(notify);
      this._resizeObserver.observe(textarea);
    }
  },

  unregister: function () {
    window.removeEventListener('resize', this._resizeHandler);
    const textarea = document.getElementById(this._textareaId);
    if (textarea) {
      textarea.removeEventListener('scroll', this._scrollHandler);
    }
    if (this._resizeObserver) {
      this._resizeObserver.disconnect();
      this._resizeObserver = null;
    }
    this._resizeHandler = null;
    this._scrollHandler = null;
    this._dotnetRef = null;
  }
};

function toggleText() {
  const expandableText = document.getElementById('expandableText');
  const indicator = document.getElementById('indicator');
  if (expandableText.classList.contains('expanded')) {
    expandableText.classList.remove('expanded');
    indicator.textContent = '[+]';
  }
  else {
    expandableText.classList.add('expanded');
    indicator.textContent = '[-]';
  }
}

// Draggable divider for SplitPanes.razor. One set of document listeners serves every splitter on every page.
// The left pane's width is the --udaped-split custom property on the container, set here and never by .NET
// after the first render, so a drag needs no interop. Arrow keys move a focused divider; double-click resets it.
window.udapEdSplit = {
  _storageKey: function (container) {
    return container.dataset.splitKey ? 'udaped-split:' + container.dataset.splitKey : null;
  },

  _set: function (container, percent, save) {
    const min = parseFloat(container.dataset.splitMin || '15');
    percent = Math.min(100 - min, Math.max(min, percent));
    container.style.setProperty('--udaped-split', percent + '%');
    container.dataset.splitNow = String(percent);
    const gutter = container.querySelector(':scope > .udaped-split-gutter');
    if (gutter) {
      gutter.setAttribute('aria-valuenow', String(Math.round(percent)));
    }
    const key = this._storageKey(container);
    if (save && key) {
      try { localStorage.setItem(key, String(percent)); } catch (e) { }
    }
  },

  _current: function (container) {
    return parseFloat(container.dataset.splitNow || container.dataset.splitInitial || '50');
  },

  restore: function (container) {
    const key = container && this._storageKey(container);
    if (!key) {
      return;
    }
    try {
      const saved = parseFloat(localStorage.getItem(key));
      if (!isNaN(saved)) {
        this._set(container, saved, false);
      }
    } catch (e) { }
  },

  _init: function () {
    const self = this;
    const gutterOf = function (event) {
      const gutter = event.target instanceof Element ? event.target.closest('.udaped-split-gutter') : null;
      return gutter && gutter.parentElement && gutter.parentElement.classList.contains('udaped-split') ? gutter : null;
    };

    document.addEventListener('pointerdown', function (event) {
      const gutter = gutterOf(event);
      if (!gutter || event.button !== 0) {
        return;
      }
      event.preventDefault();
      const container = gutter.parentElement;
      const move = function (e) {
        const rect = container.getBoundingClientRect();
        if (rect.width > 0) {
          self._set(container, (e.clientX - rect.left) / rect.width * 100, false);
        }
      };
      const end = function () {
        gutter.removeEventListener('pointermove', move);
        gutter.removeEventListener('pointerup', end);
        gutter.removeEventListener('pointercancel', end);
        container.classList.remove('udaped-split-dragging');
        self._set(container, self._current(container), true);
      };
      gutter.setPointerCapture(event.pointerId);
      container.classList.add('udaped-split-dragging');
      gutter.addEventListener('pointermove', move);
      gutter.addEventListener('pointerup', end);
      gutter.addEventListener('pointercancel', end);
    });

    document.addEventListener('dblclick', function (event) {
      const gutter = gutterOf(event);
      if (gutter) {
        const container = gutter.parentElement;
        self._set(container, parseFloat(container.dataset.splitInitial || '50'), true);
      }
    });

    document.addEventListener('keydown', function (event) {
      const gutter = gutterOf(event);
      if (!gutter) {
        return;
      }
      const container = gutter.parentElement;
      const min = parseFloat(container.dataset.splitMin || '15');
      const steps = { ArrowLeft: -5, ArrowRight: 5 };
      let next = null;
      if (event.key in steps) {
        next = self._current(container) + steps[event.key];
      } else if (event.key === 'Home') {
        next = min;
      } else if (event.key === 'End') {
        next = 100 - min;
      }
      if (next !== null) {
        event.preventDefault();
        self._set(container, next, true);
      }
    });
  }
};
window.udapEdSplit._init();
