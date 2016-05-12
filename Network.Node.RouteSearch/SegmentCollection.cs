using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Network.Node.Data;

namespace Network.Node.RouteSearch
{
    public class SegmentCollection : Dictionary<SegmentKey, IList<Segment>>
    {
        private readonly IDictionary<string, IList<cable_trees>> _junctions;
        private readonly IDictionary<string, IList<cable_trees>> _paths;
        private readonly IDictionary<string, ICollection<SegmentKey>> _neighbors = new Dictionary<string, ICollection<SegmentKey>>();
        private readonly IDictionary<string, SegmentKey> _reverseLookup = new Dictionary<string, SegmentKey>();     //to lookup segments from children

        public SegmentCollection(IDictionary<string, IList<cable_trees>> junctions, IDictionary<string, IList<cable_trees>> paths)
        {
            _junctions = junctions;
            _paths = paths;
            foreach (var junction in junctions)
            {
                LoadJunctionChildren(junction);
            }
        }

        public IList<KeyValuePair<SegmentKey, IList<Segment>>> GetSegmentsFor(string clli)
        {
            var tupleKeys = _neighbors.ContainsKey(clli) ? _neighbors[clli] : new []{_reverseLookup[clli]};
            return tupleKeys.Select(tupleKey => new KeyValuePair<SegmentKey, IList<Segment>>(tupleKey, this[tupleKey])).ToList();
        }


        private void LoadJunctionChildren(KeyValuePair<string, IList<cable_trees>> junction)
        {
            var children = FindCable(junction.Key);
            if (children.Count < 2)
            {
                //Console.WriteLine("Not a junction");
                return;
            }
            //get distinct children to allow for multiple parallel cables
            foreach (var child in children.Select(c => c.child).Distinct())
            {
                var segment = new Segment(new List<cable_trees>(children.Where(c => c.child == child)));
                var alreadyProcessed = new HashSet<string>();
                var localChild = child;
                var parent = junction.Key;
                while (true)
                {
                    if (parent == localChild)
                    {
                        //Console.Write("Failed to make segment for cable {0} to and from values the same", parent);
                        break;
                    }
                    if (alreadyProcessed.Contains(parent))
                        break;
                    alreadyProcessed.Add(parent);
                    var next = GetNext(parent, localChild);
                    //end point or junction node, end the segment and start a new one
                    //we only want to consider distinct values in case of multiple parallel cables
                    if (next == null || next.Select(c => c.child).Distinct().Count() != 1)
                    {
                        break;
                    }

                    //path node, keep crawling
                    foreach (var cable in next)
                    {
                        segment.Children.Add(cable);
                    }
                    localChild = next[0].child;
                    parent = next[0].cable;
                }
                //Console.WriteLine(segment);
                var key = new SegmentKey(segment.Start, segment.End);
                AddNeighborKey(key);
                AddReverseLookup(segment);
                AddSegment(key, segment);
            }
        }

        private void AddSegment(SegmentKey key, Segment newSegment)
        {
            if (!this.ContainsKey(key))
            {
                this[key] = new List<Segment>();
            }
            this[key].Add(newSegment);
        }

        private void AddReverseLookup(Segment segment)
        {
            foreach (var child in segment.Children)
            {
                if (child.cable != segment.Start && child.child != segment.Start)
                {
                    _reverseLookup[child.cable] = new SegmentKey(segment.Start, segment.End);
                }
                if (child.cable != segment.End && child.child != segment.End)
                {
                    _reverseLookup[child.child] = new SegmentKey(segment.Start, segment.End);
                }
            }
        }

        private void AddNeighborKey(SegmentKey key)
        {
            if (!_neighbors.ContainsKey(key.A))
            {
                _neighbors[key.A] = new HashSet<SegmentKey>();
            }
            _neighbors[key.A].Add(key);
            if (!_neighbors.ContainsKey(key.B))
            {
                _neighbors[key.B] = new HashSet<SegmentKey>();
            }
            _neighbors[key.B].Add(key);
        }


        private IList<cable_trees> FindCable(string cable)
        {
            if (!_paths.ContainsKey(cable) && !_junctions.ContainsKey(cable))
            {
                //Console.WriteLine("Disconnected cable {0}", cable);
                return null;
            }
            return _paths.ContainsKey(cable) ? _paths[cable] : _junctions[cable];
        }

        public IList<cable_trees> GetNext(string parent, string child)
        {
            var fnd = FindCable(child);
            if (fnd == null)
            {
                return null;
            }
            return fnd.Where(_ => _.child != parent).ToList();
        }

    }
}