using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using Dissonance;
using Dissonance.Audio.Playback;
using Dissonance.Integrations.Unity_NFGO;
using Dissonance.Networking;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

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

        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
    }
}

[HarmonyPatch]
internal static class Patches
{
    private static Dictionary<string, VoiceSpy> activeSpies = [];
    
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
    [HarmonyPostfix]
    private static void OnGameStart()
    {
        var dissonance = Object.FindObjectOfType<DissonanceComms>();
        
        dissonance.OnPlayerJoinedSession += OnPlayerJoinedSession;
        dissonance.OnPlayerLeftSession += OnPlayerLeftSession;
    }

    [HarmonyPatch(typeof(SpeechSessionStream), nameof(SpeechSessionStream.ReceiveFrame))]
    [HarmonyPrefix]
    private static void ReceiveAudioPacket(SpeechSessionStream __instance, ref VoicePacket packet)
    {
        if (!activeSpies.TryGetValue(__instance.PlayerName, out var spy))
            return;
        
        spy.ReceiveAudioPacket(packet);
    }

    [HarmonyPatch(typeof(SpeechSessionStream), nameof(SpeechSessionStream.StartSession))]
    [HarmonyPostfix]
    private static void OnStartSpeaking(SpeechSessionStream __instance)
    {
        if (!activeSpies.TryGetValue(__instance.PlayerName, out var spy))
            return;
        
        spy.StartCapture();
    }

    [HarmonyPatch(typeof(SpeechSessionStream), nameof(SpeechSessionStream.StopSession))]
    [HarmonyPostfix]
    private static void OnStopSpeaking(SpeechSessionStream __instance)
    {
        if (!activeSpies.TryGetValue(__instance.PlayerName, out var spy))
            return;
        
        spy.StopCapture();
    }

    // [HarmonyPatch(typeof(SpeechSession), nameof(SpeechSession.Read))]
    // [HarmonyPostfix]
    // private static void OnVoicePacketProcessed(ref SpeechSession __instance, ref ArraySegment<float> samples)
    // {
    //     if (samples.Array == SpeechSession.DesyncFixBuffer)
    //         return;
    //     
    //     if (!activeSpies.TryGetValue(__instance.Context.PlayerName, out var spy))
    //         return;
    //
    //     spy.ProcessAudioPacket(samples.Array);
    // }

    private static void OnPlayerJoinedSession(VoicePlayerState state)
    {
        if (state.PlaybackInternal is not VoicePlayback playback)
            return;
        
        if (state.Tracker is not NfgoPlayer nfgoPlayer)
            return;

        var spyObject = new GameObject($"{playback.PlayerName} Voice Spy")
        {
            transform =
            {
                parent = playback.transform,
                localPosition = Vector3.zero
            }
        };

        spyObject.AddComponent<AudioSource>();
        spyObject.AddComponent<SampleSpyComponent>();
        
        var spy = spyObject.AddComponent<VoiceSpy>();
        
        spy.PlayerUsername = nfgoPlayer.GetComponent<PlayerControllerB>().playerUsername;
        
        activeSpies.Add(state.Name, spy);
    }

    private static void OnPlayerLeftSession(VoicePlayerState state)
    {
        if (!activeSpies.Remove(state.Name, out var spy))
            return;

        Object.Destroy(spy.gameObject);
    }
}