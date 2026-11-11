using System.Runtime.InteropServices;

namespace Spectrum.Emulator;

/// <summary>
/// Native Windows WinMM waveOut audio playback for 44.1 kHz 16-bit mono PCM.
/// Provides real-time, low-latency audio buffering for the Sinclair 1-bit beeper.
/// </summary>
internal sealed class WindowsWaveOut : IDisposable
{
    private const int Slots = 16;
    private const int MaxSamples = 2048;
    private const uint WhdrDone = 1;
    private static readonly int HeaderSize = Marshal.SizeOf<WaveHeader>();

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormat
    {
        public ushort FormatTag;
        public ushort Channels;
        public uint SamplesPerSec;
        public uint AvgBytesPerSec;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort ExtraSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr Data;
        public uint BufferLength;
        public uint BytesRecorded;
        public IntPtr User;
        public uint Flags;
        public uint Loops;
        public IntPtr Next;
        public IntPtr Reserved;
    }

    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint waveOutOpen(out IntPtr handle, uint deviceId, ref WaveFormat format,
        IntPtr callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint waveOutPrepareHeader(IntPtr h, IntPtr header, uint size);
    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint waveOutUnprepareHeader(IntPtr h, IntPtr header, uint size);
    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint waveOutWrite(IntPtr h, IntPtr header, uint size);
    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint waveOutReset(IntPtr h);
    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint waveOutClose(IntPtr h);

    private readonly IntPtr[] _buffers = new IntPtr[Slots];
    private readonly IntPtr[] _headers = new IntPtr[Slots];
    private readonly bool[] _prepared = new bool[Slots];
    private IntPtr _handle;
    private int _next;
    private bool _disposed;

    public WindowsWaveOut()
    {
        var format = new WaveFormat
        {
            FormatTag = 1,
            Channels = 1,
            SamplesPerSec = 48000,
            AvgBytesPerSec = 48000 * 2,
            BlockAlign = 2,
            BitsPerSample = 16,
            ExtraSize = 0
        };

        uint rc = waveOutOpen(out _handle, unchecked((uint)-1), ref format, IntPtr.Zero, IntPtr.Zero, 0);
        if (rc != 0)
        {
            _handle = IntPtr.Zero;
            return;
        }

        for (int i = 0; i < Slots; i++)
        {
            _buffers[i] = Marshal.AllocHGlobal(MaxSamples * sizeof(short));
            _headers[i] = Marshal.AllocHGlobal(HeaderSize);
        }
    }

    public void Submit(short[] samples, int count)
    {
        if (_handle == IntPtr.Zero || count <= 0) return;
        count = Math.Min(count, MaxSamples);

        int slot = _next;
        IntPtr hdr = _headers[slot];

        if (_prepared[slot])
        {
            var existing = Marshal.PtrToStructure<WaveHeader>(hdr);
            if ((existing.Flags & WhdrDone) == 0)
            {
                return; // Slot still busy
            }
            waveOutUnprepareHeader(_handle, hdr, (uint)HeaderSize);
            _prepared[slot] = false;
        }

        Marshal.Copy(samples, 0, _buffers[slot], count);

        var header = new WaveHeader
        {
            Data = _buffers[slot],
            BufferLength = (uint)(count * sizeof(short)),
            Flags = 0
        };
        Marshal.StructureToPtr(header, hdr, false);

        if (waveOutPrepareHeader(_handle, hdr, (uint)HeaderSize) == 0)
        {
            _prepared[slot] = true;
            waveOutWrite(_handle, hdr, (uint)HeaderSize);
            _next = (slot + 1) % Slots;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            waveOutReset(_handle);
            for (int i = 0; i < Slots; i++)
            {
                if (_prepared[i])
                {
                    waveOutUnprepareHeader(_handle, _headers[i], (uint)HeaderSize);
                    _prepared[i] = false;
                }
                if (_buffers[i] != IntPtr.Zero) Marshal.FreeHGlobal(_buffers[i]);
                if (_headers[i] != IntPtr.Zero) Marshal.FreeHGlobal(_headers[i]);
            }
            waveOutClose(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
