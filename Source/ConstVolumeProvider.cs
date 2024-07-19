using Dissonance.Audio.Playback;

namespace LethalVoiceSpy;

public class ConstVolumeProvider(float volume) : IVolumeProvider
{
    public float TargetVolume => volume;
}