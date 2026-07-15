using System.Collections.Generic;

namespace Rollrate.Map
{
    /// <summary>
    /// One Page (Settore) of an Echelon: a list of columns, each column a
    /// list of nodes (the "lanes"/branches at that horizontal position).
    /// Column 0 = Entry (always exactly 1 node). Last column = always
    /// exactly 1 node too (the Exit, forced Terminal on Page 3).
    /// </summary>
    public class MapPage
    {
        public int pageNumber; // 1-3
        public List<List<MapNode>> columns = new List<List<MapNode>>();

        public List<MapNode> EntryColumn => columns.Count > 0 ? columns[0] : null;
        public List<MapNode> ExitColumn => columns.Count > 0 ? columns[columns.Count - 1] : null;
    }
}
