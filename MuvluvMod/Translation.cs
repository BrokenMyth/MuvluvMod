using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTMPro;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace MuvluvMod;

public class Translation
{
    public static string cdn = "http://localhost:5000";
    public static HttpClient client = new();
    public static Dictionary<string, string> names = [];
    public static Dictionary<string, string> teamNames = [];
    public static Dictionary<string, string> titles = [];
    public static Dictionary<string, string> subTitles = [];
    public static Dictionary<long, Dictionary<string, string>> scenes = [];

    public static AssetBundle fontBundle = null;
    public static TMP_FontAsset fontAsset = null;
    public static Material outlineMaterial = null;

    public static TMP_FontAsset rawFontAsset = null;
    public static Material rawOutlineMaterial = null;

    public static bool IsTranslated => scenes.ContainsKey(Patch.sceneId);

    public static void Initialize()
    {
        cdn = Config.TranslationCDN.Value;
        Task.Run((System.Func<Task>)LoadTranslation);
        MelonCoroutines.Start(LoadFontAsset());
    }

    public static async Task<T> GetAsync<T>(string url) where T : class
    {
        try
        {
            var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<T>();
            }
            Core.Log.Warning($"GET {url} {response.StatusCode}");
        }
        catch (System.Exception ex)
        {
            Core.Log.Error($"Error: {ex.Message}");
        }
        return null;
    }

    public static async Task LoadTranslation()
    {
        if (!Config.Translation.Value)
        {
            return;
        }
        var nameTask = GetAsync<Dictionary<string, Dictionary<string, string>>>($"{cdn}/translation/names/zh_Hans.json");
        var titleTask = GetAsync<Dictionary<string, Dictionary<string, string>>>($"{cdn}/translation/titles/zh_Hans.json");
        await Task.WhenAll(nameTask, titleTask);

        if (nameTask.Result != null)
        {
            names = nameTask.Result["speakerNames"];
            teamNames = nameTask.Result["teamNames"];
            Core.Log.Msg($"Character names translation loaded. Total: {names.Count}");
            Core.Log.Msg($"Team names translation loaded. Total: {teamNames.Count}");
        }
        else
        {
            Core.Log.Warning("Names translation load failed");
        }

        if (titleTask.Result != null)
        {
            titles = titleTask.Result["titles"];
            subTitles = titleTask.Result["subTitles"];
            Core.Log.Msg($"Scenario titles translation loaded. Total: {titles.Count}");
            Core.Log.Msg($"Scenario subtitles translation loaded. Total: {subTitles.Count}");
        }
        else
        {
            Core.Log.Warning("Titles translation load failed");
        }
    }

    public static void LoadFontBundle()
    {
        string value = Config.FontBundlePath.Value;
        string path = Path.IsPathRooted(value) ? value : Path.Combine(MelonEnvironment.ModsDirectory, value);
        if (!File.Exists(path) || fontBundle != null)
        {
            return;
        }
        fontBundle = AssetBundle.LoadFromMemory(File.ReadAllBytes(path));
    }

    public static IEnumerator LoadFontAsset()
    {
        if (fontAsset != null || !Config.Translation.Value)
        {
            yield break;
        }
        LoadFontBundle();
        if (fontBundle == null)
        {
            Core.Log.Warning("Font bundle load failed");
            yield break;
        }
        var request = fontBundle.LoadAssetAsync(Config.FontAssetName.Value);
        yield return request;

        fontAsset = request.asset.TryCast<TMP_FontAsset>();
        Core.Log.Msg($"TMP_FontAsset {fontAsset.name} is loaded");

        var materialRequest = fontBundle.LoadAssetAsync($"{Config.FontAssetName.Value} Outline");
        yield return materialRequest;

        outlineMaterial = materialRequest.asset.TryCast<Material>();
        Core.Log.Msg($"Material {outlineMaterial.name} is loaded");
    }

    public static async Task GetScenarioTranslationAsync(long sceneId)
    {
        if (scenes.ContainsKey(sceneId))
        {
            return;
        }
        var translations = await GetAsync<Dictionary<string, string>>($"{cdn}/translation/scenes/{sceneId}/zh_Hans.json");
        if (translations != null)
        {
            scenes[sceneId] = translations;
            Core.Log.Msg($"Scenario translation loaded. Total: {translations.Count}");
        }
        else
        {
            Core.Log.Warning($"Scenario translations load failed: {sceneId}");
        }
    }
}
