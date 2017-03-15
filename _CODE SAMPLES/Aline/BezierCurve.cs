namespace Utility
{
    using UnityEngine;
    using System.Collections;

    /// <summary>
    /// This class generates a BezierCurve given control points.
    /// This curve can then be used to place objects along a smooth curve, or to generate a mesh.
    /// </summary>
    public class BezierCurve : MonoBehaviour
    {
        public System.Collections.Generic.List<Vector2> m_lControlPoints;
        public int faces = 30;

        [HideInInspector]
        public System.Collections.Generic.List<Vector2> m_lv2BezierPoints;

        [HideInInspector]
        public bool m_bInitialized = false;

        [SerializeField, HideInInspector]
        public BezierCurveMesh m_cCurveMesh;

        [SerializeField, HideInInspector]
        private System.Action OnBezierChange;

        private float m_fTotalLineLength;
        public float TotalLineLength
        {
            get
            {
                return m_fTotalLineLength;
            }
        }

        private System.Action m_aOnDestroy;
        public void RegisterForOnDestroy(System.Action _newAction)
        {
            m_aOnDestroy -= _newAction;
            m_aOnDestroy += _newAction;
        }

        public void Initialize()
        {
            if (m_lv2BezierPoints != null)
                return;

            m_lControlPoints = new System.Collections.Generic.List<Vector2>(3);
            m_lControlPoints.Add(new Vector2(-1, 0));
            m_lControlPoints.Add(new Vector2(0, 0));
            m_lControlPoints.Add(new Vector2(1, 0));

            m_bInitialized = true;

            GenerateBezierCurve();
        }

        public void OnValidate()
        {
            if(m_cCurveMesh)
                SubscribeForBezierChange(m_cCurveMesh.GenerateCurveMesh);

            if (m_lControlPoints.Count > 21)
            {
                m_lControlPoints.RemoveRange(21, m_lControlPoints.Count - 21);
            }

            if (faces < 1)
                faces = 1;

            //GenerateBezierCurve();
        }

        public void GenerateBezierCurve()
        {
            m_lv2BezierPoints = new System.Collections.Generic.List<Vector2>(2);

            for (int i = 0; i <= faces; i++)
            {
                m_lv2BezierPoints.Add(GetPointAlongLine(i / (float)faces));
            }

            m_fTotalLineLength = DistanceAlongLine(m_lv2BezierPoints.Count - 1);

            if (OnBezierChange != null)
                OnBezierChange();
        }

        public Vector2 GetBezierPoint(int _index)
        {
            return m_lv2BezierPoints[_index] + (Vector2)transform.position;
        }

        public System.Collections.Generic.List<Vector2> GetLocalBezierPoints()
        {
            return m_lv2BezierPoints;
        }

        public System.Collections.Generic.List<Vector2> GetBezierPointsLocalToParent(bool _reverse = false)
        {
            System.Collections.Generic.List<Vector2> retVector = new System.Collections.Generic.List<Vector2>(m_lv2BezierPoints.Count);
            for (int i = _reverse ? retVector.Capacity - 1 : 0; _reverse ? i >= 0 : i < retVector.Capacity; i += _reverse ? -1 : 1)
            {
                retVector.Add(m_lv2BezierPoints[i] + (Vector2)this.transform.localPosition);
            }

            return retVector;
        }

        public int NumBezierPoints()
        {
            return m_lv2BezierPoints == null ? 0 : m_lv2BezierPoints.Count;
        }

        public Vector2 StartControlPoint()
        {
            return GetBezierPoint(0);
        }

        public Vector2 EndControlPoint()
        {
            return GetBezierPoint(m_lv2BezierPoints.Count - 1);
        }

        public void Enclose()
        {
            m_lControlPoints[m_lControlPoints.Count - 1] = m_lControlPoints[0];
            GenerateBezierCurve();
        }

        public Vector2 GetControlPoint(int _index)
        {
            return m_lControlPoints[_index] + (Vector2)transform.position;
        }

        public float DistanceAlongLine(int _index, bool _alreadyTriggering = false)
        {
            if (m_fTotalLineLength == 0 && _alreadyTriggering == false)
                m_fTotalLineLength = DistanceAlongLine(m_lv2BezierPoints.Count - 1, true);

            float retFloat = 0;
            for (int i = 0; i < _index; i++)
            {
                retFloat += Vector2.Distance(m_lv2BezierPoints[i], m_lv2BezierPoints[i + 1]);
            }

            return retFloat;
        }

        public bool CanSetControlPoint(int _index, Vector2 _position)
        {
            if (m_lControlPoints[_index] == _position - (Vector2)transform.position)
                return false;

            return true;
        }

        public void SetControlPoint(int _index, Vector2 _position)
        {
            m_lControlPoints[_index] = _position - (Vector2)transform.position;
            GenerateBezierCurve();
        }

        public void SubscribeForBezierChange(System.Action _toAdd)
        {
            OnBezierChange -= _toAdd;
            OnBezierChange += _toAdd;
        }

        public Vector2 GetPointAlongLine(float _t)
        {
            Vector2 retPoint = Vector2.zero;

            int n = m_lControlPoints.Count - 1;
            for (int i = 0; i <= n; i++)
            {
                retPoint +=
                    Utility.Math.BinomialCoefficient(n, i) *
                    Mathf.Pow(1 - _t, n - i) *
                    Mathf.Pow(_t, i) *
                    m_lControlPoints[i];
            }

            return retPoint;
        }

        public void Mirror(bool _alongX, bool _alongY)
        {
            bool odd = m_lControlPoints.Count % 2 != 0.0f;

            if (!odd)
            {
                Debug.LogError("To mirror, there must be an odd number of control points!");
                return;
            }

            //m_lControlPoints[m_lControlPoints.Count / 2] = Vector2.zero;
            int iteration = 1;
            for (int i = (m_lControlPoints.Count / 2) + 1; i < m_lControlPoints.Count; i++, iteration++)
            {
                Vector2 pointToMirror = m_lControlPoints[(m_lControlPoints.Count / 2) - iteration];

                Vector2 positionalOffset = new Vector2(
                    _alongY ? m_lControlPoints[m_lControlPoints.Count / 2].x : 0,
                    _alongX ? m_lControlPoints[m_lControlPoints.Count / 2].y : 0);

                // Adjust so that the center point is at 0,0 for mirroring
                pointToMirror -= positionalOffset;

                Vector2 mirroredPoint =
                    new Vector2(
                        pointToMirror.x * (_alongY ? -1 : 1),
                        pointToMirror.y * (_alongX ? -1 : 1));

                // Adjust back
                mirroredPoint += positionalOffset;

                m_lControlPoints[i] = mirroredPoint;
            }

            GenerateBezierCurve();
        }

        private void OnDestroy()
        {
            if (m_aOnDestroy != null)
                m_aOnDestroy();
        }
    }
}