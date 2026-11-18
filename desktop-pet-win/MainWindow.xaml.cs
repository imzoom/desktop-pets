using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DesktopPet
{
    public class FrameMetadata
    {
        public int index { get; set; }
        public string file { get; set; } = string.Empty;
        public int groundBaseline { get; set; }
        public int pixelCount { get; set; }
    }

    public class AnimationData
    {
        public string name { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public double fps { get; set; } = 24;
        public int durationMs { get; set; }
        public bool loop { get; set; } = true;
        public string? nextAnimation { get; set; }
        public int totalFrames { get; set; }
        public List<int> frameSequence { get; set; } = new();
        public string framesDir { get; set; } = string.Empty;
        public List<FrameMetadata> frames { get; set; } = new();

        internal List<BitmapImage> CachedBitmaps = new();
    }

    public class CharacterData
    {
        public string id { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public string defaultAnimation { get; set; } = "idle";
        public Dictionary<string, AnimationData> animations { get; set; } = new();
    }

    public class AppConfig
    {
        public string version { get; set; } = string.Empty;
        public string defaultCharacter { get; set; } = "hooded-blade";
        public Dictionary<string, CharacterData> characters { get; set; } = new();
        public Dictionary<string, AnimationData>? animations { get; set; }
    }

    public partial class MainWindow : Window
    {
        private AppConfig? _config;
        private readonly Dictionary<string, CharacterData> _characters = new();
        private CharacterData? _activeCharacter;
        private AnimationData? _currentAnimation;
        private int _sequenceIndex = 0;
        private readonly DispatcherTimer _timer = new();
        private string _baseDirectory = string.Empty;
        private Point _dragStartPoint;
        private double _speedMultiplier = 1.0;
        private bool _userIsDragging = false;

        // Autonomous AI Wandering & Random Actions
        private readonly Random _rng = new();
        private bool _isAutonomous = true;
        private int _idleFramesRemaining = 60;
        private int _runFramesRemaining = 0;
        private double _targetX = 0;
        private double _targetY = 0;
        private double _moveSpeedX = 5.0;
        private double _moveSpeedY = 2.0;
        private bool _facingLeft = false;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            KeyDown += MainWindow_PreviewKeyDown;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 80;
            Top = workArea.Bottom - Height - 80;

            LoadAnimationConfig();
            SetupTimer();

            // Default to Hooded Blade (Sword Master)
            string startChar = _characters.ContainsKey("hooded-blade") ? "hooded-blade" : (_characters.Keys.FirstOrDefault() ?? "robot");
            SwitchCharacter(startChar);

            Focus();
            Keyboard.Focus(this);
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            HandleKey(e.Key);
        }

        private void HandleKey(Key key)
        {
            if (key == Key.C)
            {
                // Toggle character
                string nextChar = (_activeCharacter?.id == "hooded-blade") ? "robot" : "hooded-blade";
                SwitchCharacter(nextChar);
            }
            else if (key == Key.Space || key == Key.S)
            {
                TriggerPrimaryAction();
            }
            else if (key == Key.A)
            {
                TriggerSecondaryAction();
            }
            else if (key == Key.J)
            {
                TriggerJumpAction();
            }
            else if (key == Key.P)
            {
                if (_activeCharacter?.id == "robot") TriggerManualAction("punch");
                else TriggerPrimaryAction();
            }
            else if (key == Key.R)
            {
                TriggerManualAction("run");
            }
            else if (key == Key.I)
            {
                TriggerManualAction("idle");
            }
            else if (key == Key.Escape)
            {
                Close();
            }
        }

        private void TriggerPrimaryAction()
        {
            if (_activeCharacter?.id == "hooded-blade")
            {
                TriggerManualAction("sword-power");
            }
            else
            {
                TriggerManualAction("kick");
            }
        }

        private void TriggerSecondaryAction()
        {
            if (_activeCharacter?.id == "hooded-blade")
            {
                TriggerManualAction("attack");
            }
            else
            {
                TriggerManualAction("punch");
            }
        }

        private void TriggerJumpAction()
        {
            if (_activeCharacter?.id == "hooded-blade")
            {
                TriggerManualAction("jump");
            }
        }

        private void TriggerManualAction(string animName)
        {
            if (animName == "run")
            {
                StartNewRunTarget();
            }
            PlayAnimation(animName);
        }

        public void SwitchCharacter(string characterId)
        {
            if (!_characters.TryGetValue(characterId, out var character)) return;

            _activeCharacter = character;
            bool isBlade = (characterId == "hooded-blade");

            if (MenuItemCharHoodedBlade != null) MenuItemCharHoodedBlade.IsChecked = isBlade;
            if (MenuItemCharRobot != null) MenuItemCharRobot.IsChecked = !isBlade;

            // Update action menu headers
            if (MenuItemPower != null)
            {
                MenuItemPower.Header = isBlade ? "⚡ Sword Power (Space / Click)" : "🥋 Kick Action (Space / Click)";
            }
            if (MenuItemAttack != null)
            {
                MenuItemAttack.Header = isBlade ? "🗡️ Sword Slash (A)" : "🥊 Punch Action (A / P)";
            }
            if (MenuItemJump != null)
            {
                MenuItemJump.Visibility = isBlade ? Visibility.Visible : Visibility.Collapsed;
            }

            Title = isBlade ? "Desktop Pet - Hooded Blade (Sword Master)" : "Desktop Pet - Robot Pet";

            RootGrid.ToolTip = isBlade
                ? "Hooded Blade: Click for Sword Power! Double-click to switch to Robot. Press 'C' to switch."
                : "Robot Pet: Click to Kick! Double-click to switch to Hooded Blade. Press 'C' to switch.";

            _idleFramesRemaining = _rng.Next(25, 60);
            PlayAnimation(_activeCharacter.defaultAnimation);
        }

        private void LoadAnimationConfig()
        {
            var candidatePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "animation-config.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "animation-config.json"),
                @"C:\Jay\Demos\dextop-pets\animation-config.json"
            };

            string configPath = string.Empty;
            foreach (var path in candidatePaths)
            {
                var full = Path.GetFullPath(path);
                if (File.Exists(full))
                {
                    configPath = full;
                    break;
                }
            }

            if (string.IsNullOrEmpty(configPath))
            {
                MessageBox.Show("Could not find animation-config.json.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _baseDirectory = Path.GetDirectoryName(configPath)!;
            string json = File.ReadAllText(configPath);
            _config = JsonSerializer.Deserialize<AppConfig>(json);

            if (_config == null) return;

            // Load multi-character structure
            if (_config.characters != null && _config.characters.Count > 0)
            {
                foreach (var (charId, character) in _config.characters)
                {
                    character.id = charId;
                    CacheCharacterBitmaps(character);
                    _characters[charId] = character;
                }
            }
            else if (_config.animations != null)
            {
                var fallbackChar = new CharacterData
                {
                    id = "default",
                    name = "Desktop Pet",
                    animations = _config.animations
                };
                CacheCharacterBitmaps(fallbackChar);
                _characters["default"] = fallbackChar;
            }
        }

        private void CacheCharacterBitmaps(CharacterData character)
        {
            foreach (var (_, anim) in character.animations)
            {
                string framesDir = Path.Combine(_baseDirectory, anim.framesDir);
                if (Directory.Exists(framesDir))
                {
                    var files = Directory.GetFiles(framesDir, "frame_*.png").OrderBy(f => f).ToList();
                    anim.CachedBitmaps = new List<BitmapImage>(files.Count);

                    foreach (var file in files)
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(file, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        anim.CachedBitmaps.Add(bitmap);
                    }
                }
            }
        }

        private void SetupTimer()
        {
            _timer.Tick += Timer_Tick;
        }

        public void PlayAnimation(string name, double? customFps = null)
        {
            if (_activeCharacter == null || !_activeCharacter.animations.TryGetValue(name, out var anim))
            {
                return;
            }

            if (anim.CachedBitmaps.Count == 0) return;

            _currentAnimation = anim;
            _sequenceIndex = 0;

            double fps = (customFps ?? anim.fps) * _speedMultiplier;
            if (fps <= 0) fps = 24;

            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
            _timer.Stop();

            RenderCurrentFrame();
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_currentAnimation == null || _currentAnimation.CachedBitmaps.Count == 0) return;

            if (_isAutonomous && !_userIsDragging)
            {
                HandleAutonomousBehavior();
            }

            var seq = _currentAnimation.frameSequence;
            if (seq.Count == 0)
            {
                seq = Enumerable.Range(0, _currentAnimation.CachedBitmaps.Count).ToList();
            }

            if (_sequenceIndex + 1 < seq.Count)
            {
                _sequenceIndex++;
                RenderCurrentFrame();
            }
            else
            {
                if (_currentAnimation.loop)
                {
                    _sequenceIndex = 0;
                    RenderCurrentFrame();
                }
                else
                {
                    string next = _currentAnimation.nextAnimation ?? "idle";
                    _idleFramesRemaining = _rng.Next(25, 75);
                    PlayAnimation(next);
                }
            }
        }

        private void HandleAutonomousBehavior()
        {
            var workArea = SystemParameters.WorkArea;
            double minX = 10;
            double maxX = Math.Max(10, workArea.Width - Width - 10);
            double minY = 10;
            double maxY = Math.Max(10, workArea.Height - Height - 10);

            string current = _currentAnimation?.name ?? "idle";
            bool isBlade = (_activeCharacter?.id == "hooded-blade");

            if (current == "run")
            {
                double dx = _targetX - Left;
                double dy = _targetY - Top;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist > 10 && _runFramesRemaining > 0)
                {
                    _runFramesRemaining--;

                    double stepX = Math.Sign(dx) * Math.Min(Math.Abs(dx), _moveSpeedX);
                    double stepY = Math.Sign(dy) * Math.Min(Math.Abs(dy), _moveSpeedY);

                    if (stepX < -0.5) SetFacing(true);
                    else if (stepX > 0.5) SetFacing(false);

                    Left = Math.Clamp(Left + stepX, minX, maxX);
                    Top = Math.Clamp(Top + stepY, minY, maxY);
                }
                else
                {
                    int roll = _rng.Next(100);
                    if (isBlade)
                    {
                        if (roll < 45) PlayAnimation("sword-power");
                        else if (roll < 75) PlayAnimation("attack");
                        else if (roll < 90) PlayAnimation("jump");
                        else
                        {
                            _idleFramesRemaining = _rng.Next(35, 80);
                            PlayAnimation("idle");
                        }
                    }
                    else
                    {
                        if (roll < 45) PlayAnimation("kick");
                        else if (roll < 80) PlayAnimation("punch");
                        else
                        {
                            _idleFramesRemaining = _rng.Next(35, 80);
                            PlayAnimation("idle");
                        }
                    }
                }
            }
            else if (current == "idle")
            {
                _idleFramesRemaining--;
                if (_idleFramesRemaining <= 0)
                {
                    int roll = _rng.Next(100);
                    if (roll < 50)
                    {
                        StartNewRunTarget();
                        PlayAnimation("run");
                    }
                    else if (isBlade)
                    {
                        SetFacing(_rng.Next(2) == 0);
                        if (roll < 75) PlayAnimation("sword-power");
                        else if (roll < 90) PlayAnimation("attack");
                        else PlayAnimation("jump");
                    }
                    else
                    {
                        SetFacing(_rng.Next(2) == 0);
                        if (roll < 75) PlayAnimation("kick");
                        else PlayAnimation("punch");
                    }
                }
            }
        }

        private void StartNewRunTarget()
        {
            var workArea = SystemParameters.WorkArea;
            double minX = 20;
            double maxX = Math.Max(20, workArea.Width - Width - 20);
            double minY = Math.Max(20, workArea.Height * 0.2);
            double maxY = Math.Max(20, workArea.Height - Height - 20);

            _targetX = _rng.NextDouble() * (maxX - minX) + minX;
            _targetY = _rng.NextDouble() * (maxY - minY) + minY;

            _moveSpeedX = _rng.NextDouble() * 3.5 + 4.5;
            _moveSpeedY = _rng.NextDouble() * 2.0 + 1.0;

            _runFramesRemaining = _rng.Next(45, 120);
        }

        private void SetFacing(bool faceLeft)
        {
            _facingLeft = faceLeft;
            PetScaleTransform.ScaleX = _facingLeft ? -1 : 1;
        }

        private void RenderCurrentFrame()
        {
            if (_currentAnimation == null || _currentAnimation.CachedBitmaps.Count == 0) return;

            var seq = _currentAnimation.frameSequence;
            int frameIdx = 0;
            if (seq.Count > 0 && _sequenceIndex < seq.Count)
            {
                frameIdx = seq[_sequenceIndex];
            }

            if (frameIdx >= 0 && frameIdx < _currentAnimation.CachedBitmaps.Count)
            {
                PetImage.Source = _currentAnimation.CachedBitmaps[frameIdx];
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            Keyboard.Focus(this);

            if (e.ChangedButton == MouseButton.Left)
            {
                _userIsDragging = true;
                _dragStartPoint = e.GetPosition(this);
                DragMove();
                _userIsDragging = false;
            }
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                // Double-click instantly toggles character!
                string nextChar = (_activeCharacter?.id == "hooded-blade") ? "robot" : "hooded-blade";
                SwitchCharacter(nextChar);
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _userIsDragging = false;
            var endPoint = e.GetPosition(this);
            double dist = (endPoint - _dragStartPoint).Length;

            if (dist < 6)
            {
                TriggerPrimaryAction();
            }
        }

        // Context Menu Event Handlers
        private void MenuCharHoodedBlade_Click(object sender, RoutedEventArgs e) => SwitchCharacter("hooded-blade");
        private void MenuCharRobot_Click(object sender, RoutedEventArgs e) => SwitchCharacter("robot");

        private void MenuPower_Click(object sender, RoutedEventArgs e) => TriggerPrimaryAction();
        private void MenuAttack_Click(object sender, RoutedEventArgs e) => TriggerSecondaryAction();
        private void MenuJump_Click(object sender, RoutedEventArgs e) => TriggerJumpAction();
        private void MenuRun_Click(object sender, RoutedEventArgs e) => TriggerManualAction("run");
        private void MenuIdle_Click(object sender, RoutedEventArgs e) => TriggerManualAction("idle");

        private void MenuAutonomous_Click(object sender, RoutedEventArgs e)
        {
            _isAutonomous = MenuItemAutonomous.IsChecked;
            if (!_isAutonomous && _currentAnimation?.name == "run")
            {
                PlayAnimation("idle");
            }
        }

        private void MenuFps12_Click(object sender, RoutedEventArgs e) => SetFpsPreset(12);
        private void MenuFps24_Click(object sender, RoutedEventArgs e) => SetFpsPreset(24);
        private void MenuFps30_Click(object sender, RoutedEventArgs e) => SetFpsPreset(30);
        private void MenuFps48_Click(object sender, RoutedEventArgs e) => SetFpsPreset(48);

        private void SetFpsPreset(double fps)
        {
            if (_currentAnimation != null)
            {
                _speedMultiplier = fps / 24.0;
                double effectiveFps = _currentAnimation.fps * _speedMultiplier;
                _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / effectiveFps);
            }
        }

        private void MenuScaleSmall_Click(object sender, RoutedEventArgs e) => SetScale(256, 256, MenuItemScaleSmall);
        private void MenuScaleMedium_Click(object sender, RoutedEventArgs e) => SetScale(384, 384, MenuItemScaleMedium);
        private void MenuScaleOriginal_Click(object sender, RoutedEventArgs e) => SetScale(512, 512, MenuItemScaleOriginal);

        private void SetScale(double width, double height, System.Windows.Controls.MenuItem selectedItem)
        {
            Width = width;
            Height = height;
            MenuItemScaleSmall.IsChecked = (selectedItem == MenuItemScaleSmall);
            MenuItemScaleMedium.IsChecked = (selectedItem == MenuItemScaleMedium);
            MenuItemScaleOriginal.IsChecked = (selectedItem == MenuItemScaleOriginal);
        }

        private void MenuTopmost_Click(object sender, RoutedEventArgs e)
        {
            Topmost = MenuItemTopmost.IsChecked;
        }

        private void MenuResetPos_Click(object sender, RoutedEventArgs e)
        {
            var workArea = SystemParameters.WorkArea;
            Left = (workArea.Width - Width) / 2;
            Top = (workArea.Height - Height) / 2;
            PlayAnimation("idle");
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
