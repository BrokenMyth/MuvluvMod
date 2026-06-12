using System;
using System.Text;
using MelonLoader;
using UnityEngine.InputSystem;

[assembly: MelonInfo(typeof(MuvluvMod.Core), "MuvluvMod", "1.0.5", "Jitsu")]
[assembly: MelonGame(null, null)]

namespace MuvluvMod;

public class Core : MelonMod
{
    public static MelonLogger.Instance Log;

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to set console encoding: {ex.Message}");
        }
        Config.Initialize();
        Patch.Initialize();
        Translation.Initialize();
    }

    public override void OnUpdate()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f2Key.wasPressedThisFrame)
        {
            Config.Translation.Value = !Config.Translation.Value;
        }
        if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
        {
            Config.EnableSkipButton.Value = !Config.EnableSkipButton.Value;
        }
        if (keyboard != null && keyboard.f4Key.wasPressedThisFrame)
        {
            Config.VoiceInterruption.Value = !Config.VoiceInterruption.Value;
        }
        if (keyboard != null && keyboard.f5Key.wasPressedThisFrame)
        {
            Config.AutoSkipBattle.Value = !Config.AutoSkipBattle.Value;
        }

        SpineControl.OnUpdate();
    }

    public override void OnLateUpdate()
    {
        SpineControl.OnLateUpdate();
    }

    public override void OnApplicationQuit()
    {
        SpineControl.FlushConfig();
    }

    public override void OnDeinitializeMelon()
    {
        SpineControl.FlushConfig();
    }
}
