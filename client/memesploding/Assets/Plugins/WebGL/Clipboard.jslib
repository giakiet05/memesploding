mergeInto(LibraryManager.library, {
  CopyToClipboard: function(textPtr) {
    var text = UTF8ToString(textPtr);
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).catch(function() {
        fallbackCopy(text);
      });
    } else {
      fallbackCopy(text);
    }
    function fallbackCopy(t) {
      var el = document.createElement('textarea');
      el.value = t;
      el.style.position = 'fixed';
      el.style.opacity = '0';
      document.body.appendChild(el);
      el.focus();
      el.select();
      try { document.execCommand('copy'); } catch(e) {}
      document.body.removeChild(el);
    }
  }
});
