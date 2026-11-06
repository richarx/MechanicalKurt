mergeInto(LibraryManager.library, {
  // Le canvas suit la densité de l'écran : sur un téléphone en densité 3, ça fait 9 fois plus de pixels à calculer qu'en densité 1.
  CapDevicePixelRatio: function (maximum) {
    Module.devicePixelRatio = Math.min(window.devicePixelRatio || 1, maximum);
  }
});
