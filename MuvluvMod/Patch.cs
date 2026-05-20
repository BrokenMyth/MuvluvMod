using System;
using System.Linq;
using System.Text.Json.Nodes;
using HarmonyLib;
using Il2CppAssets.Api.Client;
using Il2CppAssets.Battle.Overseers;
using Il2CppAssets.CustomRendererFeatures;
using Il2CppAssets.GameUi.Externals;
using Il2CppAssets.GameUi.Scenario;
using Il2CppAssets.GameUi.Scenario.Animation;
using Il2CppAssets.GameUi.Scenario.Choice;
using Il2CppAssets.GameUi.Scenario.History;
using Il2CppAssets.GameUi.Scenario.Text;
using Il2CppAssets.GameUi.Service;
using Il2CppAssets.VisualEffectData;
using Il2CppAssets.VisualEffectData.VisualEffects;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTMPro;
using Il2CppUniRx;
using Il2CppUniRx.Triggers;
using MelonLoader;
using UnityEngine;

namespace MuvluvMod;

public class Patch
{
    public static long sceneId;
    public static AdventureTitle adventureTitle;
    public static bool isPlayingScenario = false;

    public static void Initialize()
    {
        new HarmonyLib.Harmony("MuvluvMod").PatchAll(typeof(Patch));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MosaicRendererFeature), nameof(MosaicRendererFeature.Create))]
    public static void RemoveMosaic(MosaicRendererFeature __instance)
    {
        if (!Config.DynamicMosaic.Value)
        {
            __instance.passSettings.Keyword = "114514";
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(HudOverseer), nameof(HudOverseer.SetSkipAvaiability))]
    public static void EnableSkipButton(HudOverseer __instance, ref bool available)
    {
        if (Config.EnableSkipButton.Value)
        {
            available = true;
        }
        if (Config.AutoSkipBattle.Value)
        {
            __instance.ProcessSkipButtonClick();
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.StopVoice))]
    public static bool DisableStopVoice()
    {
        return Config.VoiceInterruption.Value || !isPlayingScenario;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ScenarioController), nameof(ScenarioController.Refresh), [])]
    public static void SetIsPlayingScenario()
    {
        isPlayingScenario = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ScenarioController), nameof(ScenarioController.Leave))]
    public static void SetIsNotPlayingScenario()
    {
        isPlayingScenario = false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EpisodeService), nameof(EpisodeService.DownloadSceneFrameMasters))]
    public static void LoadTranslation(EpisodeService __instance, long sceneMasterId)
    {
        Core.Log.Msg($"Scene: {sceneMasterId}");

        sceneId = sceneMasterId;

        __instance.sceneFrameMastersCache.Remove(sceneMasterId);

        if (!Config.Translation.Value) return;

        Translation.GetScenarioTranslationAsync(sceneMasterId).Wait();

        if (Translation.IsTranslated)
        {
            MelonCoroutines.Start(Translation.LoadFontAsset());
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ScenarioController), nameof(ScenarioController.GenerateFrames))]
    public static void ReplaceTranslation(Il2CppReferenceArray<SceneFrameMaster> masters)
    {
        if (!Config.Translation.Value || !Translation.IsTranslated) return;

        try
        {
            foreach (var frame in masters)
            {
                if (string.IsNullOrEmpty(frame.ConfigurationJson)) continue;

                var config = JsonNode.Parse(frame.ConfigurationJson);

                if (config?["Phrase"] is JsonObject phrase)
                {
                    if (phrase.TryGetPropertyValue("SpeakerName", out var nameNode)
                        && Translation.names.TryGetValue(nameNode.ToString(), out var speakerName))
                        phrase["SpeakerName"] = speakerName;

                    if (phrase.TryGetPropertyValue("TeamName", out var teamNode)
                        && Translation.teamNames.TryGetValue(teamNode.ToString(), out var teamName))
                        phrase["TeamName"] = teamName;

                    if (phrase.TryGetPropertyValue("Text", out var textNode)
                        && Translation.scenes[sceneId].TryGetValue(textNode.ToString(), out var text))
                        phrase["Text"] = text;
                }

                frame.ConfigurationJson = config.ToJsonString();
            }
        }
        catch (System.Exception ex)
        {
            Core.Log.Error($"Error in ReplaceTranslation: {ex.StackTrace}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ScenarioController), nameof(ScenarioController.GenerateFrame))]
    public static void ReplaceTitle(ScenarioController.ScenarioFrameViewModel __result)
    {
        if (!Config.Translation.Value || !Translation.IsTranslated || __result.TitleAnimation == null) return;

        if (Translation.titles.TryGetValue(__result.TitleAnimation.TitleHead, out string titleHead))
        {
            __result.TitleAnimation.TitleHead = titleHead;
        }
        if (Translation.subTitles.TryGetValue(__result.TitleAnimation.Title, out string title))
        {
            __result.TitleAnimation.Title = title;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ScenarioAnimationComponent), nameof(ScenarioAnimationComponent.Initialize))]
    public static void ReplaceTitleFont(ScenarioAnimationComponent __instance)
    {
        var parent = __instance.gameObject.transform.Find("ScreenAnimationParent");
        parent.OnTransformChildrenChangedAsObservable().Subscribe((System.Action<Unit>)(_ =>
        {
            Transform title = Enumerable.Range(0, parent.childCount)
                .Select(i => parent.GetChild(i))
                .FirstOrDefault(child => child.name.Contains("title", StringComparison.OrdinalIgnoreCase));

            if (title == null)
            {
                Core.Log.Warning("Title not found");
                return;
            }

            adventureTitle = title.GetComponent<AdventureTitle>();
            if (adventureTitle != null && Config.Translation.Value && Translation.IsTranslated)
            {
                adventureTitle.Title.font = Translation.fontAsset;
                adventureTitle.Body.font = Translation.fontAsset;
            }
        }));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ScenarioTextComponent), nameof(ScenarioTextComponent.OnEnable))]
    public static void ReplaceFont(ScenarioTextComponent __instance)
    {
        if (!Config.Translation.Value || !Translation.IsTranslated)
        {
            if (adventureTitle != null)
            {
                RestoreFontAsset(adventureTitle.Title);
                RestoreFontAsset(adventureTitle.Body);
            }
            RestoreFontAsset(__instance.nameText, true);
            RestoreFontAsset(__instance.affiliationText, true);
            RestoreFontAsset(__instance.sentenceText.tmpText, true);
            return;
        }

        if (Translation.rawFontAsset == null)
        {
            Translation.rawFontAsset = __instance.nameText.font;
            Translation.rawOutlineMaterial = __instance.nameText.fontMaterial;
        }

        if (adventureTitle != null)
        {
            adventureTitle.Title.font = Translation.fontAsset;
            adventureTitle.Body.font = Translation.fontAsset;
        }
        __instance.nameText.font = Translation.fontAsset;
        __instance.nameText.fontMaterial = Translation.outlineMaterial;
        __instance.affiliationText.font = Translation.fontAsset;
        __instance.affiliationText.fontMaterial = Translation.outlineMaterial;
        __instance.sentenceText.tmpText.font = Translation.fontAsset;
        __instance.sentenceText.tmpText.fontMaterial = Translation.outlineMaterial;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ScenarioTextComponent), nameof(ScenarioTextComponent.ApplySentence))]
    public static void FixLineSpacing(ScenarioTextComponent __instance)
    {
        if (!Config.Translation.Value || !Translation.IsTranslated) return;

        if (__instance.sentenceText.tmpText.font.name == Config.FontAssetName.Value)
        {
            __instance.sentenceText.tmpText.lineSpacing = 40f;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ScenarioHistoryCell), nameof(ScenarioHistoryCell.ApplyText))]
    public static void ReplaceHistoryChoice(ref string phrase, bool isAnswer)
    {
        if (!Config.Translation.Value || !Translation.IsTranslated) return;

        if (isAnswer && Translation.scenes[sceneId].TryGetValue(phrase, out string text))
        {
            phrase = text;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ScenarioHistoryCell), nameof(ScenarioHistoryCell.ApplySync))]
    public static void ReplaceHistoryFont(ScenarioHistoryCell __instance)
    {
        if (!Config.Translation.Value || !Translation.IsTranslated)
        {
            RestoreFontAsset(__instance.speakerName);
            RestoreFontAsset(__instance.text, false, -80f);
            return;
        }

        __instance.speakerName.font = Translation.fontAsset;
        __instance.text.font = Translation.fontAsset;

        __instance.text.lineSpacing = 0f;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ScenarioChoiceElementComponent), nameof(ScenarioChoiceElementComponent.Apply))]
    public static void ReplaceChoice(ScenarioChoiceElementComponent __instance, ScenarioChoiceElementComponent.Args args)
    {
        if (!Config.Translation.Value || !Translation.IsTranslated)
        {
            RestoreFontAsset(__instance.text);
            return;
        }

        if (Translation.scenes[sceneId].TryGetValue(args.Text, out string text))
        {
            args.Text = text;
            __instance.text.font = Translation.fontAsset;
        }
        else
        {
            RestoreFontAsset(__instance.text);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(VfxHandler), nameof(VfxHandler.VFX_EVENT_LightFlash))]
    public static bool DisableWhiteFlash()
    {
        return !Config.DisableWhiteFlash.Value;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(VfxHandler), nameof(VfxHandler.VFX_EVENT_DarkFlash))]
    public static bool DisableDarkFlash()
    {
        // return !Config.DisableWhiteFlash.Value;
        return true;
    }

    public static void RestoreFontAsset(TMP_Text text, bool restoreMaterial = false, float? lineSpacing = null)
    {
        if (Translation.rawFontAsset == null) return;

        text.font = Translation.rawFontAsset;

        if (restoreMaterial && Translation.rawOutlineMaterial != null)
        {
            text.fontMaterial = Translation.rawOutlineMaterial;
        }

        if (lineSpacing.HasValue) text.lineSpacing = lineSpacing.Value;
    }
}
