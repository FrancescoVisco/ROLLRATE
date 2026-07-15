using System.Collections.Generic;

namespace Rollrate.Map
{
    /// <summary>
    /// One node in the procedural map. Column 0 is always the single Entry
    /// point (no gameplay content of its own). Runtime data only - not a
    /// ScriptableObject, since maps are generated fresh, not authored.
    /// </summary>
    public class MapNode
    {
        public int page;     // 1-3
        public int column;   // 0 = Entry, 1..N = body, last = Exit/Terminal
        public int row;      // lane index within this column
        public NodeType type;

        /// <summary>Row indices in the NEXT column this node connects to (1 or 2 - "Bivi").</summary>
        public List<int> connectionsToNextColumn = new List<int>();

        public bool visited;
        public bool isCurrent;

        public override string ToString() => $"P{page}C{column}R{row}:{type}";
    }
}
