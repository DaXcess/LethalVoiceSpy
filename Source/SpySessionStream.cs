using System;
using System.Collections.Generic;
using System.IO;
using Dissonance.Audio.Playback;
using Dissonance.Datastructures;
using Dissonance.Networking;

namespace LethalVoiceSpy;

public class SpySessionStream(VoiceSpy spy) : IJitterEstimator
{
    private readonly WindowDeviationCalculator _arrivalJitterMeter = new WindowDeviationCalculator(128);
    private readonly Queue<SpySession> _awaitingActivation = new();
    private DecoderPipeline _active;
    private DateTime? _queueHeadFirstDequeueAttempt;
    private uint _currentId;
    
    public float Jitter => _arrivalJitterMeter.StdDev;
    public float Confidence => _arrivalJitterMeter.Confidence;

    public void StartSession(FrameFormat format, DateTime? now = null, IJitterEstimator jitter = null)
    {
        if (spy.PlayerName == null)
            throw new IOException("Attempted to `StartSession` but `PlayerName` is null");
        
        _active = DecoderPipelinePool.GetDecoderPipeline(format, new ConstVolumeProvider(1));

        var session = SpySession.Create(new SessionContext(spy.PlayerName, _currentId), jitter ?? this, _active, _active,
            now ?? DateTime.UtcNow);

        _currentId++;
        _awaitingActivation.Enqueue(session);
    }

    public SpySession? TryDequeueSession(DateTime? now = null)
    {
        var time = now ?? DateTime.UtcNow;

        if (_awaitingActivation.Count < 1)
            return null;

        _queueHeadFirstDequeueAttempt ??= time;
        
        var session = _awaitingActivation.Peek();
        if (session.TargetActivationTime >= time) 
            return null;
        
        session.Prepare(_queueHeadFirstDequeueAttempt.Value);
        _awaitingActivation.Dequeue();
        _queueHeadFirstDequeueAttempt = null;

        return session;
    }

    public void ReceiveFrame(VoicePacket packet, DateTime? now = null)
    {
        if (_active == null)
            return;

        var jitter = _active.Push(packet, now ?? DateTime.UtcNow);
        _arrivalJitterMeter.Update(jitter);
    }

    public void ForceReset()
    {
        _active?.Reset();

        while (_awaitingActivation.Count > 1)
            _awaitingActivation.Dequeue();

        if (_active == null && _awaitingActivation.Count > 0)
            _awaitingActivation.Dequeue();
        
        _arrivalJitterMeter.Clear();
    }

    public void StopSession()
    {
        if (_active == null)
            return;
        
        _active.Stop();
        _active = null;
    }
}