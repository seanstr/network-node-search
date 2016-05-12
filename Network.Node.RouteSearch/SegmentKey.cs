using System;
using System.Collections.Generic;
using System.Linq;

namespace Network.Node.RouteSearch
{
    public struct SegmentKey
    {
        public string A { get; set; }
        public string B { get; set; }

        public SegmentKey(string a, string b) : this()
        {
            A = a;
            B = b;
        }

        public bool Equals(SegmentKey other)
        {
            return string.Equals(A, other.A) && string.Equals(B, other.B) || string.Equals(B, other.A) && string.Equals(A, other.B);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is SegmentKey && Equals((SegmentKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var order = new List<string> {A, B}.OrderBy(_ => _).ToList();
                return string.Concat(order[0], order[1]).GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format("{0}-{1}", A, B);
        }
    }
}