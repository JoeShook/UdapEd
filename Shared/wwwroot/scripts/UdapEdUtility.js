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
