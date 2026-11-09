using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Spectrum.Core;
using Spectrum.Diagnostics;

namespace Spectrum.Emulator;

public sealed class SpectrumWindow : Form
{
    private readonly SpectrumMachine _machine;
    private readonly WindowsWaveOut _waveOut = new();
    private readonly short[] _audioBuffer = new short[1100];

    private readonly Bitmap _bitmap;
    private readonly System.Windows.Forms.Timer _uiTimer = new();
    private Thread? _emuThread;
    private volatile bool _running = true;

    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly ToolStripStatusLabel _tapeLabel;
    private readonly ToolStripStatusLabel _fpsLabel;

    private int _framesRendered;
    private int _currentFps;
    private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();

    public SpectrumWindow(SpectrumMachine machine)
    {
        _machine = machine;

        Text = "Sinclair ZX Spectrum 48K - REA Project 004 (ScientificBrad)";
        ClientSize = new Size(FerrantiUla.TotalWidth * 2, FerrantiUla.TotalHeight * 2 + 24);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        KeyPreview = true;

        _bitmap = new Bitmap(FerrantiUla.TotalWidth, FerrantiUla.TotalHeight, PixelFormat.Format32bppArgb);

        _statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel("Ready | 3.50 MHz Z80A") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _tapeLabel = new ToolStripStatusLabel("Tape: Stopped");
        _fpsLabel = new ToolStripStatusLabel("50 FPS");
        _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, _tapeLabel, _fpsLabel });
        Controls.Add(_statusStrip);

        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;

        _uiTimer.Interval = 20; // 50 Hz UI refresh
        _uiTimer.Tick += (s, e) => Invalidate();
        _uiTimer.Start();

        StartEmulationThread();
    }

    private void StartEmulationThread()
    {
        _emuThread = new Thread(EmulationLoop)
        {
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
            Name = "SpectrumEmuThread"
        };
        _emuThread.Start();
    }

    private void EmulationLoop()
    {
        var frameSw = Stopwatch.StartNew();
        const double msPerFrame = 1000.0 / 50.088; // ~19.96 ms

        while (_running)
        {
            double startMs = frameSw.Elapsed.TotalMilliseconds;

            // Execute one full 50 Hz frame (69,888 T-states)
            int frameTargetTStates = FerrantiUla.TStatesPerFrame;
            int currentFrameT = _machine.Bus.FrameTStates;
            while (_machine.Bus.FrameTStates >= currentFrameT && _machine.Bus.FrameTStates < frameTargetTStates)
            {
                _machine.Step();
            }

            // Stream audio samples to waveOut
            int samples = _machine.Beeper.ReadSamples(_audioBuffer, 0, _audioBuffer.Length);
            if (samples > 0)
            {
                _waveOut.Submit(_audioBuffer, samples);
            }

            _framesRendered++;
            if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
            {
                _currentFps = _framesRendered;
                _framesRendered = 0;
                _fpsStopwatch.Restart();

                BeginInvoke(new Action(() =>
                {
                    _fpsLabel.Text = $"{_currentFps} FPS";
                    _tapeLabel.Text = _machine.Tape.State switch
                    {
                        TapeState.Playing => $"Tape: PLAYING [{_machine.Tape.CurrentBlockIndex + 1}/{_machine.Tape.TotalBlocks}]",
                        TapeState.Paused => $"Tape: PAUSED [{_machine.Tape.CurrentBlockIndex + 1}/{_machine.Tape.TotalBlocks}]",
                        _ => "Tape: STOPPED"
                    };
                }));
            }

            // High-resolution frame pacing
            double elapsedMs = frameSw.Elapsed.TotalMilliseconds - startMs;
            int sleepMs = (int)(msPerFrame - elapsedMs);
            if (sleepMs > 1)
            {
                Thread.Sleep(sleepMs);
            }
            while (frameSw.Elapsed.TotalMilliseconds - startMs < msPerFrame)
            {
                Thread.SpinWait(50);
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        // Blit ULA Framebuffer to Bitmap
        var bmpData = _bitmap.LockBits(
            new Rectangle(0, 0, FerrantiUla.TotalWidth, FerrantiUla.TotalHeight),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);

        Marshal.Copy((int[])(object)_machine.Ula.Framebuffer, 0, bmpData.Scan0, _machine.Ula.Framebuffer.Length);
        _bitmap.UnlockBits(bmpData);

        // Render scaled bitmap (640x480)
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        e.Graphics.DrawImage(_bitmap, 0, 0, FerrantiUla.TotalWidth * 2, FerrantiUla.TotalHeight * 2);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            // System hotkeys
            case Keys.F2:
                OpenTapeDialog();
                e.Handled = true;
                return;
            case Keys.F3:
                if (_machine.Tape.State == TapeState.Playing) _machine.Tape.Pause();
                else _machine.Tape.Play();
                e.Handled = true;
                return;
            case Keys.F4:
                _machine.Tape.Rewind();
                e.Handled = true;
                return;
            case Keys.F5:
                _machine.Reset();
                e.Handled = true;
                return;
            case Keys.F9:
                SaveScreenshot();
                e.Handled = true;
                return;
        }

        MapSpectrumKey(e.KeyCode, true);
        e.Handled = true;
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        MapSpectrumKey(e.KeyCode, false);
        e.Handled = true;
    }

    private void MapSpectrumKey(Keys key, bool pressed)
    {
        switch (key)
        {
            case Keys.Back: // Delete = Caps Shift + 0
                _machine.Keyboard.SetKey(SpectrumKey.CapsShift, pressed);
                _machine.Keyboard.SetKey(SpectrumKey.D0, pressed);
                break;
            case Keys.Left: // Cursor Left = Caps Shift + 5
                _machine.Keyboard.SetKey(SpectrumKey.CapsShift, pressed);
                _machine.Keyboard.SetKey(SpectrumKey.D5, pressed);
                break;
            case Keys.Down: // Cursor Down = Caps Shift + 6
                _machine.Keyboard.SetKey(SpectrumKey.CapsShift, pressed);
                _machine.Keyboard.SetKey(SpectrumKey.D6, pressed);
                break;
            case Keys.Up: // Cursor Up = Caps Shift + 7
                _machine.Keyboard.SetKey(SpectrumKey.CapsShift, pressed);
                _machine.Keyboard.SetKey(SpectrumKey.D7, pressed);
                break;
            case Keys.Right: // Cursor Right = Caps Shift + 8
                _machine.Keyboard.SetKey(SpectrumKey.CapsShift, pressed);
                _machine.Keyboard.SetKey(SpectrumKey.D8, pressed);
                break;

            case Keys.ShiftKey:
            case Keys.LShiftKey:
            case Keys.RShiftKey:
                _machine.Keyboard.SetKey(SpectrumKey.CapsShift, pressed);
                break;

            case Keys.ControlKey:
            case Keys.LControlKey:
            case Keys.RControlKey:
                _machine.Keyboard.SetKey(SpectrumKey.SymbolShift, pressed);
                break;

            case Keys.Enter: _machine.Keyboard.SetKey(SpectrumKey.Enter, pressed); break;
            case Keys.Space: _machine.Keyboard.SetKey(SpectrumKey.Space, pressed); break;

            case Keys.A: _machine.Keyboard.SetKey(SpectrumKey.A, pressed); break;
            case Keys.B: _machine.Keyboard.SetKey(SpectrumKey.B, pressed); break;
            case Keys.C: _machine.Keyboard.SetKey(SpectrumKey.C, pressed); break;
            case Keys.D: _machine.Keyboard.SetKey(SpectrumKey.D, pressed); break;
            case Keys.E: _machine.Keyboard.SetKey(SpectrumKey.E, pressed); break;
            case Keys.F: _machine.Keyboard.SetKey(SpectrumKey.F, pressed); break;
            case Keys.G: _machine.Keyboard.SetKey(SpectrumKey.G, pressed); break;
            case Keys.H: _machine.Keyboard.SetKey(SpectrumKey.H, pressed); break;
            case Keys.I: _machine.Keyboard.SetKey(SpectrumKey.I, pressed); break;
            case Keys.J: _machine.Keyboard.SetKey(SpectrumKey.J, pressed); break;
            case Keys.K: _machine.Keyboard.SetKey(SpectrumKey.K, pressed); break;
            case Keys.L: _machine.Keyboard.SetKey(SpectrumKey.L, pressed); break;
            case Keys.M: _machine.Keyboard.SetKey(SpectrumKey.M, pressed); break;
            case Keys.N: _machine.Keyboard.SetKey(SpectrumKey.N, pressed); break;
            case Keys.O: _machine.Keyboard.SetKey(SpectrumKey.O, pressed); break;
            case Keys.P: _machine.Keyboard.SetKey(SpectrumKey.P, pressed); break;
            case Keys.Q: _machine.Keyboard.SetKey(SpectrumKey.Q, pressed); break;
            case Keys.R: _machine.Keyboard.SetKey(SpectrumKey.R, pressed); break;
            case Keys.S: _machine.Keyboard.SetKey(SpectrumKey.S, pressed); break;
            case Keys.T: _machine.Keyboard.SetKey(SpectrumKey.T, pressed); break;
            case Keys.U: _machine.Keyboard.SetKey(SpectrumKey.U, pressed); break;
            case Keys.V: _machine.Keyboard.SetKey(SpectrumKey.V, pressed); break;
            case Keys.W: _machine.Keyboard.SetKey(SpectrumKey.W, pressed); break;
            case Keys.X: _machine.Keyboard.SetKey(SpectrumKey.X, pressed); break;
            case Keys.Y: _machine.Keyboard.SetKey(SpectrumKey.Y, pressed); break;
            case Keys.Z: _machine.Keyboard.SetKey(SpectrumKey.Z, pressed); break;

            case Keys.D0: case Keys.NumPad0: _machine.Keyboard.SetKey(SpectrumKey.D0, pressed); break;
            case Keys.D1: case Keys.NumPad1: _machine.Keyboard.SetKey(SpectrumKey.D1, pressed); break;
            case Keys.D2: case Keys.NumPad2: _machine.Keyboard.SetKey(SpectrumKey.D2, pressed); break;
            case Keys.D3: case Keys.NumPad3: _machine.Keyboard.SetKey(SpectrumKey.D3, pressed); break;
            case Keys.D4: case Keys.NumPad4: _machine.Keyboard.SetKey(SpectrumKey.D4, pressed); break;
            case Keys.D5: case Keys.NumPad5: _machine.Keyboard.SetKey(SpectrumKey.D5, pressed); break;
            case Keys.D6: case Keys.NumPad6: _machine.Keyboard.SetKey(SpectrumKey.D6, pressed); break;
            case Keys.D7: case Keys.NumPad7: _machine.Keyboard.SetKey(SpectrumKey.D7, pressed); break;
            case Keys.D8: case Keys.NumPad8: _machine.Keyboard.SetKey(SpectrumKey.D8, pressed); break;
            case Keys.D9: case Keys.NumPad9: _machine.Keyboard.SetKey(SpectrumKey.D9, pressed); break;
        }
    }

    private void OpenTapeDialog()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Spectrum Tape Files (*.tap)|*.tap|All Files (*.*)|*.*",
            Title = "Select ZX Spectrum Tape Image"
        };
        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                var tap = TapFile.Load(ofd.FileName);
                _machine.Tape.InsertTape(tap);
                _statusLabel.Text = $"Inserted tape: {Path.GetFileName(ofd.FileName)} ({tap.Blocks.Count} blocks)";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to load tape: {ex.Message}", "Tape Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void SaveScreenshot()
    {
        string filename = $"screenshots/spectrum_{DateTime.Now:yyyyMMdd_HHmmss}.bmp";
        BmpWriter.SaveBmp(filename, _machine.Ula.Framebuffer, FerrantiUla.TotalWidth, FerrantiUla.TotalHeight);
        _statusLabel.Text = $"Screenshot saved to {filename}";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        _running = false;
        _uiTimer.Stop();
        _emuThread?.Join(500);
        _waveOut.Dispose();
        _bitmap.Dispose();
    }
}
