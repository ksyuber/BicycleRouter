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
        public enum WayType
        {
            None = 0,
            Unspecified = 1,
            PedestrianWay = 2,
            DirtWay = 4,
            CarWay = 8,
            BicycleWay = 16
        }

        public List<Node> nodes = new List<Node>();
        public WayType surfaceType = WayType.None;
    }

    class Map
    {
        // параметры проекции карты
        public MapProjectionParams projectionParams;

        // список всех узлов карты
        public Dictionary<string, Node> nodes = new Dictionary<string, Node>();

        // список узлов состоящих в путях
        public Dictionary<string, Node> highwayNodes = new Dictionary<string, Node>();
        
        // список путей
        public List<Way> ways = new List<Way>();

        // граф путей, представлен в виде списка смежности
        public Dictionary<string, List<Node>> graph = new Dictionary<string, List<Node>>();

        // тип покрытия ребер графа
        public Dictionary<string, Dictionary<string, Way.WayType>> surfaceType = new Dictionary<string, Dictionary<string, Way.WayType>>();

        // размер сетки для быстрого поиска узлов
        private const int searchGridCellSize = 100;

        // сетка для быстрого поиска ближашего узла
        private Dictionary<int, Dictionary<int, List<Node>>> searchGrid = new Dictionary<int, Dictionary<int, List<Node>>>();

        // метод загрузки узлов
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

        // метод загрузки путей
        private void loadWays(XmlDocument xml)
        {
            SortedSet<string> highwayTypes = new SortedSet<string>();
            foreach (XmlNode node in xml.DocumentElement.SelectNodes("way[tag[@k='highway']]"))
            {
                Way way = new Way();
                var highwayNode = node.SelectSingleNode("tag[@k='highway']");
                var footNode = node.SelectSingleNode("tag[@k='foot']");
                var bicycleNode = node.SelectSingleNode("tag[@k='bicycle']");
                var surfaceNode = node.SelectSingleNode("tag[@k='surface']");
                string highway = highwayNode != null ? highwayNode.Attributes["v"].Value : "";
                string foot = footNode != null ? footNode.Attributes["v"].Value : "";
                string bicycle = bicycleNode != null ? bicycleNode.Attributes["v"].Value : "";
                string surface = surfaceNode != null ? surfaceNode.Attributes["v"].Value : "";
                highwayTypes.Add(highway);
                if (highway == "cycleway" || bicycle == "yes" || bicycle == "designated")
                {
                    way.surfaceType |= Way.WayType.BicycleWay;
                }
                if ((surface == "asphalt" || surface == "") &&
                    (highway == "motorway" || highway == "primary" || highway == "primary_link" ||
                    highway == "secondary" || highway == "secondary_link" ||
                    highway == "tertiary" || highway == "tertiary_link" ||
                    highway == "residential" || highway == "service" ||
                    highway == "trunk" || highway == "trunk_link"))
                {
                    way.surfaceType |= Way.WayType.CarWay;
                }
                if (highway == "footway" || highway == "pedestrian" || highway == "residential" ||
                    highway == "steps" || highway == "living_street" ||
                    highway == "path" ||
                    foot == "yes" || foot == "designated" || foot == "destination")
                {
                    way.surfaceType |= Way.WayType.PedestrianWay;
                }
                if (surface == "gravel" || surface == "unpaved" || surface == "grass" ||
                    surface == "ground" || surface == "paving_stones" || surface == "sand" ||
                    highway == "living_street" || highway == "track" || highway == "path" ||
                    highway == "bridleway")
                {
                    way.surfaceType |= Way.WayType.DirtWay;
                }
                if (highway == "unclassified" || highway == "yes")
                {
                    way.surfaceType |= Way.WayType.Unspecified;
                }

                foreach (XmlNode subNode in node.SelectNodes("nd"))
                {
                    Node wayNode = nodes[subNode.Attributes["ref"].Value];
                    way.nodes.Add(wayNode);
                    highwayNodes[wayNode.id] = wayNode;
                }

                ways.Add(way);
            }
        }

        // метод построения графа путей
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

                    if (!surfaceType.ContainsKey(nodeId))
                    {
                        surfaceType[nodeId] = new Dictionary<string, Way.WayType>();
                    }

                    if (i - 1 >= 0)
                    {
                        graph[nodeId].Add(way.nodes[i - 1]);
                        surfaceType[nodeId][way.nodes[i - 1].id] = way.surfaceType;
                    }
                    if (i + 1 < way.nodes.Count)
                    {
                        graph[nodeId].Add(way.nodes[i + 1]);
                        surfaceType[nodeId][way.nodes[i + 1].id] = way.surfaceType;
                    }
                }
            }
        }

        // метод построения сетки для быстрого поиска ближайшего узла
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

        // метод поиска ближайшего узла при помощи регулярной сетки
        public Node getNearestNode(Point point)
        {
            int cellX = (int)(point.X / searchGridCellSize);
            int cellY = (int)(point.Y / searchGridCellSize);

            if (searchGrid.ContainsKey(cellX) &&
                searchGrid[cellX].ContainsKey(cellY) &&
                searchGrid[cellX][cellY].Count > 0)
            {
                searchGrid[cellX][cellY].Sort(delegate (Node node1, Node node2)
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

        // метод поиска кратчайшего пути с учетом фильтров дорог
        public List<Node> findPath(Node from, Node to, Way.WayType wayType)
        {
            // список вершин на рассмотрение
            List<Tuple<Node, double>> opened = new List<Tuple<Node, double>>();
            // связь между узлами - из какого узла попали
            Dictionary<string, Node> cameFrom = new Dictionary<string, Node>();
            // длина пути от начального узла
            Dictionary<string, double> pathLength = new Dictionary<string, double>();

            // признак найденного пути
            bool isFound = false;

            opened.Add(Tuple.Create(from, 0.0));
            pathLength[from.id] = 0;

            while (opened.Count > 0)
            {
                // берем из очереди на рассмотрение самый ближайший узел к конечному узлу
                opened.Sort((x, y) => x.Item2.CompareTo(y.Item2));
                Node current = opened[0].Item1;
                opened.RemoveAt(0);

                // если дошли до конечного узла то завершаем поиск пути
                if (current == to)
                {
                    isFound = true;
                    break;
                }

                // просматриваем все соседние узлы с учетом фильтра по типу дорог
                foreach (Node next in graph[current.id].Where(x => (this.surfaceType[current.id][x.id] & wayType) != 0))
                {
                    // вычисляем новое расстояние от начального узла до следующего
                    double nextPathLength = pathLength[current.id] + (current.coords - next.coords).Length;
                    // если новое расстояние лучше прежнего вычисленного
                    if (!pathLength.ContainsKey(next.id) || nextPathLength < pathLength[next.id])
                    {
                        // запоминаем новое расстояние, добавляем узел на рассмотрение и запоминаем откуда пришли
                        pathLength[next.id] = nextPathLength;
                        opened.Add(Tuple.Create(next, nextPathLength + (next.coords - to.coords).Length));
                        cameFrom[next.id] = current;
                    }
                }
            }

            // если путь найден, то необходимо восстановить найденный путь
            if (isFound)
            {
                // список узлов найденного пути
                List<Node> path = new List<Node>();

                // начинаем с конечного узла, порядок узлов в пути будет обратным
                Node current = to;
                // пока не дошли до начального узла
                while (current.id != from.id)
                {
                    path.Add(current);
                    current = cameFrom[current.id];
                }
                path.Add(from);

                // оборачиваем порядок узлов в пути, теперь порядок верный
                path.Reverse();

                return path;
            }

            // если путь не найден, то выбрасываем исключение
            throw new ArgumentException("There is no way between the points");
        }
    }
}
