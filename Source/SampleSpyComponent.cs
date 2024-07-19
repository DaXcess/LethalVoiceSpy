using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace LethalVoiceSpy;

public class SampleSpyComponent : MonoBehaviour
{
    public SpySession? Session { get; private set; }
    public bool HasActiveSession => Session != null;

    private float[] _temp;
    private VoiceSpy _spy;
    
    private readonly ReaderWriterLockSlim _sessionLock = new(LockRecursionPolicy.NoRecursion);
    
    public void Play(SpySession session)
    {
        if (Session != null)
            throw new IOException("Attempted to spy on a session when one is already being spied on");

        _sessionLock.EnterWriteLock();

        try
        {
            Session = session;
        }
        finally
        {
            _sessionLock.ExitWriteLock();
        }
    }

    private void Start()
    {
        _temp = new float[AudioSettings.outputSampleRate];
        _spy = GetComponent<VoiceSpy>();
    }

    private void OnEnable()
    {
        Session = null;
    }

    private void OnDisable()
    {
        Session = null;
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        try
        {
            if (!HasActiveSession)
            {
                Array.Clear(_temp, 0, _temp.Length);
                Array.Clear(data, 0, data.Length);
                return;
            }

            _sessionLock.EnterUpgradeableReadLock();
            try
            {
                if (Session is not { } session)
                {
                    Array.Clear(data, 0, data.Length);
                    Array.Clear(_temp, 0, _temp.Length);
                }
                else
                {
                    var completed = Filter(session, data, channels, _temp);
                    if (!completed) return;

                    _sessionLock.EnterWriteLock();

                    try
                    {
                        Session = null;
                    }
                    finally
                    {
                        _sessionLock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                _sessionLock.ExitUpgradeableReadLock();
            }
        }
        finally
        {
            _spy.EncodeSamples(new ArraySegment<float>(_temp, 0, data.Length / channels));
        }
    }

    private static bool Filter(SpySession session, float[] output, int channels, float[] temp)
    {
        var samples = output.Length / channels;
        var completed = session.Read(new ArraySegment<float>(temp, 0, samples));

        var sampleIdx = 0;
        for (var i = 0; i < output.Length; i += channels)
        {
            var sample = temp[sampleIdx++];

            for (var j = 0; j < channels; j++)
                output[i + j] *= sample;
        }

        return completed;
    }
}