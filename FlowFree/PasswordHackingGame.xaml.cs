using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace FlowFree;

public partial class PasswordHackingGame : UserControl
{
    private readonly string[] passwords =
    {
        "neural404",
        "matrix23",
        "synccore",
        "pulse99",
        "ai_heal",
        "aurora7",
        "fullrestore"
    };

    private int currentPasswordIndex = 0;
    private int wrongAttempts = 0;
    private bool jumpscareTriggered = false;
    private readonly Random random = new Random();
    private DispatcherTimer glitchTimer;

    private bool hasJumpscareOccurred = false;

    public event EventHandler? GameCompleted;

    public PasswordHackingGame()
    {
        InitializeComponent();
        InitializeGame();
    }

    private void InitializeGame()
    {
        PasswordInput.KeyDown += PasswordInput_KeyDown;

        // Initialize glitch effect timer
        glitchTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        glitchTimer.Tick += GlitchTimer_Tick;
        glitchTimer.Start();

        // Focus the password input
        Loaded += (s, e) => PasswordInput.Focus();
    }

    private void PasswordInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            string attempt = PasswordInput.Text.Trim();
            PasswordInput.Clear();

            // Add attempt to terminal output
            AddTerminalLine($"Attempting password: {attempt}", "#00FF00");

            if (attempt.Equals(passwords[currentPasswordIndex], StringComparison.OrdinalIgnoreCase))
            {
                HandleCorrectPassword();
            }
            else
            {
                HandleIncorrectPassword(attempt);
            }

            // Scroll to bottom
            OutputScroller.ScrollToBottom();
        }
    }

    private void HandleCorrectPassword()
    {
        currentPasswordIndex++;

        // Show success message with typing effect
        AddTerminalLine("Password accepted!", "#00FF00");
        AddTerminalLine($"System recovery progress: {(currentPasswordIndex * 100 / passwords.Length)}%", "#00FFFF");

        if (currentPasswordIndex >= passwords.Length)
        {
            // Game completed
            ShowCompletionSequence();
        }
        else
        {
            AddTerminalLine($"Enter password {currentPasswordIndex + 1}/{passwords.Length}", "#00FF00");
        }

        // Add success animation
        var scaleTransform = new ScaleTransform(1, 1);
        TerminalOutput.RenderTransform = scaleTransform;
        var animation = new DoubleAnimation(1, 1.02, TimeSpan.FromMilliseconds(200))
        {
            AutoReverse = true
        };
        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
    }

    private void HandleIncorrectPassword(string attempt)
    {
        wrongAttempts++;
        AddTerminalLine("Access denied. Invalid password.", "#FF0000");


        if (!hasJumpscareOccurred)
        {
            hasJumpscareOccurred = true;
            PlayJumpscare();
        }

        var translateTransform = new TranslateTransform();
        PasswordInput.RenderTransform = translateTransform;

        var shakeAnimation = new DoubleAnimation
        {
            From = -5,
            To = 5,
            Duration = TimeSpan.FromMilliseconds(50),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(3)
        };
        translateTransform.BeginAnimation(TranslateTransform.XProperty, shakeAnimation);
    }

    private void AddTerminalLine(string text, string colorHex)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 16,
            Margin = new Thickness(0, 5, 0, 5)
        };
        TerminalOutput.Children.Add(textBlock);
    }


    private void PlayJumpscare()
    {
        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            WindowState = WindowState.Maximized,
            Topmost = true,
            ShowInTaskbar = false, Background = Brushes.Black
        };

        var player = new MediaElement
        {
            Source = new Uri("face.mp4", UriKind.Relative),
            LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Stop,
            Stretch = Stretch.Uniform, IsMuted = false, Volume = 1
        };

        player.MediaEnded += (s, e) =>
        {
            window.Close();
            player.Close();
        };

        window.Content = player;
        window.Show();
        player.Play();
    }

    private void GlitchTimer_Tick(object? sender, EventArgs e)
    {
        // Random glitch effect
        if (random.Next(100) < 30) // 30% chance of glitch
        {
            var glitchAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0.8,
                Duration = TimeSpan.FromMilliseconds(50),
                AutoReverse = true
            };
            this.BeginAnimation(OpacityProperty, glitchAnimation);
        }
    }

    private void ShowCompletionSequence()
    {
        //  glitchTimer.Stop();
        AddTerminalLine("All passwords accepted!", "#00FF00");
        AddTerminalLine("Initiating final system restoration...", "#00FFFF");

        var gameOverWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            WindowState = WindowState.Maximized,
            Topmost = true,
            ShowInTaskbar = false,
            Background = new SolidColorBrush(Color.FromRgb(0, 0, 0))
        };

        var mainGrid = new Grid();
        var matrixBg = new MatrixBackground { Opacity = 0.5 };

        var content = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var gameOverText = new TextBlock
        {
            Text = "SYSTEM RESTORED SUCCESSFULLY",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 72,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 0),
                BlurRadius = 30,
                ShadowDepth = 0
            }
        };

        var statsPanel = new StackPanel { Margin = new Thickness(0, 30, 0, 0) };
        var statsText = new TextBlock
        {
            Text =
                $"System Recovery: 100%\nPasswords Cracked: {passwords.Length}\nAttempts: {wrongAttempts + passwords.Length}",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 24,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
            TextAlignment = TextAlignment.Center,
            LineHeight = 36
        };

        var restartButton = new Button
        {
            Content = "START NEW SESSION",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 20,
            Margin = new Thickness(0, 50, 0, 0),
            Padding = new Thickness(20, 10, 20, 10),
            Background = new SolidColorBrush(Color.FromRgb(0, 50, 0)),
            Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
            BorderThickness = new Thickness(2),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 0),
                BlurRadius = 15,
                ShadowDepth = 0
            }
        };

        // Glitch animation for "GAME OVER"
        var glitchTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };

        glitchTimer.Tick += (s, e) =>
        {
            if (random.Next(100) < 30)
            {
                var glitch = new DoubleAnimation(1, 0.7, TimeSpan.FromMilliseconds(50))
                {
                    AutoReverse = true
                };
                gameOverText.BeginAnimation(OpacityProperty, glitch);

                var shake = new DoubleAnimation(-5, 5, TimeSpan.FromMilliseconds(50))
                {
                    AutoReverse = true,
                    RepeatBehavior = new RepeatBehavior(2)
                };
                var transform = new TranslateTransform();
                gameOverText.RenderTransform = transform;
                transform.BeginAnimation(TranslateTransform.XProperty, shake);
            }
        };

        restartButton.Click += (s, e) =>
        {
            gameOverWindow.Close();
            GameCompleted?.Invoke(this, EventArgs.Empty);
        };

        statsPanel.Children.Add(statsText);
        content.Children.Add(gameOverText);
        content.Children.Add(statsPanel);
        content.Children.Add(restartButton);

        mainGrid.Children.Add(matrixBg);
        mainGrid.Children.Add(content);
        gameOverWindow.Content = mainGrid;

        gameOverWindow.Loaded += (s, e) =>
        {
            glitchTimer.Start();
            if (!hasJumpscareOccurred)
            {
                hasJumpscareOccurred = true;
                PlayJumpscare();
            }
        };

        gameOverWindow.Show();
    }
}