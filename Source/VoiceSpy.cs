using System;
using System.IO;
using BepInEx;
using GameNetcodeStuff;
using UnityEngine;

namespace LethalVoiceSpy;

public class VoiceSpy : MonoBehaviour
{
    internal PlayerControllerB player;

    private AudioEncoder encoder;
    private FileStream file;
    
    private void Start()
    {
        var directory = Path.Combine(Paths.GameRootPath, "VoiceSpy");

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var time = DateTime.Now.ToString("yy-MM-dd_hh_mm_ss");
        var path = Path.Combine(directory, $"Voice_{player.playerUsername}_{time}.aac");
        
        encoder = new AudioEncoder();
        file = File.Create(path);
    }

    public void ProcessAudioPacket(float[] samples)
    {
        var data = encoder.EncodeSamples(samples);

        file.Write(data, 0, data.Length);
    }

    private void OnDestroy()
    {
        encoder.Dispose();
        file.Close();
    }
}