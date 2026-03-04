/*
    Copyright (c) 2026 Alex Howe

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.
*/
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Poke.UI
{
    [
        ExecuteAlways,
        RequireComponent(typeof(RectTransform))
    ]
    public class LayoutItem : MonoBehaviour
    {
        [SerializeField] protected bool m_log;
        
        [Header("Layout Item")]
        [SerializeField] protected bool m_ignoreLayout = false;
        [SerializeField] protected SizeModes m_sizing;

        public bool IgnoreLayout {
            get => m_ignoreLayout;
            set {
                m_ignoreLayout = value;
                if(_parent) {
                    _parent.RefreshChildCache();
                }
            }
        }
        public RectTransform Rect => _rect;
        public DrivenTransformProperties TrackerProps {
            get => _trackerProps;
            set => _trackerProps = value;
        }
        public SizeModes SizeMode => m_sizing;
        
        protected RectTransform _rect;
        protected DrivenRectTransformTracker _tracker;
        protected DrivenTransformProperties _trackerProps;
        protected RectTransform _parentRect;
        protected Layout _parent;

        private Vector2 _parentSize;

        [Serializable]
        public struct SizeModes
        {
            public SizingMode x;
            public SizingMode y;
        }

        protected virtual void Awake() {
#if UNITY_EDITOR
            ValidatePrefabStage();
#endif
            
            _rect = GetComponent<RectTransform>();
            _tracker = new DrivenRectTransformTracker();
            
            // parent will always exist EXCEPT for in prefab editing
            // (bc Canvas has a RectTransform)
            if(transform.parent) {
                _parentRect = transform.parent.GetComponent<RectTransform>();
            }
            _parentSize = _parentRect ? _parentRect.rect.size : default;
        }

        protected virtual void OnEnable() {
            if(transform.parent) {
                _parent = transform.parent.GetComponent<Layout>();
                if(_parent) {
                    _parent.RefreshChildCache();
                }
            }

            _trackerProps = DrivenTransformProperties.None;
        }

        protected virtual void OnDisable() {
            if(_parent) {
                _parent.RefreshChildCache();
            }
        }

        public virtual void Update() {
            // Do grow sizing here if parent is not a Layout
            // Grow does nothing if there is no parent (prefab editing)
            if(!_parent && _parentRect) {
                // only update size if parent size has changed
                if(m_sizing.x == SizingMode.Grow && !Mathf.Approximately(_parentRect.rect.size.x, _parentSize.x)) {
                    _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _parentRect.rect.size.x);
                    _parentSize = _parentSize.SetX(_parentRect.rect.size.x);
                }
                if(m_sizing.y == SizingMode.Grow && !Mathf.Approximately(_parentRect.rect.size.y, _parentSize.y)) {
                    _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _parentRect.rect.size.y);
                    _parentSize = _parentSize.SetY(_parentRect.rect.size.y);
                }
                
            }
            
            _trackerProps = DrivenTransformProperties.None;
            _tracker.Clear();
        }

        private void LateUpdate() {
            if(m_sizing.x == SizingMode.FitContent || m_sizing.x == SizingMode.Grow)
                _trackerProps |= DrivenTransformProperties.SizeDeltaX;
            if(m_sizing.y == SizingMode.FitContent || m_sizing.y == SizingMode.Grow)
                _trackerProps |= DrivenTransformProperties.SizeDeltaY;

            if(_parent && !m_ignoreLayout) {
                _trackerProps |= DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.Pivot |
                                 DrivenTransformProperties.Anchors;
            }
            
            _tracker.Add(this, _rect, _trackerProps);
        }

        protected virtual void OnValidate() {
            Awake();
        }

#if UNITY_EDITOR
        private void ValidatePrefabStage() {
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject);
            if(prefabStage == null)
                return;
        
            LayoutRoot lr = gameObject.GetComponentInParent<LayoutRoot>(true);
            if(lr != null)
                return;
        
            // This way of getting to root is necessary since prefabContentsRoot and GetRootGameObjects aren't available at this point
            Transform topmostTransform = gameObject.transform;
            while (topmostTransform.parent != null)
                topmostTransform = topmostTransform.parent;
        
            GameObject layoutRootObject = new GameObject("LayoutRoot (Editor)");
            layoutRootObject.hideFlags = HideFlags.DontSaveInEditor;
            layoutRootObject.AddComponent<LayoutRoot>();
        
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(layoutRootObject, prefabStage.scene);
            topmostTransform.SetParent(layoutRootObject.transform, false);
        }
#endif
        
        public void SetParentDirty() {
            if(_parent) {
                _parent.SetDirty();
            }
        }
    }
}
