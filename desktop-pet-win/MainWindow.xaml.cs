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
        public int row { get; set; }
        public int col { get; set; }
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

    public class AppConfig
    {
        public string version { get; set; } = string.Empty;
        public string character { get; set; } = string.Empty;
        public string defaultAnimation { get; set; } = "idle";
        public Dictionary<string, AnimationData> animations { get; set; } = new();
    }

    public partial class MainWindow : Window
    {
        private AppConfig? _config;
        private readonly Dictionary<string, AnimationData> _animations = new();
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
            KeyDown += MainWindow_KeyDown;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 80;
            Top = workArea.Bottom - Height - 80;

            LoadAnimationConfig();
            SetupTimer();

            // Start in idle and pick first autonomous action after short delay
            _idleFramesRemaining = _rng.Next(30, 70);
            PlayAnimation("idle");
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space || e.Key == Key.K)
            {
                TriggerManualAction("kick");
            }
            else if (e.Key == Key.P)
            {
                TriggerManualAction("punch");
            }
            else if (e.Key == Key.R)
            {
                TriggerManualAction("run");
            }
            else if (e.Key == Key.I)
            {
                TriggerManualAction("idle");
            }
            else if (e.Key == Key.Escape)
            {
                Close();
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

            if (_config?.animations == null) return;

            foreach (var kvp in _config.animations)
            {
                var anim = kvp.Value;
                anim.name = kvp.Key;

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

                _animations[kvp.Key] = anim;
            }
        }

        private void SetupTimer()
        {
            _timer.Tick += Timer_Tick;
        }

        public void PlayAnimation(string name, double? customFps = null)
        {
            if (!_animations.TryGetValue(name, out var anim))
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

            // Handle Autonomous movement & state changes if autonomous mode is ON
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
                // Reached end of animation sequence
                if (_currentAnimation.loop)
                {
                    _sequenceIndex = 0;
                    RenderCurrentFrame();
                }
                else
                {
                    // Action completed (e.g. kick, punch)
                    // After completing a kick or punch, transition back to idle
                    // and schedule next autonomous action
                    string next = _currentAnimation.nextAnimation ?? "idle";
                    _idleFramesRemaining = _rng.Next(30, 80); // 1.2 - 3.3 seconds of idle before next action
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

            if (current == "run")
            {
                // Move robot towards target location
                double dx = _targetX - Left;
                double dy = _targetY - Top;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist > 10 && _runFramesRemaining > 0)
                {
                    _runFramesRemaining--;

                    // Step towards target
                    double stepX = Math.Sign(dx) * Math.Min(Math.Abs(dx), _moveSpeedX);
                    double stepY = Math.Sign(dy) * Math.Min(Math.Abs(dy), _moveSpeedY);

                    // Face the movement direction
                    if (stepX < -0.5) SetFacing(true);   // Face left
                    else if (stepX > 0.5) SetFacing(false); // Face right

                    Left = Math.Clamp(Left + stepX, minX, maxX);
                    Top = Math.Clamp(Top + stepY, minY, maxY);
                }
                else
                {
                    // Finished running to destination! Pick a random follow-up action:
                    // Kick (40%), Punch (40%), or Rest/Idle (20%)
                    int roll = _rng.Next(100);
                    if (roll < 40)
                    {
                        PlayAnimation("kick");
                    }
                    else if (roll < 80)
                    {
                        PlayAnimation("punch");
                    }
                    else
                    {
                        _idleFramesRemaining = _rng.Next(40, 90);
                        PlayAnimation("idle");
                    }
                }
            }
            else if (current == "idle")
            {
                _idleFramesRemaining--;
                if (_idleFramesRemaining <= 0)
                {
                    // Idle time expired! Choose next random action:
                    // 55% Run to new random place on screen
                    // 25% Random Kick
                    // 20% Random Punch
                    int roll = _rng.Next(100);
                    if (roll < 55)
                    {
                        StartNewRunTarget();
                        PlayAnimation("run");
                    }
                    else if (roll < 80)
                    {
                        // Randomly choose facing direction for kick
                        SetFacing(_rng.Next(2) == 0);
                        PlayAnimation("kick");
                    }
                    else
                    {
                        // Randomly choose facing direction for punch
                        SetFacing(_rng.Next(2) == 0);
                        PlayAnimation("punch");
                    }
                }
            }
        }

        private void StartNewRunTarget()
        {
            var workArea = SystemParameters.WorkArea;
            double minX = 20;
            double maxX = Math.Max(20, workArea.Width - Width - 20);
            double minY = Math.Max(20, workArea.Height * 0.2); // Wander across bottom 80% of screen
            double maxY = Math.Max(20, workArea.Height - Height - 20);

            // Pick a random location on screen
            _targetX = _rng.NextDouble() * (maxX - minX) + minX;
            _targetY = _rng.NextDouble() * (maxY - minY) + minY;

            // Randomize running speed slightly for natural movement
            _moveSpeedX = _rng.NextDouble() * 3.5 + 4.5; // 4.5 to 8 px per frame
            _moveSpeedY = _rng.NextDouble() * 2.0 + 1.0; // 1 to 3 px per frame

            _runFramesRemaining = _rng.Next(45, 120); // 2 to 5 seconds of running
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
            if (e.ChangedButton == MouseButton.Left)
            {
                _userIsDragging = true;
                _dragStartPoint = e.GetPosition(this);
                DragMove();
                _userIsDragging = false;
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _userIsDragging = false;
            var endPoint = e.GetPosition(this);
            double dist = (endPoint - _dragStartPoint).Length;

            // If it was a quick click without dragging, trigger kick action!
            if (dist < 6)
            {
                PlayAnimation("kick");
            }
        }

        // Context Menu Event Handlers
        private void MenuAutonomous_Click(object sender, RoutedEventArgs e)
        {
            _isAutonomous = MenuItemAutonomous.IsChecked;
            if (!_isAutonomous && _currentAnimation?.name == "run")
            {
                PlayAnimation("idle");
            }
        }

        private void MenuKick_Click(object sender, RoutedEventArgs e) => TriggerManualAction("kick");
        private void MenuPunch_Click(object sender, RoutedEventArgs e) => TriggerManualAction("punch");
        private void MenuRun_Click(object sender, RoutedEventArgs e) => TriggerManualAction("run");
        private void MenuIdle_Click(object sender, RoutedEventArgs e) => TriggerManualAction("idle");

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

        private void MenuScaleSmall_Click(object sender, RoutedEventArgs e)
        {
            SetScale(256, 192, MenuItemScaleSmall);
        }

        private void MenuScaleMedium_Click(object sender, RoutedEventArgs e)
        {
            SetScale(384, 288, MenuItemScaleMedium);
        }

        private void MenuScaleOriginal_Click(object sender, RoutedEventArgs e)
        {
            SetScale(512, 384, MenuItemScaleOriginal);
        }

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
