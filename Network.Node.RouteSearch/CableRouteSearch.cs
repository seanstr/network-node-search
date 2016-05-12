using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.Remoting;
using Network.Node.Data;
using System.Data;
using System.Diagnostics;

namespace Network.Node.RouteSearch
{
    public class CableRouteSearch
    {
        private clliEntities _clliEntities;

        public CableRouteSearch()
        {
            _clliEntities = new clliEntities();
        }

        public SegmentCollection Segments
        {
            get { return new CableTree(_clliEntities).Segments; }
        }

        /// <summary>
        /// Starts the route search and returns all discovered valid routes
        /// </summary>
        /// <returns>DataSet containing one DataTable for each discovered valid route</returns>
        public IEnumerable<CABLES_V> Search(string startCLLI, string endCLLI, string via = null)
        {
            if (startCLLI == endCLLI) return new CABLES_V[0];

            if (via == null || startCLLI == via || endCLLI == via)
                return FindRoute(startCLLI, endCLLI);
            
            var route1 = FindRoute(startCLLI, via).ToList();
            var route2 = FindRoute(via, endCLLI).ToList();
            if (route1.Last().CABLE_CLFI == route2.First().CABLE_CLFI)
                route2.RemoveAt(0);
            route1.AddRange(route2);
            return route1;
        }

        private IEnumerable<CABLES_V> FindRoute(string startCLLI, string endCLLI)
        {
            var startSegments = Segments.GetSegmentsFor(startCLLI);
            var endSegments = Segments.GetSegmentsFor(endCLLI);
            IEnumerable<string> route;
            if (startSegments.Count > 1)
            {
                var nodes = new Dictionary<string, int>();
                foreach (var startSegment in startSegments)
                {
                    if (!nodes.ContainsKey(startSegment.Key.A))
                        nodes[startSegment.Key.A] = 0;
                    if (!nodes.ContainsKey(startSegment.Key.B))
                        nodes[startSegment.Key.B] = 0;
                    nodes[startSegment.Key.A]++;
                    nodes[startSegment.Key.B]++;
                }
                string startNode = null;
                int max = 0;
                foreach (var key in nodes.Keys)
                {
                    if (nodes[key] > max)
                    {
                        startNode = key;
                        max = nodes[key];
                    }
                }
                route = FindRoute(startNode, endSegments);
            }
            else
            {
                route = FindRoute(new[] {startSegments[0].Key.A, startSegments[0].Key.B}, endSegments);
            }
            if (route == null)
            {
                throw new Exception("No route found");
            }
            var prunedRoute = PruneRoute(route);
            return ConvertToListOfCables(prunedRoute, startCLLI, endCLLI);
        }

        private IList<string> PruneRoute(IEnumerable<string> enumerableRoute)
        {
            var route = new List<string>(enumerableRoute);
            while (route.Distinct().Count() != route.Count)
            {
                var duplicate = route.First(_ => route.Count(__ => __ == _) > 1);
                var firstSegment = route.TakeWhile(_ => _ != duplicate);
                var lastNode = route[route.Count()-2]; // to deal with the lastSegment ending on the segment previous to the actual last one
                var lastSegment = route.AsEnumerable().Reverse().TakeWhile(_ => _ != duplicate).Reverse();
                route = new List<string>(firstSegment);
                route.Add(duplicate);
                if (lastSegment.Any())
                {
                    route.AddRange(lastSegment);
                }
                else
                {
                    route.Add(lastNode);
                }
            }
            return route;
        }

        private IEnumerable<string> FindRoute(IList<string> nodes, IList<KeyValuePair<SegmentKey, IList<Segment>>> endSegments)
        {
            //assumption that nodes only ever has 2 nodes since it would be the case of a segment
            if (nodes.Count != 2) throw new Exception("Nodes should only have 2 items");

            var routes = nodes.Select(node => FindRoute(node, endSegments)).Where(_ => _ != null && _.Any()).ToList();
            var minRouteCount = routes.Min(_ => _.Count());
            var route = routes.First(_ => _.Count() == minRouteCount).Select(_ => _).ToList();
            if (nodes.All(route.Contains)) return route;

            //In this case the route starts in the middle of the segment and we need to ensure that portion of it is in the returned route
            var includedNode = nodes.First(route.Contains);
            var missingNode = nodes.First(_ => !route.Contains(_));
            if (route[0] == includedNode)
                route.Insert(0, missingNode);
            else
                route.Add(missingNode);
            return route;
        }

        private IEnumerable<string> FindRoute(string node, IList<KeyValuePair<SegmentKey, IList<Segment>>> endSegments)
        {
            var startSegments = Segments.GetSegmentsFor(node);

            Stack<KeyValuePair<string, ICollection<SegmentKey>>> route;
            Stack<KeyValuePair<string, ICollection<SegmentKey>>> shortestRoute = null;

            var processed = new HashSet<SegmentKey>();
            while (!startSegments.All(_ => processed.Contains(_.Key)))
            {
                //ensure that the end segment can always be processed
                foreach (var endSegment in endSegments)
                    processed.Remove(endSegment.Key);

                route = new Stack<KeyValuePair<string, ICollection<SegmentKey>>>();
                var remainingStartSegments = startSegments.Reverse().Where(_ => !processed.Contains(_.Key)).ToList();
                route.Push(new KeyValuePair<string, ICollection<SegmentKey>>(node, new HashSet<SegmentKey>(remainingStartSegments.Select(_ => _.Key))));
                var searchSegments = new Stack<KeyValuePair<SegmentKey, IList<Segment>>>(remainingStartSegments);

                while (searchSegments.Count > 0)
                {
                    var toProcess = searchSegments.Pop();
                    var processingNode = route.Peek();

                    processed.Add(toProcess.Key);
                    processingNode.Value.Remove(toProcess.Key);
                    string nextNode = toProcess.Key.A == processingNode.Key ? toProcess.Key.B : toProcess.Key.A;
                    var nextRoute = new KeyValuePair<string, ICollection<SegmentKey>>(nextNode, new HashSet<SegmentKey>());
                    route.Push(nextRoute);
                    foreach (var nextSegment in Segments.GetSegmentsFor(nextNode).Where(_ => !processed.Contains(_.Key)))
                    {
                        searchSegments.Push(nextSegment);
                        nextRoute.Value.Add(nextSegment.Key);
                    }

                    if (endSegments.Any(_ => Equals(_.Key, toProcess.Key)))
                    {
                        break;
                    }

                    if (route.Count == 0)
                    {
                        break;
                    }

                    //ensure we're not adding an empty route
                    //dead end
                    while (route.Peek().Value.Count == 0)
                    {
                        route.Pop();
                        if (route.Count == 0)
                        {
                            break;
                        }
                    }

                    if (route.Count == 0)
                    {
                        break;
                    }

                }
                if (shortestRoute == null || (route.Count != 0 && shortestRoute.Count > route.Count))
                {
                    shortestRoute = new Stack<KeyValuePair<string, ICollection<SegmentKey>>>(route);
                }
                //shortest possible route to prevent infinite loop
                if (shortestRoute.Count >= 1 && shortestRoute.Count <= 2) break;
            }

            return shortestRoute == null ? new String[0] : shortestRoute.Select(_ => _.Key);
        }

        private IEnumerable<CABLES_V> ConvertToListOfCables(IList<string> route, string startCLLI, string endCLLI)
        {
            var segmentKeys = new List<SegmentKey>();
            //we want to traverse the route in reverse since it is originally a stack
            for (int i = route.Count - 2; i >= 0; i--)
            {
                segmentKeys.Add(new SegmentKey(route[i + 1], route[i]));
            }
            var unprunedSegments = segmentKeys.SelectMany(_ =>
            {
                var segments = Segments[_];
                return segments.Count == 1 ? segments : segments.Where(s => s.Start == _.A && s.End == _.B);
            }).SelectMany(s => s.Children).Select(s => s.cable_name).ToList();
            var unorderedCables = _clliEntities.CABLES_V.Where(_ => unprunedSegments.Contains(_.CABLE_CLFI)).ToList(); 

            //find route within the list
            var nextCLLI = startCLLI;
            CABLES_V startCable = null;
            bool oneEnd = false;
            CABLES_V nextCable;
            var prunedCables = new List<CABLES_V>();
            do
            {
                nextCable = unorderedCables.FirstOrDefault(_ => !prunedCables.Contains(_) && _.START_CUST_SITE_CLLI == nextCLLI) ??
                            unorderedCables.FirstOrDefault(_ => !prunedCables.Contains(_) && _.END_CUST_SITE_CLLI == nextCLLI);
                if (startCable == null) startCable = nextCable;
                if (nextCable == null && startCable != null && !oneEnd)
                {
                    oneEnd = true;
                    prunedCables.Clear();
                    nextCable = unorderedCables.FirstOrDefault(_ => !CablesEquivalent(startCable, _) && _.START_CUST_SITE_CLLI == startCLLI) ??
                            unorderedCables.FirstOrDefault(_ => !CablesEquivalent(startCable, _) && _.END_CUST_SITE_CLLI == startCLLI);
                }
                if (nextCable == null) break;


                prunedCables.AddRange(unorderedCables.Where(_ => CablesEquivalent(_, nextCable)));
                nextCLLI = nextCable.START_CUST_SITE_CLLI == nextCLLI ? nextCable.END_CUST_SITE_CLLI : nextCable.START_CUST_SITE_CLLI;
            } while (nextCable.START_CUST_SITE_CLLI != endCLLI && nextCable.END_CUST_SITE_CLLI != endCLLI && prunedCables.Count < unprunedSegments.Count);

            return prunedCables;
        }

        private static bool CablesEquivalent(CABLES_V a, CABLES_V b)
        {
            return (a.START_CUST_SITE_CLLI == b.START_CUST_SITE_CLLI &&
                    a.END_CUST_SITE_CLLI == b.END_CUST_SITE_CLLI) ||
                   (a.START_CUST_SITE_CLLI == b.END_CUST_SITE_CLLI &&
                    a.END_CUST_SITE_CLLI == b.START_CUST_SITE_CLLI);
        }
    }
}