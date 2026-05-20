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
        Translation.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            Core.Log.Msg($"[Translation] Enable => {newValue}");
        });
    }
}
