using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts
{
    [RequireComponent(typeof(ScrollRect))]
    public class HorizontalScrollSnap : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
    {
        private Transform _screensContainer;

        private int _screens = 1;

        private bool _fastSwipeTimer;
        private int _fastSwipeCounter;
        private int _fastSwipeTarget = 30;

        private System.Collections.Generic.List<Vector3> _positions;
        private ScrollRect _scrollRect;
        private Vector3 _lerpTarget;
        private bool _lerp;

        [Serializable]
        public class SelectionChangeStartEvent : UnityEvent
        {
        }

        [Serializable]
        public class SelectionChangeEndEvent : UnityEvent
        {
        }

        [Tooltip("The gameobject that contains toggles which suggest pagination. (optional)")]
        public GameObject Pagination;

        [Tooltip("Button to go to the next page. (optional)")]
        public GameObject NextButton;

        [Tooltip("Button to go to the previous page. (optional)")]
        public GameObject PrevButton;

        [Tooltip("Transition speed between pages. (optional)")]
        public float TransitionSpeed = 7.5f;

        public Boolean UseFastSwipe = true;
        public int FastSwipeThreshold = 100;

        private bool _startDrag = true;
        private Vector3 _startPosition = new Vector3();

        [Tooltip("The currently active page")] private int _currentScreen;

        [Tooltip("The screen / page to start the control on")]
        [SerializeField]
        public int StartingScreen = 1;

        [Tooltip("The distance between two pages based on page height, by default pages are next to each other")]
        [SerializeField]
        [Range(1, 8)]
        public float PageStep = 1;

        public int CurrentPage => _currentScreen;

        [SerializeField]
        private SelectionChangeStartEvent _mOnSelectionChangeStartEvent = new SelectionChangeStartEvent();

        public SelectionChangeStartEvent OnSelectionChangeStartEvent
        {
            get => _mOnSelectionChangeStartEvent;
            set => _mOnSelectionChangeStartEvent = value;
        }

        [SerializeField] private SelectionChangeEndEvent m_OnSelectionChangeEndEvent = new SelectionChangeEndEvent();

        public SelectionChangeEndEvent OnSelectionChangeEndEvent
        {
            get => m_OnSelectionChangeEndEvent;
            set => m_OnSelectionChangeEndEvent = value;
        }

        // Use this for initialization
        private void Awake()
        {
            _scrollRect = gameObject.GetComponent<ScrollRect>();

            if (_scrollRect.horizontalScrollbar || _scrollRect.verticalScrollbar)
            {
                Debug.LogWarning(
                    "Warning, using scrollbars with the Scroll Snap controls is not advised as it causes unpredictable results");
            }

            _screensContainer = _scrollRect.content;

            DistributePages();

            if (NextButton)
                NextButton.GetComponent<AnimatedButton>().onClick.AddListener(() => { NextScreen(); });

            if (PrevButton)
                PrevButton.GetComponent<AnimatedButton>().onClick.AddListener(() => { PreviousScreen(); });
        }

        private void Start()
        {
            UpdateChildPositions();
            _lerp = false;
            _currentScreen = StartingScreen - 1;

            _scrollRect.horizontalNormalizedPosition = (float)(_currentScreen) / (_screens - 1);

            ChangeBulletsInfo(_currentScreen);
        }

        private void Update()
        {
            if (_lerp)
            {
                _screensContainer.localPosition = Vector3.Lerp(_screensContainer.localPosition, _lerpTarget,
                    TransitionSpeed * Time.deltaTime);
                if (Vector3.Distance(_screensContainer.localPosition, _lerpTarget) < 0.1f)
                {
                    _lerp = false;

                    EndScreenChange();
                }

                //change the info bullets at the bottom of the screen. Just for visual effect
                if (Vector3.Distance(_screensContainer.localPosition, _lerpTarget) < 10f)
                {
                    ChangeBulletsInfo(CurrentScreen());
                }
            }

            if (_fastSwipeTimer)
            {
                _fastSwipeCounter++;
            }
        }

        private bool _fastSwipe = false; //to determine if a fast swipe was performed

        //Function for switching screens with buttons
        public void NextScreen()
        {
            if (_currentScreen < _screens - 1)
            {
                StartScreenChange();

                _currentScreen++;
                _lerp = true;
                _lerpTarget = _positions[_currentScreen];

                ChangeBulletsInfo(_currentScreen);
            }
        }

        //Function for switching screens with buttons
        public void PreviousScreen()
        {
            if (_currentScreen > 0)
            {
                StartScreenChange();

                _currentScreen--;
                _lerp = true;
                _lerpTarget = _positions[_currentScreen];

                ChangeBulletsInfo(_currentScreen);
            }
        }

        /// <summary>
        /// Function for switching to a specific screen
        /// *Note, this is based on a 0 starting index - 0 to x
        /// </summary>
        /// <param name="screenIndex">0 starting index of page to jump to</param>
        public void GoToScreen(int screenIndex)
        {
            if (screenIndex <= _screens - 1 && screenIndex >= 0)
            {
                StartScreenChange();

                _lerp = true;
                _currentScreen = screenIndex;
                _lerpTarget = _positions[_currentScreen];

                ChangeBulletsInfo(_currentScreen);
            }
        }

        //Because the CurrentScreen function is not so reliable, these are the functions used for swipes
        private void NextScreenCommand()
        {
            if (_currentScreen < _screens - 1)
            {
                _lerp = true;
                _currentScreen++;
                _lerpTarget = _positions[_currentScreen];

                ChangeBulletsInfo(_currentScreen);
            }
        }

        //Because the CurrentScreen function is not so reliable, these are the functions used for swipes
        private void PrevScreenCommand()
        {
            if (_currentScreen > 0)
            {
                _lerp = true;
                _currentScreen--;
                _lerpTarget = _positions[_currentScreen];

                ChangeBulletsInfo(_currentScreen);
            }
        }


        //find the closest registered point to the releasing point
        private Vector3 FindClosestFrom(Vector3 start, System.Collections.Generic.List<Vector3> positions)
        {
            Vector3 closest = Vector3.zero;
            float distance = Mathf.Infinity;

            foreach (Vector3 position in _positions)
            {
                if (Vector3.Distance(start, position) < distance)
                {
                    distance = Vector3.Distance(start, position);
                    closest = position;
                }
            }

            return closest;
        }


        //returns the current screen that the is seeing
        public int CurrentScreen()
        {
            var pos = FindClosestFrom(_screensContainer.localPosition, _positions);
            return _currentScreen = GetPageforPosition(pos);
        }

        //changes the bullets on the bottom of the page - pagination
        private void ChangeBulletsInfo(int currentScreen)
        {
            if (Pagination)
                for (int i = 0; i < Pagination.transform.childCount; i++)
                {
                    Pagination.transform.GetChild(i).GetComponent<Toggle>().isOn = (currentScreen == i)
                        ? true
                        : false;
                }
        }

        //used for changing between screen resolutions
        private void DistributePages()
        {
            int _offset = 0;
            float _dimension = 0;
            Rect panelDimensions = gameObject.GetComponent<RectTransform>().rect;
            float currentXPosition = 0;
            var pageStepValue = (int)panelDimensions.width * ((PageStep == 0) ? 3 : PageStep);


            for (int i = 0; i < _screensContainer.transform.childCount; i++)
            {
                RectTransform child = _screensContainer.transform.GetChild(i).gameObject.GetComponent<RectTransform>();
                currentXPosition = _offset + (int)(i * pageStepValue);
                child.sizeDelta = new Vector2(panelDimensions.width, panelDimensions.height);
                child.anchoredPosition = new Vector2(currentXPosition, 0f);
                child.anchorMin = new Vector2(0f, child.anchorMin.y);
                child.anchorMax = new Vector2(0f, child.anchorMax.y);
                child.pivot = new Vector2(0f, child.pivot.y);
            }

            _dimension = currentXPosition + _offset * -1;

            _screensContainer.GetComponent<RectTransform>().offsetMax = new Vector2(_dimension, 0f);
        }

        private void UpdateChildPositions()
        {
            _screens = _screensContainer.childCount;

            _positions = new System.Collections.Generic.List<Vector3>();

            if (_screens <= 0) return;

            for (float i = 0; i < _screens; ++i)
            {
                _scrollRect.horizontalNormalizedPosition = i / (_screens - 1);
                _positions.Add(_screensContainer.localPosition);
            }
        }

        private int GetPageforPosition(Vector3 pos)
        {
            for (var i = 0; i < _positions.Count; i++)
            {
                if (_positions[i] == pos)
                {
                    return i;
                }
            }

            return 0;
        }

        private void OnValidate()
        {
            var childCount = gameObject.GetComponent<ScrollRect>().content.childCount;
            if (StartingScreen > childCount - 1)
            {
                StartingScreen = childCount - 1;
            }

            if (StartingScreen < 0)
            {
                StartingScreen = 0;
            }
        }

        /// <summary>
        /// Add a new child to this Scroll Snap and recalculate it's children
        /// </summary>
        /// <param name="GO">GameObject to add to the ScrollSnap</param>
        public void AddChild(GameObject GO)
        {
            _scrollRect.horizontalNormalizedPosition = 0;
            GO.transform.SetParent(_screensContainer);
            DistributePages();

            _scrollRect.horizontalNormalizedPosition = (float)(_currentScreen) / (_screens - 1);
        }

        /// <summary>
        /// Remove a new child to this Scroll Snap and recalculate it's children 
        /// *Note, this is an index address (0-x)
        /// </summary>
        /// <param name="index"></param>
        /// <param name="ChildRemoved"></param>
        public void RemoveChild(int index, out GameObject ChildRemoved)
        {
            ChildRemoved = null;
            if (index < 0 || index > _screensContainer.childCount)
            {
                return;
            }

            _scrollRect.horizontalNormalizedPosition = 0;
            var children = _screensContainer.transform;
            var i = 0;
            foreach (Transform child in children)
            {
                if (i == index)
                {
                    child.SetParent(null);
                    ChildRemoved = child.gameObject;
                    break;
                }

                i++;
            }

            DistributePages();
            if (_currentScreen > _screens - 1)
            {
                _currentScreen = _screens - 1;
            }

            _scrollRect.horizontalNormalizedPosition = (float)(_currentScreen) / (_screens - 1);
        }

        private void StartScreenChange()
        {
            OnSelectionChangeStartEvent.Invoke();
        }

        private void EndScreenChange()
        {
            OnSelectionChangeEndEvent.Invoke();
        }

        #region Interfaces

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_fastSwipeTimer) StartScreenChange();
            _startPosition = _screensContainer.localPosition;
            _fastSwipeCounter = 0;
            _fastSwipeTimer = true;
            _currentScreen = CurrentScreen();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _startDrag = true;
            if (_scrollRect.horizontal)
            {
                if (UseFastSwipe)
                {
                    _fastSwipe = false;
                    _fastSwipeTimer = false;
                    if (_fastSwipeCounter <= _fastSwipeTarget)
                    {
                        if (Math.Abs(_startPosition.x - _screensContainer.localPosition.x) > FastSwipeThreshold)
                        {
                            _fastSwipe = true;
                        }
                    }

                    if (_fastSwipe)
                    {
                        if (_startPosition.x - _screensContainer.localPosition.x > 0)
                        {
                            NextScreenCommand();
                        }
                        else
                        {
                            PrevScreenCommand();
                        }
                    }
                    else
                    {
                        _lerp = true;
                        _lerpTarget = FindClosestFrom(_screensContainer.localPosition, _positions);
                        _currentScreen = GetPageforPosition(_lerpTarget);
                    }
                }
                else
                {
                    _lerp = true;
                    _lerpTarget = FindClosestFrom(_screensContainer.localPosition, _positions);
                    _currentScreen = GetPageforPosition(_lerpTarget);
                }
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            _lerp = false;
            if (_startDrag)
            {
                OnBeginDrag(eventData);
                _startDrag = false;
            }
        }

        #endregion
    }
}