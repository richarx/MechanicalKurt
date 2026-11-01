using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;

public class ScratchRecord : MonoBehaviour
{
    [SerializeField] private PigeonController _pigeonController;
    [SerializeField] private MusicPlayer _musicPlayer;
    [SerializeField] private Camera _camera;

    [SerializeField] private float _scratchTime = 1f;
    [SerializeField] private int _repetitions = 3;

    private CinemachineBrain _brain;
    private CinemachineVirtualCameraBase[] _virtualCameras;
    private CinemachineOrbitalFollow[] _orbits;
    private PidgeonVfx _pigeonVfx;
    private PigeonSFX _pigeonSfx;
    private CancellationTokenSource _scratchCancellation;
    private bool _initialized;

    private void Start()
    {
        if (_pigeonController == null)
            _pigeonController = FindAnyObjectByType<PigeonController>();
        if (_musicPlayer == null)
            _musicPlayer = FindAnyObjectByType<MusicPlayer>();
        if (_camera == null)
            _camera = Camera.main;

        if (_pigeonController == null || _musicPlayer == null || !_musicPlayer.HasAudioSources || _camera == null)
        {
            Debug.LogError("ScratchRecord : assigner le pigeon, la musique avec ses deux AudioSources et la caméra.", this);
            enabled = false;
            return;
        }

        _brain = _camera.GetComponent<CinemachineBrain>();
        _virtualCameras = _pigeonController.GetComponentsInChildren<CinemachineVirtualCameraBase>(true);
        _orbits = _pigeonController.GetComponentsInChildren<CinemachineOrbitalFollow>(true);
        _pigeonVfx = _pigeonController.GetComponent<PidgeonVfx>();
        _pigeonSfx = _pigeonController.GetComponent<PigeonSFX>();
        _initialized = true;
    }

    [Button]
    public void LaunchScratch()
    {
        LaunchScratch(_repetitions);
    }

    public void LaunchScratch(int repetitions)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !_initialized || _scratchCancellation != null)
            return;

        if (_scratchTime <= 0f || float.IsNaN(_scratchTime) || float.IsInfinity(_scratchTime) || repetitions <= 0)
        {
            Debug.LogWarning("ScratchRecord : la durée et le nombre de répétitions doivent être supérieurs à zéro.", this);
            return;
        }

        if (_pigeonController.IsStun || _pigeonController.IsMovementFrozen)
            return;

        _scratchCancellation = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        RunScratch(_scratchCancellation, repetitions).Forget();
    }

    private async UniTask RunScratch(CancellationTokenSource cancellation, int repetitions)
    {
        try
        {
            Transform pigeon = _pigeonController.transform;
            Vector3 position = pigeon.position;
            Quaternion rotation = pigeon.rotation;
            Vector3 velocity = _pigeonController.LastVelocity;
            MusicPlayer.PlaybackState music = _musicPlayer.CapturePlaybackState();
            PigeonSFX.FlightPlaybackState flightSound = _pigeonSfx != null
                ? _pigeonSfx.CaptureFlightPlaybackState() : default;
            Vector3 cameraPosition = _camera.transform.position;
            Quaternion cameraRotation = _camera.transform.rotation;
            Pose[] cameraPoses = new Pose[_virtualCameras.Length];
            Vector3[] orbitAxes = new Vector3[_orbits.Length];
            TimeSpan duration = TimeSpan.FromSeconds(_scratchTime);

            for (int i = 0; i < _virtualCameras.Length; i++)
                cameraPoses[i] = new Pose(_virtualCameras[i].State.RawPosition, _virtualCameras[i].State.RawOrientation);
            for (int i = 0; i < _orbits.Length; i++)
                orbitAxes[i] = new Vector3(_orbits[i].HorizontalAxis.Value, _orbits[i].VerticalAxis.Value, _orbits[i].RadialAxis.Value);

            for (int repeat = 0; repeat < repetitions; repeat++)
            {
                bool cancelled = await UniTask.Delay(duration, cancellationToken: cancellation.Token).SuppressCancellationThrow();
                if (cancelled)
                    return;

                // Ces objets peuvent disparaître lors d'un changement de scène.
                if (_pigeonController == null || _musicPlayer == null || _camera == null)
                    return;

                _pigeonController.Teleport(position, rotation, velocity, snapCameraBehind: false);
                if (_pigeonVfx != null)
                    _pigeonVfx.ResetAfterTeleport();
                if (_pigeonSfx != null)
                    _pigeonSfx.RestoreFlightPlaybackState(flightSound);
                _musicPlayer.RestorePlaybackState(music);
                if (_brain != null)
                    _brain.ResetState();

                for (int i = 0; i < _virtualCameras.Length; i++)
                {
                    if (_virtualCameras[i] != null)
                        _virtualCameras[i].ForceCameraPosition(cameraPoses[i].position, cameraPoses[i].rotation);
                }
                for (int i = 0; i < _orbits.Length; i++)
                {
                    CinemachineOrbitalFollow orbit = _orbits[i];
                    if (orbit == null)
                        continue;
                    orbit.HorizontalAxis.Value = orbitAxes[i].x;
                    orbit.VerticalAxis.Value = orbitAxes[i].y;
                    orbit.RadialAxis.Value = orbitAxes[i].z;
                    orbit.HorizontalAxis.CancelRecentering();
                    orbit.VerticalAxis.CancelRecentering();
                    orbit.RadialAxis.CancelRecentering();
                }
                _camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            }
        }
        finally
        {
            if (_scratchCancellation == cancellation)
                _scratchCancellation = null;
            cancellation.Dispose();
        }
    }

    private void OnDisable()
    {
        _scratchCancellation?.Cancel();
    }
}
