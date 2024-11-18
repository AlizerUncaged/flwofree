using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace FlowFree;

public partial class PasswordHackingGame : UserControl
{
    private readonly string[] passwords =
    {
        "debug404",
        "reboot23",
        "fixcore",
        "patch99",
        "ai_heal",
        "aurora1",
        "systemrestore"
    };

    private int currentPasswordIndex = 0;
    private readonly Random random = new Random();
    private DispatcherTimer glitchTimer;

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
        AddTerminalLine("Access denied. Invalid password.", "#FF0000");

        // Add error animation
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
            Margin = new Thickness(0, 5, 0, 5)
        };
        TerminalOutput.Children.Add(textBlock);
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

    private async void ShowCompletionSequence()
    {
        glitchTimer.Stop();

        AddTerminalLine("All passwords accepted!", "#00FF00");
        AddTerminalLine("Initiating final system restoration...", "#00FFFF");

        // Add completion animation
        await Task.Delay(1000);

        for (int i = 0; i < 5; i++)
        {
            AddTerminalLine($"{'█'.ToString().PadRight(random.Next(20, 50), '█')}", "#00FF00");
            await Task.Delay(200);
        }

        AddTerminalLine("AURORA SYSTEM RESTORED SUCCESSFULLY", "#00FF00");

        await Task.Delay(1500);
        GameCompleted?.Invoke(this, EventArgs.Empty);
    }
}