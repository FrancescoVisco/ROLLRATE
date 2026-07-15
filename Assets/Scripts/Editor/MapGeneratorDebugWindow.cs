using UnityEditor;
using UnityEngine;
using Rollrate.Map;

namespace Rollrate.Editor
{
    /// <summary>
    /// Window/Rollrate/Map Generator Debug. Generates a single Page with
    /// the chosen Page number and Grade, printing its structure as text -
    /// lets us validate MapGenerator's logic before building the real
    /// graphical Map UI on top of it.
    /// </summary>
    public class MapGeneratorDebugWindow : EditorWindow
    {
        private int pageNumber = 1;
        private int grade = 1;
        private string result = "";
        private Vector2 scroll;

        [MenuItem("Window/Rollrate/Map Generator Debug")]
        public static void ShowWindow()
        {
            GetWindow<MapGeneratorDebugWindow>("Map Generator Debug");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("ROLLRATE - Map Generator Debug", EditorStyles.boldLabel);
            pageNumber = EditorGUILayout.IntSlider("Page Number (1-3)", pageNumber, 1, 3);
            grade = EditorGUILayout.IntSlider("Grade (1-5)", grade, 1, 5);

            if (GUILayout.Button("Generate", GUILayout.Height(30)))
            {
                MapPage page = MapGenerator.GeneratePage(pageNumber, grade);
                result = FormatPage(page);
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(result, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private string FormatPage(MapPage page)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Page {page.pageNumber} (Grade {grade}) ===");

            for (int c = 0; c < page.columns.Count; c++)
            {
                sb.AppendLine($"-- Column {c} --");
                var column = page.columns[c];
                for (int r = 0; r < column.Count; r++)
                {
                    MapNode node = column[r];
                    string connections = node.connectionsToNextColumn.Count > 0
                        ? string.Join(",", node.connectionsToNextColumn)
                        : "(none)";
                    sb.AppendLine($"  Row {r}: {node.type,-10} -> next rows [{connections}]");
                }
            }

            return sb.ToString();
        }
    }
}
