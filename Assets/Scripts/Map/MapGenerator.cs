using System.Collections.Generic;
using UnityEngine;

namespace Rollrate.Map
{
    /// <summary>
    /// Generates one Page of the procedural map, following the design
    /// doc's Section 7 rules: base percentages per node type (varying by
    /// Page 1/2/3), Grade-based caste modifiers, and the safety filters
    /// (Double Conflict, Terminal Check, Shop Adjacency).
    ///
    /// SIMPLIFICATIONS (documented, not silent):
    /// - "Bivi" (fork) percentage governs whether a node connects to 1 or 2
    ///   nodes in the next column; connectivity is guaranteed (no orphaned
    ///   unreachable nodes), but exact fork topology is randomized rather
    ///   than hand-authored.
    /// - The Double Conflict Filter is applied per ROW-LANE (same row index
    ///   across consecutive columns), not per actual graph path, since a
    ///   branching graph can have many paths to the same node. This is a
    ///   practical approximation of "un percorso" from the design doc.
    /// - Oscuramento (Grade IV+ fog of war) and Singolarità (Grade V forced
    ///   fight off-path) are gameplay/UI rules, not generation rules - they
    ///   belong in the Map UI / node-entry logic, not here.
    /// </summary>
    public static class MapGenerator
    {
        public static int BodyColumnCount = 4;
        public static int RowsPerColumn = 3;

        /// <summary>Base percentages per node type, for P1/P2/P3 (index 0/1/2). Must sum to 100 per page.</summary>
        private static readonly Dictionary<NodeType, float[]> BasePercentages = new()
        {
            { NodeType.Conflict,  new[] { 50f, 35f, 45f } },
            { NodeType.Merchant,  new[] { 15f, 15f, 20f } },
            { NodeType.Archive,   new[] { 15f, 10f, 10f } },
            { NodeType.Overload,  new[] {  0f, 15f,  5f } },
            { NodeType.Glitch,    new[] { 10f, 10f,  5f } },
            { NodeType.Bonfire,   new[] {  5f, 10f, 10f } },
            { NodeType.Dismantle, new[] {  5f,  5f,  5f } },
        };

        /// <summary>"Bivi" (fork) frequency per page - chance a node connects to 2 nodes in the next column instead of 1.</summary>
        private static readonly float[] ForkChance = { 0.15f, 0.30f, 0.50f };

        public static MapPage GeneratePage(int pageNumber, int grade)
        {
            var page = new MapPage { pageNumber = pageNumber };

            // --- Column 0: Entry (single node, no gameplay content, never clickable) ---
            var entryColumn = new List<MapNode> { new MapNode { page = pageNumber, column = 0, row = 0, type = NodeType.Entry } };
            page.columns.Add(entryColumn);

            // --- Body columns ---
            for (int c = 1; c <= BodyColumnCount; c++)
            {
                var column = new List<MapNode>();
                for (int r = 0; r < RowsPerColumn; r++)
                {
                    NodeType type = RollNodeType(pageNumber, grade);
                    column.Add(new MapNode { page = pageNumber, column = c, row = r, type = type });
                }
                ApplyDoubleConflictFilter(page, column, pageNumber, grade);
                page.columns.Add(column);
            }

            // --- Exit column: forced Terminal on Page 3, otherwise a normal single rolled node ---
            int exitColumnIndex = BodyColumnCount + 1;
            NodeType exitType = pageNumber == 3 ? NodeType.Terminal : RollNodeType(pageNumber, grade);
            var exitColumn = new List<MapNode> { new MapNode { page = pageNumber, column = exitColumnIndex, row = 0, type = exitType } };
            page.columns.Add(exitColumn);

            GenerateConnections(page, pageNumber);
            ApplyShopAdjacencyFilter(page, pageNumber, grade);

            return page;
        }

        private static NodeType RollNodeType(int pageNumber, int grade)
        {
            int pageIndex = Mathf.Clamp(pageNumber - 1, 0, 2);
            var weights = new Dictionary<NodeType, float>();

            foreach (var kvp in BasePercentages)
            {
                weights[kvp.Key] = kvp.Value[pageIndex];
            }

            // Grade caste modifiers (Section 7): Grade II +10% Conflict, Grade III +10% Overload.
            if (grade == 2) weights[NodeType.Conflict] += 10f;
            if (grade == 3) weights[NodeType.Overload] += 10f;

            float total = 0f;
            foreach (var w in weights.Values) total += w;

            float roll = Random.Range(0f, total);
            float cumulative = 0f;
            foreach (var kvp in weights)
            {
                cumulative += kvp.Value;
                if (roll <= cumulative) return kvp.Key;
            }
            return NodeType.Conflict; // fallback, shouldn't normally hit
        }

        /// <summary>
        /// If the same row-lane generated 2 Conflict nodes in a row across
        /// the previous 2 columns, this one must be rerolled as Archive
        /// (60%) or Merchant (40%) instead - matching the design doc's
        /// "Regola del Doppio Conflitto" (originally Archivio 60% / Manutenzione 40%,
        /// updated here to Merchant since Manutenzione was unified into Merchant).
        /// </summary>
        private static void ApplyDoubleConflictFilter(MapPage page, List<MapNode> currentColumn, int pageNumber, int grade)
        {
            int columnIndex = currentColumn[0].column;
            if (columnIndex < 2) return; // need at least 2 previous columns to check

            for (int r = 0; r < currentColumn.Count; r++)
            {
                bool prevIsConflict = GetNodeAt(page, columnIndex - 1, r)?.type == NodeType.Conflict;
                bool prevPrevIsConflict = GetNodeAt(page, columnIndex - 2, r)?.type == NodeType.Conflict;

                if (currentColumn[r].type == NodeType.Conflict && prevIsConflict && prevPrevIsConflict)
                {
                    currentColumn[r].type = Random.value < 0.6f ? NodeType.Archive : NodeType.Merchant;
                }
            }
        }

        private static MapNode GetNodeAt(MapPage page, int column, int row)
        {
            if (column < 0 || column >= page.columns.Count) return null;
            var col = page.columns[column];
            return row < col.Count ? col[row] : null;
        }

        /// <summary>Connects each node to 1 or 2 nodes in the next column ("Bivi"), guaranteeing every node has at least one incoming connection.</summary>
        private static void GenerateConnections(MapPage page, int pageNumber)
        {
            float forkChance = ForkChance[Mathf.Clamp(pageNumber - 1, 0, 2)];

            for (int c = 0; c < page.columns.Count - 1; c++)
            {
                var current = page.columns[c];
                var next = page.columns[c + 1];
                var incomingCount = new int[next.Count];

                foreach (MapNode node in current)
                {
                    int primaryTarget = Mathf.Clamp(Mathf.RoundToInt((float)node.row / Mathf.Max(1, current.Count - 1) * (next.Count - 1)), 0, next.Count - 1);
                    node.connectionsToNextColumn.Add(primaryTarget);
                    incomingCount[primaryTarget]++;

                    bool fork = next.Count > 1 && Random.value < forkChance;
                    if (fork)
                    {
                        int altTarget = primaryTarget == next.Count - 1 ? primaryTarget - 1 : primaryTarget + 1;
                        altTarget = Mathf.Clamp(altTarget, 0, next.Count - 1);
                        if (altTarget != primaryTarget)
                        {
                            node.connectionsToNextColumn.Add(altTarget);
                            incomingCount[altTarget]++;
                        }
                    }
                }

                // Guarantee connectivity: any node in `next` with zero incoming connections gets one from a random node in `current`.
                for (int i = 0; i < next.Count; i++)
                {
                    if (incomingCount[i] == 0)
                    {
                        MapNode randomSource = current[Random.Range(0, current.Count)];
                        randomSource.connectionsToNextColumn.Add(i);
                    }
                }
            }
        }

        /// <summary>Rerolls the destination node's type if two directly-connected nodes would both be Merchant.</summary>
        private static void ApplyShopAdjacencyFilter(MapPage page, int pageNumber, int grade)
        {
            for (int c = 0; c < page.columns.Count - 1; c++)
            {
                var current = page.columns[c];
                var next = page.columns[c + 1];

                foreach (MapNode node in current)
                {
                    if (node.type != NodeType.Merchant) continue;

                    foreach (int targetRow in node.connectionsToNextColumn)
                    {
                        MapNode target = next[targetRow];
                        if (target.type == NodeType.Merchant)
                        {
                            NodeType replacement;
                            do { replacement = RollNodeType(pageNumber, grade); }
                            while (replacement == NodeType.Merchant);
                            target.type = replacement;
                        }
                    }
                }
            }
        }
    }
}
