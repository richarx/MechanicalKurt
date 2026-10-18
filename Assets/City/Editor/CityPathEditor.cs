using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CityPath), true)]
public class CityPathEditor : OdinEditor
{
    private bool _isAddingPoints;
    private int _selectedIndex = -1;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        CityPath path = (CityPath)target;
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            _isAddingPoints = GUILayout.Toggle(_isAddingPoints, "Ajouter des points dans la Scene", "Button");
            if (_isAddingPoints)
                EditorGUILayout.HelpBox("Survoler un collider et appuyer sur Ctrl gauche pour ajouter un point. Echap termine l'ajout.", MessageType.Info);

            if (_selectedIndex >= 0 && _selectedIndex < path.Waypoints.Count)
            {
                EditorGUILayout.LabelField($"Point selectionne : {_selectedIndex}");
                if (GUILayout.Button("Supprimer ce point"))
                {
                    Undo.RecordObject(path, "Supprimer un point de chemin");
                    path.RemovePoint(_selectedIndex);
                    _selectedIndex = Mathf.Min(_selectedIndex, path.Waypoints.Count - 1);
                    SaveChange(path);
                }
            }
        }
 }

    private void OnSceneGUI()
    {
        CityPath path = (CityPath)target;
        if (Application.isPlaying)
            return;

        Event current = Event.current;
        if (_isAddingPoints && current.type == EventType.KeyDown)
        {
            if (current.keyCode == KeyCode.Escape)
            {
                _isAddingPoints = false;
                current.Use();
                Repaint();
            }
            else if (current.keyCode == KeyCode.LeftControl)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    Undo.RecordObject(path, "Ajouter un point de chemin");
                    path.AddPoint(hit.point);
                    _selectedIndex = path.Waypoints.Count - 1;
                    SaveChange(path);
                }
                current.Use();
            }
        }

        Color previousColor = Handles.color;
        for (int i = 0; i < path.PointCount; i++)
        {
            Vector3 position = path.GetPointPosition(i);
            Handles.color = i == _selectedIndex ? Color.yellow : path.GizmoColor;
            float size = HandleUtility.GetHandleSize(position) * 0.08f;
            if (Handles.Button(position, Quaternion.identity, size, size, Handles.SphereHandleCap))
            {
                _selectedIndex = i;
                Repaint();
            }
        }
        Handles.color = previousColor;

        if (_selectedIndex < 0 || _selectedIndex >= path.PointCount)
            return;

        EditorGUI.BeginChangeCheck();
        Quaternion rotation = UnityEditor.Tools.pivotRotation == PivotRotation.Local ? path.transform.rotation : Quaternion.identity;
        Vector3 movedPosition = Handles.PositionHandle(path.GetPointPosition(_selectedIndex), rotation);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(path, "Deplacer un point de chemin");
            path.UpdatePointFromEditor(_selectedIndex, path.transform.InverseTransformPoint(movedPosition));
            SaveChange(path);
        }
    }

    private static void SaveChange(CityPath path)
    {
        PrefabUtility.RecordPrefabInstancePropertyModifications(path);
        EditorUtility.SetDirty(path);
        SceneView.RepaintAll();
    }
}
