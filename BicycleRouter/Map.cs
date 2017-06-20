using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace BicycleRouter
{
    struct GeoCoords
    {
        public const double earthRadius = 6371007;

        public double latitude;
        public double longitude;

        public GeoCoords(double latitude = 0, double longitude = 0)
        {
            this.latitude = latitude;
            this.longitude = longitude;
        }

        public Point mercatorProject()
        {
            return new Point(
                (longitude - 180) / 360.0,
                (1.0 - Math.Log(Math.Tan(latitude * Math.PI / 180.0) + 1.0 / Math.Cos(latitude * Math.PI / 180.0)) / Math.PI - 1.0) / 2.0);
        }
    }

    class Node
    {
        public string id { get; private set; }
        public GeoCoords geoCoords { get; private set; }
        public Point coords { get; private set; }

        public Node(string id, GeoCoords geoCoords, Point coords)
        {
            this.id = id;
            this.geoCoords = geoCoords;
            this.coords = coords;
        }
    }

    class MapProjectionParams
    {
        public double width { get; private set; }
        public double height { get; private set; }

        private Point factor;
        private Point minCoords;
        private Point maxCoords;

        public MapProjectionParams(
            double width, double height,
            GeoCoords minBoundPoint, GeoCoords maxBoundPoint)
        {
            this.width = width;
            this.height = height;
            this.minCoords = minBoundPoint.mercatorProject();
            this.maxCoords = maxBoundPoint.mercatorProject();
            this.factor = new Point(
                width / (maxCoords.X - minCoords.X),
                height / (minCoords.Y - maxCoords.Y));
        }

        public Point mercatorProject(GeoCoords geoCoords)
        {
            Point coords = geoCoords.mercatorProject();
            coords.X = (coords.X - minCoords.X) * factor.X;
            coords.Y = (coords.Y - maxCoords.Y) * factor.Y;

            return coords;
        }
    }

    class Way
    {
        public List<Node> nodes = new List<Node>();
        public bool bicycle = false;
    }

    class Map
    {
        public MapProjectionParams projectionParams;
        public Dictionary<string, Node> nodes = new Dictionary<string, Node>();
        public Dictionary<string, Node> highwayNodes = new Dictionary<string, Node>();
        public List<Way> ways = new List<Way>();
        public Dictionary<string, List<Node>> graph = new Dictionary<string, List<Node>>();

        private const int searchGridCellSize = 100;
        private Dictionary<int, Dictionary<int, List<Node>>> searchGrid = new Dictionary<int, Dictionary<int, List<Node>>>();

        private void loadNodes(XmlDocument xml)
        {
            foreach (XmlNode node in xml.DocumentElement.SelectNodes("node"))
            {
                string id = node.Attributes["id"].Value;
                GeoCoords geoCoords = new GeoCoords(
                    Double.Parse(node.Attributes["lat"].Value, CultureInfo.InvariantCulture),
                    Double.Parse(node.Attributes["lon"].Value, CultureInfo.InvariantCulture));

                nodes[id] = new Node(id, geoCoords, projectionParams.mercatorProject(geoCoords));
            }
        }

        private void loadWays(XmlDocument xml)
        {
            foreach (XmlNode node in xml.DocumentElement.SelectNodes("way[tag[@k='highway']]"))
            {
                Way way = new Way();
                foreach (XmlNode subNode in node.SelectNodes("nd"))
                {
                    Node wayNode = nodes[subNode.Attributes["ref"].Value];
                    way.nodes.Add(wayNode);
                    highwayNodes[wayNode.id] = wayNode;
                }
                var highway = node.SelectSingleNode("tag[@k='highway']");
                var bicycle = node.SelectSingleNode("tag[@k='bicycle']");
                way.bicycle = 
                    highway != null && highway.Attributes["v"].Value == "cycleway" ||
                    bicycle != null && (bicycle.Attributes["v"].Value == "yes" || 
                                        bicycle.Attributes["v"].Value == "designated");

                ways.Add(way);
            }
        }

        private void fillGraph()
        {
            foreach (Way way in ways)
            {
                for (int i = 0; i < way.nodes.Count; ++i)
                {
                    string nodeId = way.nodes[i].id;
                    if (!graph.ContainsKey(nodeId))
                    {
                        graph[nodeId] = new List<Node>();
                    }

                    if (i - 1 >= 0)
                    {
                        graph[nodeId].Add(way.nodes[i - 1]);
                    }
                    if (i + 1 < way.nodes.Count)
                    {
                        graph[nodeId].Add(way.nodes[i + 1]);
                    }
                }
            }
        }

        private void fillSearchGrid()
        {
            foreach (var node in highwayNodes)
            {
                int cellX = (int)(node.Value.coords.X / searchGridCellSize);
                if (!searchGrid.ContainsKey(cellX))
                {
                    searchGrid[cellX] = new Dictionary<int, List<Node>>();
                }
                int cellY = (int)(node.Value.coords.Y / searchGridCellSize);
                if (!searchGrid[cellX].ContainsKey(cellY))
                {
                    searchGrid[cellX][cellY] = new List<Node>();
                }
                searchGrid[cellX][cellY].Add(node.Value);
            }
        }

        public Map(string filename, double width, double height)
        {
            XmlDocument xml = new XmlDocument();
            xml.Load(filename);

            XmlNode bounds = xml.DocumentElement.SelectSingleNode("bounds");

            projectionParams = new MapProjectionParams(width, height,
                new GeoCoords(
                    Double.Parse(bounds.Attributes["minlat"].Value, CultureInfo.InvariantCulture),
                    Double.Parse(bounds.Attributes["minlon"].Value, CultureInfo.InvariantCulture)),
                new GeoCoords(
                    Double.Parse(bounds.Attributes["maxlat"].Value, CultureInfo.InvariantCulture),
                    Double.Parse(bounds.Attributes["maxlon"].Value, CultureInfo.InvariantCulture)));

            loadNodes(xml);
            loadWays(xml);
            fillGraph();
            fillSearchGrid();
        }

        public Node getNearestNode(Point point)
        {
            int cellX = (int)(point.X / searchGridCellSize);
            int cellY = (int)(point.Y / searchGridCellSize);

            if (searchGrid.ContainsKey(cellX) &&
                searchGrid[cellX].ContainsKey(cellY) &&
                searchGrid[cellX][cellY].Count > 0)
            {
                searchGrid[cellX][cellY].Sort(delegate(Node node1, Node node2)
                {
                    return (node1.coords - point).LengthSquared.CompareTo((node2.coords - point).LengthSquared);
                });

                return searchGrid[cellX][cellY].First(node => highwayNodes.ContainsKey(node.id));
            }
            else
            {
                throw new ArgumentException("No nodes to the point");
            }
        }

        public List<Node> findPath(Node from, Node to)
        {
            SortedSet<string> closed = new SortedSet<string>();
            SortedList<double, Node> opened = new SortedList<double, Node>();
            Dictionary<string, Node> cameFrom = new Dictionary<string, Node>();
            Dictionary<string, double> pathLength = new Dictionary<string, double>();

            bool isFound = false;

            opened.Add(0, from);
            pathLength[from.id] = 0;

            while (opened.Count > 0)
            {
                Node current = opened.Values[0];
                opened.RemoveAt(0);

                if (current == to)
                {
                    isFound = true;
                    break;
                }

                foreach (Node next in graph[current.id])
                {
                    double nextPathLength = pathLength[current.id] + (current.coords - next.coords).Length;
                    if (!pathLength.ContainsKey(next.id) || nextPathLength < pathLength[next.id])
                    {
                        pathLength[next.id] = nextPathLength;
                        double nextPriority = nextPathLength + (next.coords - to.coords).Length;
                        opened.Add(nextPriority, next);
                        cameFrom[next.id] = current;
                    }
                }
            }

            if (isFound)
            {
                List<Node> path = new List<Node>();

                Node current = to;
                while (current.id != from.id)
                {
                    path.Add(current);
                    current = cameFrom[current.id];
                }
                path.Add(from);

                path.Reverse();

                return path;
            }

            throw new ArgumentException("There is no way between the points");
        }
    }
}
