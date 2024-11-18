using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace FlowFree;

public class MatrixBackground : Canvas
    {
        private readonly Random random = new Random();
        private readonly DispatcherTimer timer;
        private readonly List<MatrixColumn> columns = new List<MatrixColumn>();
        
        public MatrixBackground()
        {
            // Set up timer for animation
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            timer.Tick += Timer_Tick;
            
            // Initialize when control is loaded
            Loaded += MatrixBackground_Loaded;
            
            // Set background
            Background = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0));
        }

        private void MatrixBackground_Loaded(object sender, RoutedEventArgs e)
        {
            CreateColumns();
            timer.Start();
        }

        private void CreateColumns()
        {
            columns.Clear();
            Children.Clear();

            int columnWidth = 20;
            int numColumns = (int)(ActualWidth / columnWidth);

            for (int i = 0; i < numColumns; i++)
            {
                var column = new MatrixColumn(random.Next(5, 15), i * columnWidth, ActualHeight);
                columns.Add(column);
                foreach (var symbol in column.Symbols)
                {
                    Children.Add(symbol);
                }
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            foreach (var column in columns)
            {
                column.Update();
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            CreateColumns();
        }

        private class MatrixColumn
        {
            private readonly List<TextBlock> symbols = new List<TextBlock>();
            private readonly Random random = new Random();
            private readonly double x;
            private readonly double maxY;
            private double speed;

            public IReadOnlyList<TextBlock> Symbols => symbols;

            public MatrixColumn(int length, double x, double maxY)
            {
                this.x = x;
                this.maxY = maxY;
                speed = random.Next(3, 8);

                for (int i = 0; i < length; i++)
                {
                    var symbol = CreateSymbol(i);
                    symbols.Add(symbol);
                }
            }

            private TextBlock CreateSymbol(int index)
            {
                return new TextBlock
                {
                    Text = Random.Shared.Next(2).ToString(),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromArgb(
                        (byte)(100 + Random.Shared.Next(155)),
                        0,
                        (byte)(200 + Random.Shared.Next(55)),
                        0)),
                    Opacity = 0.8 - (index * 0.1),
                    RenderTransform = new TranslateTransform(x, -20 * index)
                };
            }

            public void Update()
            {
                for (int i = 0; i < symbols.Count; i++)
                {
                    var symbol = symbols[i];
                    var transform = (TranslateTransform)symbol.RenderTransform;
                    transform.Y += speed;

                    // Reset if symbol goes off screen
                    if (transform.Y > maxY)
                    {
                        transform.Y = -20;
                        symbol.Text = Random.Shared.Next(2).ToString();
                        speed = random.Next(3, 8);
                    }
                }
            }
        }
    }
