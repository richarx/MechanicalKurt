mergeInto(LibraryManager.library, {
  ClipboardCopy: function (textPtr) {
    var text = UTF8ToString(textPtr);
    function fallback() {
      var area = document.createElement('textarea');
      area.value = text;
      area.style.position = 'fixed';
      area.style.opacity = '0';
      document.body.appendChild(area);
      area.focus();
      area.select();
      try { document.execCommand('copy'); } catch (e) { }
      document.body.removeChild(area);
    }
    if (navigator.clipboard && navigator.clipboard.writeText)
      navigator.clipboard.writeText(text).catch(fallback);
    else
      fallback();
  },

  ClipboardRequestPaste: function (objectPtr, methodPtr) {
    var target = UTF8ToString(objectPtr);
    var method = UTF8ToString(methodPtr);
    if (!navigator.clipboard || !navigator.clipboard.readText)
      return;
    navigator.clipboard.readText().then(function (text) {
      if (text) SendMessage(target, method, text);
    }).catch(function () { });
  },

  // Unity bloque le collage par défaut du navigateur : on écoute l'événement « paste » et le raccourci Ctrl/Cmd+V.
  ClipboardListenPaste: function (objectPtr, methodPtr) {
    var target = UTF8ToString(objectPtr);
    var method = UTF8ToString(methodPtr);
    if (window.__pigeonPaste) {
      document.removeEventListener('paste', window.__pigeonPaste.onPaste, true);
      window.removeEventListener('keydown', window.__pigeonPaste.onKey, true);
    }
    var onPaste = function (e) {
      var data = e.clipboardData || window.clipboardData;
      var text = data ? data.getData('text') : '';
      if (text) SendMessage(target, method, text);
    };
    var onKey = function (e) {
      if (!(e.ctrlKey || e.metaKey) || (e.key !== 'v' && e.key !== 'V'))
        return;
      if (navigator.clipboard && navigator.clipboard.readText)
        navigator.clipboard.readText().then(function (text) {
          if (text) SendMessage(target, method, text);
        }).catch(function () { });
    };
    window.__pigeonPaste = { onPaste: onPaste, onKey: onKey };
    document.addEventListener('paste', onPaste, true);
    window.addEventListener('keydown', onKey, true);
  }
});
