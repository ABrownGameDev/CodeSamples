namespace Editor
{
    using UnityEngine;
    using System.Collections;
    using UnityEditor;

    /// <summary>
    /// This class offers a scene and inspector interface to the functionality of the BezierCurve class.
    /// It places buttons in the inspector to call functionality of the script.
    /// It places handles in the scene to allow easy manipulation of control points on the bezier curve.
    /// </summary>
    [CustomEditor(typeof(Utility.BezierCurve))]
    public class BezierCurveEditor : Editor
    {
        public void OnSceneGUI()
        {
            Utility.BezierCurve bezierCurve = (target as Utility.BezierCurve);

            if (bezierCurve.NumBezierPoints() == 0)
                return;

            Undo.undoRedoPerformed -= bezierCurve.GenerateBezierCurve;
            Undo.undoRedoPerformed += bezierCurve.GenerateBezierCurve;

            for (int i = 1; i < bezierCurve.NumBezierPoints(); i++)
            {
                Handles.DrawLine(bezierCurve.GetBezierPoint(i - 1), bezierCurve.GetBezierPoint(i));
            }

            for (int i = 0; i < bezierCurve.m_lControlPoints.Count; i++)
            {
                if (i == (bezierCurve.m_lControlPoints.Count / 2))
                    Handles.color = Color.green;
                else
                    Handles.color = Color.red;

                Vector3 pointPosition = Handles.FreeMoveHandle(bezierCurve.GetControlPoint(i), Quaternion.identity, 0.3f, Vector2.one, Handles.CircleCap);

                if (bezierCurve.CanSetControlPoint(i, pointPosition))
                {
                    Undo.RecordObject(target, "Change curve");
                    bezierCurve.SetControlPoint(i, pointPosition);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                }

                string label;

                if (i != 0 && i != bezierCurve.m_lControlPoints.Count - 1)
                    Handles.DrawLine(bezierCurve.GetControlPoint(i), bezierCurve.GetBezierPoint(
                        (int)((bezierCurve.NumBezierPoints() - 1) * ((float)i / (bezierCurve.m_lControlPoints.Count - 1)))));

                if (i == 0)
                    label = "Start Point";
                else if (i == bezierCurve.m_lControlPoints.Count - 1)
                    label = "End Point";
                else
                    label = i.ToString();

                GUIStyle style = new GUIStyle();
                style.richText = true;
                Handles.Label(bezierCurve.GetControlPoint(i), "<color=red>" + label + "</color>", style);
            }
        }

        public override void OnInspectorGUI()
        {
            Utility.BezierCurve curve = target as Utility.BezierCurve;

            if (curve.m_bInitialized == false)
                curve.Initialize();

            base.OnInspectorGUI();

            if (GUILayout.Button("Enclose"))
            {
                curve.Enclose();
            }

            if (GUILayout.Button("Mirror Along X"))
            {
                curve.Mirror(true, false);
            }

            if (GUILayout.Button("Mirror Along Y"))
            {
                curve.Mirror(false, true);
            }

            if (GUILayout.Button("Mirror Along X&Y"))
            {
                curve.Mirror(true, true);
            }

            if (GUILayout.Button("Generate Curve"))
            {
                curve.GenerateBezierCurve();
            }
        }
    }
}