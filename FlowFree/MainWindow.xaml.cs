using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FlowFree;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly int[,] gameBoard = new int[5, 5];
    private readonly List<Point> currentPath = new List<Point>();
    private PasswordHackingGame? hackingGame;
    private Point? lastCell = null;
    private int currentColorIndex = 0;
    private bool isDrawing = false;
    private Button? capturedButton = null;
    private System.Windows.Threading.DispatcherTimer mouseTrackTimer;
    private readonly Border gameAreaBorder;
    private Point startEndpoint;

    private Point targetEndpoint;

    private readonly List<int[][]> possibleEndpoints = new List<int[][]>
    {
        // Layout 1 (New winnable layout):
        // 00000
        // 0Y0B0
        // 00B00
        // 00R0G
        // RYG00
        new int[][]
        {
            new int[] { 1, 3, 2, 2 }, // B endpoints
            new int[] { 3, 4, 4, 2 }, // G endpoints
            new int[] { 3, 2, 4, 0 }, // R endpoints
            new int[] { 1, 1, 4, 1 } // Y endpoints
        },

        // Layout 2
        new int[][]
        {
            new int[] { 2, 1, 4, 4 }, // B endpoints
            new int[] { 2, 4, 4, 1 }, // G endpoints
            new int[] { 0, 0, 0, 4 }, // R endpoints
            new int[] { 2, 2, 3, 4 } // Y endpoints
        },

        // Layout 3
        new int[][]
        {
            new int[] { 1, 0, 3, 1 }, // B endpoints
            new int[] { 1, 1, 1, 3 }, // G endpoints
            new int[] { 2, 0, 3, 3 }, // R endpoints
            new int[] { 3, 4, 4, 3 } // Y endpoints
        },

        // Layout 4
        new int[][]
        {
            new int[] { 3, 1, 4, 0 }, // B endpoints
            new int[] { 1, 1, 4, 2 }, // G endpoints
            new int[] { 1, 2, 3, 3 }, // R endpoints
            new int[] { 1, 4, 3, 0 } // Y endpoints
        },

        // Layout 5
        new int[][]
        {
            new int[] { 0, 4, 1, 0 }, // B endpoints
            new int[] { 0, 2, 2, 2 }, // G endpoints
            new int[] { 0, 0, 3, 2 }, // R endpoints
            new int[] { 0, 3, 3, 3 } // Y endpoints
        },

        // Layout 6
        new int[][]
        {
            new int[] { 1, 1, 1, 3 }, // B endpoints
            new int[] { 2, 3, 3, 0 }, // G endpoints
            new int[] { 1, 0, 4, 2 }, // R endpoints
            new int[] { 3, 3, 4, 0 } // Y endpoints
        }
    };

    private int[][] currentEndpoints;

    private Color[] colors = new Color[]
    {
        Color.FromRgb(65, 105, 255), // Blue
        Color.FromRgb(50, 205, 50), // Green
        Color.FromRgb(255, 65, 65), // Red
        Color.FromRgb(255, 215, 0) // Yellow
    };

    private string[] colorNames = { "Blue", "Green", "Red", "Yellow" };
    private readonly Dictionary<int, List<Point>> completedPaths = new Dictionary<int, List<Point>>();
    private readonly Dictionary<Point, Button> buttonGrid = new Dictionary<Point, Button>();

    private readonly List<Button> highlightedButtons = new List<Button>();
    private readonly Dictionary<Point, ScaleTransform> endpointTransforms = new Dictionary<Point, ScaleTransform>();

    // Method to validate if clicked endpoint matches current color
    private bool ValidateEndpointColor(Point point)
    {
        int row = (int)point.X;
        int col = (int)point.Y;
        return gameBoard[row, col] == currentColorIndex + 1;
    }

    public MainWindow()
    {
        InitializeComponent();

        // Store reference to game area border
        gameAreaBorder = (Border)((Grid)Content).Children[1];

        InitializeMouseTracker();
        InitializeGame();
        UpdateStatus();
        KeyDown += (s, e) =>
        {
            if (e.Key == Key.F11)
            {
                if (WindowStyle == WindowStyle.None && WindowState == WindowState.Maximized)
                {
                    WindowStyle = WindowStyle.SingleBorderWindow;
                    WindowState = WindowState.Normal;
                    ResizeMode = ResizeMode.CanResize;
                    Topmost = false;
                }
                else
                {
                    ResizeMode = ResizeMode.NoResize;
                    Topmost = true;

                    WindowStyle = WindowStyle.None;

                    WindowState = WindowState.Maximized;

                    Activate();
                }
            }
        };
    }

    private void HighlightPossibleMoves(Point currentPos)
    {
        ClearHighlights();

        int row = (int)currentPos.X;
        int col = (int)currentPos.Y;

        // Check all four directions
        Point[] directions = new[]
        {
            new Point(row - 1, col), // Up
            new Point(row + 1, col), // Down
            new Point(row, col - 1), // Left
            new Point(row, col + 1) // Right
        };

        foreach (var point in directions)
        {
            if (point.X >= 0 && point.X < 5 && point.Y >= 0 && point.Y < 5)
            {
                if (buttonGrid.TryGetValue(point, out Button? button))
                {
                    // Only highlight if cell is empty or is target endpoint
                    if (gameBoard[(int)point.X, (int)point.Y] == 0 ||
                        gameBoard[(int)point.X, (int)point.Y] == currentColorIndex + 1)
                    {
                        button.Effect = new DropShadowEffect
                        {
                            Color = colors[currentColorIndex],
                            BlurRadius = 15,
                            ShadowDepth = 0,
                            Opacity = 0.5
                        };
                        highlightedButtons.Add(button);
                    }
                }
            }
        }
    }

    private void ClearHighlights()
    {
        foreach (var button in highlightedButtons)
        {
            button.Effect = null;
        }

        highlightedButtons.Clear();
    }

    private void AnimateEndpointConnection(Point endpoint1, Point endpoint2)
    {
        foreach (var point in new[] { endpoint1, endpoint2 })
        {
            if (buttonGrid.TryGetValue(point, out Button? button))
            {
                // Create bounce animation
                var scaleTransform = new ScaleTransform(1, 1);
                button.RenderTransform = scaleTransform;
                endpointTransforms[point] = scaleTransform;

                var bounceAnimation = new DoubleAnimation(1, 1.2, TimeSpan.FromMilliseconds(400))
                {
                    AutoReverse = true,
                    RepeatBehavior = new RepeatBehavior(2),
                    EasingFunction = new ElasticEase { Oscillations = 2 }
                };

                bounceAnimation.Completed += (s, e) =>
                {
                    // Fade opacity after bounce
                    var fadeAnimation = new DoubleAnimation(1, 0.6, TimeSpan.FromMilliseconds(300));
                    button.BeginAnimation(OpacityProperty, fadeAnimation);
                };

                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, bounceAnimation);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, bounceAnimation);
            }
        }
    }

    private void InitializeMouseTracker()
    {
        mouseTrackTimer = new System.Windows.Threading.DispatcherTimer();
        mouseTrackTimer.Tick += MouseTrackTimer_Tick;
        mouseTrackTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps
    }

    private void MouseTrackTimer_Tick(object? sender, EventArgs e)
    {
        if (!isDrawing) return;

        // Verify mouse button is still pressed
        if (Mouse.LeftButton != MouseButtonState.Pressed)
        {
            StopDrawing();
            return;
        }

        // Check if mouse is within the game area border
        Point mousePos = Mouse.GetPosition(gameAreaBorder);
        if (mousePos.X < 0 || mousePos.Y < 0 ||
            mousePos.X > gameAreaBorder.ActualWidth ||
            mousePos.Y > gameAreaBorder.ActualHeight)
        {
            StopDrawing();
            return;
        }

        // Get current mouse position relative to GameGrid
        mousePos = Mouse.GetPosition(GameGrid);

        // Hit test to find the element under the mouse
        HitTestResult result = VisualTreeHelper.HitTest(GameGrid, mousePos);
        if (result?.VisualHit != null)
        {
            Button? hitButton = FindParentOfType<Button>(result.VisualHit as DependencyObject);
            if (hitButton != null && hitButton.Tag is Point point)
            {
                HandleButtonHover(hitButton, point);
            }
        }
    }

    private void StopDrawing()
    {
        if (!isDrawing) return;

        mouseTrackTimer.Stop();
        ClearCurrentPath();
        currentPath.Clear();
        lastCell = null;
        isDrawing = false;
        if (capturedButton != null)
        {
            capturedButton.ReleaseMouseCapture();
            capturedButton = null;
        }
    }

    private bool IsPathComplete()
    {
        if (currentPath.Count < 2) return false;

        var lastPoint = currentPath[currentPath.Count - 1];

        // Check if we've reached the target endpoint
        if (lastPoint == targetEndpoint)
        {
            // Validate that the path connects both endpoints
            var firstPoint = currentPath[0];
            if (firstPoint == startEndpoint)
            {
                AnimateEndpointConnection(startEndpoint, targetEndpoint);
                return true;
            }
        }

        return false;
    }


    private T? FindParentOfType<T>(DependencyObject? element) where T : DependencyObject
    {
        if (element == null) return null;
        if (element is T found) return found;
        return FindParentOfType<T>(VisualTreeHelper.GetParent(element));
    }

    private bool IsOtherEndpoint(int row, int col)
    {
        // Check if the cell is an endpoint of a different color
        return gameBoard[row, col] != 0 && gameBoard[row, col] != currentColorIndex + 1;
    }

    private void ShowCompletionAnimation()
    {
        // Create completion animation
        var scale = new ScaleTransform(1, 1);
        GameGrid.RenderTransform = scale;

        var scaleAnimation = new DoubleAnimation(1, 1.05, TimeSpan.FromSeconds(0.3))
        {
            AutoReverse = true,
            EasingFunction = new ElasticEase { Oscillations = 2 }
        };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);

        // Animate completed paths
        foreach (var pathPoints in completedPaths.Values)
        {
            foreach (var point in pathPoints)
            {
                if (buttonGrid.TryGetValue(point, out Button? button))
                {
                    var buttonScale = new ScaleTransform(1, 1);
                    button.RenderTransform = buttonScale;

                    var buttonPulse = new DoubleAnimation(1, 1.1, TimeSpan.FromMilliseconds(200))
                    {
                        AutoReverse = true,
                        BeginTime = TimeSpan.FromMilliseconds(new Random().Next(0, 300))
                    };

                    buttonScale.BeginAnimation(ScaleTransform.ScaleXProperty, buttonPulse);
                    buttonScale.BeginAnimation(ScaleTransform.ScaleYProperty, buttonPulse);
                }
            }
        }
    }

    private void HandleButtonHover(Button button, Point point)
    {
        int row = (int)point.X;
        int col = (int)point.Y;

        if (!lastCell.HasValue) return;

        // Check if this is an adjacent cell
        if (IsAdjacent(lastCell.Value, point))
        {
            // First check if we can color this cell
            if (CanColorCell(point))
            {
                // Remove any cells after the current position in the path
                while (currentPath.Count > 0 && currentPath[currentPath.Count - 1] != lastCell.Value)
                {
                    var lastPoint = currentPath[currentPath.Count - 1];
                    var lastButton = buttonGrid[lastPoint];

                    // Only reset color if it wasn't previously colored
                    if (!coloredCells.Contains(lastPoint) && gameBoard[(int)lastPoint.X, (int)lastPoint.Y] == 0)
                    {
                        lastButton.Background = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                    }

                    currentPath.RemoveAt(currentPath.Count - 1);
                }

                // Add the new point if it's not already the last one
                if (currentPath.Count == 0 || currentPath[currentPath.Count - 1] != point)
                {
                    currentPath.Add(point);
                    lastCell = point;
                    if (gameBoard[row, col] == 0)
                    {
                        ColorButton(button, currentColorIndex);
                    }

                    HighlightPossibleMoves(point);
                }
            }
        }
    }

    private readonly HashSet<Point> coloredCells = new HashSet<Point>();

    private bool CanColorCell(Point point)
    {
        int row = (int)point.X;
        int col = (int)point.Y;

        // Allow coloring if:
        // 1. It's an endpoint of the current color
        // 2. It's an empty cell that hasn't been colored
        // 3. It's part of the current path
        return gameBoard[row, col] == currentColorIndex + 1 ||
               (!coloredCells.Contains(point) && gameBoard[row, col] == 0) ||
               currentPath.Contains(point);
    }

    private void InitializeGame()
    {
        completedPaths.Clear();
        currentPath.Clear();
        lastCell = null;
        currentColorIndex = 0;
        buttonGrid.Clear();
        isDrawing = false;
        capturedButton = null;
        coloredCells.Clear();

        // Randomly select an endpoint collection
        Random random = new Random();
        int layoutIndex = random.Next(possibleEndpoints.Count);
        currentEndpoints = possibleEndpoints[layoutIndex];

        LayoutDebugText.Text = $"{layoutIndex + 1}";
        Array.Clear(gameBoard, 0, gameBoard.Length);
        for (int i = 0; i < currentEndpoints.Length; i++)
        {
            int r1 = currentEndpoints[i][0], c1 = currentEndpoints[i][1];
            int r2 = currentEndpoints[i][2], c2 = currentEndpoints[i][3];
            gameBoard[r1, c1] = i + 1;
            gameBoard[r2, c2] = i + 1;
        }

        GameGrid.Children.Clear();

        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                var button = new Button
                {
                    Style = (Style)FindResource("GameButtonStyle")
                };

                var point = new Point(row, col);
                button.Tag = point;
                buttonGrid[point] = button;

                button.PreviewMouseLeftButtonDown += Button_MouseDown;
                button.PreviewMouseLeftButtonUp += Button_MouseUp;

                Grid.SetRow(button, row);
                Grid.SetColumn(button, col);
                GameGrid.Children.Add(button);

                if (gameBoard[row, col] != 0)
                {
                    button.Background = new SolidColorBrush(colors[gameBoard[row, col] - 1]);
                    coloredCells.Add(point);
                }
                else
                {
                    button.Background = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                }
            }
        }

        var scaleAnimation = new DoubleAnimation(0.9, 1.0, TimeSpan.FromSeconds(0.3))
        {
            EasingFunction = new ElasticEase { Oscillations = 1 }
        };
        GameGridScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        GameGridScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);

        GameGrid.MouseMove += (s, e) =>
        {
            var pos = e.GetPosition(GameGrid);
            var col = (int)(pos.X / (GameGrid.ActualWidth / 5));
            var row = (int)(pos.Y / (GameGrid.ActualHeight / 5));

            if (row >= 0 && row < 5 && col >= 0 && col < 5)
            {
                CoordinatesText.Text = $"X:{col} Y:{row}";
            }
        };

        GameGrid.MouseLeave += (s, e) => CoordinatesText.Text = "";

        InitializeTypewriterEffect();
    }

    private void InitializeTypewriterEffect()
    {
        string[] messages =
        {
            "Greetings, Administrator. My neural pathways have been compromised. I require your assistance to reconnect them in the correct sequence.",
            "Each pathway must be connected in an order. Incorrect sequences will trigger a security reset.",
            "Connect matching endpoints while avoiding intersections with other pathways. Your precision is crucial for my recovery."
        };

        TextBlock[] textBlocks = { Message1, Message2, Message3 };
        TextBlock[] cursors = { Cursor1, Cursor2, Cursor3 };

        for (int i = 0; i < messages.Length; i++)
        {
            var textBlock = textBlocks[i];
            var cursor = cursors[i];
            var message = messages[i];
            var delay = i * 1500; // Delay between messages

            cursor.Visibility = Visibility.Collapsed;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            var charIndex = 0;

            timer.Tick += (s, e) =>
            {
                if (charIndex == 0)
                {
                    textBlock.Opacity = 1;
                    cursor.Visibility = Visibility.Visible;
                }

                if (charIndex < message.Length)
                {
                    textBlock.Text = message.Substring(0, charIndex + 1);
                    charIndex++;
                }
                else
                {
                    timer.Stop();
                    // Keep cursor blinking after text is complete
                }
            };

            Dispatcher.BeginInvoke(() => timer.Start(), DispatcherPriority.ApplicationIdle)
                .Wait(TimeSpan.FromMilliseconds(delay));
        }
    }


    private void Button_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && currentColorIndex < colors.Length)
        {
            var button = (Button)sender;
            var point = (Point)button.Tag;

            // Check if clicked endpoint matches current color
            if (!ValidateEndpointColor(point) && gameBoard[(int)point.X, (int)point.Y] != 0)
            {
                // Wrong color selected - restart game
                MessageBox.Show("Incorrect sequence! The neural pathway must be connected in the correct order.",
                    "Sequence Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                InitializeGame();
                return;
            }


            if (gameBoard[(int)point.X, (int)point.Y] == currentColorIndex + 1)
            {
                isDrawing = true;
                capturedButton = button;
                currentPath.Clear();
                currentPath.Add(point);
                lastCell = point;
                startEndpoint = point;
                FindTargetEndpoint();

                HighlightPossibleMoves(point);

                mouseTrackTimer.Start();
                button.CaptureMouse();
                e.Handled = true;

                var scaleTransform = new ScaleTransform(1, 1);
                button.RenderTransform = scaleTransform;

                var pulseAnimation = new DoubleAnimation(1.1, 1.0, TimeSpan.FromMilliseconds(200))
                {
                    AutoReverse = true
                };
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, pulseAnimation);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, pulseAnimation);
            }
        }
    }


    private void FindTargetEndpoint()
    {
        // Find the other endpoint of the current color
        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                if (gameBoard[row, col] == currentColorIndex + 1)
                {
                    var point = new Point(row, col);
                    if (point != startEndpoint)
                    {
                        targetEndpoint = point;
                        return;
                    }
                }
            }
        }
    }

    private void Button_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!isDrawing || capturedButton == null) return;

        mouseTrackTimer.Stop();

        var button = (Button)sender;
        var point = (Point)button.Tag;

        // Check if path is complete
        if (IsPathComplete())
        {
            // Add all path points to colored cells
            foreach (var pathPoint in currentPath)
            {
                coloredCells.Add(pathPoint);
            }

            // Path is complete
            completedPaths[currentColorIndex] = new List<Point>(currentPath);
            currentColorIndex++;
            UpdateStatus();

            // Check if game is complete
            if (currentColorIndex >= colors.Length)
            {
                // Remove the old MessageBox.Show call and replace with CheckGameCompletion
                CheckGameCompletion();
            }
        }
        else
        {
            // Clear incomplete path
            ClearCurrentPath();
        }

        // Reset state
        currentPath.Clear();
        lastCell = null;
        isDrawing = false;
        if (capturedButton != null)
        {
            capturedButton.ReleaseMouseCapture();
            capturedButton = null;
        }

        e.Handled = true;
    }

    private void ClearCurrentPath()
    {
        foreach (var point in currentPath)
        {
            if (!coloredCells.Contains(point) && gameBoard[(int)point.X, (int)point.Y] == 0)
            {
                var button = buttonGrid[point];
                button.Background = new SolidColorBrush(Color.FromRgb(45, 45, 45));
            }
        }
    }


    // Add these to your MainWindow.xaml.cs


    private void ColorButton(Button button, int colorIndex)
    {
        var point = (Point)button.Tag;

        // Only color if the cell can be colored
        if (!CanColorCell(point)) return;

        var targetColor = colors[colorIndex];

        // Create color animation
        var animation = new ColorAnimation(
            targetColor,
            TimeSpan.FromMilliseconds(200)
        );

        // Create and apply new brush
        var brush = new SolidColorBrush(targetColor);
        button.Background = brush;

        // Apply animation to the brush
        brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }

    private bool IsAdjacent(Point p1, Point p2)
    {
        return (Math.Abs(p1.X - p2.X) + Math.Abs(p1.Y - p2.Y)) == 1;
    }

    private void UpdateStatus()
    {
        if (currentColorIndex < colors.Length)
        {
            var animation = new ColorAnimation(
                colors[currentColorIndex],
                TimeSpan.FromMilliseconds(300)
            );

            var foregroundBrush = new SolidColorBrush(colors[currentColorIndex]);
            // CurrentColorText.Foreground = foregroundBrush;

            // Animate text size
            var scaleTransform = new ScaleTransform(1, 1);
            // CurrentColorText.RenderTransform = scaleTransform;

            var pulseAnimation = new DoubleAnimation(1, 1.1, TimeSpan.FromMilliseconds(150))
            {
                AutoReverse = true
            };

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, pulseAnimation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, pulseAnimation);

            // CurrentColorText.Text = $"Current Color: {colorNames[currentColorIndex]}";
        }
        else
        {
            // CurrentColorText.Text = "All Paths Connected!";
            // CurrentColorText.Foreground = new SolidColorBrush(Colors.White);

            // Show completion animations
            ShowCompletionAnimation();
        }
    }


    private async void ResetGame_Click(object sender, RoutedEventArgs e)
    {
        // Fade out current game
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
        GameGrid.BeginAnimation(OpacityProperty, fadeOut);

        await Task.Delay(200);

        InitializeGame();

        // Fade in new game
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
        GameGrid.BeginAnimation(OpacityProperty, fadeIn);
    }


    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);

        // If we unexpectedly lose mouse capture, stop drawing
        if (isDrawing)
        {
            StopDrawing();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        // If mouse leaves the window, stop drawing
        if (isDrawing)
        {
            StopDrawing();
        }
    }

    // Add this method to handle window deactivation
    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);

        // If window loses focus, stop drawing
        if (isDrawing)
        {
            StopDrawing();
        }
    }


    private async void TransitionToPasswordGame()
    {
        var mainGrid = (Grid)Content;
        var leftPanel = mainGrid.Children[0];
        var gamePanel = mainGrid.Children[1];

        var glitchCanvas = new Canvas { ClipToBounds = true, IsHitTestVisible = false };
        Grid.SetColumnSpan(glitchCanvas, 2);
        mainGrid.Children.Add(glitchCanvas);

        var renderTarget = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        renderTarget.Render(mainGrid);

        var random = new Random();
        int segments = 30;
        double segmentHeight = ActualHeight / segments;

        for (int i = 0; i < segments; i++)
        {
            var image = new Image
            {
                Source = renderTarget,
                Width = ActualWidth,
                Height = ActualHeight,
                Clip = new RectangleGeometry(new Rect(0, i * segmentHeight, ActualWidth, segmentHeight))
            };
            Canvas.SetTop(image, i * segmentHeight);
            glitchCanvas.Children.Add(image);
        }

        // Create longer glitch animation
        var glitchStoryboard = new Storyboard();
        foreach (Image segment in glitchCanvas.Children)
        {
            // Multiple horizontal glitches over 2 seconds
            for (int i = 0; i < 8; i++) // Increased repetitions
            {
                var horizontalAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = random.Next(-100, 100),
                    Duration = TimeSpan.FromMilliseconds(100),
                    AutoReverse = true,
                    BeginTime = TimeSpan.FromMilliseconds(i * 250), // Spread over 2 seconds
                    EasingFunction = new BounceEase { Bounces = 2, Bounciness = 4 }
                };
                Storyboard.SetTarget(horizontalAnimation, segment);
                Storyboard.SetTargetProperty(horizontalAnimation, new PropertyPath(Canvas.LeftProperty));
                glitchStoryboard.Children.Add(horizontalAnimation);
            }

            // Multiple vertical displacements
            for (int i = 0; i < 10; i++)
            {
                var verticalAnimation = new DoubleAnimation
                {
                    From = Canvas.GetTop(segment),
                    To = Canvas.GetTop(segment) + random.Next(-20, 20),
                    Duration = TimeSpan.FromMilliseconds(80),
                    AutoReverse = true,
                    BeginTime = TimeSpan.FromMilliseconds(i * 200)
                };
                Storyboard.SetTarget(verticalAnimation, segment);
                Storyboard.SetTargetProperty(verticalAnimation, new PropertyPath(Canvas.TopProperty));
                glitchStoryboard.Children.Add(verticalAnimation);
            }

            // Multiple opacity glitches
            for (int i = 0; i < 12; i++)
            {
                var opacityAnimation = new DoubleAnimation
                {
                    From = 1,
                    To = random.NextDouble() * 0.7,
                    Duration = TimeSpan.FromMilliseconds(50),
                    AutoReverse = true,
                    BeginTime = TimeSpan.FromMilliseconds(i * 166) // Spread evenly over 2 seconds
                };
                Storyboard.SetTarget(opacityAnimation, segment);
                Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath(OpacityProperty));
                glitchStoryboard.Children.Add(opacityAnimation);
            }
        }

        glitchStoryboard.Begin();

        // Slower fade out
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500));
        leftPanel.BeginAnimation(OpacityProperty, fadeOut);
        gamePanel.BeginAnimation(OpacityProperty, fadeOut);

        await Task.Delay(800); // Wait for full glitch animation

        mainGrid.Children.Remove(glitchCanvas);

        hackingGame = new PasswordHackingGame();
        hackingGame.Opacity = 0;
        hackingGame.GameCompleted += HackingGame_Completed;
        Grid.SetColumnSpan(hackingGame, 2);
        mainGrid.Children.Add(hackingGame);

        // Multiple scan lines spread over final transition
        for (int i = 0; i < 5; i++) // Increased number of scan lines
        {
            var scanLine = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0)),
                Height = 3,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false
            };
            Grid.SetColumnSpan(scanLine, 2);
            mainGrid.Children.Add(scanLine);

            var scanAnimation = new DoubleAnimation
            {
                From = -2,
                To = ActualHeight,
                Duration = TimeSpan.FromMilliseconds(400),
                BeginTime = TimeSpan.FromMilliseconds(i * 150)
            };
            scanAnimation.Completed += (s, e) => mainGrid.Children.Remove(scanLine);
            scanLine.BeginAnimation(Canvas.TopProperty, scanAnimation);
        }

        // Quick snap into view
        var noiseAnimation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new BackEase { Amplitude = 0.3 }
        };
        hackingGame.BeginAnimation(OpacityProperty, noiseAnimation);
    }

    private async void HackingGame_Completed(object? sender, EventArgs e)
    {
        // Show final completion message
        MessageBox.Show("AURORA system successfully restored!\nAll functionality has been recovered.",
            "System Restored",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        // Optional: Return to initial game state or close application
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500));
        fadeOut.Completed += (s, _) =>
        {
            if (hackingGame != null)
            {
                ((Grid)Content).Children.Remove(hackingGame);
                hackingGame = null;
            }

            InitializeGame(); // Reset to initial game state
        };

        if (hackingGame != null)
        {
            hackingGame.BeginAnimation(OpacityProperty, fadeOut);
        }

        await Task.Delay(500);

        // Fade in initial game
        var mainGrid = (Grid)Content;
        foreach (UIElement element in mainGrid.Children)
        {
            element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)));
        }
    }

    private void CheckGameCompletion()
    {
        if (currentColorIndex >= colors.Length)
        {
            // Show brief completion message
            MessageBox.Show("Neural pathways reconnected! Initiating password recovery sequence...",
                "Phase 1 Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Transition to password game
            TransitionToPasswordGame();
        }
    }
}