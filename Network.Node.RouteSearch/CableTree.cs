using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Network.Node.Data;

namespace Network.Node.RouteSearch
{
    public class CableTree
    {
        private readonly clliEntities _db;
        private static IDictionary<string, IList<cable_trees>> _junctions;
        private static IDictionary<string, IList<cable_trees>> _paths;
        private static SegmentCollection _segments;
        private static readonly object Semafor = new object();
        private static DateTime _segmentsCreated;
        private static readonly int CacheTtl = Convert.ToInt32(ConfigurationManager.AppSettings["RouteCacheTTL"]);

        public CableTree(clliEntities db)
        {
            _db = db;
        }

        public SegmentCollection Segments
        {
            get
            {
                lock (Semafor)
                {
                    if (_segments != null && _segmentsCreated >= DateTime.Now.AddSeconds(-CacheTtl)) return _segments;
                    Clear();
                    _segments = new SegmentCollection(Junctions, Paths);
                    _segmentsCreated = DateTime.Now;
                    return _segments;
                }
            }
        }

        public void Clear()
        {
            lock (Semafor)
            {
                _segments = null;
                _junctions = null;
                _paths = null;
            }
        }

        private IDictionary<string, IList<cable_trees>> Junctions
        {
            get
            {
                lock (Semafor)
                {
                    if (_junctions != null) return _junctions;
                    _junctions = new Dictionary<string, IList<cable_trees>>();
                    //all cable cllis that have more than 2 associated end points
                    //we'll put them all in a list keyd by the parent cable clli
                    var subselect =
                        _db.cable_trees.GroupBy(_ => _.cable)
                            .Where(grp => grp.Count() > 2)
                            .Select(grp => grp.Key);

                    foreach (var cable in _db.cable_trees.Where(_ => subselect.Contains(_.cable)))
                    {
                        if (!_junctions.ContainsKey(cable.cable))
                        {
                            _junctions[cable.cable] = new List<cable_trees>();
                        }
                        if (_junctions[cable.cable].All(_ => _.id != cable.id))
                            _junctions[cable.cable].Add(cable);
                    }
                }
                return _junctions;
            }
        }

        private IDictionary<string, IList<cable_trees>> Paths
        {
            get
            {
                lock (Semafor)
                {
                    if (_paths != null) return _paths;
                    _paths = new Dictionary<string, IList<cable_trees>>();
                    //all cable cllis that have more than 2 associated end points
                    //we'll put them all in a list keyd by the parent cable clli
                    var subselect =
                        _db.cable_trees.GroupBy(_ => _.cable)
                            .Where(grp => grp.Count() <= 2)
                            .Select(grp => grp.Key);

                    foreach (var cable in _db.cable_trees.Where(_ => subselect.Contains(_.cable)))
                    {
                        if (!_paths.ContainsKey(cable.cable))
                        {
                            _paths[cable.cable] = new List<cable_trees>();
                        }
                        if (_paths[cable.cable].All(_ => _.id != cable.id))
                            _paths[cable.cable].Add(cable);
                    }
                }
                return _paths;

            }
        }
    }
}