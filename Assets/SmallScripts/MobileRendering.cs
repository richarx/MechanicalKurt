using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Allège le rendu sur téléphone, au lancement et sans toucher aux assets : le navigateur sur ordinateur garde les
/// réglages de Mobile_RPAsset et CityProfile, qu'il partage aujourd'hui avec le téléphone.
/// </summary>
public static class MobileRendering
{
    public const float MaxDevicePixelRatio = 1.5f;
    public const int ShadowResolution = 512;
    public const float ShadowDistance = 25f;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void CapDevicePixelRatio(float maximum);
#else
    static void CapDevicePixelRatio(float maximum) { }
#endif

    static readonly List<VolumeComponent> DisabledEffects = new List<VolumeComponent>();
    static RenderPipelineAsset _sourcePipeline;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        DisabledEffects.Clear();
        _sourcePipeline = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyOnPhones()
    {
        if (!Application.isMobilePlatform)
            return;
        Apply();
        SceneManager.sceneLoaded += (_, _) => DisableExpensiveEffects();
    }

    /// <summary>Applique les réglages sur une copie du pipeline, et coupe les effets les plus coûteux.</summary>
    public static void Apply()
    {
        CapDevicePixelRatio(MaxDevicePixelRatio);
        DisableExpensiveEffects();

        RenderPipelineAsset current = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
        if (_sourcePipeline != null || current is not UniversalRenderPipelineAsset source)
            return;
        _sourcePipeline = QualitySettings.renderPipeline;
        UniversalRenderPipelineAsset light = Object.Instantiate(source);
        light.supportsHDR = false;
        light.supportsCameraOpaqueTexture = false;
        light.mainLightShadowmapResolution = ShadowResolution;
        light.shadowDistance = ShadowDistance;
        QualitySettings.renderPipeline = light;
    }

    /// <summary>Tous les profils chargés sont concernés : UnderWaterEffect change de profil en entrant dans l'eau.</summary>
    static void DisableExpensiveEffects()
    {
        foreach (VolumeProfile profile in Resources.FindObjectsOfTypeAll<VolumeProfile>())
            foreach (VolumeComponent effect in profile.components)
                if (effect is DepthOfField or Bloom or FilmGrain or MotionBlur && effect.active)
                {
                    effect.active = false;
                    DisabledEffects.Add(effect);
                }
    }

#if UNITY_EDITOR
    /// <summary>Scénarios : remet les réglages d'origine (ces profils sont des assets, à ne pas laisser modifiés dans l'éditeur).</summary>
    public static void RestoreForTests()
    {
        foreach (VolumeComponent effect in DisabledEffects)
            if (effect != null)
                effect.active = true;
        DisabledEffects.Clear();
        if (QualitySettings.renderPipeline == _sourcePipeline)
            return;
        RenderPipelineAsset light = QualitySettings.renderPipeline;
        QualitySettings.renderPipeline = _sourcePipeline;
        _sourcePipeline = null;
        if (light != null)
            Object.Destroy(light);
    }
#endif
}
