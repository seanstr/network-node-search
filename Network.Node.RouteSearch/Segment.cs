using System;
using System.Collections.Generic;
using System.Text;
using Network.Node.Data;

namespace Network.Node.RouteSearch
{
    public class Segment
    {
        public Segment(IList<cable_trees> children)
        {
            Children = children;
        }

        public string Start { get { return Children.Count > 0 ? Children[0].cable : String.Empty; } }
        public string End { get { return Children.Count > 0 ? Children[Children.Count - 1].child : String.Empty; } }
        public IList<cable_trees> Children { get; private set; }

        public override string ToString()
        {
            var builder = new StringBuilder();
            foreach (var child in Children)
            {
                builder.AppendLine(String.Format("{0}, {1}, {2}", child.cable, child.cable_name, child.child));
            }
            return String.Format("Start: {0}, End: {1}, Children: {2}", Start, End, builder);
        }
    }
}