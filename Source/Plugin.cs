using BepInEx;
using Dissonance;
using HarmonyLib;
using UnityEngine;

namespace LethalVoiceSpy;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private const string PLUGIN_GUID = "io.daxcess.voicespy";
    private const string PLUGIN_NAME = "LethalVoiceSpy";
    private const string PLUGIN_VERSION = "1.0.0";
    
    private void Awake()
    {
        LethalVoiceSpy.Logger.SetSource(Logger);
    }
}

[HarmonyPatch]
internal static class Patches
{
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
    [HarmonyPostfix]
    private static void OnGameStart()
    {
        var dissonance = Object.FindObjectOfType<DissonanceComms>();
        
        dissonance.OnPlayerJoinedSession += OnPlayerJoinedSession;
        dissonance.OnPlayerLeftSession += OnPlayerLeftSession;
    }

    private static void OnPlayerJoinedSession(VoicePlayerState state)
    {
        Logger.LogDebug($"Player joined: {state.Tracker}");
    }

    private static void OnPlayerLeftSession(VoicePlayerState state)
    {
        Logger.LogDebug($"Player left: {state.Tracker}");
    }
}