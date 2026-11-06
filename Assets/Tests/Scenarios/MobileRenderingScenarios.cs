#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public partial class ScenarioRunner
{
    static bool EffectActive<T>(VolumeProfile profile) where T : VolumeComponent => profile.TryGet(out T effect) && effect.active;

    IEnumerator MobileRenderingIsLighter()
    {
        var source = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        yield return Check("Le pipeline d'origine est un URP", source != null);
        bool sourceHdr = source.supportsHDR;
        int sourceShadows = source.mainLightShadowmapResolution;
        VolumeProfile city = source.volumeProfile;
        yield return Check("Sur ordinateur, flou de profondeur, bloom et grain restent actifs", city != null && EffectActive<DepthOfField>(city)
            && EffectActive<Bloom>(city) && EffectActive<FilmGrain>(city), city != null ? city.name : "pas de profil");
        Capture(Camera.main, "rendering_before");

        Defer(MobileRendering.RestoreForTests);
        MobileRendering.Apply();
        yield return new WaitForSecondsRealtime(0.3f);

        var light = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        yield return Check("Le téléphone a son propre pipeline, plus léger", light != null && light != source && !light.supportsHDR && !light.supportsCameraOpaqueTexture
            && light.mainLightShadowmapResolution == MobileRendering.ShadowResolution && Mathf.Approximately(light.shadowDistance, MobileRendering.ShadowDistance),
            light == null ? "aucun" : $"HDR {light.supportsHDR}, opaque {light.supportsCameraOpaqueTexture}, ombres {light.mainLightShadowmapResolution} sur {light.shadowDistance} m");
        yield return Check("L'asset d'origine n'a pas changé", source.supportsHDR == sourceHdr && source.mainLightShadowmapResolution == sourceShadows);
        yield return Check("Flou de profondeur, bloom et grain sont coupés", !EffectActive<DepthOfField>(city) && !EffectActive<Bloom>(city) && !EffectActive<FilmGrain>(city));
        yield return Check("Le rendu tourne toujours", Camera.main != null && Camera.main.isActiveAndEnabled);
        Capture(Camera.main, "rendering_mobile");

        MobileRendering.RestoreForTests();
        yield return new WaitForSecondsRealtime(0.3f);
        yield return Check("Remis en place : pipeline et effets d'origine", QualitySettings.renderPipeline == source
            && EffectActive<DepthOfField>(city) && EffectActive<Bloom>(city) && EffectActive<FilmGrain>(city));
    }
}
#endif
