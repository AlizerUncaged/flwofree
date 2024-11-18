using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
    private readonly Border gameAreaBorder; // Reference to the game area border
    private Point startEndpoint; // Track where we started drawing from
    private Point targetEndpoint; // Track where we need to connect to

    private Color[] colors = new Color[]
    {
        Color.FromRgb(255, 65, 65), // Red
        Color.FromRgb(255, 140, 0), // Orange
        Color.FromRgb(65, 105, 255), // Blue
        Color.FromRgb(50, 205, 50) // Green
    };

    private string[] colorNames = { "Red", "Orange", "Blue", "Green" };
    private readonly Dictionary<int, List<Point>> completedPaths = new Dictionary<int, List<Point>>();
    private readonly Dictionary<Point, Button> buttonGrid = new Dictionary<Point, Button>();


    public MainWindow()
    {
        InitializeComponent();

        // Store reference to game area border
        gameAreaBorder = (Border)((Grid)Content).Children[1];

        InitializeMouseTracker();
        InitializeGame();
        UpdateStatus();
    }

    private void InitializeMouseTracker()
    {
        mouseTrackTimer = new System.Windows.Threading.DispatcherTimer();
        mouseTrackTimer.Tick += MouseTrackTimer_Tick;
        mouseTrackTimer.Interval = TimeSpan.FromMilliseconds(40); // ~60fps
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
        coloredCells.Clear(); // Clear colored cells tracking

        int[][] endpoints = new int[][]
        {
            new int[] { 0, 3, 1, 0 }, // Red endpoints (R): (0,3) and (1,0)
            new int[] { 0, 4, 4, 0 }, // Green endpoints (G): (0,4) and (4,0)
            new int[] { 2, 2, 4, 2 }, // Yellow endpoints (Y): (2,2) and (4,2)
            new int[] { 3, 3, 4, 1 } // Blue endpoints (B): (3,3) and (4,1)
        };

        /* The layout matches this exact grid:
         * 0 0 0 R G    [0,0] [0,1] [0,2] [0,3] [0,4]
         * R 0 0 0 0    [1,0] [1,1] [1,2] [1,3] [1,4]
         * 0 0 Y 0 0    [2,0] [2,1] [2,2] [2,3] [2,4]
         * 0 0 0 B 0    [3,0] [3,1] [3,2] [3,3] [3,4]
         * G B Y 0 0    [4,0] [4,1] [4,2] [4,3] [4,4]
         */

        // Initialize the game board with endpoints
        Array.Clear(gameBoard, 0, gameBoard.Length);
        for (int i = 0; i < endpoints.Length; i++)
        {
            int y1 = endpoints[i][0], x1 = endpoints[i][1]; // First endpoint
            int y2 = endpoints[i][2], x2 = endpoints[i][3]; // Second endpoint
            gameBoard[y1, x1] = i + 1;
            gameBoard[y2, x2] = i + 1;
        }

        // Update colors array to match the image

        // colorNames = new string[] { "Red", "Green", "Yellow", "Blue" };

        Array.Clear(gameBoard, 0, gameBoard.Length);
        for (int i = 0; i < endpoints.Length; i++)
        {
            int r1 = endpoints[i][0], c1 = endpoints[i][1];
            int r2 = endpoints[i][2], c2 = endpoints[i][3];
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

                // If this is an endpoint, color it immediately and add to colored cells
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

        // Add a scale animation for game start
        var scaleAnimation = new DoubleAnimation(0.9, 1.0, TimeSpan.FromSeconds(0.3))
        {
            EasingFunction = new ElasticEase { Oscillations = 1 }
        };
        GameGridScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        GameGridScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);

        UpdateStatus();
    }

    private void Button_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && currentColorIndex < colors.Length)
        {
            var button = (Button)sender;
            var point = (Point)button.Tag;
            int row = (int)point.X;
            int col = (int)point.Y;

            // Only start drawing if we click on an endpoint of the current color
            if (gameBoard[row, col] == currentColorIndex + 1)
            {
                isDrawing = true;
                capturedButton = button;
                currentPath.Clear();
                currentPath.Add(point);
                lastCell = point;
                startEndpoint = point;

                // Find the target endpoint
                FindTargetEndpoint();

                mouseTrackTimer.Start();
                button.CaptureMouse(); // Ensure mouse capture
                e.Handled = true;

                // Add pulse animation on start
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
            CurrentColorText.Foreground = foregroundBrush;

            // Animate text size
            var scaleTransform = new ScaleTransform(1, 1);
            CurrentColorText.RenderTransform = scaleTransform;

            var pulseAnimation = new DoubleAnimation(1, 1.1, TimeSpan.FromMilliseconds(150))
            {
                AutoReverse = true
            };

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, pulseAnimation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, pulseAnimation);

            CurrentColorText.Text = $"Current Color: {colorNames[currentColorIndex]}";
        }
        else
        {
            CurrentColorText.Text = "All Paths Connected!";
            CurrentColorText.Foreground = new SolidColorBrush(Colors.White);

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

        // Create glitch effect canvas
        var glitchCanvas = new Canvas
        {
            ClipToBounds = true,
            IsHitTestVisible = false
        };
        Grid.SetColumnSpan(glitchCanvas, 2);
        mainGrid.Children.Add(glitchCanvas);

        // Take screenshot of current game state
        var renderTarget = new RenderTargetBitmap(
            (int)ActualWidth,
            (int)ActualHeight,
            96, 96,
            PixelFormats.Pbgra32);
        renderTarget.Render(mainGrid);

        // Create glitch segments
        var random = new Random();
        int segments = 20;
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

        // Animate glitch effect
        var glitchStoryboard = new Storyboard();

        foreach (Image segment in glitchCanvas.Children)
        {
            // Horizontal glitch
            var horizontalAnimation = new DoubleAnimation
            {
                From = 0,
                To = random.Next(-50, 50),
                Duration = TimeSpan.FromMilliseconds(300),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };
            Storyboard.SetTarget(horizontalAnimation, segment);
            Storyboard.SetTargetProperty(horizontalAnimation, new PropertyPath(Canvas.LeftProperty));
            glitchStoryboard.Children.Add(horizontalAnimation);

            // Opacity glitch
            var opacityAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0.5,
                Duration = TimeSpan.FromMilliseconds(100),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(6)
            };
            Storyboard.SetTarget(opacityAnimation, segment);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath(OpacityProperty));
            glitchStoryboard.Children.Add(opacityAnimation);
        }

        // Play glitch animation
        glitchStoryboard.Begin();

        // Fade out current game
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500));
        leftPanel.BeginAnimation(OpacityProperty, fadeOut);
        gamePanel.BeginAnimation(OpacityProperty, fadeOut);

        await Task.Delay(800);

        // Remove glitch canvas
        mainGrid.Children.Remove(glitchCanvas);

        // Create and setup hacking game
        hackingGame = new PasswordHackingGame();
        hackingGame.Opacity = 0;
        hackingGame.GameCompleted += HackingGame_Completed;

        Grid.SetColumnSpan(hackingGame, 2);
        mainGrid.Children.Add(hackingGame);

        // Create scan line effect
        var scanLine = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(40, 0, 255, 0)),
            Height = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false
        };
        Grid.SetColumnSpan(scanLine, 2);
        mainGrid.Children.Add(scanLine);

        // Animate scan line
        var scanAnimation = new DoubleAnimation
        {
            From = -2,
            To = ActualHeight,
            Duration = TimeSpan.FromSeconds(1),
            EasingFunction = new QuadraticEase()
        };
        scanAnimation.Completed += (s, e) => mainGrid.Children.Remove(scanLine);
        scanLine.BeginAnimation(Canvas.TopProperty, scanAnimation);

        // Fade in hacking game with digital noise effect
        var random2 = new Random();
        var noiseAnimation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(1000))
        {
            EasingFunction = new CubicEase()
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