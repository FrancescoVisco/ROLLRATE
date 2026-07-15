using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Rollrate.Core;
using Rollrate.Data;

namespace Rollrate.Map
{
    /// <summary>
    /// Renders and drives the current Page: generates it, positions node
    /// buttons in a grid, draws simple connection lines, tracks the
    /// player's position, and enters a node's scene when clicked.
    ///
    /// SCOPE SO FAR: Merchant/Bonfire/Dismantle/Conflict/Overload/Glitch/
    /// Archive are all fully functional. Only Terminal (Boss +
    /// Recalibration) still logs a placeholder message instead of
    /// transitioning.
    /// </summary>
    public class MapController : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private Transform nodesContainer;
        [SerializeField] private Transform linesContainer;
        [SerializeField] private GameObject nodeButtonPrefab;
        [SerializeField] private GameObject connectionLinePrefab; // a plain Image, stretched/rotated between 2 points
        [Tooltip("Maximum column spacing - shrinks automatically if the page has too many columns to fit the container at this spacing.")]
        [SerializeField] private float columnSpacing = 220f;
        [Tooltip("Maximum row spacing - shrinks automatically if the widest column has too many rows to fit the container at this spacing.")]
        [SerializeField] private float rowSpacing = 160f;
        [SerializeField] private float lineThickness = 4f;
        [Tooltip("Visual size of one node button - subtracted from the container's available space before computing spacing, so edge nodes don't visually overflow past the container even when their CENTER is correctly positioned inside it.")]
        [SerializeField] private Vector2 nodeVisualSize = new Vector2(100f, 100f);

        [Header("Node Type -> Scene mapping (leave blank for 'not yet wired')")]
        [SerializeField] private string merchantSceneName = "ShopScene";
        [SerializeField] private string bonfireSceneName = "RestNodeScene";
        [SerializeField] private string dismantleSceneName = "DismantleScene";
        [SerializeField] private string combatSceneName = "CombatScene";
        [SerializeField] private string archiveSceneName = "ArchiveScene";

        [Header("Combat Node Setup")]
        [SerializeField] private EnemyRegistry enemyRegistry;

        private MapPage _currentPage;
        private MapNode _currentNode;
        private readonly List<GameObject> _spawnedVisuals = new List<GameObject>();
        private readonly Dictionary<MapNode, MapNodeButtonUI> _buttonsByNode = new Dictionary<MapNode, MapNodeButtonUI>();
        private string _pendingSceneName;

        private void Start()
        {
            if (RunManager.Instance == null)
            {
                Debug.LogError("[MapController] RunManager.Instance is null - the Map scene must be the persistent base scene with RunManager, loaded first.");
                return;
            }

            SceneManager.sceneUnloaded += OnAnySceneUnloaded;

            // A stretch-anchored NodesContainer's rect.width/height isn't
            // reliably finalized on the very first frame - wait one frame
            // so the layout has settled before reading it for spacing math.
            StartCoroutine(GenerateFirstPageNextFrame());
        }

        private System.Collections.IEnumerator GenerateFirstPageNextFrame()
        {
            yield return null;
            GenerateAndRenderPage(RunManager.Instance.State.currentPage);
        }

        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnAnySceneUnloaded;
        }

        private void GenerateAndRenderPage(int pageNumber)
        {
            var state = RunManager.Instance.State;
            state.currentPage = pageNumber;

            _currentPage = MapGenerator.GeneratePage(pageNumber, state.currentEchelon);
            _currentNode = _currentPage.EntryColumn[0];
            _currentNode.visited = true;
            _currentNode.isCurrent = true;

            RenderPage();
        }

        private void RenderPage()
        {
            foreach (GameObject go in _spawnedVisuals) if (go != null) Destroy(go);
            _spawnedVisuals.Clear();
            _buttonsByNode.Clear();

            // Compute how many columns/rows this Page actually needs, then
            // shrink the spacing (never grow it beyond the serialized
            // values) so the whole page always fits inside the container's
            // actual size - Grade V pages (up to 7 columns) would otherwise
            // overflow a container sized for Grade I's 3 columns.
            var containerRect = nodesContainer as RectTransform;
            int totalColumns = _currentPage.columns.Count;
            int maxRows = 1;
            foreach (var col in _currentPage.columns) maxRows = Mathf.Max(maxRows, col.Count);

            float effectiveColumnSpacing = columnSpacing;
            float effectiveRowSpacing = rowSpacing;

            if (containerRect != null && containerRect.rect.width > 1f && containerRect.rect.height > 1f)
            {
                // Subtract the node's own size first - otherwise we're only
                // guaranteeing node CENTERS stay inside the container, and
                // the edge nodes' visual bounds still poke out past it by
                // roughly half their width/height.
                float usableWidth = Mathf.Max(0f, containerRect.rect.width - nodeVisualSize.x);
                float usableHeight = Mathf.Max(0f, containerRect.rect.height - nodeVisualSize.y);

                if (totalColumns > 1)
                {
                    effectiveColumnSpacing = Mathf.Min(columnSpacing, usableWidth / (totalColumns - 1));
                }
                if (maxRows > 1)
                {
                    effectiveRowSpacing = Mathf.Min(rowSpacing, usableHeight / (maxRows - 1));
                }
            }

            var positions = new Dictionary<MapNode, Vector2>();
            float totalWidth = (_currentPage.columns.Count - 1) * effectiveColumnSpacing;

            for (int c = 0; c < _currentPage.columns.Count; c++)
            {
                var column = _currentPage.columns[c];
                float totalHeight = (column.Count - 1) * effectiveRowSpacing;

                for (int r = 0; r < column.Count; r++)
                {
                    MapNode node = column[r];
                    Vector2 pos = new Vector2(-totalWidth / 2f + c * effectiveColumnSpacing, -totalHeight / 2f + r * effectiveRowSpacing);
                    positions[node] = pos;

                    GameObject buttonGO = Instantiate(nodeButtonPrefab, nodesContainer);
                    buttonGO.GetComponent<RectTransform>().anchoredPosition = pos;
                    var button = buttonGO.GetComponent<MapNodeButtonUI>();
                    button.Setup(node, this);
                    _buttonsByNode[node] = button;
                    _spawnedVisuals.Add(buttonGO);
                }
            }

            // Draw connection lines
            foreach (var column in _currentPage.columns)
            {
                foreach (MapNode node in column)
                {
                    foreach (int targetRow in node.connectionsToNextColumn)
                    {
                        if (node.column + 1 >= _currentPage.columns.Count) continue;
                        MapNode target = _currentPage.columns[node.column + 1][targetRow];
                        DrawLine(positions[node], positions[target]);
                    }
                }
            }

            RefreshAllButtons();
        }

        private void DrawLine(Vector2 from, Vector2 to)
        {
            if (connectionLinePrefab == null || linesContainer == null) return;

            GameObject lineGO = Instantiate(connectionLinePrefab, linesContainer);
            var rect = lineGO.GetComponent<RectTransform>();

            Vector2 diff = to - from;
            float distance = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            rect.sizeDelta = new Vector2(distance, lineThickness);
            rect.anchoredPosition = from + diff / 2f;
            rect.localRotation = Quaternion.Euler(0, 0, angle);

            _spawnedVisuals.Add(lineGO);
        }

        private void RefreshAllButtons()
        {
            foreach (var kvp in _buttonsByNode) kvp.Value.Refresh();
        }

        /// <summary>True if this node is directly reachable from the player's current position.</summary>
        public bool IsReachable(MapNode node)
        {
            if (_currentNode == null) return false;
            if (node.column != _currentNode.column + 1) return false;
            return _currentNode.connectionsToNextColumn.Contains(node.row);
        }

        /// <summary>
        /// Oscuramento (design doc Section 7, Grade IV+): node TYPES beyond
        /// 2 columns of distance from the player's current position are
        /// hidden ("???") until visited - structure/connections stay fully
        /// visible, only the content is unknown. Never applies below Grade IV.
        /// </summary>
        public bool IsHiddenByFog(MapNode node)
        {
            if (_currentNode == null) return false;
            if (RunManager.Instance.State.currentEchelon < 4) return false;
            return (node.column - _currentNode.column) > 2;
        }

        /// <summary>Maps a node type to its scene name, or null/empty if not wired up yet.</summary>
        private string GetSceneNameForType(NodeType type)
        {
            switch (type)
            {
                case NodeType.Merchant: return merchantSceneName;
                case NodeType.Bonfire: return bonfireSceneName;
                case NodeType.Dismantle: return dismantleSceneName;
                case NodeType.Conflict: return combatSceneName;
                case NodeType.Overload: return combatSceneName;
                case NodeType.Archive: return archiveSceneName;
                case NodeType.Terminal: return combatSceneName;
                default: return null; // Glitch already resolves to another type before this is called
            }
        }

        public void TryEnterNode(MapNode node)
        {
            if (!IsReachable(node)) return;

            if (node.type == NodeType.Glitch)
            {
                node.type = RollGlitchOutcome();
                Debug.Log($"[MapController] Glitch revealed as: {node.type}");
                if (_buttonsByNode.TryGetValue(node, out var revealedButton))
                {
                    revealedButton.Setup(node, this); // refreshes label + color to the revealed type
                }
            }

            string sceneName = GetSceneNameForType(node.type);
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.Log($"[MapController] {node.type} node isn't wired to a scene yet - staying on the Map for now.");
                return;
            }

            if (node.type == NodeType.Conflict || node.type == NodeType.Overload || node.type == NodeType.Terminal)
            {
                if (enemyRegistry == null)
                {
                    Debug.LogError("[MapController] No Enemy Registry assigned - can't pick an enemy for this fight.");
                    return;
                }

                var state = RunManager.Instance.State;
                EnemyTier tier = node.type == NodeType.Terminal ? EnemyTier.Guardian
                                : node.type == NodeType.Overload ? EnemyTier.Elite
                                : EnemyTier.Base;
                EnemyData enemy = enemyRegistry.GetRandom(state.currentEchelon, tier);

                if (enemy == null)
                {
                    Debug.LogError($"[MapController] No {tier} enemy found for Grade {state.currentEchelon} - check the Enemy Registry.");
                    return;
                }

                CombatNodeContext.PendingEnemy = enemy;
            }

            _pendingSceneName = sceneName;
            NodeSceneLoader.EnterNode(sceneName);

            // Advance position immediately (visually) - the actual node
            // "content" plays out in the additively loaded scene.
            _currentNode.isCurrent = false;
            node.visited = true;
            node.isCurrent = true;
            _currentNode = node;
            RefreshAllButtons();
        }

        /// <summary>
        /// Glitch reveal: one of the other 6 real node types, picked
        /// uniformly at random. Never Entry/Terminal (structural, not
        /// random) or Glitch itself (would be a no-op reveal).
        /// </summary>
        private NodeType RollGlitchOutcome()
        {
            NodeType[] possibleOutcomes =
            {
                NodeType.Conflict,
                NodeType.Merchant,
                NodeType.Archive,
                NodeType.Overload,
                NodeType.Bonfire,
                NodeType.Dismantle
            };
            return possibleOutcomes[Random.Range(0, possibleOutcomes.Length)];
        }

        private void OnAnySceneUnloaded(Scene unloadedScene)
        {
            if (unloadedScene.name != _pendingSceneName) return;
            _pendingSceneName = null;

            // Any node that caused defeat (Combat HP 0, Archive's Ambition
            // failure, etc.) fragments progress back to Grade I, Page 1 -
            // GameState.ApplyFragmentation (via RunManager.HandleDefeat)
            // already reset currentEchelon/currentPage, so just regenerate.
            if (CombatNodeContext.LastNodeCausedDefeat)
            {
                CombatNodeContext.LastNodeCausedDefeat = false;
                Debug.Log("[MapController] Defeat - Fragmentation. Returning to Grade I, Page 1.");
                GenerateAndRenderPage(1);
                return;
            }

            // Reached the end of this Page - generate the next one.
            if (_currentNode.column == _currentPage.columns.Count - 1)
            {
                var state = RunManager.Instance.State;
                if (state.currentPage < 3)
                {
                    GenerateAndRenderPage(state.currentPage + 1);
                }
                else
                {
                    ApplyRecalibration(state);
                }
            }
        }

        /// <summary>
        /// Post-Terminal-victory transition (design doc Section 7): pay the
        /// Tassa di Sfarzo (a percentage of CURRENT Scrap - always payable
        /// by construction, since it's proportional rather than a fixed
        /// amount), then mandatory Core Die evolution (the ONLY way the
        /// Core evolves - never via the Shop), then advance to the next
        /// Grade's Page 1. Grade V has no further Grade to advance to -
        /// treated as a full run completion for now.
        /// </summary>
        private void ApplyRecalibration(GameState state)
        {
            // Tax percentage for the transition LEAVING the current grade (I->II 10%, II->III 15%, III->IV 20%, IV->V 25%).
            float[] taxByGrade = { 0.10f, 0.15f, 0.20f, 0.25f };

            if (state.currentEchelon <= taxByGrade.Length)
            {
                int tax = Mathf.CeilToInt(state.scrap * taxByGrade[state.currentEchelon - 1]);
                state.scrap -= tax;
                Debug.Log($"[MapController] Recalibrazione: Tassa di Sfarzo pagata ({tax} Scrap).");
            }

            if (state.coreDie.nextTier != null)
            {
                Debug.Log($"[MapController] Recalibrazione: Core evoluto da {state.coreDie.displayName} a {state.coreDie.nextTier.displayName}.");
                state.coreDie = state.coreDie.nextTier;
            }
            else
            {
                Debug.Log($"[MapController] Recalibrazione: Core già al Grado massimo ({state.coreDie.displayName}) - nessuna evoluzione.");
            }

            if (state.currentEchelon < 5)
            {
                state.currentEchelon++;
                Debug.Log($"[MapController] Recalibrazione completata - avanzato a Grado {state.currentEchelon}.");
                GenerateAndRenderPage(1);
            }
            else
            {
                Debug.Log("[MapController] Guardiano di Grado V sconfitto - run completata! (schermata di fine run non ancora implementata)");
            }
        }
    }
}
