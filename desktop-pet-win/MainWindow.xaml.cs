using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        private enum RoamingPace { Chill, Active, Still }

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

        // Position & Pacing State (in WPF Device-Independent Pixels)
        private double _currentPosX;
        private double _currentPosY;
        private readonly Random _rng = new();
        private bool _isAutonomous = true;
        private RoamingPace _roamingPace = RoamingPace.Chill; // Default: Chill & Relaxed
        private int _idleFramesRemaining = 200; // 8+ seconds of calm start
        private int _runFramesRemaining = 0;
        private double _targetX = 0;
        private double _targetY = 0;
        private double _moveSpeed = 3.6;
        private bool _facingLeft = false;

        // 👁️ 20-20-20 Eye Health Care Timer & State
        private readonly DispatcherTimer _eyeHealthTimer = new();
        private int _eyeHealthIntervalMinutes = 20; // Default: 20 minutes (20-20-20 rule)
        private int _eyeHealthSecondsRemaining = 20 * 60;
        private bool _eyeHealthEnabled = true;
        private bool _isEyeBreakActive = false;
        private int _eyeBreakSecondsLeft = 20;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            KeyDown += MainWindow_PreviewKeyDown;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Guarantee pet starts clearly visible in bottom-right corner of user's primary screen
                Left = SystemParameters.WorkArea.Right - Width - 60;
                Top = SystemParameters.WorkArea.Bottom - Height - 40;

                _currentPosX = Left;
                _currentPosY = Top;

                LoadAnimationConfig();
                SetupTimer();
                SetupEyeHealthTimer();

                string startChar = _characters.ContainsKey("hooded-blade") ? "hooded-blade" : (_characters.Keys.FirstOrDefault() ?? "robot");
                SwitchCharacter(startChar);

                Focus();
                Keyboard.Focus(this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MainWindow_Loaded: {ex}");
            }
        }

        private List<Rect> GetConnectedScreens()
        {
            var screens = new List<Rect>();

            // 1. Primary screen in WPF DIP coordinates (guaranteed exact)
            var primaryWork = new Rect(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Top,
                                       SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);
            screens.Add(primaryWork);

            // 2. Multi-monitor support across extended screens using WPF VirtualScreen coordinates
            double vLeft = SystemParameters.VirtualScreenLeft;
            double vWidth = SystemParameters.VirtualScreenWidth;
            double vTop = SystemParameters.VirtualScreenTop;

            // Secondary screen to the left (e.g. DISPLAY5 at X < 0)
            if (vLeft < -50)
            {
                double leftWidth = Math.Abs(vLeft);
                screens.Add(new Rect(vLeft, vTop, leftWidth, primaryWork.Height));
            }

            // Secondary screen to the right (if present)
            double rightEdge = vLeft + vWidth;
            if (rightEdge > primaryWork.Right + 50)
            {
                screens.Add(new Rect(primaryWork.Right, vTop, rightEdge - primaryWork.Right, primaryWork.Height));
            }

            return screens;
        }

        #region Eye Health 20-20-20 System

        private void SetupEyeHealthTimer()
        {
            _eyeHealthSecondsRemaining = _eyeHealthIntervalMinutes * 60;
            _eyeHealthTimer.Interval = TimeSpan.FromSeconds(1);
            _eyeHealthTimer.Tick += EyeHealthTimer_Tick;
            _eyeHealthTimer.Start();
        }

        private void EyeHealthTimer_Tick(object? sender, EventArgs e)
        {
            if (!_eyeHealthEnabled) return;

            if (_isEyeBreakActive)
            {
                _eyeBreakSecondsLeft--;
                if (_eyeBreakSecondsLeft > 0)
                {
                    EyeHealthProgress.Value = _eyeBreakSecondsLeft;
                    EyeHealthCountdown.Text = $"Looking 20ft away... {_eyeBreakSecondsLeft}s";
                }
                else
                {
                    EyeHealthProgress.Value = 0;
                    EyeHealthCountdown.Text = "Eyes refreshed! Keep up the good work ✨";
                    DismissEyeHealthAlert(delayedFade: true);
                }
                return;
            }

            _eyeHealthSecondsRemaining--;
            if (_eyeHealthSecondsRemaining <= 0)
            {
                ShowEyeHealthAlert();
            }
        }

        public void ShowEyeHealthAlert()
        {
            _isEyeBreakActive = true;
            _eyeBreakSecondsLeft = 20;

            bool isBlade = (_activeCharacter?.id == "hooded-blade");

            // Character-specific personality dialogues for eye health
            if (isBlade)
            {
                EyeHealthBubble.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                EyeHealthHeader.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                EyeHealthHeader.Text = "🥷 Blade Master: Eye Care Stance";
                EyeHealthMessage.Text = "20 minutes of focus have passed! Rest your warrior vision — gaze at an object 20 feet into the horizon for 20 seconds to keep your sight razor sharp! 🍃";
                EyeHealthProgress.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            }
            else
            {
                EyeHealthBubble.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
                EyeHealthHeader.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
                EyeHealthHeader.Text = "🤖 Robot: Optical Health Protocol";
                EyeHealthMessage.Text = "20 minutes of screen time detected! Optical sensors recommend resting your human eyes: look 20 feet away for 20 seconds. 🔭";
                EyeHealthProgress.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
            }

            EyeHealthProgress.Maximum = 20;
            EyeHealthProgress.Value = 20;
            EyeHealthCountdown.Text = "Looking 20ft away... 20s";

            // Make bubble visible and smoothly fade in
            EyeHealthBubble.Visibility = Visibility.Visible;
            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(350));
            EyeHealthBubble.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Play gentle system chime
            try
            {
                SystemSounds.Asterisk.Play();
            }
            catch { }

            // Bring pet to calm idle stance during break
            PlayAnimation("idle");
        }

        public void DismissEyeHealthAlert(bool delayedFade = false)
        {
            _isEyeBreakActive = false;
            _eyeHealthSecondsRemaining = _eyeHealthIntervalMinutes * 60; // Reset next interval countdown

            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(delayedFade ? 1200 : 300));
            if (delayedFade)
            {
                fadeOut.BeginTime = TimeSpan.FromSeconds(1.5);
            }
            fadeOut.Completed += (s, e) =>
            {
                if (!_isEyeBreakActive)
                {
                    EyeHealthBubble.Visibility = Visibility.Collapsed;
                }
            };
            EyeHealthBubble.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void EyeHealthBubble_Click(object sender, MouseButtonEventArgs e)
        {
            DismissEyeHealthAlert(delayedFade: false);
        }

        private void MenuEyeHealthToggle_Click(object sender, RoutedEventArgs e)
        {
            _eyeHealthEnabled = MenuItemEyeHealthEnabled.IsChecked;
            if (!_eyeHealthEnabled && _isEyeBreakActive)
            {
                DismissEyeHealthAlert(delayedFade: false);
            }
        }

        private void MenuEyeHealthTest_Click(object sender, RoutedEventArgs e)
        {
            ShowEyeHealthAlert();
        }

        private void MenuEyeInterval20_Click(object sender, RoutedEventArgs e) => SetEyeInterval(20, MenuItemEyeInterval20);
        private void MenuEyeInterval30_Click(object sender, RoutedEventArgs e) => SetEyeInterval(30, MenuItemEyeInterval30);
        private void MenuEyeInterval45_Click(object sender, RoutedEventArgs e) => SetEyeInterval(45, MenuItemEyeInterval45);

        private void SetEyeInterval(int minutes, System.Windows.Controls.MenuItem selectedItem)
        {
            _eyeHealthIntervalMinutes = minutes;
            _eyeHealthSecondsRemaining = minutes * 60;
            MenuItemEyeInterval20.IsChecked = (selectedItem == MenuItemEyeInterval20);
            MenuItemEyeInterval30.IsChecked = (selectedItem == MenuItemEyeInterval30);
            MenuItemEyeInterval45.IsChecked = (selectedItem == MenuItemEyeInterval45);
        }

        #endregion

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            HandleKey(e.Key);
        }

        private void HandleKey(Key key)
        {
            if (key == Key.C)
            {
                string nextChar = (_activeCharacter?.id == "hooded-blade") ? "robot" : "hooded-blade";
                SwitchCharacter(nextChar);
            }
            else if (key == Key.M)
            {
                JumpToNextScreen();
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
                StartNewRunTarget(forceLongDistance: true);
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

            Title = isBlade ? "Desktop Pet - Hooded Blade (Sword Master & Eye Care)" : "Desktop Pet - Robot Pet & Eye Care";

            RootGrid.ToolTip = isBlade
                ? "Hooded Blade: Click for Sword Power! Double-click to switch to Robot. Press 'M' to switch screens. 20-20-20 Eye health enabled."
                : "Robot Pet: Click to Kick! Double-click to switch to Hooded Blade. Press 'M' to switch screens. 20-20-20 Eye health enabled.";

            _idleFramesRemaining = _rng.Next(150, 300); // 6 to 12 seconds of peaceful idle
            PlayAnimation(_activeCharacter.defaultAnimation);
        }

        private void LoadAnimationConfig()
        {
            var candidatePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "animation-config.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "animation-config.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "animation-config.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "animation-config.json")
            };

            string configPath = string.Empty;
            string json = string.Empty;

            foreach (var path in candidatePaths)
            {
                var full = Path.GetFullPath(path);
                if (File.Exists(full))
                {
                    configPath = full;
                    _baseDirectory = Path.GetDirectoryName(configPath)!;
                    json = File.ReadAllText(configPath);
                    break;
                }
            }

            // Fallback to embedded assembly resource if not found on disk (e.g. running standalone single .exe)
            if (string.IsNullOrEmpty(json))
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using var stream = asm.GetManifestResourceStream("animation-config.json")
                                ?? asm.GetManifestResourceStream("DesktopPet.animation-config.json");
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    json = reader.ReadToEnd();
                }
            }

            if (string.IsNullOrEmpty(json))
            {
                MessageBox.Show("Could not find animation-config.json.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _config = JsonSerializer.Deserialize<AppConfig>(json);

            if (_config == null) return;

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
            var asm = System.Reflection.Assembly.GetExecutingAssembly();

            foreach (var (_, anim) in character.animations)
            {
                string framesDir = string.IsNullOrEmpty(_baseDirectory) ? "" : Path.Combine(_baseDirectory, anim.framesDir);
                bool loadedFromDisk = false;

                if (!string.IsNullOrEmpty(framesDir) && Directory.Exists(framesDir))
                {
                    var files = Directory.GetFiles(framesDir, "frame_*.png").OrderBy(f => f).ToList();
                    if (files.Count > 0)
                    {
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
                        loadedFromDisk = true;
                    }
                }

                // Fallback to embedded assembly resources if not found on disk
                if (!loadedFromDisk)
                {
                    anim.CachedBitmaps = new List<BitmapImage>();
                    string normalizedDir = anim.framesDir.Replace('\\', '/').Trim('/');

                    for (int i = 0; i < anim.totalFrames; i++)
                    {
                        string frameFileName = $"frame_{i:D3}.png";
                        string resPath = $"{normalizedDir}/{frameFileName}";

                        var stream = asm.GetManifestResourceStream(resPath)
                                  ?? asm.GetManifestResourceStream($"DesktopPet.{resPath}");

                        if (stream != null)
                        {
                            using (stream)
                            {
                                var bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.StreamSource = stream;
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                                bitmap.EndInit();
                                bitmap.Freeze();
                                anim.CachedBitmaps.Add(bitmap);
                            }
                        }
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

            // Pause autonomous roaming during active eye breaks so pet stays calmly resting
            if (_isAutonomous && !_userIsDragging && !_isEyeBreakActive)
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
                    // Action finished: rest peacefully for 6 to 14 seconds
                    string next = _currentAnimation.nextAnimation ?? "idle";
                    _idleFramesRemaining = _rng.Next(140, 320);
                    PlayAnimation(next);
                }
            }
        }

        private void HandleAutonomousBehavior()
        {
            string current = _currentAnimation?.name ?? "idle";
            bool isBlade = (_activeCharacter?.id == "hooded-blade");

            if (current == "run")
            {
                double dx = _targetX - _currentPosX;
                double dy = _targetY - _currentPosY;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist > 10 && _runFramesRemaining > 0)
                {
                    _runFramesRemaining--;

                    double speed = Math.Min(dist * 0.08 + 2.8, _moveSpeed);
                    double stepX = (dx / dist) * speed;
                    double stepY = (dy / dist) * speed;

                    if (stepX < -0.3) SetFacing(true);
                    else if (stepX > 0.3) SetFacing(false);

                    _currentPosX += stepX;
                    _currentPosY += stepY;

                    double vLeft = SystemParameters.VirtualScreenLeft;
                    double vRight = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width;
                    double vTop = SystemParameters.VirtualScreenTop;
                    double vBottom = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height;

                    _currentPosX = Math.Clamp(_currentPosX, vLeft, vRight);
                    _currentPosY = Math.Clamp(_currentPosY, vTop, vBottom);

                    Left = _currentPosX;
                    Top = _currentPosY;
                }
                else
                {
                    // Arrived at destination: rest peacefully or do an occasional move
                    int roll = _rng.Next(100);
                    if (isBlade)
                    {
                        if (roll < 30) PlayAnimation("sword-power");
                        else if (roll < 55) PlayAnimation("attack");
                        else
                        {
                            // Stay idle and rest for 7 to 16 seconds
                            _idleFramesRemaining = _rng.Next(160, 380);
                            PlayAnimation("idle");
                        }
                    }
                    else
                    {
                        if (roll < 30) PlayAnimation("kick");
                        else if (roll < 55) PlayAnimation("punch");
                        else
                        {
                            _idleFramesRemaining = _rng.Next(160, 380);
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
                    if (_roamingPace == RoamingPace.Still)
                    {
                        // In Still mode: never runs, only rests or does occasional stationary action
                        int stillRoll = _rng.Next(100);
                        if (stillRoll < 20)
                        {
                            SetFacing(_rng.Next(2) == 0);
                            PlayAnimation(isBlade ? "sword-power" : "kick");
                        }
                        else
                        {
                            _idleFramesRemaining = _rng.Next(200, 450);
                        }
                        return;
                    }

                    int roll = _rng.Next(100);
                    int runThreshold = (_roamingPace == RoamingPace.Active) ? 40 : 18; // Chill mode: 18% run chance

                    if (roll < runThreshold)
                    {
                        StartNewRunTarget(forceLongDistance: false);
                        PlayAnimation("run");
                    }
                    else if (roll < runThreshold + 40)
                    {
                        // 40% chance: Continue resting peacefully in idle!
                        _idleFramesRemaining = _rng.Next(140, 300); // Another 6-12 seconds of calm
                    }
                    else if (isBlade)
                    {
                        // Occasional cool move in place
                        SetFacing(_rng.Next(2) == 0);
                        int moveRoll = _rng.Next(100);
                        if (moveRoll < 55) PlayAnimation("sword-power");
                        else if (moveRoll < 85) PlayAnimation("attack");
                        else PlayAnimation("jump");
                    }
                    else
                    {
                        SetFacing(_rng.Next(2) == 0);
                        if (_rng.Next(100) < 60) PlayAnimation("kick");
                        else PlayAnimation("punch");
                    }
                }
            }
        }

        private void StartNewRunTarget(bool forceLongDistance = false)
        {
            var screens = GetConnectedScreens();
            var petCenter = new Point(Left + Width / 2, Top + Height / 2);
            var currentScreen = screens.FirstOrDefault(s => s.Contains(petCenter));
            if (currentScreen == default) currentScreen = screens[0];

            Rect targetScreen;

            // In Chill mode: 80% strolls nearby on current screen, 20% cross-monitor
            bool crossMonitor = (screens.Count > 1) && (forceLongDistance || (_rng.Next(100) < (_roamingPace == RoamingPace.Active ? 50 : 20)));

            if (crossMonitor)
            {
                var otherScreens = screens.Where(s => s != currentScreen).ToList();
                targetScreen = otherScreens[_rng.Next(otherScreens.Count)];

                double minX = targetScreen.Left + 30;
                double maxX = Math.Max(minX, targetScreen.Right - Width - 30);
                double minY = targetScreen.Top + targetScreen.Height * 0.20;
                double maxY = Math.Max(minY, targetScreen.Bottom - Height - 30);

                _targetX = _rng.NextDouble() * (maxX - minX) + minX;
                _targetY = _rng.NextDouble() * (maxY - minY) + minY;
            }
            else
            {
                // Local stroll on current screen (80 to 240 pixels away)
                targetScreen = currentScreen;
                double strollOffsetX = (_rng.NextDouble() * 200 + 70) * (_rng.Next(2) == 0 ? 1 : -1);
                double strollOffsetY = (_rng.NextDouble() * 80 - 40);

                double candX = Left + strollOffsetX;
                double candY = Top + strollOffsetY;

                double minX = targetScreen.Left + 25;
                double maxX = Math.Max(minX, targetScreen.Right - Width - 25);
                double minY = targetScreen.Top + targetScreen.Height * 0.15;
                double maxY = Math.Max(minY, targetScreen.Bottom - Height - 25);

                _targetX = Math.Clamp(candX, minX, maxX);
                _targetY = Math.Clamp(candY, minY, maxY);
            }

            _currentPosX = Left;
            _currentPosY = Top;

            double dx = _targetX - _currentPosX;
            double dy = _targetY - _currentPosY;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            // Relaxed, natural running speed
            _moveSpeed = (_roamingPace == RoamingPace.Active) ? (_rng.NextDouble() * 2.0 + 4.8) : (_rng.NextDouble() * 1.2 + 3.2);
            _runFramesRemaining = Math.Max(40, (int)(dist / _moveSpeed) + 20);
        }

        public void JumpToNextScreen()
        {
            var screens = GetConnectedScreens();
            if (screens.Count <= 1) return;

            var petCenter = new Point(Left + Width / 2, Top + Height / 2);
            var currentIdx = screens.FindIndex(s => s.Contains(petCenter));
            if (currentIdx < 0) currentIdx = 0;

            int nextIdx = (currentIdx + 1) % screens.Count;
            var nextScreen = screens[nextIdx];

            _currentPosX = nextScreen.Left + (nextScreen.Width - Width) / 2;
            _currentPosY = nextScreen.Bottom - Height - 60;
            Left = _currentPosX;
            Top = _currentPosY;
            _idleFramesRemaining = _rng.Next(150, 300);
            PlayAnimation("idle");
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
                _currentPosX = Left;
                _currentPosY = Top;
            }
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                string nextChar = (_activeCharacter?.id == "hooded-blade") ? "robot" : "hooded-blade";
                SwitchCharacter(nextChar);
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _userIsDragging = false;
            _currentPosX = Left;
            _currentPosY = Top;
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

        private void MenuPaceCalm_Click(object sender, RoutedEventArgs e) => SetPace(RoamingPace.Chill);
        private void MenuPaceActive_Click(object sender, RoutedEventArgs e) => SetPace(RoamingPace.Active);
        private void MenuPaceStill_Click(object sender, RoutedEventArgs e) => SetPace(RoamingPace.Still);

        private void SetPace(RoamingPace pace)
        {
            _roamingPace = pace;
            MenuItemPaceCalm.IsChecked = (pace == RoamingPace.Chill);
            MenuItemPaceActive.IsChecked = (pace == RoamingPace.Active);
            MenuItemPaceStill.IsChecked = (pace == RoamingPace.Still);

            if (pace == RoamingPace.Still && _currentAnimation?.name == "run")
            {
                PlayAnimation("idle");
            }
            _idleFramesRemaining = (pace == RoamingPace.Chill) ? 200 : 80;
        }

        private void MenuNextScreen_Click(object sender, RoutedEventArgs e) => JumpToNextScreen();

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

        private void SetScale(double petW, double petH, System.Windows.Controls.MenuItem selectedItem)
        {
            PetImage.Width = petW;
            PetImage.Height = petH;
            Width = petW + 64;
            Height = petH + 104;

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
            var screens = GetConnectedScreens();
            var primary = screens[0];

            _currentPosX = primary.Left + (primary.Width - Width) / 2;
            _currentPosY = primary.Top + (primary.Height - Height) / 2;
            Left = _currentPosX;
            Top = _currentPosY;
            _idleFramesRemaining = _rng.Next(150, 300);
            PlayAnimation("idle");
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
