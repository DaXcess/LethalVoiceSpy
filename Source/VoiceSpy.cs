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

        var time = DateTime.Now.ToString("yy-MM-dd_HH_mm_ss.fff");
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

    private double _lastEncodeTime;
    private double _totalTime;
    private double _totalSamples;
    
    public void EncodeSamples(ArraySegment<float> samples)
    {
        // Ignore first tick
        if (_lastEncodeTime == 0)
            _lastEncodeTime = Time.realtimeSinceStartupAsDouble;

        var diff = (Time.realtimeSinceStartupAsDouble - _lastEncodeTime);
        _totalTime += diff;

        if (_totalTime > 0.1)
        {
            var compensateSamples = 48000 * _totalTime - _totalSamples;

            if (compensateSamples > 1024)
            {
                Logger.LogWarning($"Lag spike detected, compensating with {compensateSamples} samples of silence");
                
                var iterations = Math.Max(0, (compensateSamples / 1024) - 2);
                var buffer = new float[1024];
                
                for (var i = 0; i < iterations; i++)
                {
                    var silence = _encoder.EncodeSamples(buffer);
                    _output.Write(silence, 0, silence.Length);
                }
            }
            
            _totalSamples = 0;
            _totalTime = 0;
        }
        
        _lastEncodeTime = Time.realtimeSinceStartupAsDouble;
        _totalSamples += samples.Count;
        
        var data = _encoder.EncodeSamples(samples);
        _output.Write(data, 0, data.Length);
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