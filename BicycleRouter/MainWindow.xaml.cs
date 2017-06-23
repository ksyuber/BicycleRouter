using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BicycleRouter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Map map;

        private bool isTranslating = false;
        private Point lastMousePosition;
        private Point lastMapViewGridMousePosition;

        private Node fromNode;
        private Node toNode;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "XML (*.xml)|*.xml";
            openFileDialog.Multiselect = false;
            if (openFileDialog.ShowDialog() == true)
            {
                map = new Map(openFileDialog.FileName, 6000, 5000);
                drawMap();
            }
        }

        private void drawPath(List<Node> path, double strokeThickness, Color color)
        {
            Polyline polyline = new Polyline();
            polyline.StrokeThickness = strokeThickness;
            polyline.Stroke = new SolidColorBrush(color);

            polyline.Opacity = 1;
            foreach (Node node in path)
            {
                polyline.Points.Add(node.coords);
            }
            MapView.Children.Add(polyline);
        }

        private void drawMap()
        {
            foreach (Way way in map.ways)
            {
                if (way.surfaceType == Way.SurfaceType.Cycleway)
                {
                    drawPath(way.nodes, 4, Colors.Goldenrod);
                }
                else if (way.surfaceType == Way.SurfaceType.Asphalt)
                {
                    drawPath(way.nodes, 3, Colors.LightSteelBlue);
                }
                else if (way.surfaceType == Way.SurfaceType.Unpaved)
                {
                    drawPath(way.nodes, 2, Colors.Tomato);
                }
                else if (way.surfaceType == Way.SurfaceType.Footway)
                {
                    drawPath(way.nodes, 2, Colors.LightPink);
                }
                else
                {
                    drawPath(way.nodes, 1, Colors.Black);
                }
            }
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MapView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                isTranslating = true;
            }
            lastMousePosition = e.GetPosition(this);
            lastMapViewGridMousePosition = e.GetPosition(MapView);
        }

        private void MapView_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
            {
                isTranslating = false;
            }
        }

        private void MapView_MouseMove(object sender, MouseEventArgs e)
        {
            if (isTranslating)
            {
                Point mousePosition = e.GetPosition(this);

                var delta = mousePosition - lastMousePosition;

                MapViewTranslateTransform.X += delta.X;
                MapViewTranslateTransform.Y += delta.Y;

                lastMousePosition = mousePosition;
            }
        }

        private void findPath()
        {
            if (FromHereEllipse.IsVisible && ToHereEllipse.IsVisible)
            {
                Path.Visibility = Visibility.Visible;
                Path.Points.Clear();

                Way.SurfaceType surfaceType = Way.SurfaceType.None;
                if (AnySurface.IsChecked == true)
                {
                    surfaceType |= Way.SurfaceType.Unspecified;
                }
                if (UnpavedSurface.IsChecked == true)
                {
                    surfaceType |= Way.SurfaceType.Unpaved;
                }
                if (AsphaltSurface.IsChecked == true)
                {
                    surfaceType |= Way.SurfaceType.Asphalt;
                }
                if (PedestrianSurface.IsChecked == true)
                {
                    surfaceType |= Way.SurfaceType.Footway;
                }
                if (CyclewaySurface.IsChecked == true)
                {
                    surfaceType |= Way.SurfaceType.Cycleway;
                }

                try
                {
                    foreach (Node node in map.findPath(fromNode, toNode, surfaceType))
                    {
                        Path.Points.Add(node.coords);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private void FromHere_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                fromNode = map.getNearestNode(lastMapViewGridMousePosition);

                Point fromHerePosition = fromNode.coords;
                fromHerePosition.X -= MapView.ActualWidth * 0.5;
                fromHerePosition.Y -= MapView.ActualHeight * 0.5;

                FromHereTranslateTransform.X = fromHerePosition.X;
                FromHereTranslateTransform.Y = fromHerePosition.Y;
                FromHereEllipse.Visibility = Visibility.Visible;

                findPath();
            }
            catch (Exception)
            {
            }
        }

        private void ToHere_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                toNode = map.getNearestNode(lastMapViewGridMousePosition);

                Point toHerePosition = toNode.coords;
                toHerePosition.X -= MapView.ActualWidth * 0.5;
                toHerePosition.Y -= MapView.ActualHeight * 0.5;

                ToHereTranslateTransform.X = toHerePosition.X;
                ToHereTranslateTransform.Y = toHerePosition.Y;
                ToHereEllipse.Visibility = Visibility.Visible;

                findPath();
            }
            catch (Exception)
            {
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            FromHereEllipse.Visibility = Visibility.Hidden;
            ToHereEllipse.Visibility = Visibility.Hidden;
            Path.Visibility = Visibility.Hidden;
        }

        private void AnySurface_Click(object sender, RoutedEventArgs e)
        {
            findPath();
        }

        private void AsphaltSurface_Click(object sender, RoutedEventArgs e)
        {
            findPath();
        }

        private void CyclewaySurface_Click(object sender, RoutedEventArgs e)
        {
            findPath();
        }

        private void UnpavedSurface_Click(object sender, RoutedEventArgs e)
        {
            findPath();
        }

        private void PedestrianSurface_Click(object sender, RoutedEventArgs e)
        {
            findPath();
        }
    }
}
