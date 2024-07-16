using System.IO;
using BepInEx;
using Dissonance.Audio;
using GameNetcodeStuff;
using NAudio.Wave;
using UnityEngine;

namespace LethalVoiceSpy;

public class VoiceSpy : MonoBehaviour
{
    private AudioFileWriter writer;
    
    private void Awake()
    {
        var directory = Path.Combine(Paths.GameRootPath, "VoiceSpy");

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        
        var playerController = GetComponent<PlayerControllerB>();
        var filename = $"Voice_{playerController.playerUsername}_{playerController.actualClientId}.wav";
        var path = Path.Combine(directory, filename);
        
        // TODO: Figure out Wave Format
        writer = new AudioFileWriter(path, new WaveFormat(441000, 2));
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        writer.WriteSamples(data);
    }

    private void OnDestroy()
    {
        writer.Dispose();
    }
}