namespace PlayerInput
{
    using UnityEngine;
    using UnityEngine.EventSystems;
    using System.Collections.Generic;

    /// <summary>
    /// This class handles all touch/mouse-based input requests.
    /// By using Unity's internal raycasting functionality, it naturally respects the "Raycast Target" on UI elements
    /// </summary>
    public class InputManager : Interfaces.Updateable
    {
        private const int MAX_TOUCHES_HANDLED = 2;
        private const float TAP_WINDOW = 0.2f;

        private bool m_bEnableSceneInput = true;
        private bool m_bEnableUIInput = true;

        private System.Action<Vector2> OnSceneClick;
        private System.Action<Vector2> OnSceneHold;
        private System.Action<Vector2, Vector2> OnSceneDrag;
        private System.Action<Vector2> OnSceneRelease;
        private System.Action<Vector2> OnSceneTap;

        private System.Action<Vector2, RaycastHit2D?> OnUIClick;
        private System.Action<Vector2, RaycastHit2D?> OnUIHold;
        private System.Action<Vector2, Vector2, RaycastHit2D?> OnUIDrag;
        private System.Action<Vector2, RaycastHit2D?> OnUIRelease;
        private System.Action<Vector2, RaycastHit2D?> OnUITap;

        public Vector2 MousePosition
        {
            get
            {
                return (Vector2)Input.mousePosition;
            }
        }

        private struct RaycastInfo
        {
#pragma warning disable 0414 
            Vector2 _raycastPosition;
            string _raycastLayer;
#pragma warning restore 0414

            public RaycastInfo(Vector2 _position, string _layer)
            {
                _raycastPosition = _position;
                _raycastLayer = _layer;
            }
        }

        private Dictionary<RaycastInfo, RaycastHit2D> m_dRaycastsThisFrame;

        private static InputManager instance;
        public static InputManager Instance
        {
            get
            {
                return instance;
            }
        }

        public enum TouchType
        {
            UI,
            Scene
        }

        private class TypedTouch
        {
            public int m_nFingerID;
            public TouchType m_eTouchType;
            public Vector2 m_vPrevPosition;
            public float m_fDuration = 0.0f;

            public TypedTouch(int _fingerID, TouchType _touchType, Vector2 _position)
            {
                m_nFingerID = _fingerID;
                m_eTouchType = _touchType;
                m_vPrevPosition = _position;
            }
        }

        private List<TypedTouch> m_lTouches;
        private Vector2 m_vPrevMousePosition;
        private float m_fSinceClick;

        private DrawCollidableLines m_dcLines;
        public DrawCollidableLines DrawCollidableLines
        {
            get { return m_dcLines; }
        }

        private static System.Action m_aOnInitialization;
        public static void RegisterForInitialization(System.Action _newAction)
        {
            m_aOnInitialization += _newAction;
        }

        public InputManager() : base()
        {
            // Register the handler that draws collidable lines
            m_dcLines = new DrawCollidableLines(this);

            OnDestroy += delegate { m_dcLines.DestroyAllLinesInstantly(); };

            instance = this;

            m_lTouches = new List<TypedTouch>();
            m_vPrevMousePosition = new Vector2();
            m_fSinceClick = 0.0f;
            m_dRaycastsThisFrame = new Dictionary<RaycastInfo, RaycastHit2D>();

            if (m_aOnInitialization != null)
                m_aOnInitialization();
        }

        public override void LateUpdate()
        {
            if (Utility.CommonRefs.Player == null || Utility.CommonRefs.PlayerRigidBody.simulated == false) return;
            m_dRaycastsThisFrame.Clear();

            if (IsPaused())
            {
                Debug.Log("Input paused");
                return;
            }

            if (Application.isMobilePlatform)
                HandleMobileInput();
            else
            {
#if UNITY_EDITOR
                if (UnityEditor.EditorApplication.isRemoteConnected && Input.touchCount > 0)
                    HandleMobileInput();
                else
#endif
                    HandlePCInput();
            }

            return;
        }

        public void EnableSceneInput()
        {
            m_bEnableSceneInput = true;
        }

        public void DisableSceneInput()
        {
            m_bEnableSceneInput = false;
            m_lTouches.RemoveAll(x => x.m_eTouchType == TouchType.Scene);
        }

        public void EnableUIInput()
        {
            m_bEnableUIInput = true;
        }

        public void DisableUIInput()
        {
            m_bEnableUIInput = false;
            m_lTouches.RemoveAll(x => x.m_eTouchType == TouchType.UI);
        }

        public bool SceneInputEnabled()
        {
            return m_bEnableSceneInput;
        }

        public bool UIInputEnabled()
        {
            return m_bEnableUIInput;
        }

        private void HandleMobileInput()
        {
            List<Touch> currentTouches = new List<Touch>(MAX_TOUCHES_HANDLED);

            for (int i = 0; i < MAX_TOUCHES_HANDLED && i < Input.touchCount; i++)
                currentTouches.Add(Input.GetTouch(i));

            // Add any touches as their type
            for (int i = 0; i < currentTouches.Count; i++)
            {
                TouchType currentTouchType = InputOnLayer(currentTouches[i].position, "UI") ? TouchType.UI : TouchType.Scene;
                if ((TouchType.Scene == currentTouchType && m_lTouches.Find(x => TouchType.Scene == x.m_eTouchType) != null) || // Don't allow more than one scene touch
                    (TouchType.Scene == currentTouchType && m_bEnableSceneInput == false) || // Don't allow new scene touches if it's disabled
                    (TouchType.UI == currentTouchType && m_bEnableUIInput == false)) // Don't allow new UI touches if it's disabled
                    continue;

                if (m_lTouches.Find(x => x.m_nFingerID == currentTouches[i].fingerId) == null)
                    m_lTouches.Add(new TypedTouch(currentTouches[i].fingerId, currentTouchType, currentTouches[i].position));
            }

            for (int i = 0; i < currentTouches.Count; i++)
            {
                // Debug.Log("Handling mobile input!");
                TypedTouch touchWithFingerID = m_lTouches.Find(x => x.m_nFingerID == currentTouches[i].fingerId);
                if (touchWithFingerID == null) continue;

                if (TouchType.Scene == touchWithFingerID.m_eTouchType && m_bEnableSceneInput)
                {
                    if (TouchPhase.Began == currentTouches[i].phase &&
                        OnSceneClick != null)
                        OnSceneClick(currentTouches[i].position);
                    else if (TouchPhase.Ended == currentTouches[i].phase)
                    {
                        if (OnSceneRelease != null)
                            OnSceneRelease(currentTouches[i].position);

                        if (touchWithFingerID.m_fDuration < TAP_WINDOW && OnSceneTap != null)
                            OnSceneTap(currentTouches[i].position);

                        // Remove the touch that was associated w/ this fingerID
                        m_lTouches.RemoveAll(x => x.m_nFingerID == currentTouches[i].fingerId);
                    }
                    else
                    {
                        if (OnSceneHold != null)
                            OnSceneHold(currentTouches[i].position);

                        if (OnSceneDrag != null)
                            OnSceneDrag(touchWithFingerID.m_vPrevPosition, currentTouches[i].position);
                    }
                }
                else if (TouchType.UI == touchWithFingerID.m_eTouchType && m_bEnableUIInput)
                {
                    RaycastHit2D uiRaycast = InputOnLayer(currentTouches[i].position, "UI");
                    if (TouchPhase.Began == currentTouches[i].phase &&
                        OnUIClick != null)
                        OnUIClick(currentTouches[i].position, uiRaycast);
                    else if (TouchPhase.Ended == currentTouches[i].phase)
                    {
                        if (OnUIRelease != null)
                            OnUIRelease(currentTouches[i].position, uiRaycast);

                        if (touchWithFingerID.m_fDuration < TAP_WINDOW && OnUITap != null)
                            OnUITap(currentTouches[i].position, uiRaycast);

                        // Remove the touch that was associated w/ this fingerID
                        m_lTouches.RemoveAll(x => x.m_nFingerID == currentTouches[i].fingerId);
                    }
                    else
                    {
                        if (OnUIDrag != null)
                            OnUIDrag(touchWithFingerID.m_vPrevPosition, currentTouches[i].position, uiRaycast);

                        if (OnUIHold != null)
                            OnUIHold(currentTouches[i].position, uiRaycast);
                    }
                }

                // Update our touches
                touchWithFingerID.m_vPrevPosition = currentTouches[i].position;
                touchWithFingerID.m_fDuration += Time.deltaTime;
            }
        }

        private void HandlePCInput()
        {
            RaycastHit2D uiRaycast = InputOnLayer(MousePosition, "UI");

            m_fSinceClick += Time.deltaTime;
            if (!uiRaycast && m_bEnableSceneInput)
            {
                if (Input.GetMouseButtonDown(0) &&
                    OnSceneClick != null)
                {
                    OnSceneClick(MousePosition);
                }
                else if (Input.GetMouseButton(0))
                {
                    if (OnSceneDrag != null)
                        OnSceneDrag(m_vPrevMousePosition, MousePosition);

                    if (OnSceneHold != null)
                        OnSceneHold(MousePosition);
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    if (OnSceneRelease != null)
                        OnSceneRelease(MousePosition);
                    if (m_fSinceClick < TAP_WINDOW && OnSceneTap != null)
                        OnSceneTap(MousePosition);
                }
                else
                    m_fSinceClick = 0.0f;
            }
            else if (m_bEnableUIInput)
            {
                if (Input.GetMouseButtonDown(0) &&
                    OnUIClick != null)
                {
                    OnUIClick(MousePosition, uiRaycast);
                }
                else if (Input.GetMouseButton(0))
                {
                    if (OnUIDrag != null)
                        OnUIDrag(m_vPrevMousePosition, MousePosition, uiRaycast);

                    if (OnUIHold != null)
                        OnUIHold(MousePosition, uiRaycast);
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    if (OnUIRelease != null)
                        OnUIRelease(MousePosition, uiRaycast);
                    if (m_fSinceClick < TAP_WINDOW && OnUITap != null)
                        OnUITap(MousePosition, uiRaycast);
                }
                else
                    m_fSinceClick = 0.0f;
            }

            m_vPrevMousePosition = MousePosition;
        }

        #region Scene Input Callbacks
        public void RegisterForSceneClick(System.Action<Vector2> _toRegister)
        {
            UnregisterForSceneClick(_toRegister);
            OnSceneClick += _toRegister;
        }

        public void UnregisterForSceneClick(System.Action<Vector2> _toUnregister)
        {
            OnSceneClick -= _toUnregister;
        }

        public void RegisterForSceneHold(System.Action<Vector2> _toRegister)
        {
            UnregisterForSceneHold(_toRegister);
            OnSceneHold += _toRegister;
        }

        public void UnregisterForSceneHold(System.Action<Vector2> _toUnregister)
        {
            OnSceneHold -= _toUnregister;
        }

        public void RegisterForSceneDrag(System.Action<Vector2, Vector2> _toRegister)
        {
            UnregisterForSceneDrag(_toRegister);
            OnSceneDrag += _toRegister;
        }

        public void UnregisterForSceneDrag(System.Action<Vector2, Vector2> _toUnregister)
        {
            OnSceneDrag -= _toUnregister;
        }

        public void RegisterForSceneRelease(System.Action<Vector2> _toRegister)
        {
            UnregisterForSceneRelease(_toRegister);
            OnSceneRelease += _toRegister;
        }

        public void UnregisterForSceneRelease(System.Action<Vector2> _toUnregister)
        {
            OnSceneRelease -= _toUnregister;
        }

        public void RegisterForSceneTap(System.Action<Vector2> _toRegister)
        {
            UnregisterForSceneTap(_toRegister);
            OnSceneTap += _toRegister;
        }

        public void UnregisterForSceneTap(System.Action<Vector2> _toUnregister)
        {
            OnSceneTap -= _toUnregister;
        }
        #endregion
        #region UI Input Callbacks 
        public void RegisterForUIClick(System.Action<Vector2, RaycastHit2D?> _toRegister)
        {
            UnregisterForUIClick(_toRegister);
            OnUIClick += _toRegister;
        }

        public void UnregisterForUIClick(System.Action<Vector2, RaycastHit2D?> _toUnregister)
        {
            OnUIClick -= _toUnregister;
        }

        public void RegisterForUIHold(System.Action<Vector2, RaycastHit2D?> _toRegister)
        {
            UnregisterForUIHold(_toRegister);
            OnUIHold += _toRegister;
        }

        public void UnregisterForUIHold(System.Action<Vector2, RaycastHit2D?> _toUnregister)
        {
            OnUIHold -= _toUnregister;
        }

        public void RegisterForUIDrag(System.Action<Vector2, Vector2, RaycastHit2D?> _toRegister)
        {
            UnregisterForUIDrag(_toRegister);
            OnUIDrag += _toRegister;
        }

        public void UnregisterForUIDrag(System.Action<Vector2, Vector2, RaycastHit2D?> _toUnregister)
        {
            OnUIDrag -= _toUnregister;
        }

        public void RegisterForUIRelease(System.Action<Vector2, RaycastHit2D?> _toRegister)
        {
            UnregisterForUIRelease(_toRegister);
            OnUIRelease += _toRegister;
        }

        public void UnregisterForUIRelease(System.Action<Vector2, RaycastHit2D?> _toUnregister)
        {
            OnUIRelease -= _toUnregister;
        }

        public void RegisterForUITap(System.Action<Vector2, RaycastHit2D?> _toRegister)
        {
            UnregisterForUITap(_toRegister);
            OnUITap += _toRegister;
        }

        public void UnregisterForUITap(System.Action<Vector2, RaycastHit2D?> _toUnregister)
        {
            OnUITap -= _toUnregister;
        }
        #endregion

        /// <summary>
        /// This first checks to see if another raycast was done in the same location and layer within the same frame.
        /// If so, it returns that, else it does the raycast and stores the result for future input requests.
        /// </summary>
        public RaycastHit2D InputOnLayer(Vector2 _inputPosition, string _layer)
        {
            if (m_dRaycastsThisFrame.ContainsKey(new RaycastInfo(_inputPosition, _layer)) == false)
                m_dRaycastsThisFrame.Add(new RaycastInfo(_inputPosition, _layer), Physics2D.Raycast((Vector2)
                    Utility.CommonRefs.Camera.ScreenToWorldPoint(_inputPosition),
                    Vector3.forward, float.MaxValue, 1 << LayerMask.NameToLayer(_layer)));

            return m_dRaycastsThisFrame[new RaycastInfo(_inputPosition, _layer)];
        }

        public override void OnApplicationQuit()
        {
            m_dcLines.OnApplicationQuit();
        }       
    }
}