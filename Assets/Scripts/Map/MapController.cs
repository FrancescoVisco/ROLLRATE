using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Rollrate.Core;

namespace Rollrate.Map
{
    /// <summary>
    /// Renders and drives the current Page: generates it, positions node
    /// buttons in a grid, draws simple connection lines, tracks the
    /// player's position, and enters a node's scene when clicked (if that
    /// node type is wired up yet - see sceneNameByType).
    ///
    /// SCOPE OF THIS FIRST VERSION: Merchant/Bonfire/Dismantle nodes are
    /// fully functional (their scenes already exist). Conflict/Overload/
    /// Terminal (need dynamic enemy selection + boss/Recalibration logic)
    /// and Archive/Glitch (not built yet) will log a placeholder message
    /// instead of transitioning - so map navigation is testable now without
    /// blocking on those still-pending systems.
    /// </summary>
    public class MapController : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private Transform nodesContainer;
        [SerializeField] private Transform linesContainer;
        [SerializeField] private GameObject nodeButtonPrefab;
        [SerializeField] private GameObject connectionLinePrefab; // a plain Image, stretched/rotated between 2 points
        [SerializeField] private float columnSpacing = 220f;
        [SerializeField] private float rowSpacing = 160f;
        [SerializeField] private float lineThickness = 4f;

        [Header("Node Type -> Scene mapping (leave blank for 'not yet wired')")]
        [SerializeField] private string merchantSceneName = "ShopScene";
        [SerializeField] private string bonfireSceneName = "RestNodeScene";
        [SerializeField] private string dismantleSceneName = "DismantleScene";

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

            var positions = new Dictionary<MapNode, Vector2>();
            float totalWidth = (_currentPage.columns.Count - 1) * columnSpacing;

            for (int c = 0; c < _currentPage.columns.Count; c++)
            {
                var column = _currentPage.columns[c];
                float totalHeight = (column.Count - 1) * rowSpacing;

                for (int r = 0; r < column.Count; r++)
                {
                    MapNode node = column[r];
                    Vector2 pos = new Vector2(-totalWidth / 2f + c * columnSpacing, -totalHeight / 2f + r * rowSpacing);
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

        /// <summary>Maps a node type to its scene name, or null/empty if not wired up yet.</summary>
        private string GetSceneNameForType(NodeType type)
        {
            switch (type)
            {
                case NodeType.Merchant: return merchantSceneName;
                case NodeType.Bonfire: return bonfireSceneName;
                case NodeType.Dismantle: return dismantleSceneName;
                default: return null; // Conflict/Overload/Archive/Glitch/Terminal - not wired yet
            }
        }

        public void TryEnterNode(MapNode node)
        {
            if (!IsReachable(node)) return;

            string sceneName = GetSceneNameForType(node.type);
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.Log($"[MapController] {node.type} node isn't wired to a scene yet - staying on the Map for now.");
                return;
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

        private void OnAnySceneUnloaded(Scene unloadedScene)
        {
            if (unloadedScene.name != _pendingSceneName) return;
            _pendingSceneName = null;

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
                    Debug.Log("[MapController] Reached the Terminal node's scene and returned - Echelon advancement/Recalibration isn't wired yet.");
                }
            }
        }
    }
}
