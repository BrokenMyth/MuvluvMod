using System.IO;
using MelonLoader;
using MelonLoader.Utils;

namespace MuvluvMod;

public static class Config
{
    public static readonly string FilePath = Path.Combine(MelonEnvironment.UserDataDirectory, "MuvluvMod.cfg");

    public static MelonPreferences_Category GeneralCategory;
    public static MelonPreferences_Category TranslationCategory;

    public static MelonPreferences_Entry<bool> DynamicMosaic;
    public static MelonPreferences_Entry<bool> EnableSkipButton;
    public static MelonPreferences_Entry<bool> VoiceInterruption;
    public static MelonPreferences_Entry<bool> AutoSkipBattle;
    public static MelonPreferences_Entry<bool> DisableWhiteFlash;
    public static MelonPreferences_Entry<float> SpineScale;
    public static MelonPreferences_Entry<float> SpineOffsetX;
    public static MelonPreferences_Entry<float> SpineOffsetY;
    public static MelonPreferences_Entry<float> SpineMoveSensitivity;
    public static MelonPreferences_Entry<float> SpineScaleSensitivity;
    public static MelonPreferences_Entry<float> PortraitSpineScale;
    public static MelonPreferences_Entry<float> PortraitSpineOffsetX;
    public static MelonPreferences_Entry<float> PortraitSpineOffsetY;
    public static MelonPreferences_Entry<float> PortraitSpineMoveSensitivity;
    public static MelonPreferences_Entry<float> PortraitSpineScaleSensitivity;
    public static MelonPreferences_Entry<bool> SpineDebugLog;

    public static MelonPreferences_Entry<bool> Translation;
    public static MelonPreferences_Entry<string> TranslationCDN;
    public static MelonPreferences_Entry<string> FontBundlePath;
    public static MelonPreferences_Entry<string> FontAssetName;

    public static void Initialize()
    {
        GeneralCategory = MelonPreferences.CreateCategory("General");
        TranslationCategory = MelonPreferences.CreateCategory("Translation");
        GeneralCategory.SetFilePath(FilePath);
        TranslationCategory.SetFilePath(FilePath);

        DynamicMosaic = GeneralCategory.CreateEntry("DynamicMosaic", false, "是否开启游戏内动态马赛克");
        EnableSkipButton = GeneralCategory.CreateEntry("EnableSkipButton", false, "是否总是开启跳过按钮");
        VoiceInterruption = GeneralCategory.CreateEntry("VoiceInterruption", true, "剧情中播放下一句话时是否中断当前语音");
        AutoSkipBattle = GeneralCategory.CreateEntry("AutoSkipBattle", false, "自动跳过战斗（自动按跳过键，不受跳过键开关影响）");
        DisableWhiteFlash = GeneralCategory.CreateEntry("DisableWhiteFlash", false, "禁用剧本演出中的白屏闪烁效果");
        SpineScale = GeneralCategory.CreateEntry("SpineScale", 1f, "剧情Spine缩放倍率（按+/-调整）");
        SpineOffsetX = GeneralCategory.CreateEntry("SpineOffsetX", 0f, "剧情Spine水平偏移（按左右方向键调整）");
        SpineOffsetY = GeneralCategory.CreateEntry("SpineOffsetY", 0f, "剧情Spine垂直偏移（按上下方向键调整）");
        SpineMoveSensitivity = GeneralCategory.CreateEntry("SpineMoveSensitivity", 25f, "剧情Spine位置调整灵敏度（本地坐标/秒）");
        SpineScaleSensitivity = GeneralCategory.CreateEntry("SpineScaleSensitivity", 0.5f, "剧情Spine缩放调整灵敏度（倍率/秒）");
        PortraitSpineScale = GeneralCategory.CreateEntry("PortraitSpineScale", 1f, "剧情立绘Spine缩放倍率（按Shift +/-调整）");
        PortraitSpineOffsetX = GeneralCategory.CreateEntry("PortraitSpineOffsetX", 0f, "剧情立绘Spine水平偏移（按Shift 左右方向键调整）");
        PortraitSpineOffsetY = GeneralCategory.CreateEntry("PortraitSpineOffsetY", 0f, "剧情立绘Spine垂直偏移（按Shift 上下方向键调整）");
        PortraitSpineMoveSensitivity = GeneralCategory.CreateEntry("PortraitSpineMoveSensitivity", 25f, "剧情立绘Spine位置调整灵敏度（本地坐标/秒）");
        PortraitSpineScaleSensitivity = GeneralCategory.CreateEntry("PortraitSpineScaleSensitivity", 0.5f, "剧情立绘Spine缩放调整灵敏度（倍率/秒）");
        SpineDebugLog = GeneralCategory.CreateEntry("SpineDebugLog", true, "是否打印剧情Spine目标物体路径（确认生效后可关闭）");

        Translation = TranslationCategory.CreateEntry("Enable", true, "是否开启汉化");
        TranslationCDN = TranslationCategory.CreateEntry("CdnURL", "https://raw.githubusercontent.com/anosu/muvluvgg-translation/refs/heads/main", "翻译加载的CDN");
        FontBundlePath = TranslationCategory.CreateEntry("FontBundlePath", "font/sarasagothicsc-bold", "TMP字体AssetBundle的路径");
        FontAssetName = TranslationCategory.CreateEntry("FontAssetName", "SarasaGothicSC-Bold SDF", "AssetBundle中TMP_FontAsset的名称");

        Core.Log.Msg($"Translation: {(Translation.Value ? "Enabled" : "Disabled")}");
        Core.Log.Msg($"Translation CDN: {TranslationCDN.Value}");
        Core.Log.Msg($"Font Bundle Path: {FontBundlePath.Value}");
        Core.Log.Msg($"Font Asset Name: {FontAssetName.Value}");

        DynamicMosaic.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            Core.Log.Msg($"[General] DynamicMosaic => {newValue}");
        });
        EnableSkipButton.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            Core.Log.Msg($"[General] EnableSkipButton => {newValue}");
        });
        VoiceInterruption.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            Core.Log.Msg($"[General] VoiceInterruption => {newValue}");
        });
        AutoSkipBattle.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            Core.Log.Msg($"[General] AutoSkipBattle => {newValue}");
        });
        DisableWhiteFlash.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            Core.Log.Msg($"[General] DisableWhiteFlash => {newValue}");
        });
        SpineScale.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            Core.Log.Msg($"[General] SpineScale => {newValue}");
        });
        SpineOffsetX.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            Core.Log.Msg($"[General] SpineOffsetX => {newValue}");
        });
        SpineOffsetY.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            Core.Log.Msg($"[General] SpineOffsetY => {newValue}");
        });
        SpineMoveSensitivity.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            Core.Log.Msg($"[General] SpineMoveSensitivity => {newValue}");
        });
        SpineScaleSensitivity.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            Core.Log.Msg($"[General] SpineScaleSensitivity => {newValue}");
        });
        PortraitSpineScale.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            Core.Log.Msg($"[General] PortraitSpineScale => {newValue}");
        });
        PortraitSpineOffsetX.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            Core.Log.Msg($"[General] PortraitSpineOffsetX => {newValue}");
        });
        PortraitSpineOffsetY.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            Core.Log.Msg($"[General] PortraitSpineOffsetY => {newValue}");
        });
        PortraitSpineMoveSensitivity.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            Core.Log.Msg($"[General] PortraitSpineMoveSensitivity => {newValue}");
        });
        PortraitSpineScaleSensitivity.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            Core.Log.Msg($"[General] PortraitSpineScaleSensitivity => {newValue}");
        });
        SpineDebugLog.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            Core.Log.Msg($"[General] SpineDebugLog => {newValue}");
        });
        Translation.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            Core.Log.Msg($"[Translation] Enable => {newValue}");
        });
    }

    public static void SaveGeneral(bool printMessage = false)
    {
        GeneralCategory?.SaveToFile(printMessage);
    }
}
