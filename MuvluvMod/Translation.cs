﻿﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    public static string localRepoPath;
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
        localRepoPath = Path.Combine(Path.GetFullPath(Path.Combine(MelonEnvironment.ModsDirectory, "..")), "muvluvgg-translation", "translation");
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

        Dictionary<string, Dictionary<string, string>> nameResult = null;
        Dictionary<string, Dictionary<string, string>> titleResult = null;

        string localNamesPath = Path.Combine(localRepoPath, "names", "zh_Hans.json");
        string localTitlesPath = Path.Combine(localRepoPath, "titles", "zh_Hans.json");

        if (File.Exists(localNamesPath))
        {
            try
            {
                var node = JsonNode.Parse(await File.ReadAllTextAsync(localNamesPath));
                var speakerNames = new Dictionary<string, string>();
                foreach (var kv in (JsonObject)node["speakerNames"])
                    speakerNames[kv.Key] = kv.Value.ToString();
                var teamDict = new Dictionary<string, string>();
                foreach (var kv in (JsonObject)node["teamNames"])
                    teamDict[kv.Key] = kv.Value.ToString();
                nameResult = new Dictionary<string, Dictionary<string, string>>
                {
                    ["speakerNames"] = speakerNames,
                    ["teamNames"] = teamDict
                };
                Core.Log.Msg("Names translation loaded from local");
            }
            catch (System.Exception ex) { Core.Log.Warning($"Failed to load local names: {ex.Message}"); }
        }
        if (File.Exists(localTitlesPath))
        {
            try
            {
                var node = JsonNode.Parse(await File.ReadAllTextAsync(localTitlesPath));
                var titlesDict = new Dictionary<string, string>();
                foreach (var kv in (JsonObject)node["titles"])
                    titlesDict[kv.Key] = kv.Value.ToString();
                var subTitlesDict = new Dictionary<string, string>();
                foreach (var kv in (JsonObject)node["subTitles"])
                    subTitlesDict[kv.Key] = kv.Value.ToString();
                titleResult = new Dictionary<string, Dictionary<string, string>>
                {
                    ["titles"] = titlesDict,
                    ["subTitles"] = subTitlesDict
                };
                Core.Log.Msg("Titles translation loaded from local");
            }
            catch (System.Exception ex) { Core.Log.Warning($"Failed to load local titles: {ex.Message}"); }
        }

        if (nameResult == null || titleResult == null)
        {
            var nameTask = nameResult == null ? GetAsync<Dictionary<string, Dictionary<string, string>>>($"{cdn}/translation/names/zh_Hans.json") : Task.FromResult<Dictionary<string, Dictionary<string, string>>>(null);
            var titleTask = titleResult == null ? GetAsync<Dictionary<string, Dictionary<string, string>>>($"{cdn}/translation/titles/zh_Hans.json") : Task.FromResult<Dictionary<string, Dictionary<string, string>>>(null);
            await Task.WhenAll(nameTask, titleTask);

            if (nameResult == null && nameTask.Result != null)
            {
                nameResult = nameTask.Result;
                Core.Log.Msg("Names translation loaded from CDN");
            }
            if (titleResult == null && titleTask.Result != null)
            {
                titleResult = titleTask.Result;
                Core.Log.Msg("Titles translation loaded from CDN");
            }
        }

        if (nameResult != null)
        {
            names = nameResult["speakerNames"];
            teamNames = nameResult["teamNames"];
            Core.Log.Msg($"Character names translation loaded. Total: {names.Count}");
            Core.Log.Msg($"Team names translation loaded. Total: {teamNames.Count}");
        }
        else
        {
            Core.Log.Warning("Names translation load failed");
        }

        if (titleResult != null)
        {
            titles = titleResult["titles"];
            subTitles = titleResult["subTitles"];
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

        string localPath = Path.Combine(localRepoPath, "scenes", sceneId.ToString(), "zh_Hans.json");
        if (File.Exists(localPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(localPath);
                var node = JsonNode.Parse(json);
                var dict = new Dictionary<string, string>();
                foreach (var kv in (JsonObject)node)
                    dict[kv.Key] = kv.Value.ToString();
                if (dict.Count > 0)
                {
                    bool hasValue = false;
                    foreach (var v in dict.Values) { if (!string.IsNullOrEmpty(v)) { hasValue = true; break; } }
                    if (hasValue)
                {
                    scenes[sceneId] = dict;
                    Core.Log.Msg($"Scenario translation loaded from local: {sceneId} ({dict.Count} entries)");
                    return;
                }
                }
            }
            catch (System.Exception ex)
            {
                Core.Log.Warning($"Failed to read local translation {sceneId}: {ex.Message}");
            }
        }

        var translations = await GetAsync<Dictionary<string, string>>($"{cdn}/translation/scenes/{sceneId}/zh_Hans.json");
        if (translations != null)
        {
            scenes[sceneId] = translations;
            Core.Log.Msg($"Scenario translation loaded from CDN: {sceneId} ({translations.Count} entries)");
        }
        else
        {
            Core.Log.Warning($"Scenario translation not found: {sceneId}");
        }
    }

    public static void SavePendingSceneTranslation(long sceneId, Dictionary<string, string> texts)
    {
        var filtered = new Dictionary<string, string>();
        foreach (var kv in texts)
        {
            if (!string.IsNullOrEmpty(kv.Key))
                filtered[kv.Key] = kv.Value;
        }
        if (filtered.Count == 0) return;

        string pendingDir = Path.Combine(localRepoPath, "scenes_pending", sceneId.ToString());
        string pendingPath = Path.Combine(pendingDir, "zh_Hans.json");

        if (File.Exists(pendingPath)) return;

        try
        {
            Directory.CreateDirectory(pendingDir);
            var obj = new JsonObject();
            foreach (var kv in filtered)
                obj[kv.Key] = kv.Value;
            File.WriteAllText(pendingPath, obj.ToJsonString(new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }), System.Text.Encoding.UTF8);
            Core.Log.Msg($"Pending translation saved: {pendingPath} ({filtered.Count} entries)");
        }
        catch (System.Exception ex)
        {
            Core.Log.Warning($"Failed to save pending translation {sceneId}: {ex.Message}");
        }
    }
}
