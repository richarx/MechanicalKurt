mergeInto(LibraryManager.library, {
  // Inclinaison du téléphone, lue depuis le navigateur. iOS exige une autorisation demandée après un toucher, et le
  // navigateur refuse ces capteurs dans une iframe d'un autre site si elle ne les autorise pas : TiltHasData reste alors à 0.
  TiltStart: function () {
    if (window.__pigeonTilt)
      return;
    var tilt = window.__pigeonTilt = { roll: 0, pitch: 0, last: -1e9, x: 0, y: 0, z: 0, ready: false };
    var isIos = /iPad|iPhone|iPod/.test(navigator.userAgent)
      || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);

    function onMotion(e) {
      var a = e.accelerationIncludingGravity;
      if (!a || a.x === null || a.y === null || a.z === null)
        return;
      // Même sens partout : iOS donne ces valeurs à l'opposé des autres.
      var sign = isIos ? -1 : 1;
      var smoothing = tilt.ready ? 0.2 : 1;
      tilt.x += (sign * a.x - tilt.x) * smoothing;
      tilt.y += (sign * a.y - tilt.y) * smoothing;
      tilt.z += (sign * a.z - tilt.z) * smoothing;
      tilt.ready = true;

      // Axes de l'écran, selon le sens où le téléphone est tenu.
      var angle = (screen.orientation && screen.orientation.angle) || window.orientation || 0;
      var radians = angle * Math.PI / 180;
      var cos = Math.cos(radians), sin = Math.sin(radians);
      var right = tilt.x * cos - tilt.y * sin;
      var up = tilt.x * sin + tilt.y * cos;

      tilt.roll = Math.atan2(-right, Math.sqrt(up * up + tilt.z * tilt.z)) * 180 / Math.PI;
      tilt.pitch = Math.atan2(up, tilt.z) * 180 / Math.PI;
      tilt.last = performance.now();
    }

    function listen() { window.addEventListener('devicemotion', onMotion, true); }

    if (typeof DeviceMotionEvent !== 'undefined' && typeof DeviceMotionEvent.requestPermission === 'function') {
      var ask = function () {
        document.removeEventListener('touchend', ask, true);
        DeviceMotionEvent.requestPermission().then(function (state) {
          if (state === 'granted') listen();
        }).catch(function () { });
      };
      document.addEventListener('touchend', ask, true);
    } else {
      listen();
    }
  },

  TiltHasData: function () {
    return window.__pigeonTilt && performance.now() - window.__pigeonTilt.last < 1000 ? 1 : 0;
  },

  TiltRoll: function () {
    return window.__pigeonTilt ? window.__pigeonTilt.roll : 0;
  },

  TiltPitch: function () {
    return window.__pigeonTilt ? window.__pigeonTilt.pitch : 0;
  }
});
