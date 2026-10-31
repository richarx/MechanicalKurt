mergeInto(LibraryManager.library, {
  // Onglet fermé ou rechargé : prévient Unity tant que la page tourne encore (pour fermer proprement la partie).
  PageListenClose: function (objectPtr, methodPtr) {
    var target = UTF8ToString(objectPtr);
    var method = UTF8ToString(methodPtr);
    window.addEventListener('pagehide', function () {
      try { SendMessage(target, method); } catch (e) { }
    });
  }
});
