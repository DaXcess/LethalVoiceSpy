using System;
using Dissonance.Audio.Playback;
using NAudio.Wave;
using UnityEngine;

namespace LethalVoiceSpy;

public struct SpySession
{
    private SessionContext _context;
    private IDecoderPipeline _pipeline;
    private IRemoteChannelProvider _channels;
    private DateTime _creationTime;
    private IJitterEstimator _jitter;
    private float _minimumDelay;

    public SessionContext Context => _context;
    public DateTime TargetActivationTime => _creationTime + Delay;
    public WaveFormat OutputWaveFormat => _pipeline.OutputFormat;
    public TimeSpan Delay => TimeSpan.FromSeconds(Mathf.Clamp(
        Mathf.LerpUnclamped(SpeechSession.InitialBufferDelay, _jitter.Jitter * 2.5f, _jitter.Confidence), _minimumDelay,
        0.75f));
    
    private SpySession(SessionContext context, IJitterEstimator jitter, IDecoderPipeline pipeline,
        IRemoteChannelProvider channels, DateTime now)
    {
        _context = context;
        _pipeline = pipeline;
        _channels = channels;
        _creationTime = now;
        _jitter = jitter;
        _minimumDelay = (float)(1.5 * _pipeline.InputFrameTime.TotalSeconds);
    }

    internal static SpySession Create(SessionContext context, IJitterEstimator jitter, IDecoderPipeline pipeline,
        IRemoteChannelProvider channels, DateTime now)
    {
        return new SpySession(context, jitter, pipeline, channels, now);
    }

    public void Prepare(DateTime timeOfFirstDequeueAttempt)
    {
        _pipeline.Prepare(_context);
        
        var num = Math.Max(0, (timeOfFirstDequeueAttempt - _creationTime - Delay).Ticks);
        var bufferTime = _pipeline.BufferTime;

        if (bufferTime.Ticks > Delay.Ticks * 3 + SpeechSession.FixedDelayToleranceTicks + num)
        {
            var timeSpan = TimeSpan.FromTicks(bufferTime.Ticks - Delay.Ticks);
            var totalSamples = (int)(timeSpan.TotalSeconds * OutputWaveFormat.SampleRate);

            while (totalSamples > 0)
            {
                var length = Math.Min(totalSamples, SpeechSession.DesyncFixBuffer.Length);
                if (length == 0)
                    break;

                Read(new ArraySegment<float>(SpeechSession.DesyncFixBuffer, 0, length));
                totalSamples -= length;
            }
        }
        
        _pipeline.EnableDynamicSync();
    }

    public bool Read(ArraySegment<float> samples)
    {
        return _pipeline.Read(samples);
    }
}