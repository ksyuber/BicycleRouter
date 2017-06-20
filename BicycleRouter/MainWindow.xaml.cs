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
                map = new Map(openFileDialog.FileName, MapView.Width, MapView.Height);
                drawMap();
            }
        }

        private void drawPath(List<Node> path, double strokeThickness, Color color)
        {
            Polyline polyline = new Polyline();
            polyline.StrokeThickness = strokeThickness;
            polyline.Stroke = new SolidColorBrush(color);
            
            polyline.Opacity = 0.5;
            foreach (Node node in path)
            {
                polyline.Points.Add(node.coords);
            }
            MapViewGrid.Children.Add(polyline);
        }

        private void drawMap()
        {
            foreach (Way way in map.ways)
            {
                if (way.bicycle)
                {
                    drawPath(way.nodes, 3, Color.FromRgb(196, 128, 0));
                }
                else
                {
                    drawPath(way.nodes, 1, Color.FromRgb(0, 0, 0));
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
            lastMapViewGridMousePosition = e.GetPosition(MapViewGrid);
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
                Point mousePosition = e.GetPosition(MapView);

                var delta = mousePosition - lastMousePosition;

                MapViewTranslateTransform.X += delta.X;
                MapViewTranslateTransform.Y += delta.Y;

                lastMousePosition = mousePosition;
            }
        }

        private void findPath()
        {
            if (fromNode != null && toNode != null)
            {
                Path.Visibility = Visibility.Visible;
                Path.Points.Clear();
                foreach (Node node in map.findPath(fromNode, toNode))
                {
                    Path.Points.Add(node.coords);
                }
            }
        }

        private void FromHere_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                fromNode = map.getNearestNode(lastMapViewGridMousePosition);

                Point fromHerePosition = fromNode.coords;
                fromHerePosition.X -= MapViewGrid.ActualWidth * 0.5;
                fromHerePosition.Y -= MapViewGrid.ActualHeight * 0.5;

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
                toHerePosition.X -= MapViewGrid.ActualWidth * 0.5;
                toHerePosition.Y -= MapViewGrid.ActualHeight * 0.5;

                ToHereTranslateTransform.X = toHerePosition.X;
                ToHereTranslateTransform.Y = toHerePosition.Y;
                ToHereEllipse.Visibility = Visibility.Visible;

                findPath();
            }
            catch (Exception)
            {
            }
        }
    }
}
