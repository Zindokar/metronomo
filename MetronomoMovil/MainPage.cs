using MetronomoMovil.Audio;
using MetronomoMovil.Core;
using Microsoft.Maui.Controls.Shapes;

namespace MetronomoMovil;

/// <summary>
/// Misma funcionalidad que la versión de escritorio, adaptada al tacto:
/// botones grandes (≥ 56 pt), mantener pulsado −/+ repite, vibración al tocar,
/// subdivisiones como botones en vez de lista desplegable y el botón de inicio
/// abajo, al alcance del pulgar.
/// </summary>
public sealed class MainPage : ContentPage
{
    private static readonly Color Bg = Color.FromArgb("#1C1E24");
    private static readonly Color Card = Color.FromArgb("#282B33");
    private static readonly Color CardHi = Color.FromArgb("#353946");
    private static readonly Color Fg = Color.FromArgb("#E6E8EE");
    private static readonly Color Muted = Color.FromArgb("#969BA8");
    private static readonly Color AccentCol = Color.FromArgb("#FF8C42");
    private static readonly Color BeatCol = Color.FromArgb("#5AAAFF");
    private static readonly Color PlayCol = Color.FromArgb("#2E7D50");
    private static readonly Color StopCol = Color.FromArgb("#A03C3C");

    private static readonly (string Num, string Name, int Value)[] Subdivisions =
    {
        ("1", "Negras", 1), ("2", "Corcheas", 2), ("3", "Tresillos", 3), ("4", "Semicorch.", 4),
        ("5", "Quintillos", 5), ("6", "Seisillos", 6), ("7", "Septillos", 7), ("8", "Fusas", 8),
    };

    private readonly AudioPlayer _player = new();
    private MetronomeEngine Engine => _player.Engine;

    private readonly Entry _bpmEntry;
    private readonly Slider _bpmSlider;
    private readonly Label _beatsLabel;
    private readonly List<Border> _subButtons = new();
    private readonly Slider _volSlider;
    private readonly Slider _subVolSlider;
    private readonly Label _volValue;
    private readonly Label _subVolValue;
    private readonly Switch _accentSwitch;
    private readonly GraphicsView _indicatorView;
    private readonly BeatIndicator _indicator = new();
    private readonly Button _playButton;
    private readonly IDispatcherTimer _uiTimer;
    private IDispatcherTimer? _repeatTimer;
    private bool _updatingBpm;

    public MainPage()
    {
        BackgroundColor = Bg;
        Shell.SetNavBarIsVisible(this, false);
        Padding = new Thickness(16, 8, 16, 16);

        // ---------- Tempo ----------
        _bpmEntry = new Entry
        {
            Text = "120",
            FontSize = 76,
            FontAttributes = FontAttributes.Bold,
            TextColor = Fg,
            BackgroundColor = Colors.Transparent,
            HorizontalTextAlignment = TextAlignment.Center,
            Keyboard = Keyboard.Numeric,
            MaxLength = 3,
            WidthRequest = 190,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };
        _bpmEntry.Focused += (_, _) => Dispatcher.Dispatch(() =>
        {
            _bpmEntry.CursorPosition = 0;
            _bpmEntry.SelectionLength = _bpmEntry.Text?.Length ?? 0;
        });
        _bpmEntry.TextChanged += (_, e) =>
        {
            // Solo dígitos.
            var digits = new string((e.NewTextValue ?? "").Where(char.IsDigit).ToArray());
            if (digits != e.NewTextValue) _bpmEntry.Text = digits;
        };
        _bpmEntry.Completed += (_, _) => CommitBpmText();
        _bpmEntry.Unfocused += (_, _) => CommitBpmText();

        var tempoRow = new Grid
        {
            ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) },
            Margin = new Thickness(0, 8, 0, 0),
        };
        tempoRow.Add(RoundButton("−", 64, () => SetBpm(Engine.Bpm - 1)), 0);
        tempoRow.Add(_bpmEntry, 1);
        tempoRow.Add(RoundButton("+", 64, () => SetBpm(Engine.Bpm + 1)), 2);

        var bpmCaption = new Label
        {
            Text = "BPM  ·  toca el número para escribirlo",
            TextColor = Muted, FontSize = 13, HorizontalOptions = LayoutOptions.Center,
        };

        _bpmSlider = new Slider
        {
            Minimum = MetronomeEngine.MinBpm, Maximum = MetronomeEngine.MaxBpm, Value = 120,
            MinimumTrackColor = BeatCol, MaximumTrackColor = CardHi, ThumbColor = Fg,
            HeightRequest = 48,
        };
        _bpmSlider.ValueChanged += (_, e) => { if (!_updatingBpm) SetBpm((int)Math.Round(e.NewValue)); };

        // ---------- Tiempos por compás ----------
        _beatsLabel = new Label
        {
            Text = "4", FontSize = 30, FontAttributes = FontAttributes.Bold, TextColor = Fg,
            HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
            WidthRequest = 64,
        };
        var beatsRow = new Grid
        {
            ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto) },
            ColumnSpacing = 8,
        };
        beatsRow.Add(SectionLabel("Tiempos por compás", 0).WithVCenter(), 0);
        beatsRow.Add(RoundButton("−", 52, () => SetBeats(Engine.BeatsPerBar - 1), CardHi), 1);
        beatsRow.Add(_beatsLabel, 2);
        beatsRow.Add(RoundButton("+", 52, () => SetBeats(Engine.BeatsPerBar + 1), CardHi), 3);

        // ---------- Subdivisión: rejilla de botones 4×2 ----------
        var subGrid = new Grid
        {
            ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) },
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto) },
            ColumnSpacing = 8, RowSpacing = 8,
        };
        for (int i = 0; i < Subdivisions.Length; i++)
        {
            int index = i;
            var chip = new Border
            {
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                BackgroundColor = Card,
                HeightRequest = 64,
                Content = new VerticalStackLayout
                {
                    VerticalOptions = LayoutOptions.Center,
                    Spacing = 0,
                    Children =
                    {
                        new Label { Text = Subdivisions[i].Num, FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Fg, HorizontalOptions = LayoutOptions.Center },
                        new Label { Text = Subdivisions[i].Name, FontSize = 11, TextColor = Muted, HorizontalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.NoWrap },
                    },
                },
            };
            chip.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => { Haptic(); SetSubdivision(index); }) });
            _subButtons.Add(chip);
            subGrid.Add(chip, i % 4, i / 4);
        }

        // ---------- Volúmenes ----------
        (_volSlider, _volValue) = (MakeVolumeSlider(80), PercentLabel(80));
        _volSlider.ValueChanged += (_, e) => { Engine.MasterVolume = (float)(e.NewValue / 100); _volValue.Text = $"{(int)e.NewValue} %"; Save(); };
        (_subVolSlider, _subVolValue) = (MakeVolumeSlider(85), PercentLabel(85));
        _subVolSlider.ValueChanged += (_, e) => { Engine.SubVolume = (float)(e.NewValue / 100); _subVolValue.Text = $"{(int)e.NewValue} %"; Save(); };

        // ---------- Acento ----------
        _accentSwitch = new Switch { IsToggled = true, OnColor = AccentCol, ThumbColor = Fg, HorizontalOptions = LayoutOptions.End };
        _accentSwitch.Toggled += (_, e) => { Engine.AccentFirst = e.Value; Save(); };
        var accentRow = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, MinimumHeightRequest = 48 };
        accentRow.Add(new Label { Text = "Acentuar el primer tiempo", TextColor = Fg, FontSize = 16, VerticalOptions = LayoutOptions.Center }, 0);
        accentRow.Add(_accentSwitch, 1);

        var settings = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                tempoRow, bpmCaption, _bpmSlider,
                CardOf(beatsRow),
                CardOf(new VerticalStackLayout { Spacing = 10, Children = { SectionLabel("Subdivisión", 0), subGrid } }),
                CardOf(new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        LabelWithValue("Volumen", _volValue), _volSlider,
                        LabelWithValue("Volumen subdivisión", _subVolValue), _subVolSlider,
                    },
                }),
                CardOf(accentRow),
            },
        };

        // ---------- Indicador + Iniciar (fijos abajo, zona del pulgar) ----------
        _indicatorView = new GraphicsView { Drawable = _indicator, HeightRequest = 64, BackgroundColor = Colors.Transparent };
        var indicatorCard = CardOf(_indicatorView);
        indicatorCard.Padding = 4;

        _playButton = new Button
        {
            Text = "▶  Iniciar",
            FontSize = 22, FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White, BackgroundColor = PlayCol,
            CornerRadius = 16, HeightRequest = 72,
        };
        _playButton.Clicked += (_, _) => { Haptic(); TogglePlay(); };

        var root = new Grid
        {
            RowDefinitions = { new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) },
            RowSpacing = 12,
        };
        root.Add(new ScrollView { Content = settings }, 0, 0);
        root.Add(indicatorCard, 0, 1);
        root.Add(_playButton, 0, 2);
        Content = root;

        // Tocar fuera del número de BPM cierra el teclado.
        var tapOutside = new TapGestureRecognizer();
        tapOutside.Tapped += (_, _) => { if (_bpmEntry.IsFocused) _bpmEntry.Unfocus(); };
        settings.GestureRecognizers.Add(tapOutside);

        _uiTimer = Dispatcher.CreateTimer();
        _uiTimer.Interval = TimeSpan.FromMilliseconds(16);
        _uiTimer.Tick += (_, _) => UpdateIndicator();

        Load();
    }

    // ---------------- Lógica ----------------

    private void SetBpm(int bpm)
    {
        bpm = Math.Clamp(bpm, MetronomeEngine.MinBpm, MetronomeEngine.MaxBpm);
        Engine.Bpm = bpm;
        _updatingBpm = true;
        _bpmEntry.Text = bpm.ToString();
        _bpmSlider.Value = bpm;
        _updatingBpm = false;
        Save();
    }

    private void CommitBpmText()
    {
        if (int.TryParse(_bpmEntry.Text, out int v)) SetBpm(v);
        else _bpmEntry.Text = Engine.Bpm.ToString();
    }

    private void SetBeats(int beats)
    {
        Engine.BeatsPerBar = beats;
        _beatsLabel.Text = Engine.BeatsPerBar.ToString();
        _indicator.Beats = Engine.BeatsPerBar;
        _indicatorView.Invalidate();
        Save();
    }

    private void SetSubdivision(int index)
    {
        Engine.Subdivision = Subdivisions[index].Value;
        _indicator.Subdivision = Engine.Subdivision;
        for (int i = 0; i < _subButtons.Count; i++)
        {
            bool sel = i == index;
            _subButtons[i].BackgroundColor = sel ? BeatCol : Card;
            var stack = (VerticalStackLayout)_subButtons[i].Content!;
            ((Label)stack[0]).TextColor = sel ? Bg : Fg;
            ((Label)stack[1]).TextColor = sel ? Bg : Muted;
        }
        _indicatorView.Invalidate();
        Save();
    }

    private void TogglePlay()
    {
        if (_bpmEntry.IsFocused) _bpmEntry.Unfocus();
        if (!_player.IsPlaying)
        {
            try
            {
                _player.Start();
            }
            catch (Exception ex)
            {
                DisplayAlertAsync("Metrónomo", "No se pudo iniciar el audio:\n" + ex.Message, "OK");
                return;
            }
            DeviceDisplay.Current.KeepScreenOn = true;
            _playButton.Text = "■  Detener";
            _playButton.BackgroundColor = StopCol;
            _uiTimer.Start();
        }
        else
        {
            _player.Stop();
            DeviceDisplay.Current.KeepScreenOn = false;
            _uiTimer.Stop();
            _indicator.Set(-1, -1);
            _indicatorView.Invalidate();
            _playButton.Text = "▶  Iniciar";
            _playButton.BackgroundColor = PlayCol;
        }
    }

    private void UpdateIndicator()
    {
        var ev = Engine.GetLatestPlayed(_player.PlayedFrames);
        if (ev is { } e && _indicator.Set(e.Beat, e.Sub)) _indicatorView.Invalidate();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_player.IsPlaying) TogglePlay();
    }

    // ---------------- Persistencia ----------------

    private bool _loading;

    private void Load()
    {
        _loading = true;
        SetBpm(Preferences.Default.Get("bpm", 120));
        SetBeats(Preferences.Default.Get("beats", 4));
        SetSubdivision(Math.Clamp(Preferences.Default.Get("subIndex", 0), 0, Subdivisions.Length - 1));
        _volSlider.Value = Preferences.Default.Get("vol", 80);
        _subVolSlider.Value = Preferences.Default.Get("subVol", 85);
        _accentSwitch.IsToggled = Preferences.Default.Get("accent", true);
        _loading = false;
    }

    private void Save()
    {
        if (_loading) return;
        Preferences.Default.Set("bpm", Engine.Bpm);
        Preferences.Default.Set("beats", Engine.BeatsPerBar);
        Preferences.Default.Set("subIndex", Array.FindIndex(Subdivisions, s => s.Value == Engine.Subdivision));
        Preferences.Default.Set("vol", (int)_volSlider.Value);
        Preferences.Default.Set("subVol", (int)_subVolSlider.Value);
        Preferences.Default.Set("accent", _accentSwitch.IsToggled);
    }

    // ---------------- Controles ----------------

    /// <summary>Botón redondo; al mantenerlo pulsado repite la acción (cada vez más rápido).</summary>
    private Button RoundButton(string text, int size, Action action, Color? background = null)
    {
        var b = new Button
        {
            Text = text, FontSize = size * 0.45, TextColor = Fg, BackgroundColor = background ?? Card,
            WidthRequest = size, HeightRequest = size, CornerRadius = size / 2, Padding = 0,
            VerticalOptions = LayoutOptions.Center,
        };
        int repeats = 0;
        b.Pressed += (_, _) =>
        {
            Haptic();
            action();
            repeats = 0;
            _repeatTimer?.Stop();
            _repeatTimer = Dispatcher.CreateTimer();
            _repeatTimer.Interval = TimeSpan.FromMilliseconds(400);
            _repeatTimer.Tick += (_, _) =>
            {
                action();
                repeats++;
                _repeatTimer!.Interval = TimeSpan.FromMilliseconds(repeats > 15 ? 30 : 80);
            };
            _repeatTimer.Start();
        };
        b.Released += (_, _) => _repeatTimer?.Stop();
        return b;
    }

    private static Border CardOf(View content) => new()
    {
        Content = content,
        BackgroundColor = Card,
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = 16 },
        Padding = new Thickness(14, 12),
    };

    private static Label SectionLabel(string text, double top) => new()
    {
        Text = text, TextColor = Muted, FontSize = 14, Margin = new Thickness(0, top, 0, 0),
    };

    private static Label PercentLabel(int v) => new()
    {
        Text = $"{v} %", TextColor = Fg, FontSize = 14, HorizontalOptions = LayoutOptions.End,
    };

    private static Grid LabelWithValue(string text, Label value)
    {
        var g = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        g.Add(SectionLabel(text, 0), 0);
        g.Add(value, 1);
        return g;
    }

    private static Slider MakeVolumeSlider(int value) => new()
    {
        Minimum = 0, Maximum = 100, Value = value,
        MinimumTrackColor = BeatCol, MaximumTrackColor = CardHi, ThumbColor = Fg,
        HeightRequest = 44,
    };

    private static void Haptic()
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
    }

    /// <summary>Un círculo por tiempo y puntos pequeños para la subdivisión (igual que en escritorio).</summary>
    private sealed class BeatIndicator : IDrawable
    {
        private int _beat = -1, _sub = -1;
        public int Beats { get; set; } = 4;
        public int Subdivision { get; set; } = 1;

        public bool Set(int beat, int sub)
        {
            if (beat == _beat && sub == _sub) return false;
            _beat = beat; _sub = sub;
            return true;
        }

        public void Draw(ICanvas canvas, RectF rect)
        {
            int n = Math.Max(1, Beats);
            float slot = rect.Width / n;
            float d = Math.Min(30f, slot * 0.6f);
            var off = Color.FromArgb("#464A56");

            for (int i = 0; i < n; i++)
            {
                float cx = rect.X + slot * i + slot / 2;
                float cy = rect.Y + 24;
                bool current = i == _beat;
                bool onBeat = current && _sub == 0;
                canvas.FillColor = current ? (i == 0 && Beats > 1 ? AccentCol : BeatCol) : off;
                float dd = onBeat ? d : current ? d * 0.75f : d * 0.6f;
                canvas.FillCircle(cx, cy, dd / 2);

                if (Subdivision > 1 && current)
                {
                    float sp = Math.Min(12f, slot / (Subdivision + 1));
                    float startX = cx - sp * (Subdivision - 1) / 2;
                    for (int s = 0; s < Subdivision; s++)
                    {
                        canvas.FillColor = s <= _sub ? BeatCol : off;
                        canvas.FillCircle(startX + s * sp, rect.Y + 50, 3.5f);
                    }
                }
            }
        }
    }
}

internal static class ViewExtensions
{
    public static T WithVCenter<T>(this T view) where T : View
    {
        view.VerticalOptions = LayoutOptions.Center;
        return view;
    }
}
