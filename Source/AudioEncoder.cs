using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LethalVoiceSpy;

public class AudioEncoder : IDisposable
{
    [DllImport("voicespy_encoder.dll", EntryPoint = "create_encoder")]
    private static extern bool ffi_CreateEncoder(out IntPtr encoder);

    [DllImport("voicespy_encoder.dll", EntryPoint = "encode")]
    private static extern bool ffi_Encode(IntPtr encoder, IntPtr samples, uint sampleCount, out IntPtr data,
        out uint dataLength);
    
    [DllImport("voicespy_encoder.dll", EntryPoint = "destroy_encoder")]
    private static extern void ffi_DestroyEncoder(IntPtr encoder);

    private readonly IntPtr encoder;
    private bool disposed;
    
    public AudioEncoder()
    {
        if (!ffi_CreateEncoder(out encoder))
            throw new IOException("Failed to create AAC encoder");
    }

    public unsafe byte[] EncodeSamples(ArraySegment<float> samples)
    {
        var sampleCount = samples.Count;

        fixed (float* p = samples.Array)
        {
            if (!ffi_Encode(encoder, (IntPtr)(p + samples.Offset), (uint)sampleCount, out var data, out var dataLength))
                throw new IOException("Failed to encode samples");

            var result = new byte[dataLength];
            Marshal.Copy(data, result, 0, (int)dataLength);

            return result;
        }
    }

    ~AudioEncoder()
    {
        Dispose();
    }
    
    public void Dispose()
    {
        if (disposed)
            return;
        
        ffi_DestroyEncoder(encoder);
        disposed = true;
    }
}