using System;
using System.Collections.Generic;
using Il2CppAssets.GameUi.Scenario;
using Il2CppAssets.GameUi.Scenario.Animation;
using Il2CppAssets.Utilities.Spine;
using Il2CppAssets.VisualEffectData;
using Il2CppSpine.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MuvluvMod;

public static class SpineControl
{
    private const float MinimumScale = 0.05f;
    private const float SaveDelaySeconds = 0.5f;
    private const float TargetRefreshIntervalSeconds = 0.25f;
    private const float ScenarioResolveIntervalSeconds = 1f;
    private const float NoTargetLogIntervalSeconds = 5f;
    private const string BackgroundManagerResolutionPath = "Root Lifetime Scope/Background Manager/Resolution Constraint";

    private static readonly Dictionary<int, AppliedTransformState> AppliedStates = new();
    private static readonly List<int> InactiveStateIds = new();
    private static readonly List<SpineTargetCandidate> CandidateTargets = new();
    private static readonly List<SpineTargetCandidate> CurrentTargets = new();
    private static readonly Dictionary<int, AppliedTransformState> PortraitAppliedStates = new();
    private static readonly List<int> PortraitInactiveStateIds = new();
    private static readonly List<SpineTargetCandidate> PortraitCandidateTargets = new();
    private static readonly List<SpineTargetCandidate> CurrentPortraitTargets = new();
    private static readonly Dictionary<int, ObservedMecanimTarget> ObservedMecanimTargets = new();
    private static readonly Dictionary<int, GlobalSpineTarget> GlobalSpineTargets = new();
    private static readonly HashSet<int> CandidateTargetIds = new();
    private static readonly HashSet<int> PortraitCandidateTargetIds = new();
    private static readonly HashSet<int> FrameAppliedIds = new();
    private static readonly HashSet<int> PortraitFrameAppliedIds = new();
    private static readonly HashSet<int> LoggedTargetIds = new();
    private static readonly HashSet<int> LoggedPortraitTargetIds = new();
    private static readonly HashSet<int> LoggedObservedMecanimIds = new();
    private static readonly HashSet<int> LoggedParentIds = new();

    private static ScenarioAnimationComponent scenarioAnimationComponent;
    private static Transform backgroundLayerParent;
    private static Transform foregroundLayerParent;
    private static bool pendingSave;
    private static bool forceTargetRefresh = true;
    private static float lastChangedAt;
    private static float lastTargetRefreshAt;
    private static float lastScenarioResolveAt;
    private static float lastNoTargetLogAt;

    public static void SetScenarioAnimationComponent(ScenarioAnimationComponent component)
    {
        if (!IsUnityObjectAlive(component)) return;

        var changed = !IsUnityObjectAlive(scenarioAnimationComponent)
                      || scenarioAnimationComponent.GetInstanceID() != component.GetInstanceID();

        scenarioAnimationComponent = component;

        if (!changed) return;

        ClearTrackedSpines();
        forceTargetRefresh = true;
        LogDebug($"ScenarioAnimationComponent: {GetTransformPath(component.transform)}");
    }

    public static void ClearTrackedSpines()
    {
        CandidateTargets.Clear();
        CurrentTargets.Clear();
        PortraitCandidateTargets.Clear();
        CurrentPortraitTargets.Clear();
        ObservedMecanimTargets.Clear();
        GlobalSpineTargets.Clear();
        CandidateTargetIds.Clear();
        PortraitCandidateTargetIds.Clear();
        FrameAppliedIds.Clear();
        PortraitFrameAppliedIds.Clear();
        LoggedTargetIds.Clear();
        LoggedPortraitTargetIds.Clear();
        LoggedObservedMecanimIds.Clear();
        LoggedParentIds.Clear();
        forceTargetRefresh = true;
    }

    public static void MarkScenarioVfxChanged(string reason)
    {
        forceTargetRefresh = true;
        LogDebug($"Scenario screen VFX changed: {reason}");
    }

    public static void TrackScenarioMecanimTrigger(MecanimController controller, string triggerName)
    {
        if (!Patch.isPlayingScenario || !IsSpecialScenarioTrigger(triggerName)) return;
        if (!IsUnityObjectAlive(controller) || !IsUnityObjectAlive(controller.transform)) return;

        ResolveScenarioAnimationComponent();
        if (!IsUnityObjectAlive(scenarioAnimationComponent)) return;
        if (!IsAncestorOf(scenarioAnimationComponent.transform, controller.transform)) return;

        var transform = controller.transform;
        var id = transform.GetInstanceID();
        ObservedMecanimTargets[id] = new ObservedMecanimTarget(transform, $"MecanimController.SetTrigger({triggerName})");
        forceTargetRefresh = true;

        if (Config.SpineDebugLog.Value && LoggedObservedMecanimIds.Add(id))
        {
            LogDebug($"Observed special scenario Mecanim trigger {triggerName} => {GetTransformPath(transform)}");
        }
    }

    public static void OnUpdate()
    {
        HandleInput();
    }

    public static void OnLateUpdate()
    {
        ApplyToCurrentScenarioSpines();
        SavePendingIfIdle();
    }

    public static void FlushConfig()
    {
        if (!pendingSave) return;

        Config.SaveGeneral();
        pendingSave = false;
    }

    private static void HandleInput()
    {
        if (!IsScenarioControlActive() || Keyboard.current == null) return;

        var keyboard = Keyboard.current;
        if (keyboard.f8Key.wasPressedThisFrame)
        {
            DumpCurrentTargets("F8");
        }

        var portraitMode = IsPortraitControlModifierPressed(keyboard);
        var deltaTime = Mathf.Max(Time.unscaledDeltaTime, 1f / 120f);
        var moveStep = Mathf.Max(0f, portraitMode ? Config.PortraitSpineMoveSensitivity.Value : Config.SpineMoveSensitivity.Value) * deltaTime;
        var scaleStep = Mathf.Max(0f, portraitMode ? Config.PortraitSpineScaleSensitivity.Value : Config.SpineScaleSensitivity.Value) * deltaTime;

        var offsetX = portraitMode ? Config.PortraitSpineOffsetX.Value : Config.SpineOffsetX.Value;
        var offsetY = portraitMode ? Config.PortraitSpineOffsetY.Value : Config.SpineOffsetY.Value;
        var scale = Mathf.Max(MinimumScale, portraitMode ? Config.PortraitSpineScale.Value : Config.SpineScale.Value);

        if (keyboard.leftArrowKey.isPressed) offsetX -= moveStep;
        if (keyboard.rightArrowKey.isPressed) offsetX += moveStep;
        if (keyboard.downArrowKey.isPressed) offsetY -= moveStep;
        if (keyboard.upArrowKey.isPressed) offsetY += moveStep;

        if (keyboard.minusKey.isPressed || keyboard.numpadMinusKey.isPressed) scale -= scaleStep;
        if (keyboard.equalsKey.isPressed || keyboard.numpadPlusKey.isPressed) scale += scaleStep;

        scale = Mathf.Max(MinimumScale, scale);

        var changed = portraitMode
            ? !Mathf.Approximately(offsetX, Config.PortraitSpineOffsetX.Value)
              || !Mathf.Approximately(offsetY, Config.PortraitSpineOffsetY.Value)
              || !Mathf.Approximately(scale, Config.PortraitSpineScale.Value)
            : !Mathf.Approximately(offsetX, Config.SpineOffsetX.Value)
              || !Mathf.Approximately(offsetY, Config.SpineOffsetY.Value)
              || !Mathf.Approximately(scale, Config.SpineScale.Value);

        if (!changed) return;

        if (portraitMode)
        {
            Config.PortraitSpineOffsetX.Value = offsetX;
            Config.PortraitSpineOffsetY.Value = offsetY;
            Config.PortraitSpineScale.Value = scale;
        }
        else
        {
            Config.SpineOffsetX.Value = offsetX;
            Config.SpineOffsetY.Value = offsetY;
            Config.SpineScale.Value = scale;
        }

        forceTargetRefresh = true;
        MarkConfigChanged();
    }

    private static bool IsPortraitControlModifierPressed(Keyboard keyboard)
    {
        return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
    }

    private static void MarkConfigChanged()
    {
        pendingSave = true;
        lastChangedAt = Time.realtimeSinceStartup;
    }

    private static void SavePendingIfIdle()
    {
        if (!pendingSave || Time.realtimeSinceStartup - lastChangedAt < SaveDelaySeconds) return;

        Config.SaveGeneral();
        pendingSave = false;
    }

    private static void ApplyToCurrentScenarioSpines(bool forceRefresh = false)
    {
        var scenarioControlActive = IsScenarioControlActive();

        try
        {
            RefreshTargetList(forceRefresh || forceTargetRefresh, scenarioControlActive);
            forceTargetRefresh = false;
        }
        catch (Exception ex)
        {
            Core.Log.Error($"[SpineControl] Failed to refresh targets: {ex}");
            forceTargetRefresh = true;
            return;
        }

        ApplyTargets(
            CurrentTargets,
            AppliedStates,
            InactiveStateIds,
            FrameAppliedIds,
            LoggedTargetIds,
            "large",
            Config.SpineOffsetX.Value,
            Config.SpineOffsetY.Value,
            Config.SpineScale.Value);

        if (scenarioControlActive)
        {
            ApplyTargets(
                CurrentPortraitTargets,
                PortraitAppliedStates,
                PortraitInactiveStateIds,
                PortraitFrameAppliedIds,
                LoggedPortraitTargetIds,
                "portrait",
                Config.PortraitSpineOffsetX.Value,
                Config.PortraitSpineOffsetY.Value,
                Config.PortraitSpineScale.Value);
        }
        else
        {
            CleanupDeadStates(PortraitAppliedStates);
        }

        if (scenarioControlActive && CurrentTargets.Count == 0 && Config.SpineDebugLog.Value
            && Time.realtimeSinceStartup - lastNoTargetLogAt >= NoTargetLogIntervalSeconds)
        {
            lastNoTargetLogAt = Time.realtimeSinceStartup;
            Core.Log.Warning("[SpineControl] No large Background Manager parent target yet. Waiting for Background/Foreground containers.");
        }
    }

    private static void RefreshTargetList(bool force, bool includeScenarioTargets)
    {
        if (!force && Time.realtimeSinceStartup - lastTargetRefreshAt < TargetRefreshIntervalSeconds) return;

        lastTargetRefreshAt = Time.realtimeSinceStartup;
        CandidateTargets.Clear();
        CurrentTargets.Clear();
        CandidateTargetIds.Clear();
        PortraitCandidateTargets.Clear();
        CurrentPortraitTargets.Clear();
        PortraitCandidateTargetIds.Clear();

        AddBackgroundManagerParentTargets();
        if (includeScenarioTargets)
        {
            AddPortraitParentTargets();
        }

        SelectTopLevelTargets(CandidateTargets, CurrentTargets);
        SelectTopLevelTargets(PortraitCandidateTargets, CurrentPortraitTargets);
    }

    private static void AddBackgroundManagerParentTargets()
    {
        ResolveBackgroundManagerParents();

        AddBackgroundManagerParentTarget(backgroundLayerParent, "Background");
        AddBackgroundManagerParentTarget(foregroundLayerParent, "Foreground");
    }

    private static void AddBackgroundManagerParentTarget(Transform transform, string layerName)
    {
        if (!IsUnityObjectAlive(transform)) return;

        AddCandidate(transform, $"background-manager-parent/{layerName}", false);
    }

    private static void AddPortraitParentTargets()
    {
        AddScenarioChildPortraitTarget("ModelAnimationParent/Stand");
        AddScenarioChildPortraitTarget("ScreenAnimationParent/Stand");
        AddScenarioChildPortraitTarget("ScreenAnimationParent/StandBack");
        AddScenarioChildPortraitTarget("ModelAnimationParent/Shot/ShotContainer");
    }

    private static void AddScenarioChildPortraitTarget(string relativePath)
    {
        if (!IsUnityObjectAlive(scenarioAnimationComponent)) return;

        var transform = scenarioAnimationComponent.transform.Find(relativePath);
        if (!IsUnityObjectAlive(transform)) return;

        AddPortraitCandidate(transform, $"scenario-portrait-parent/{relativePath}", false);
    }

    private static void ResolveBackgroundManagerParents()
    {
        backgroundLayerParent = ResolveBackgroundManagerParent(backgroundLayerParent, "Background");
        foregroundLayerParent = ResolveBackgroundManagerParent(foregroundLayerParent, "Foreground");
    }

    private static Transform ResolveBackgroundManagerParent(Transform current, string layerName)
    {
        if (IsUnityObjectAlive(current)) return current;

        var transform = FindBackgroundManagerParent(layerName);
        if (!IsUnityObjectAlive(transform)) return null;

        if (Config.SpineDebugLog.Value && LoggedParentIds.Add(transform.GetInstanceID()))
        {
            LogDebug($"Background Manager parent resolved {layerName}: {GetTransformPath(transform)} pos={FormatVector(transform.localPosition)} scale={FormatVector(transform.localScale)}");
        }

        forceTargetRefresh = true;
        return transform;
    }

    private static Transform FindBackgroundManagerParent(string layerName)
    {
        var exactObject = GameObject.Find($"{BackgroundManagerResolutionPath}/{layerName}");
        if (IsUnityObjectAlive(exactObject)) return exactObject.transform;

        var resolutionObject = GameObject.Find(BackgroundManagerResolutionPath);
        if (!IsUnityObjectAlive(resolutionObject)) return null;

        var child = resolutionObject.transform.Find(layerName);
        return IsUnityObjectAlive(child) ? child : null;
    }

    private static void AddScenarioVfxTargets()
    {
        if (!IsUnityObjectAlive(scenarioAnimationComponent)) return;

        var buffer = scenarioAnimationComponent.vfxBuffer;
        if (buffer != null)
        {
            try
            {
                foreach (var pair in buffer)
                {
                    AddVfxCandidate(pair.Value.Item1, $"vfxBuffer/{pair.Key} assetId={pair.Value.Item2}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Skipped vfxBuffer scan: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static void AddObservedMecanimTargets()
    {
        foreach (var pair in ObservedMecanimTargets)
        {
            var target = pair.Value;
            if (!IsUnityObjectAlive(target.Transform) || !target.Transform.gameObject.activeInHierarchy) continue;

            AddCandidate(target.Transform, target.Source);
        }
    }

    private static bool AddVfxCandidate(VfxHandler vfx, string source)
    {
        if (!IsUnityObjectAlive(vfx) || !vfx.gameObject.activeInHierarchy) return false;

        return AddCandidate(vfx.transform, source);
    }

    private static bool AddCandidate(Transform transform, string source)
    {
        return AddCandidate(transform, source, CandidateTargets, CandidateTargetIds, true);
    }

    private static bool AddCandidate(Transform transform, string source, bool requireActive)
    {
        return AddCandidate(transform, source, CandidateTargets, CandidateTargetIds, requireActive);
    }

    private static bool AddPortraitCandidate(Transform transform, string source)
    {
        return AddCandidate(transform, source, PortraitCandidateTargets, PortraitCandidateTargetIds, true);
    }

    private static bool AddPortraitCandidate(Transform transform, string source, bool requireActive)
    {
        return AddCandidate(transform, source, PortraitCandidateTargets, PortraitCandidateTargetIds, requireActive);
    }

    private static bool AddCandidate(
        Transform transform,
        string source,
        List<SpineTargetCandidate> candidateTargets,
        HashSet<int> candidateTargetIds,
        bool requireActive)
    {
        if (!IsUnityObjectAlive(transform)) return false;
        if (requireActive && !transform.gameObject.activeInHierarchy) return false;

        var id = transform.GetInstanceID();
        if (!candidateTargetIds.Add(id)) return false;

        candidateTargets.Add(new SpineTargetCandidate(transform, source, GetDepth(transform)));
        return true;
    }

    private static void SelectTopLevelTargets(List<SpineTargetCandidate> candidateTargets, List<SpineTargetCandidate> currentTargets)
    {
        candidateTargets.Sort((left, right) => left.Depth.CompareTo(right.Depth));

        foreach (var candidate in candidateTargets)
        {
            if (!IsUnityObjectAlive(candidate.Transform)) continue;

            var nestedUnderSelectedTarget = false;
            foreach (var selected in currentTargets)
            {
                if (IsAncestorOf(selected.Transform, candidate.Transform))
                {
                    nestedUnderSelectedTarget = true;
                    break;
                }
            }

            if (!nestedUnderSelectedTarget)
            {
                currentTargets.Add(candidate);
            }
        }
    }

    private static void ApplyTargets(
        List<SpineTargetCandidate> targets,
        Dictionary<int, AppliedTransformState> states,
        List<int> inactiveStateIds,
        HashSet<int> frameAppliedIds,
        HashSet<int> loggedTargetIds,
        string label,
        float offsetX,
        float offsetY,
        float scale)
    {
        inactiveStateIds.Clear();
        foreach (var id in states.Keys)
        {
            inactiveStateIds.Add(id);
        }

        frameAppliedIds.Clear();
        foreach (var target in targets)
        {
            try
            {
                ApplyTarget(target, states, inactiveStateIds, frameAppliedIds, loggedTargetIds, label, offsetX, offsetY, scale);
            }
            catch (Exception ex)
            {
                Core.Log.Error($"[SpineControl] Failed to apply {label} target {target.Source}: {ex}");
            }
        }

        foreach (var id in inactiveStateIds)
        {
            if (!states.TryGetValue(id, out var state) || IsUnityObjectAlive(state.Transform)) continue;

            states.Remove(id);
        }
    }

    private static void CleanupDeadStates(Dictionary<int, AppliedTransformState> states)
    {
        InactiveStateIds.Clear();
        foreach (var id in states.Keys)
        {
            InactiveStateIds.Add(id);
        }

        foreach (var id in InactiveStateIds)
        {
            if (!states.TryGetValue(id, out var state) || IsUnityObjectAlive(state.Transform)) continue;

            states.Remove(id);
        }
    }

    private static void ApplyTarget(
        SpineTargetCandidate target,
        Dictionary<int, AppliedTransformState> states,
        List<int> inactiveStateIds,
        HashSet<int> frameAppliedIds,
        HashSet<int> loggedTargetIds,
        string label,
        float offsetX,
        float offsetY,
        float scale)
    {
        var transform = target.Transform;
        if (!IsUnityObjectAlive(transform)) return;

        var id = transform.GetInstanceID();
        if (!frameAppliedIds.Add(id)) return;

        inactiveStateIds.Remove(id);

        if (!states.TryGetValue(id, out var state))
        {
            state = new AppliedTransformState();
            states[id] = state;
        }
        state.Transform = transform;

        if (Config.SpineDebugLog.Value && loggedTargetIds.Add(id))
        {
            LogDebug($"Target[{label}] {target.Source} => {GetTransformPath(transform)} pos={FormatVector(transform.localPosition)} scale={FormatVector(transform.localScale)}");
        }

        ApplyTransform(transform, state, offsetX, offsetY, scale);
    }

    private static void ApplyTransform(Transform transform, AppliedTransformState state, float offsetX, float offsetY, float scale)
    {
        var rawPosition = transform.localPosition;
        var rawScale = transform.localScale;

        if (state.HasLastApplied)
        {
            if (Approximately(rawPosition, state.LastAppliedPosition))
            {
                rawPosition = new Vector3(
                    rawPosition.x - state.LastOffsetX,
                    rawPosition.y - state.LastOffsetY,
                    rawPosition.z);
            }

            if (Approximately(rawScale, state.LastAppliedScale) && state.LastScale > MinimumScale)
            {
                rawScale = new Vector3(
                    rawScale.x / state.LastScale,
                    rawScale.y / state.LastScale,
                    rawScale.z / state.LastScale);
            }
        }

        scale = Mathf.Max(MinimumScale, scale);

        var appliedPosition = new Vector3(rawPosition.x + offsetX, rawPosition.y + offsetY, rawPosition.z);
        var appliedScale = new Vector3(rawScale.x * scale, rawScale.y * scale, rawScale.z * scale);

        if (!Approximately(transform.localPosition, appliedPosition))
        {
            transform.localPosition = appliedPosition;
        }

        if (!Approximately(transform.localScale, appliedScale))
        {
            transform.localScale = appliedScale;
        }

        state.LastAppliedPosition = appliedPosition;
        state.LastAppliedScale = appliedScale;
        state.LastOffsetX = offsetX;
        state.LastOffsetY = offsetY;
        state.LastScale = scale;
        state.HasLastApplied = true;
    }

    private static bool IsScenarioControlActive()
    {
        ResolveScenarioAnimationComponent();
        if (!IsUnityObjectAlive(scenarioAnimationComponent)) return false;

        return Patch.isPlayingScenario || scenarioAnimationComponent.gameObject.activeInHierarchy;
    }

    private static void ResolveScenarioAnimationComponent()
    {
        if (IsUnityObjectAlive(scenarioAnimationComponent)) return;
        if (Time.realtimeSinceStartup - lastScenarioResolveAt < ScenarioResolveIntervalSeconds) return;

        lastScenarioResolveAt = Time.realtimeSinceStartup;

        var components = UnityEngine.Object.FindObjectsOfType<ScenarioAnimationComponent>(true);
        ScenarioAnimationComponent fallback = null;

        foreach (var component in components)
        {
            if (!IsUnityObjectAlive(component)) continue;

            if (component.gameObject.activeInHierarchy)
            {
                SetScenarioAnimationComponent(component);
                return;
            }

            fallback ??= component;
        }

        if (fallback != null)
        {
            SetScenarioAnimationComponent(fallback);
        }
    }

    private static void DumpCurrentTargets(string reason)
    {
        RefreshTargetList(true, IsScenarioControlActive());
        Core.Log.Msg($"[SpineControl] Dump targets ({reason}): {CurrentTargets.Count}");

        foreach (var target in CurrentTargets)
        {
            var transform = target.Transform;
            if (!IsUnityObjectAlive(transform)) continue;

            Core.Log.Msg($"[SpineControl]   [large] {target.Source} => {GetTransformPath(transform)} pos={FormatVector(transform.localPosition)} scale={FormatVector(transform.localScale)}");
        }

        Core.Log.Msg($"[SpineControl] Dump portrait targets ({reason}): {CurrentPortraitTargets.Count}");
        foreach (var target in CurrentPortraitTargets)
        {
            var transform = target.Transform;
            if (!IsUnityObjectAlive(transform)) continue;

            Core.Log.Msg($"[SpineControl]   [portrait] {target.Source} => {GetTransformPath(transform)} pos={FormatVector(transform.localPosition)} scale={FormatVector(transform.localScale)}");
        }

        DumpScenarioRuntimeObjects();
        DumpGlobalSpineScan("F8");
    }

    private static bool IsUnityObjectAlive(UnityEngine.Object unityObject)
    {
        try
        {
            return unityObject != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsAncestorOf(Transform ancestor, Transform child)
    {
        if (!IsUnityObjectAlive(ancestor) || !IsUnityObjectAlive(child)) return false;

        var current = child.parent;
        while (current != null)
        {
            if (current.GetInstanceID() == ancestor.GetInstanceID()) return true;
            current = current.parent;
        }

        return false;
    }

    private static int GetDepth(Transform transform)
    {
        var depth = 0;
        var current = transform;
        while (current != null && depth < 128)
        {
            depth++;
            current = current.parent;
        }

        return depth;
    }

    private static string GetTransformPath(Transform transform)
    {
        if (!IsUnityObjectAlive(transform)) return "<null>";

        var names = new List<string>();
        var current = transform;
        while (current != null && names.Count < 128)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private static string FormatVector(Vector3 vector)
    {
        return $"({vector.x:F2}, {vector.y:F2}, {vector.z:F2})";
    }

    private static void DumpScenarioRuntimeObjects()
    {
        if (!IsUnityObjectAlive(scenarioAnimationComponent)) return;

        var root = scenarioAnimationComponent.transform;
        var mecanimControllers = root.GetComponentsInChildren<MecanimController>(true);
        Core.Log.Msg($"[SpineControl] Scenario MecanimControllers: {mecanimControllers?.Length ?? 0}");
        if (mecanimControllers != null)
        {
            foreach (var controller in mecanimControllers)
            {
                if (!IsUnityObjectAlive(controller)) continue;

                var transform = controller.transform;
                Core.Log.Msg($"[SpineControl]   Mecanim trigger={controller.trigger} active={controller.gameObject.activeInHierarchy} => {GetTransformPath(transform)} pos={FormatVector(transform.localPosition)} scale={FormatVector(transform.localScale)}");
            }
        }

        var vfxHandlers = root.GetComponentsInChildren<VfxHandler>(true);
        Core.Log.Msg($"[SpineControl] Scenario VfxHandlers: {vfxHandlers?.Length ?? 0}");
        if (vfxHandlers == null) return;

        foreach (var vfx in vfxHandlers)
        {
            if (!IsUnityObjectAlive(vfx)) continue;

            var transform = vfx.transform;
            Core.Log.Msg($"[SpineControl]   VFX active={vfx.gameObject.activeInHierarchy} enabled={vfx.enabled} => {GetTransformPath(transform)} pos={FormatVector(transform.localPosition)} scale={FormatVector(transform.localScale)}");
        }
    }

    private static void DumpGlobalSpineScan(string reason)
    {
        GlobalSpineTargets.Clear();

        var mecanimControllerCount = AddGlobalSpineComponents<MecanimController>(nameof(MecanimController));
        var skeletonControllerCount = AddGlobalSpineComponents<SkeletonController>(nameof(SkeletonController));
        var skeletonMecanimCount = AddGlobalSpineComponents<SkeletonMecanim>(nameof(SkeletonMecanim));
        var skeletonAnimationCount = AddGlobalSpineComponents<SkeletonAnimation>(nameof(SkeletonAnimation));
        var skeletonGraphicCount = AddGlobalSpineComponents<SkeletonGraphic>(nameof(SkeletonGraphic));
        var skeletonRendererCount = AddGlobalSpineComponents<SkeletonRenderer>(nameof(SkeletonRenderer));

        if (!Config.SpineDebugLog.Value) return;

        if (GlobalSpineTargets.Count == 0)
        {
            Core.Log.Warning($"[SpineControl] Global Spine scan ({reason}): 0 transforms.");
            return;
        }

        var targets = new List<GlobalSpineTarget>(GlobalSpineTargets.Values);
        targets.Sort((left, right) => string.Compare(GetTransformPath(left.Transform), GetTransformPath(right.Transform), StringComparison.Ordinal));

        var backgroundManagerTargets = new List<GlobalSpineTarget>();
        foreach (var target in targets)
        {
            if (!IsUnityObjectAlive(target.Transform) || !target.Transform.gameObject.activeInHierarchy) continue;
            if (IsLargeScenarioBackgroundManagerSpine(target.Transform))
            {
                backgroundManagerTargets.Add(target);
            }
        }

        if (backgroundManagerTargets.Count > 0 && reason != "F8")
        {
            Core.Log.Msg($"[SpineControl] Background Manager Spine target found ({backgroundManagerTargets.Count}):");
            foreach (var target in backgroundManagerTargets)
            {
                var transform = target.Transform;
                Core.Log.Msg($"[SpineControl]   [target] {target.ComponentNames} scene={GetSceneName(transform)} => {GetTransformPath(transform)} pos={FormatVector(transform.localPosition)} scale={FormatVector(transform.localScale)}");
            }

            return;
        }

        Core.Log.Msg(
            $"[SpineControl] Global Spine scan ({reason}): {GlobalSpineTargets.Count} transforms "
            + $"components: MecanimController={mecanimControllerCount}, SkeletonController={skeletonControllerCount}, "
            + $"SkeletonMecanim={skeletonMecanimCount}, SkeletonAnimation={skeletonAnimationCount}, "
            + $"SkeletonGraphic={skeletonGraphicCount}, SkeletonRenderer={skeletonRendererCount}");

        foreach (var target in targets)
        {
            var transform = target.Transform;
            if (!IsUnityObjectAlive(transform)) continue;

            var scope = IsUnityObjectAlive(scenarioAnimationComponent) && IsAncestorOf(scenarioAnimationComponent.transform, transform)
                ? "scenario"
                : "global";

            Core.Log.Msg($"[SpineControl]   [{scope}] {target.ComponentNames} active={transform.gameObject.activeInHierarchy} scene={GetSceneName(transform)} => {GetTransformPath(transform)} pos={FormatVector(transform.localPosition)} scale={FormatVector(transform.localScale)}");
        }
    }

    private static int AddGlobalSpineComponents<T>(string componentName)
        where T : Component
    {
        try
        {
            var components = UnityEngine.Object.FindObjectsOfType<T>(true);
            if (components == null) return 0;

            foreach (var component in components)
            {
                if (!IsUnityObjectAlive(component) || !IsUnityObjectAlive(component.transform)) continue;

                AddGlobalSpineTarget(component.transform, componentName);
            }

            return components.Length;
        }
        catch (Exception ex)
        {
            if (Config.SpineDebugLog.Value)
            {
                Core.Log.Warning($"[SpineControl] Global Spine scan skipped {componentName}: {ex.GetType().Name}: {ex.Message}");
            }

            return 0;
        }
    }

    private static void AddGlobalSpineTarget(Transform transform, string componentName)
    {
        var id = transform.GetInstanceID();
        if (GlobalSpineTargets.TryGetValue(id, out var target))
        {
            target.AddComponentName(componentName);
            return;
        }

        GlobalSpineTargets[id] = new GlobalSpineTarget(transform, componentName);
    }

    private static string GetSceneName(Transform transform)
    {
        try
        {
            return transform.gameObject.scene.name;
        }
        catch
        {
            return "<unknown>";
        }
    }

    private static bool IsLargeScenarioBackgroundManagerSpine(Transform transform)
    {
        if (!IsUnityObjectAlive(transform)) return false;

        var path = GetTransformPath(transform);
        return (path.Contains("/Background Manager/Resolution Constraint/Foreground/", StringComparison.Ordinal)
                || path.Contains("/Background Manager/Resolution Constraint/Background/", StringComparison.Ordinal))
               && !path.Contains("/Limbo Of Unused Objects/", StringComparison.Ordinal)
               && !path.Contains("/Persistent Components Canvas/", StringComparison.Ordinal);
    }

    private static bool IsScenarioPortraitSpine(Transform transform)
    {
        if (!IsUnityObjectAlive(transform)) return false;

        var path = GetTransformPath(transform);
        if (!path.Contains("/GameUi/Scenario/", StringComparison.Ordinal)) return false;

        return path.Contains("/ModelAnimationParent/Stand/", StringComparison.Ordinal)
               || path.Contains("/ScreenAnimationParent/Stand/", StringComparison.Ordinal)
               || path.Contains("/ScreenAnimationParent/StandBack/", StringComparison.Ordinal)
               || path.Contains("/ModelAnimationParent/Shot/ShotContainer/", StringComparison.Ordinal);
    }

    private static bool IsSpecialScenarioTrigger(string triggerName)
    {
        if (string.IsNullOrEmpty(triggerName)) return false;

        return triggerName is "FirstCameraIn"
            or "SecondCameraIn"
            or "ThirdCameraIn"
            or "Animation_1"
            or "Animation_2"
            or "Animation_3"
            or "Animation_4"
            or "Animation_5"
            or "Animation_6"
            or "Animation_7"
            or "Animation_8"
            or "Animation_9"
            or "Animation_2_Loop"
            or "Animation_3_Loop"
            or "Animation_4_Loop";
    }

    private static void LogDebug(string message)
    {
        if (Config.SpineDebugLog.Value)
        {
            Core.Log.Msg($"[SpineControl] {message}");
        }
    }

    private static bool Approximately(Vector3 left, Vector3 right)
    {
        return (left - right).sqrMagnitude < 0.0001f;
    }

    private sealed class SpineTargetCandidate
    {
        public readonly Transform Transform;
        public readonly string Source;
        public readonly int Depth;

        public SpineTargetCandidate(Transform transform, string source, int depth)
        {
            Transform = transform;
            Source = source;
            Depth = depth;
        }
    }

    private sealed class ObservedMecanimTarget
    {
        public readonly Transform Transform;
        public readonly string Source;

        public ObservedMecanimTarget(Transform transform, string source)
        {
            Transform = transform;
            Source = source;
        }
    }

    private sealed class GlobalSpineTarget
    {
        public readonly Transform Transform;
        public string ComponentNames;

        public GlobalSpineTarget(Transform transform, string componentName)
        {
            Transform = transform;
            ComponentNames = componentName;
        }

        public void AddComponentName(string componentName)
        {
            if (ComponentNames.Contains(componentName, StringComparison.Ordinal)) return;

            ComponentNames += $",{componentName}";
        }
    }

    private sealed class AppliedTransformState
    {
        public Transform Transform;
        public bool HasLastApplied;
        public Vector3 LastAppliedPosition;
        public Vector3 LastAppliedScale;
        public float LastOffsetX;
        public float LastOffsetY;
        public float LastScale = 1f;
    }
}
