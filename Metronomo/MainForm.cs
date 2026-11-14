using System.Drawing.Drawing2D;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Metronomo;

public sealed class MainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(28, 30, 36);
    private static readonly Color Panel = Color.FromArgb(40, 43, 51);
    private static readonly Color Fg = Color.FromArgb(230, 232, 238);
    private static readonly Color Muted = Color.FromArgb(150, 155, 168);
    private static readonly Color AccentCol = Color.FromArgb(255, 140, 66);
    private static readonly Color BeatCol = Color.FromArgb(90, 170, 255);

    private static readonly (string Name, int Value)[] Subdivisions =
    {
        ("Negras (1)", 1), ("Corcheas (2)", 2), ("Tresillos (3)", 3), ("Semicorcheas (4)", 4),
        ("Quintillos (5)", 5), ("Seisillos (6)", 6), ("Septillos (7)", 7), ("Fusas (8)", 8),
    };

    private readonly MetronomeEngine _engine;
    private WasapiOut? _output;

    private readonly TextBox _bpmBox = new();
    private readonly TrackBar _bpmTrack = new();
    private readonly NumericUpDown _beatsUpDown = new();
    private readonly ComboBox _subCombo = new();
    private readonly TrackBar _volTrack = new();
    private readonly TrackBar _subVolTrack = new();
    private readonly CheckBox _accentCheck = new();
    private readonly Button _playButton = new();
    private readonly BeatIndicator _indicator = new();
    private readonly System.Windows.Forms.Timer _uiTimer = new() { Interval = 15 };

    public MainForm()
    {
        Text = "Metrónomo";
        BackColor = Bg;
        ForeColor = Fg;
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        // Formato nativo del dispositivo → sin remuestreo, mínima carga de CPU.
        int sampleRate = 48000, channels = 2;
        try
        {
            using var en = new MMDeviceEnumerator();
            var mix = en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).AudioClient.MixFormat;
            sampleRate = mix.SampleRate;
            channels = mix.Channels;
        }
        catch { /* se usan valores por defecto */ }
        _engine = new MetronomeEngine(sampleRate, channels);

        BuildUi();
        ActiveControl = _playButton; // que el campo de tempo no empiece seleccionado
        _uiTimer.Tick += (_, _) => UpdateIndicator();
        _uiTimer.Start();
    }

    private void BuildUi()
    {
        // Diseño en rejilla con filas de alto automático: el tamaño lo dicta el texto,
        // así nada se recorta aunque cambie la fuente o la escala de Windows.
        var root = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16),
            Location = Point.Empty,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));

        void AddRow(Control left, Control? right = null, int topGap = 0)
        {
            int row = root.RowCount++;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.Margin = new Padding(4, 4 + topGap, 4, 4);
            root.Controls.Add(left, 0, row);
            if (right == null) root.SetColumnSpan(left, 2);
            else
            {
                right.Margin = new Padding(4, 4 + topGap, 4, 4);
                root.Controls.Add(right, 1, row);
            }
        }

        // --- Tempo: [−]  120  [+] ---
        var tempoRow = new TableLayoutPanel
        {
            ColumnCount = 3, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        tempoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        tempoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tempoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        tempoRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var minus = MakeButton("−", (_, _) => SetBpm(_engine.Bpm - 1));
        var plus = MakeButton("+", (_, _) => SetBpm(_engine.Bpm + 1));
        minus.Size = plus.Size = new Size(60, 50);
        minus.Anchor = plus.Anchor = AnchorStyles.None;

        // Campo de tempo editable: clic, escribir el número y Enter (Esc cancela).
        _bpmBox.Text = "120";
        _bpmBox.Font = new Font("Segoe UI Semibold", 44f);
        _bpmBox.BorderStyle = BorderStyle.None;
        _bpmBox.BackColor = Bg;
        _bpmBox.ForeColor = Fg;
        _bpmBox.TextAlign = HorizontalAlignment.Center;
        _bpmBox.MaxLength = 3;
        _bpmBox.Width = 170;
        _bpmBox.Anchor = AnchorStyles.None;
        _bpmBox.KeyPress += (_, e) => { if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar)) e.Handled = true; };
        _bpmBox.Enter += (_, _) => { _bpmBox.BackColor = Panel; BeginInvoke(_bpmBox.SelectAll); };
        _bpmBox.MouseUp += (_, _) => { if (_bpmBox.SelectionLength == 0 && _bpmBox.Text.Length > 0 && _justFocused) _bpmBox.SelectAll(); _justFocused = false; };
        _bpmBox.GotFocus += (_, _) => _justFocused = true;
        _bpmBox.Leave += (_, _) => { CommitBpmText(); _bpmBox.BackColor = Bg; };
        new ToolTip().SetToolTip(_bpmBox, "Haz clic y escribe el tempo (Enter para aplicar)");
        tempoRow.Controls.Add(minus, 0, 0);
        tempoRow.Controls.Add(_bpmBox, 1, 0);
        tempoRow.Controls.Add(plus, 2, 0);
        AddRow(tempoRow);

        var bpmCaption = MakeLabel("BPM");
        bpmCaption.Anchor = AnchorStyles.None;
        AddRow(bpmCaption);

        _bpmTrack.Minimum = 20;
        _bpmTrack.Maximum = 400;
        _bpmTrack.TickStyle = TickStyle.None;
        _bpmTrack.Value = 120;
        _bpmTrack.Dock = DockStyle.Fill;
        _bpmTrack.ValueChanged += (_, _) => SetBpm(_bpmTrack.Value);
        AddRow(_bpmTrack);

        // --- Compás y subdivisión ---
        AddRow(MakeLabel("Tiempos por compás"), MakeLabel("Subdivisión"), topGap: 10);

        _beatsUpDown.Minimum = 1;
        _beatsUpDown.Maximum = 16;
        _beatsUpDown.Value = 4;
        _beatsUpDown.BackColor = Panel;
        _beatsUpDown.ForeColor = Fg;
        _beatsUpDown.Dock = DockStyle.Fill;
        _beatsUpDown.ValueChanged += (_, _) =>
        {
            _engine.BeatsPerBar = (int)_beatsUpDown.Value;
            _indicator.Beats = _engine.BeatsPerBar;
        };

        _subCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _subCombo.FlatStyle = FlatStyle.Flat;
        _subCombo.BackColor = Panel;
        _subCombo.ForeColor = Fg;
        _subCombo.Dock = DockStyle.Fill;
        foreach (var s in Subdivisions) _subCombo.Items.Add(s.Name);
        _subCombo.SelectedIndex = 0;
        _subCombo.SelectedIndexChanged += (_, _) =>
        {
            _engine.Subdivision = Subdivisions[_subCombo.SelectedIndex].Value;
            _indicator.Subdivision = _engine.Subdivision;
        };
        AddRow(_beatsUpDown, _subCombo);

        // --- Volúmenes ---
        AddRow(MakeLabel("Volumen"), MakeLabel("Volumen subdivisión"), topGap: 10);

        _volTrack.Maximum = 100;
        _volTrack.Value = 80;
        _volTrack.TickStyle = TickStyle.None;
        _volTrack.Dock = DockStyle.Fill;
        _volTrack.ValueChanged += (_, _) => _engine.MasterVolume = _volTrack.Value / 100f;

        _subVolTrack.Maximum = 100;
        _subVolTrack.Value = 85;
        _subVolTrack.TickStyle = TickStyle.None;
        _subVolTrack.Dock = DockStyle.Fill;
        _subVolTrack.ValueChanged += (_, _) => _engine.SubVolume = _subVolTrack.Value / 100f;
        AddRow(_volTrack, _subVolTrack);

        _accentCheck.Text = "Acentuar el primer tiempo";
        _accentCheck.Checked = true;
        _accentCheck.AutoSize = true;
        _accentCheck.CheckedChanged += (_, _) => _engine.AccentFirst = _accentCheck.Checked;
        AddRow(_accentCheck);

        // --- Indicador visual ---
        _indicator.Size = new Size(0, 56);
        _indicator.Dock = DockStyle.Fill;
        _indicator.BackColor = Panel;
        AddRow(_indicator, topGap: 6);

        // --- Start / Stop ---
        _playButton.Text = "▶  Iniciar  (Espacio)";
        _playButton.Font = new Font("Segoe UI Semibold", 13f);
        StyleButton(_playButton);
        _playButton.BackColor = Color.FromArgb(46, 125, 80);
        _playButton.Dock = DockStyle.Fill;
        _playButton.MinimumSize = new Size(0, 54);
        _playButton.Click += (_, _) => TogglePlay();
        AddRow(_playButton, topGap: 8);

        Controls.Add(root);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (ActiveControl == _bpmBox)
        {
            if (keyData == Keys.Enter) { CommitBpmText(); _playButton.Focus(); return true; }
            if (keyData == Keys.Escape) { _bpmBox.Text = _engine.Bpm.ToString(); _playButton.Focus(); return true; }
        }
        switch (keyData)
        {
            case Keys.Space: TogglePlay(); return true;
            case Keys.Up: case Keys.Right: SetBpm(_engine.Bpm + 1); return true;
            case Keys.Down: case Keys.Left: SetBpm(_engine.Bpm - 1); return true;
            case Keys.PageUp: SetBpm(_engine.Bpm + 10); return true;
            case Keys.PageDown: SetBpm(_engine.Bpm - 10); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void SetBpm(int bpm)
    {
        bpm = Math.Clamp(bpm, _bpmTrack.Minimum, _bpmTrack.Maximum);
        _engine.Bpm = bpm;
        _bpmBox.Text = bpm.ToString();
        if (_bpmTrack.Value != bpm) _bpmTrack.Value = bpm;
    }

    private bool _justFocused;

    private void CommitBpmText()
    {
        if (int.TryParse(_bpmBox.Text, out int v)) SetBpm(v);
        else _bpmBox.Text = _engine.Bpm.ToString();
    }

    private void TogglePlay()
    {
        if (_output == null)
        {
            try
            {
                _engine.Reset();
                _output = new WasapiOut(AudioClientShareMode.Shared, true, 30);
                _output.Init(_engine);
                _output.Play();
                _playButton.Text = "■  Detener  (Espacio)";
                _playButton.BackColor = Color.FromArgb(160, 60, 60);
            }
            catch (Exception ex)
            {
                _output?.Dispose();
                _output = null;
                MessageBox.Show(this, "No se pudo abrir el dispositivo de audio:\n" + ex.Message, "Metrónomo",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        else
        {
            _output.Stop();
            _output.Dispose();
            _output = null;
            _indicator.Set(-1, -1);
            _playButton.Text = "▶  Iniciar  (Espacio)";
            _playButton.BackColor = Color.FromArgb(46, 125, 80);
        }
    }

    private void UpdateIndicator()
    {
        if (_output == null) return;
        long played = _output.GetPosition() / _output.OutputWaveFormat.BlockAlign;
        var ev = _engine.GetLatestPlayed(played);
        if (ev is { } e) _indicator.Set(e.Beat, e.Sub);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _uiTimer.Stop();
        _output?.Stop();
        _output?.Dispose();
        base.OnFormClosing(e);
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text, ForeColor = Muted, AutoSize = true,
    };

    private static Button MakeButton(string text, EventHandler onClick)
    {
        var b = new Button { Text = text };
        StyleButton(b);
        b.Click += onClick;
        return b;
    }

    private static void StyleButton(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.BackColor = Panel;
        b.ForeColor = Fg;
        b.TabStop = false;
        b.Cursor = Cursors.Hand;
        if (b.Text is "−" or "+") b.Font = new Font("Segoe UI", 20f);
    }

    /// <summary>Muestra un círculo por tiempo y puntos pequeños para la subdivisión.</summary>
    private sealed class BeatIndicator : Control
    {
        private int _beat = -1, _sub = -1;
        public int Beats { get; set { field = value; Invalidate(); } } = 4;
        public int Subdivision { get; set { field = value; Invalidate(); } } = 1;

        public BeatIndicator()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public void Set(int beat, int sub)
        {
            if (beat == _beat && sub == _sub) return;
            _beat = beat; _sub = sub;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            int n = Math.Max(1, Beats);
            float slot = (float)Width / n;
            float d = Math.Min(28f, slot * 0.6f);
            using var off = new SolidBrush(Color.FromArgb(70, 74, 86));
            using var accent = new SolidBrush(AccentCol);
            using var beat = new SolidBrush(BeatCol);

            for (int i = 0; i < n; i++)
            {
                float cx = slot * i + slot / 2;
                float cy = 22;
                bool active = i == _beat && _sub == 0;
                var brush = active ? (i == 0 ? accent : beat) : (i == _beat ? (i == 0 ? accent : beat) : off);
                float dd = active ? d : (i == _beat ? d * 0.75f : d * 0.6f);
                g.FillEllipse(brush, cx - dd / 2, cy - dd / 2, dd, dd);

                // Puntos de subdivisión bajo el tiempo actual.
                if (Subdivision > 1 && i == _beat)
                {
                    float sp = Math.Min(10f, slot / (Subdivision + 1));
                    float startX = cx - sp * (Subdivision - 1) / 2;
                    for (int s = 0; s < Subdivision; s++)
                    {
                        var b = s <= _sub ? beat : off;
                        g.FillEllipse(b, startX + s * sp - 3, 44, 6, 6);
                    }
                }
            }
        }
    }
}
