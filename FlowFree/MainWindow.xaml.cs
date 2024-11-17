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
    private Point? lastCell = null;
    private int currentColorIndex = 0;
    private bool isDrawing = false;
    private Button? capturedButton = null;
    private System.Windows.Threading.DispatcherTimer mouseTrackTimer;
    private readonly Border gameAreaBorder; // Reference to the game area border

    private readonly Color[] colors = new Color[]
    {
        Color.FromRgb(255, 65, 65), // Red
        Color.FromRgb(255, 140, 0), // Orange
        Color.FromRgb(65, 105, 255), // Blue
        Color.FromRgb(50, 205, 50) // Green
    };

    private readonly string[] colorNames = { "Red", "Orange", "Blue", "Green" };
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

        // Check if mouse is within the game area border
        Point mousePos = Mouse.GetPosition(gameAreaBorder);
        if (mousePos.X < 0 || mousePos.Y < 0 ||
            mousePos.X > gameAreaBorder.ActualWidth ||
            mousePos.Y > gameAreaBorder.ActualHeight)
        {
            // Mouse is outside game area
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

        // Create a guaranteed solvable layout with non-interfering paths
        int[][] endpoints = new int[][]
        {
            new int[] { 0, 0, 4, 0 },     // Red endpoints (vertical on left)
            new int[] { 0, 4, 4, 4 },     // Orange endpoints (vertical on right)
            new int[] { 0, 2, 4, 2 },     // Blue endpoints (vertical in middle)
            new int[] { 2, 1, 2, 3 }      // Green endpoints (horizontal in middle)
        };


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
                mouseTrackTimer.Start();
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

    private void Button_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!isDrawing || capturedButton == null) return;

        mouseTrackTimer.Stop();

        var button = (Button)sender;
        var point = (Point)button.Tag;
        int row = (int)point.X;
        int col = (int)point.Y;

        // Check if we've reached the other endpoint
        if (gameBoard[row, col] == currentColorIndex + 1 && currentPath.Count > 1)
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
                MessageBox.Show("Congratulations! You've successfully reconnected all neural pathways!", 
                    "AI Restored", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
            }
        }
        else
        {
            // Clear incomplete path
            foreach (var pathPoint in currentPath)
            {
                if (!coloredCells.Contains(pathPoint) && gameBoard[(int)pathPoint.X, (int)pathPoint.Y] == 0)
                {
                    var pathButton = buttonGrid[pathPoint];
                    pathButton.Background = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                }
            }
        }

        // Reset state
        currentPath.Clear();
        lastCell = null;
        isDrawing = false;
        capturedButton = null;
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
}