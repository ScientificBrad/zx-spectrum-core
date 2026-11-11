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
    private readonly short[] _audioBuffer = new short[2048];

    private readonly Bitmap _bitmap;
    private readonly System.Windows.Forms.Timer _uiTimer = new();
    private Thread? _emuThread;
    private volatile bool _running = true;

    private readonly MenuStrip _menuStrip;
    private readonly ToolStripMenuItem _fastTapeItem;
    private readonly ToolStripMenuItem _bypassCopyProtectionItem;
    private readonly ToolStripMenuItem _turbo1xItem;
    private readonly ToolStripMenuItem _turbo4xItem;
    private readonly ToolStripMenuItem _turbo10xItem;
    private readonly ToolStripMenuItem _turboMaxItem;
    private readonly ToolStripMenuItem _muteItem;

    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly ToolStripStatusLabel _tapeLabel;
    private readonly ToolStripStatusLabel _cpuLabel;
    private readonly ToolStripStatusLabel _fpsLabel;

    private int _guiFramesRendered;
    private int _cpuFramesRendered;
    private int _currentFps;
    private double _currentCpuMhz = 3.50;
    private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
    private bool _f3Down;

    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint timeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint timeEndPeriod(uint uPeriod);

    public SpectrumWindow(SpectrumMachine machine)
    {
        _machine = machine;

        Text = "RESX";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        KeyPreview = true;

        _bitmap = new Bitmap(FerrantiUla.TotalWidth, FerrantiUla.TotalHeight, PixelFormat.Format32bppArgb);

        // 1. Build MenuStrip
        _menuStrip = new MenuStrip();

        // File Menu
        var fileMenu = new ToolStripMenuItem("&File");
        fileMenu.DropDownItems.Add("&Open Tape... (F2)", null, (_, _) => OpenTapeDialog());
        fileMenu.DropDownItems.Add("&Reset (F5)", null, (_, _) =>
        {
            lock (_machine) _machine.Reset();
        });
        fileMenu.DropDownItems.Add("&Save Screenshot (F9)", null, (_, _) => SaveScreenshot());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("E&xit", null, (_, _) => Close());

        // Tape Menu
        var tapeMenu = new ToolStripMenuItem("&Tape");
        tapeMenu.DropDownItems.Add("&Quick LOAD \"\" && Play (F6)", null, (_, _) => QuickLoadTape());
        tapeMenu.DropDownItems.Add("&Play / Pause (F3)", null, (_, _) => ToggleTapePlayPause());
        tapeMenu.DropDownItems.Add("&Stop", null, (_, _) =>
        {
            lock (_machine) _machine.Tape.Stop();
        });
        tapeMenu.DropDownItems.Add("&Rewind (F4)", null, (_, _) =>
        {
            lock (_machine) _machine.Tape.Rewind();
        });
        tapeMenu.DropDownItems.Add(new ToolStripSeparator());

        _fastTapeItem = new ToolStripMenuItem("Instant Tape Loading (Turbo)") { Checked = _machine.FastTapeLoadEnabled };
        _fastTapeItem.Click += (_, _) =>
        {
            lock (_machine)
            {
                _machine.FastTapeLoadEnabled = !_machine.FastTapeLoadEnabled;
                _fastTapeItem.Checked = _machine.FastTapeLoadEnabled;
            }
        };

        _bypassCopyProtectionItem = new ToolStripMenuItem("Bypass Copy Protection") { Checked = _machine.BypassCopyProtection };
        _bypassCopyProtectionItem.Click += (_, _) =>
        {
            lock (_machine)
            {
                _machine.BypassCopyProtection = !_machine.BypassCopyProtection;
                _bypassCopyProtectionItem.Checked = _machine.BypassCopyProtection;
            }
        };

        tapeMenu.DropDownItems.Add(_fastTapeItem);
        tapeMenu.DropDownItems.Add(_bypassCopyProtectionItem);
        tapeMenu.DropDownItems.Add(new ToolStripSeparator());

        _turbo1xItem = new ToolStripMenuItem("Speed: Authentic 1×", null, (_, _) => SetTurboSpeed(TurboSpeed.Normal1x)) { Checked = true };
        _turbo4xItem = new ToolStripMenuItem("Speed: Turbo 4×", null, (_, _) => SetTurboSpeed(TurboSpeed.Turbo4x));
        _turbo10xItem = new ToolStripMenuItem("Speed: Turbo 10×", null, (_, _) => SetTurboSpeed(TurboSpeed.Turbo10x));
        _turboMaxItem = new ToolStripMenuItem("Speed: Max / Unlimited", null, (_, _) => SetTurboSpeed(TurboSpeed.Unlimited));

        tapeMenu.DropDownItems.AddRange(new ToolStripItem[] { _turbo1xItem, _turbo4xItem, _turbo10xItem, _turboMaxItem });

        // Audio Menu
        var audioMenu = new ToolStripMenuItem("&Audio");
        _muteItem = new ToolStripMenuItem("&Mute Audio (Ctrl+M)", null, (_, _) => ToggleMute());
        audioMenu.DropDownItems.Add(_muteItem);
        audioMenu.DropDownItems.Add(new ToolStripSeparator());

        var tapeVolMenu = new ToolStripMenuItem("Tape Monitor Sound");
        tapeVolMenu.DropDownItems.Add("100% Volume", null, (_, _) => _machine.Beeper.TapeVolume = 1.0f);
        tapeVolMenu.DropDownItems.Add("50% Volume (Default)", null, (_, _) => _machine.Beeper.TapeVolume = 0.5f);
        tapeVolMenu.DropDownItems.Add("25% Volume", null, (_, _) => _machine.Beeper.TapeVolume = 0.25f);
        tapeVolMenu.DropDownItems.Add("Mute Tape Audio", null, (_, _) => _machine.Beeper.TapeVolume = 0.0f);

        var beeperVolMenu = new ToolStripMenuItem("Beeper Audio");
        beeperVolMenu.DropDownItems.Add("100% Volume", null, (_, _) => _machine.Beeper.BeeperVolume = 1.0f);
        beeperVolMenu.DropDownItems.Add("50% Volume (Default)", null, (_, _) => _machine.Beeper.BeeperVolume = 0.5f);
        beeperVolMenu.DropDownItems.Add("25% Volume", null, (_, _) => _machine.Beeper.BeeperVolume = 0.25f);
        beeperVolMenu.DropDownItems.Add("Mute Beeper", null, (_, _) => _machine.Beeper.BeeperVolume = 0.0f);

        audioMenu.DropDownItems.Add(tapeVolMenu);
        audioMenu.DropDownItems.Add(beeperVolMenu);

        _menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, tapeMenu, audioMenu });
        MainMenuStrip = _menuStrip;
        Controls.Add(_menuStrip);

        // 2. Build StatusStrip
        _statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel("Ready | RESX") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _tapeLabel = new ToolStripStatusLabel("Tape: STOPPED");
        _cpuLabel = new ToolStripStatusLabel("CPU: 3.50 MHz");
        _fpsLabel = new ToolStripStatusLabel("GUI: 50 FPS");
        _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, _tapeLabel, _cpuLabel, _fpsLabel });
        Controls.Add(_statusStrip);

        // Window size fitting screen canvas, menu, and status bar
        ClientSize = new Size(FerrantiUla.TotalWidth * 2, FerrantiUla.TotalHeight * 2 + _menuStrip.Height + _statusStrip.Height);

        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        Deactivate += (_, _) =>
        {
            lock (_machine)
            {
                _machine.ReleaseAllKeys();
                _machine.KempstonJoystick = 0x00;
            }
        };

        _uiTimer.Interval = 20; // 50 Hz UI refresh
        _uiTimer.Tick += (s, e) => Invalidate();
        _uiTimer.Start();

        StartEmulationThread();
    }

    private void SetTurboSpeed(TurboSpeed speed)
    {
        lock (_machine)
        {
            _machine.TurboMode = speed;
        }
        _turbo1xItem.Checked = (speed == TurboSpeed.Normal1x);
        _turbo4xItem.Checked = (speed == TurboSpeed.Turbo4x);
        _turbo10xItem.Checked = (speed == TurboSpeed.Turbo10x);
        _turboMaxItem.Checked = (speed == TurboSpeed.Unlimited);
    }

    private void ToggleMute()
    {
        _machine.Beeper.Muted = !_machine.Beeper.Muted;
        _muteItem.Checked = _machine.Beeper.Muted;
    }

    private void ToggleTapePlayPause()
    {
        lock (_machine)
        {
            if (_machine.Tape.State == TapeState.Playing)
                _machine.Tape.Pause();
            else
                _machine.Tape.Play();
        }
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
        timeBeginPeriod(1);
        try
        {
            var frameSw = Stopwatch.StartNew();
            const double msPerFrame = 1000.0 / 50.088; // ~19.96 ms
            double nextTargetMs = frameSw.Elapsed.TotalMilliseconds;

            while (_running)
            {
                nextTargetMs += msPerFrame;

                int framesToRun = 1;
                if (_machine.Tape.State == TapeState.Playing)
                {
                    framesToRun = (int)_machine.TurboMode;
                }

                lock (_machine)
                {
                    _machine.RunFrames(framesToRun);
                }

                _cpuFramesRendered += framesToRun;
                _guiFramesRendered++;

                // Stream audio samples to waveOut
                int samples = _machine.Beeper.ReadSamples(_audioBuffer, 0, _audioBuffer.Length);
                if (samples > 0)
                {
                    _waveOut.Submit(_audioBuffer, samples);
                }

                if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
                {
                    _currentFps = _guiFramesRendered;
                    double elapsedSec = _fpsStopwatch.ElapsedMilliseconds / 1000.0;
                    _currentCpuMhz = (_cpuFramesRendered * 69888.0) / (elapsedSec * 1_000_000.0);

                    int cpuFrames = _cpuFramesRendered;
                    _guiFramesRendered = 0;
                    _cpuFramesRendered = 0;
                    _fpsStopwatch.Restart();

                    BeginInvoke(new Action(() =>
                    {
                        _fpsLabel.Text = $"GUI: {_currentFps} FPS";
                        _cpuLabel.Text = $"CPU: {_currentCpuMhz:F2} MHz ({(cpuFrames / (double)_currentFps):F1}×)";

                        _tapeLabel.Text = _machine.Tape.State switch
                        {
                            TapeState.Playing => $"Tape: PLAYING [{_machine.Tape.CurrentBlockIndex + 1}/{_machine.Tape.TotalBlocks}]",
                            TapeState.Paused => $"Tape: PAUSED [{_machine.Tape.CurrentBlockIndex + 1}/{_machine.Tape.TotalBlocks}]",
                            _ => "Tape: STOPPED"
                        };

                        if (_machine.RamDisk.Files.Count > 0)
                        {
                            _statusLabel.Text = $"Ready | RAM Disk: {_machine.RamDisk.Files.Count} files stored";
                        }
                    }));
                }

                // Frame pacing (only sleep at normal speed or when not max turbo)
                double remainingMs = nextTargetMs - frameSw.Elapsed.TotalMilliseconds;
                if (remainingMs > 1.5)
                {
                    Thread.Sleep((int)(remainingMs - 1.0));
                }
                while (frameSw.Elapsed.TotalMilliseconds < nextTargetMs)
                {
                    Thread.SpinWait(20);
                }

                // Guard against timing drift
                if (frameSw.Elapsed.TotalMilliseconds - nextTargetMs > 100)
                {
                    nextTargetMs = frameSw.Elapsed.TotalMilliseconds;
                }
            }
        }
        finally
        {
            timeEndPeriod(1);
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

        // Render scaled bitmap (640x480) below the menu bar
        int topOffset = _menuStrip.Height;
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        e.Graphics.DrawImage(_bitmap, 0, topOffset, FerrantiUla.TotalWidth * 2, FerrantiUla.TotalHeight * 2);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.M)
        {
            ToggleMute();
            e.Handled = true;
            return;
        }

        switch (e.KeyCode)
        {
            case Keys.F2:
                OpenTapeDialog();
                e.Handled = true;
                return;
            case Keys.F3:
                if (!_f3Down)
                {
                    _f3Down = true;
                    ToggleTapePlayPause();
                }
                e.Handled = true;
                return;
            case Keys.F4:
                lock (_machine) _machine.Tape.Rewind();
                e.Handled = true;
                return;
            case Keys.F5:
                lock (_machine) _machine.Reset();
                e.Handled = true;
                return;
            case Keys.F6:
                QuickLoadTape();
                e.Handled = true;
                return;
            case Keys.F9:
                SaveScreenshot();
                e.Handled = true;
                return;
        }

        lock (_machine) MapSpectrumKey(e.KeyCode, true);
        e.Handled = true;
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F3)
        {
            _f3Down = false;
            e.Handled = true;
            return;
        }

        lock (_machine) MapSpectrumKey(e.KeyCode, false);
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
            case Keys.Left: // Cursor Left / Kempston Left
                _machine.KempstonJoystick = (byte)(pressed ? (_machine.KempstonJoystick | 0x02) : (_machine.KempstonJoystick & ~0x02));
                _machine.Keyboard.SetKey(SpectrumKey.D5, pressed);
                if (Control.ModifierKeys.HasFlag(Keys.Shift)) _machine.Keyboard.SetKey(SpectrumKey.CapsShift, pressed);
                break;
            case Keys.Down: // Cursor Down / Kempston Down
                _machine.KempstonJoystick = (byte)(pressed ? (_machine.KempstonJoystick | 0x04) : (_machine.KempstonJoystick & ~0x04));
                _machine.Keyboard.SetKey(SpectrumKey.D6, pressed);
                if (Control.ModifierKeys.HasFlag(Keys.Shift)) _machine.Keyboard.SetKey(SpectrumKey.CapsShift, pressed);
                break;
            case Keys.Up: // Cursor Up / Kempston Up
                _machine.KempstonJoystick = (byte)(pressed ? (_machine.KempstonJoystick | 0x08) : (_machine.KempstonJoystick & ~0x08));
                _machine.Keyboard.SetKey(SpectrumKey.D7, pressed);
                if (Control.ModifierKeys.HasFlag(Keys.Shift)) _machine.Keyboard.SetKey(SpectrumKey.CapsShift, pressed);
                break;
            case Keys.Right: // Cursor Right / Kempston Right
                _machine.KempstonJoystick = (byte)(pressed ? (_machine.KempstonJoystick | 0x01) : (_machine.KempstonJoystick & ~0x01));
                _machine.Keyboard.SetKey(SpectrumKey.D8, pressed);
                if (Control.ModifierKeys.HasFlag(Keys.Shift)) _machine.Keyboard.SetKey(SpectrumKey.CapsShift, pressed);
                break;

            case Keys.Alt:
                _machine.KempstonJoystick = (byte)(pressed ? (_machine.KempstonJoystick | 0x10) : (_machine.KempstonJoystick & ~0x10));
                _machine.Keyboard.SetKey(SpectrumKey.D0, pressed);
                break;

            case Keys.ShiftKey:
            case Keys.LShiftKey:
            case Keys.RShiftKey:
                _machine.Keyboard.SetKey(SpectrumKey.CapsShift, pressed);
                break;

            case Keys.ControlKey:
            case Keys.LControlKey:
                _machine.Keyboard.SetKey(SpectrumKey.SymbolShift, pressed);
                break;
            case Keys.RControlKey:
                _machine.KempstonJoystick = (byte)(pressed ? (_machine.KempstonJoystick | 0x10) : (_machine.KempstonJoystick & ~0x10));
                _machine.Keyboard.SetKey(SpectrumKey.D0, pressed);
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

            case Keys.OemQuotes:
                _machine.Keyboard.SetKey(SpectrumKey.SymbolShift, pressed);
                _machine.Keyboard.SetKey(SpectrumKey.P, pressed);
                break;
        }
    }

    private void QuickLoadTape()
    {
        lock (_machine)
        {
            if (_machine.Tape.CurrentTape == null)
            {
                OpenTapeDialog(autoQuickLoad: true);
                return;
            }
            _machine.QuickLoad();
            _statusLabel.Text = "Quick LOAD \"\" & Play initiated...";
        }
    }

    private void OpenTapeDialog(bool autoQuickLoad = false)
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
                lock (_machine)
                {
                    _machine.Tape.InsertTape(tap);
                    if (autoQuickLoad)
                    {
                        _machine.QuickLoad();
                    }
                }
                _statusLabel.Text = autoQuickLoad
                    ? $"Inserted {Path.GetFileName(ofd.FileName)} & Quick Loading..."
                    : $"Inserted tape: {Path.GetFileName(ofd.FileName)} ({tap.Blocks.Count} blocks)";
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
