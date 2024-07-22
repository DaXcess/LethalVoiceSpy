using System;
using System.IO;
using BepInEx;
using Dissonance.Audio.Codecs;
using Dissonance.Audio.Playback;
using Dissonance.Networking;
using NAudio.Wave;
using UnityEngine;

namespace LethalVoiceSpy;

public class VoiceSpy : MonoBehaviour
{
    public string PlayerUsername { get; set; }
    public string PlayerName => _playback.PlayerName;

    private readonly SpySessionStream _sessions;
    private SampleSpyComponent _player;
    private VoicePlayback _playback;
    private AudioSource _audioSource;
    
    private AudioEncoder _encoder;
    private FileStream _output;

    private DateTime _lastEncodeTime = DateTime.UtcNow;

    private readonly float[] SILENCE = new float[960]; 
    
    public VoiceSpy()
    {
        _sessions = new SpySessionStream(this);
    }
    
    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _player = GetComponent<SampleSpyComponent>();
        _playback = GetComponentInParent<VoicePlayback>();
    }

    private void Start()
    {
        var directory = Path.Combine(Paths.GameRootPath, "VoiceSpy");

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var time = DateTime.Now.ToString("yy-MM-dd_HH_mm_ss");
        var path = Path.Combine(directory, $"Voice_{PlayerUsername}_{time}.aac");

        _encoder = new AudioEncoder();
        _output = File.Create(path);
    }

    private void OnEnable()
    {
        if (_audioSource.spatialize)
            _audioSource.spatialize = false;

        _audioSource.clip = AudioClip.Create("Flatline", 4096, 1, AudioSettings.outputSampleRate, false,
            buf => Array.Fill(buf, 1));
        _audioSource.loop = true;
        _audioSource.pitch = 1;
        _audioSource.dopplerLevel = 0;
        _audioSource.mute = false;
        _audioSource.priority = 0;
        _audioSource.spatialBlend = 0;
        
        // TODO: Make this 0 once testing concludes
        _audioSource.volume = 0;
    }

    private void OnDisable()
    {
        _sessions.StopSession();
    }

    private void Update()
    {
        if (!_player.HasActiveSession)
        {
            var session = _sessions.TryDequeueSession();
            if (session != null)
            {
                _player.Play(session.Value);
                _audioSource.Play();
            }
        }
        
        if (_audioSource.mute)
            _audioSource.mute = false;
    }

    public void StartCapture()
    {
        // This frame format determines the *incoming* audio format (which is always Opus, 48kHz, Mono with a frame size of 960 frames)
        _sessions.StartSession(new FrameFormat(Codec.Opus, new WaveFormat(48000, 1), 960));
    }

    public void StopCapture()
    {
        _sessions.StopSession();
    }

    public void ReceiveAudioPacket(VoicePacket packet)
    {
        _sessions.ReceiveFrame(packet);
    }
    
    public void EncodeSamples(ArraySegment<float> samples)
    {
        var lastTime = (DateTime.UtcNow - _lastEncodeTime).TotalMilliseconds;
        
        if (lastTime > 40)
        {
            var chunks = (int)Math.Floor(lastTime / 20) - 1;

            Logger.LogWarning(
                $"Lag spike detected, encoding {chunks} chunk{(chunks == 1 ? "" : "s")} of silence to compensate");

            for (var i = 0; i < chunks; i++)
            {
                var silence = _encoder.EncodeSamples(SILENCE);
                _output.Write(silence, 0, silence.Length);
            }
        }
        
        var data = _encoder.EncodeSamples(samples);
        _output.Write(data, 0, data.Length);
        
        _lastEncodeTime = DateTime.UtcNow;
    }

    public void ForceReset()
    {
        _sessions.ForceReset();
    }

    private void OnDestroy()
    {
        _encoder.Dispose();
        _output.Close();
    }
}