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
using System.Collections.Generic;
using UnityEngine;

namespace Poke.UI
{
    public class Layout : LayoutItem, IComparable<Layout>
    {
        /* THINGS THAT CAN CAUSE A LAYOUT UPDATE
            - non-grow child RectTransform changes size
            - number of children change
            - child is enabled/disabled
            - this container changes
        */
        public event Action OnLayoutChanged;
        
        [Header("Layout")]
        [SerializeField] private Margins            m_padding;
        [SerializeField] private LayoutDirection    m_direction;
        [SerializeField] private Justification      m_justifyContent;
        [SerializeField] private Alignment          m_alignContent;
        [SerializeField] private float              m_innerSpacing;

        public int ChildCount =>            _children?.Count ?? 0;
        public int Depth =>                 _depth;
        public int GrowChildCount =>        _growChildren?.Count ?? 0;
        public LayoutDirection Direction => m_direction;
        public bool NeedsRefresh =>         _dirty;

        private readonly int MAX_DEPTH = 100;

        private List<ChildInfo>             _children = new();
        private Vector2                     _contentSize;
        private int                         _depth;
        private bool                        _dirty;
        private Vector2Int                  _growChildCount;
        private List<LayoutItem>            _growChildren;
        private int                         _ignoreCount;
        private Vector2                     _lastSize;
        private LayoutItem[]                _layoutItems;
        private readonly Vector3[]          _rectCorners = new Vector3[4];
        private LayoutRoot                  _root;

        #region TypeDef
        public enum Justification
        {
            Start,
            Center,
            End,
            SpaceBetween
        }
        
        public enum Alignment
        {
            Start,
            Center,
            End
        }
        
        public enum LayoutDirection
        {
            Row,
            Column,
            RowReverse,
            ColumnReverse
        }

        private class ChildInfo
        {
            public int index;
            public RectTransform rect;
            public Vector2 size;
            public bool enabled;
            public bool ignoreLayout;
        }
        #endregion
        
        #region Layout MonoBehavior
        protected override void Awake() {
            base.Awake();
            _growChildren = new List<LayoutItem>();
        }

        protected override void OnEnable() {
            base.OnEnable();
            
            // find LayoutRoot
            Transform t = transform;
            while(_root == null) {
                if(t.TryGetComponent(out LayoutRoot root)) {
                    _root = root;
                    break;
                }
                
                if(t.parent == null) {
                    Debug.LogError("No LayoutRoot found! Aborting.");
                    break;
                }

                t = t.parent;
                _depth++;

                if(_depth > MAX_DEPTH) {
                    Debug.LogError("Hit max search depth! Aborting.");
                    break;
                }
            }
            
            _root?.RegisterLayout(this);
            RefreshChildCache();
            _dirty = true;
        }

        protected override void OnDisable() {
            base.OnDisable();
            _root?.UnregisterLayout(this);
        }

        public override void Update() {
            base.Update();
            
            bool layoutChanged = _dirty;
            bool needsCacheRefresh = false;
            
            // check for changes in children
            foreach(ChildInfo c in _children) {
                if(!c.rect) {
                    layoutChanged = true;
                    needsCacheRefresh = true;
                    continue;
                }
                
                // check if child index has changed
                if(c.rect.GetSiblingIndex() != c.index) {
                    layoutChanged = true;
                    needsCacheRefresh = true;
                }
                
                // check if item was disabled this frame
                if(c.rect.gameObject.activeInHierarchy != c.enabled) {
                    c.enabled = c.rect.gameObject.activeInHierarchy;
                    layoutChanged = true;
                }

                LayoutItem li = _layoutItems[c.index];
                if(li) {
                    // check if ignore layout toggled this frame
                    if(li.IgnoreLayout != c.ignoreLayout) {
                        c.ignoreLayout = li.IgnoreLayout;
                        layoutChanged = true;
                    }
                }
                else {
                    _tracker.Add(
                        this,
                        c.rect,
                        DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.Pivot
                            | DrivenTransformProperties.Anchors
                    );
                }
                
                // check if item changed size this frame
                if(!(li && li.SizeMode.x == SizingMode.Grow) && !Mathf.Approximately(c.rect.rect.size.x, c.size.x)) {
                    c.size = c.size.SetX(c.rect.rect.size.x); 
                    layoutChanged = true;
                }
                if(!(li && li.SizeMode.y == SizingMode.Grow) && !Mathf.Approximately(c.rect.rect.size.y, c.size.y)) {
                    c.size = c.size.SetY(c.rect.rect.size.y);
                    layoutChanged = true;
                }
            }
            
            // check if the container changed this frame
            if(!Mathf.Approximately(_lastSize.x, _rect.rect.size.x) || !Mathf.Approximately(_lastSize.y, _rect.rect.size.y)) {
                layoutChanged = true;
            }
            // check if any children were added/removed this frame
            if(transform.childCount != _children.Count) {
                layoutChanged = true;
                needsCacheRefresh = true;
            }
            
            if(layoutChanged) {
                _dirty = true;
                OnLayoutChanged?.Invoke();
                
                if(needsCacheRefresh)
                    RefreshChildCache();
            }

            _lastSize = _rect.rect.size;
        }

        private void OnDrawGizmosSelected() {
            _rect.GetWorldCorners(_rectCorners);

            Matrix4x4 ltw = _rect.localToWorldMatrix;
            
            foreach(Vector3 v in _rectCorners) {
                LayoutUtil.DrawCenteredDebugBox(v, 0.15f, 0.15f, Color.red);
            }

            Rect r = new Rect(_rectCorners[0], _rectCorners[2] - _rectCorners[0]);
            r.position += (Vector2)(ltw * new Vector2(m_padding.left, m_padding.bottom));
            r.size -= (Vector2)(ltw * new Vector2(m_padding.left + m_padding.right, m_padding.top + m_padding.bottom));
            
            LayoutUtil.DrawDebugBox(r, _rect.position.z, Color.green);
        }
        #endregion

        private bool CheckIgnoreElem(ChildInfo ci) {
            return !ci.enabled || ci.ignoreLayout;
        }

        private void SetAnchorPivotX(RectTransform rt, float x) {
            rt.anchorMin = rt.anchorMin.SetX(x);
            rt.anchorMax = rt.anchorMax.SetX(x);
            rt.pivot = rt.pivot.SetX(x);
        }
        private void SetAnchorPivotY(RectTransform rt, float y) {
            rt.anchorMin = rt.anchorMin.SetY(y);
            rt.anchorMax = rt.anchorMax.SetY(y);
            rt.pivot = rt.pivot.SetY(y);
        }
        
        #region LAYOUT PASSES
        public void ComputeFitSize() {
            _growChildren.Clear();
            _growChildCount = new Vector2Int(0, 0);
            _ignoreCount = 0;
            
            if(_children.Count > 0) {
                LayoutItem li = null;
                
                // get number of disabled/ignore children, reset rect trackers
                foreach(ChildInfo c in _children) {
                    if(CheckIgnoreElem(c)) {
                        _ignoreCount++;
                    }
                }

                float primarySize = m_justifyContent == Justification.SpaceBetween ? 0 : m_innerSpacing * (_children.Count-_ignoreCount-1);
                float crossSize = 0;
                
                // calculate content size
                float maxCrossSize = 0;
                foreach(ChildInfo elem in _children) {
                    // skip disabled/ignore items
                    if(CheckIgnoreElem(elem))
                        continue;
                    
                    bool growX = false, growY = false;
                    
                    li = _layoutItems[elem.index];
                    if(li) {
                        growX = li.SizeMode.x == SizingMode.Grow;
                        growY = li.SizeMode.y == SizingMode.Grow;
                        if(growX || growY) {
                            _growChildren.Add(li);
                            _growChildCount.x += growX ? 1 : 0;
                            _growChildCount.y += growY ? 1 : 0;
                        }
                    }
                    
                    switch(m_direction) {
                        case LayoutDirection.Row:
                        case LayoutDirection.RowReverse:
                            primarySize += growX ? 0 : elem.rect.sizeDelta.x;
                            maxCrossSize = Mathf.Max(maxCrossSize, growY ? 0 : elem.rect.sizeDelta.y);
                            break;
                        case LayoutDirection.Column:
                        case LayoutDirection.ColumnReverse:
                            primarySize += growY ? 0 : elem.rect.sizeDelta.y;
                            maxCrossSize = Mathf.Max(maxCrossSize, growX ? 0 : elem.rect.sizeDelta.x);
                            break;
                    }
                }
                crossSize += maxCrossSize;

                // save content size for later
                switch(m_direction) {
                    case LayoutDirection.Row:
                    case LayoutDirection.RowReverse:
                        _contentSize = new Vector2(primarySize, crossSize);
                        break;
                    case LayoutDirection.Column:
                    case LayoutDirection.ColumnReverse:
                        _contentSize = new Vector2(crossSize, primarySize);
                        break;
                }
                
                // apply fit sizing X
                if(m_sizing.x == SizingMode.FitContent) {
                    switch(m_direction) {
                        case LayoutDirection.Row:
                        case LayoutDirection.RowReverse:
                            _rect.SetSizeWithCurrentAnchors(
                                RectTransform.Axis.Horizontal,
                                primarySize + m_padding.left + m_padding.right
                            );
                            break;
                        case LayoutDirection.Column:
                        case LayoutDirection.ColumnReverse:
                            _rect.SetSizeWithCurrentAnchors(
                                RectTransform.Axis.Horizontal,
                                crossSize + m_padding.left + m_padding.right
                            );
                            break;
                    }
                }
                
                // apply fit sizing Y
                if(m_sizing.y == SizingMode.FitContent) {
                    switch(m_direction) {
                        case LayoutDirection.Row:
                        case LayoutDirection.RowReverse:
                            _rect.SetSizeWithCurrentAnchors(
                                RectTransform.Axis.Vertical,
                                crossSize + m_padding.top + m_padding.bottom
                            );
                            break;
                        case LayoutDirection.Column:
                        case LayoutDirection.ColumnReverse:
                            _rect.SetSizeWithCurrentAnchors(
                                RectTransform.Axis.Vertical,
                                primarySize + m_padding.top + m_padding.bottom
                            );
                            break;
                    }
                }
                
                if(m_log) Debug.Log($"[L:{gameObject.name}] calculated rect size: {_rect.rect.size:f3}");
            }
            else {
                _contentSize = Vector2.zero;
            }
            
            if(m_log) Debug.Log($"[L:{gameObject.name}] content size: {_contentSize:f3}");
        }

        public void GrowChildren() {
            if(_growChildren.Count > 0) {
                if(m_log) Debug.Log($"[L:{gameObject.name}] growing {_growChildren.Count} children");
                
                Vector2 size;
                float crossSize;
                float leftover;
                switch(m_direction) {
                    case LayoutDirection.Row:
                    case LayoutDirection.RowReverse:
                        leftover = _rect.rect.size.x - _contentSize.x - m_padding.left - m_padding.right;
                        crossSize = _rect.rect.size.y - m_padding.top - m_padding.bottom;
                        size = new Vector2(leftover / _growChildCount.x, crossSize);

                        foreach(LayoutItem li in _growChildren) {
                            if(li.SizeMode.x == SizingMode.Grow) {
                                li.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
                                _contentSize.x += size.x;
                            }

                            if(li.SizeMode.y == SizingMode.Grow) {
                                li.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
                                _contentSize.y = Mathf.Max(size.y, _contentSize.y);
                            }
                        }

                        break;
                    case LayoutDirection.Column:
                    case LayoutDirection.ColumnReverse:
                        leftover = _rect.rect.size.y - _contentSize.y - m_padding.top - m_padding.bottom;
                        crossSize = _rect.rect.size.x - m_padding.left - m_padding.right;
                        size = new Vector2(crossSize, leftover / _growChildCount.y);

                        foreach(LayoutItem li in _growChildren) {
                            if(li.SizeMode.y == SizingMode.Grow) {
                                li.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
                                _contentSize.y += size.y;
                            }

                            if(li.SizeMode.x == SizingMode.Grow) {
                                li.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
                                _contentSize.x = Mathf.Max(size.x, _contentSize.x);
                            }
                        }
                        
                        break;
                }
            }
        }
        
        public void ComputeLayout() {
            if(_children.Count < 1) {
                return;
            }
            
            // apply RectTransform DrivenTransformProperties
            foreach(ChildInfo c in _children) {
                // skip disabled/ignore items
                if(CheckIgnoreElem(c))
                    continue;
            }
            
            // primary axis pass
            float primaryOffset = 0;
            float spacing = 0;
            float leftover = 0;
            int index = 0;
            
            switch(m_direction) {
                // ROW -> PRIMARY AXIS
                case LayoutDirection.Row:
                    switch(m_justifyContent) {
                        case Justification.Start:
                            primaryOffset += m_padding.left;
                            
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotX(c.rect, 0);

                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(primaryOffset);
                                primaryOffset += c.rect.sizeDelta.x + m_innerSpacing;
                            }
                            break;
                        case Justification.Center:
                            primaryOffset -= (_contentSize.x + m_padding.left + m_padding.right) / 2;
                            
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotX(c.rect, 0.5f);

                                primaryOffset += c.rect.sizeDelta.x / 2;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(primaryOffset + m_padding.left);
                                primaryOffset += c.rect.sizeDelta.x / 2 + m_innerSpacing;
                            }
                            break;
                        case Justification.End:
                            primaryOffset -= m_padding.right + _contentSize.x;
                            
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotX(c.rect, 1);

                                primaryOffset += c.rect.sizeDelta.x;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(primaryOffset);
                                primaryOffset += m_innerSpacing;
                            }
                            break;
                        case Justification.SpaceBetween:
                            primaryOffset += m_padding.left;
                            leftover = _rect.rect.size.x - _contentSize.x - m_padding.left - m_padding.right;
                            
                            if(_children.Count > 1)
                                spacing = leftover / (_children.Count-_ignoreCount-1);

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotX(c.rect, 0);

                                if(index != 0) {
                                    primaryOffset += spacing;
                                }
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(primaryOffset);
                                primaryOffset += c.rect.sizeDelta.x;
                                index++;
                            }
                            break;
                    }
                    break;
                // ROW_REVERSE -> PRIMARY AXIS
                case LayoutDirection.RowReverse:
                    switch(m_justifyContent) {
                        case Justification.Start:
                            primaryOffset += m_padding.left + _contentSize.x;
                            
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotX(c.rect, 0);

                                primaryOffset -= c.rect.sizeDelta.x + m_innerSpacing;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(primaryOffset);
                            }
                            break;
                        case Justification.Center:
                            primaryOffset += (_contentSize.x + m_padding.left + m_padding.right) / 2;
                            
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotX(c.rect, 0.5f);

                                primaryOffset -= c.rect.sizeDelta.x / 2;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(primaryOffset - m_padding.right);
                                primaryOffset -= c.rect.sizeDelta.x / 2 + m_innerSpacing;
                            }
                            break;
                        case Justification.End:
                            primaryOffset += m_padding.right;
                            
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotX(c.rect, 1);

                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(-primaryOffset);
                                primaryOffset += c.rect.sizeDelta.x + m_innerSpacing;
                            }
                            break;
                        case Justification.SpaceBetween:
                            primaryOffset += m_padding.right;
                            
                            leftover = _rect.rect.size.x - _contentSize.x - m_padding.left - m_padding.right;
                            
                            if(_children.Count > 1)
                                spacing = leftover / (_children.Count-1);
                                
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotX(c.rect, 1);
                                
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(-primaryOffset);
                                primaryOffset += c.rect.sizeDelta.x + spacing;
                            }
                            break;
                    }
                    break;
                // COLUMN -> PRIMARY AXIS
                case LayoutDirection.Column:
                    switch(m_justifyContent) {
                        case Justification.Start:
                            primaryOffset -= m_padding.top;
                            
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotY(c.rect, 1);

                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(primaryOffset);
                                primaryOffset -= c.rect.sizeDelta.y + m_innerSpacing;
                            }
                            break;
                        case Justification.Center:
                            primaryOffset += (_contentSize.y + m_padding.top + m_padding.bottom) / 2;
                            
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotY(c.rect, 0.5f);

                                primaryOffset -= c.rect.sizeDelta.y / 2;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(primaryOffset - m_padding.top);
                                primaryOffset -= c.rect.sizeDelta.y / 2 + m_innerSpacing;
                            }
                            break;
                        case Justification.End:
                            primaryOffset += _contentSize.y;
                            
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotY(c.rect, 0);

                                primaryOffset -= c.rect.sizeDelta.y;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(primaryOffset - m_padding.top);
                                primaryOffset -= m_innerSpacing;
                            }
                            break;
                        case Justification.SpaceBetween:
                            primaryOffset += m_padding.top;
                            leftover = _rect.rect.size.y - _contentSize.y - m_padding.top - m_padding.bottom;
                            
                            if(_children.Count > 1)
                                spacing = leftover / (_children.Count-_ignoreCount-1);
                                
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotY(c.rect, 1);
                                
                                if(index != 0) {
                                    primaryOffset += spacing;
                                }
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(-primaryOffset);
                                primaryOffset += c.rect.sizeDelta.y;

                                index++;
                            }
                            break;
                    }
                    break;
                // COLUMN_REVERSE -> PRIMARY AXIS
                case LayoutDirection.ColumnReverse:
                    switch(m_justifyContent) {
                        case Justification.Start:
                            primaryOffset -= m_padding.top + _contentSize.y;
                            
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotY(c.rect, 1);

                                primaryOffset += c.rect.sizeDelta.y;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(primaryOffset);
                                primaryOffset += m_innerSpacing;
                            }
                            break;
                        case Justification.Center:
                            primaryOffset -= (_contentSize.y + m_padding.top + m_padding.bottom) / 2;
                            
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotY(c.rect, 0.5f);

                                primaryOffset += c.rect.sizeDelta.y / 2;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(primaryOffset - m_padding.top);
                                primaryOffset += c.rect.sizeDelta.y / 2 + m_innerSpacing;
                            }
                            break;
                        case Justification.End:
                            primaryOffset += m_padding.bottom;
                            
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotY(c.rect, 0);

                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(primaryOffset);
                                primaryOffset += c.rect.sizeDelta.y + m_innerSpacing;
                            }
                            break;
                        case Justification.SpaceBetween:
                            primaryOffset += m_padding.bottom;
                            
                            leftover = _rect.rect.size.y - _contentSize.y - m_padding.top - m_padding.bottom;
                            
                            if(_children.Count > 1)
                                spacing = leftover / (_children.Count-1);
                                
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotY(c.rect, 0);
                                
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(primaryOffset);
                                primaryOffset += c.rect.sizeDelta.y + spacing;
                            }
                            break;
                    }
                    break;
            }
            
            // cross axis pass
            float crossOffset = 0;
            switch(m_direction) {
                // ROW -> CROSS
                // ROW_REVERSE -> CROSS
                case LayoutDirection.Row:
                case LayoutDirection.RowReverse:
                    switch(m_alignContent) {
                        case Alignment.Start:
                            crossOffset += m_padding.top;
                            
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotY(c.rect, 1);

                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(-crossOffset);
                            }
                            break;
                        case Alignment.Center:
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotY(c.rect, 0.5f);

                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(m_padding.bottom/2 - m_padding.top/2);
                            }
                            break;
                        case Alignment.End:
                            crossOffset += m_padding.bottom;
                            
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotY(c.rect, 0);

                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(crossOffset);
                            }
                            break;
                    }
                    break;
                // COLUMN -> CROSS
                // COLUMN_REVERSE -> CROSS
                case LayoutDirection.Column:
                case LayoutDirection.ColumnReverse:
                    switch(m_alignContent) {
                        case Alignment.Start:
                            crossOffset += m_padding.left;
                            
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotX(c.rect, 0);

                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(crossOffset);
                            }
                            break;
                        case Alignment.Center:
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotX(c.rect, 0.5f);

                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(m_padding.left/2 - m_padding.right/2);
                            }
                            break;
                        case Alignment.End:
                            crossOffset += m_padding.right;
                            
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;
                                
                                SetAnchorPivotX(c.rect, 1);

                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(-crossOffset);
                            }
                            break;
                    }
                    break;
            }

            _dirty = false;
        }
        #endregion
        
        public int CompareTo(Layout other) {
            if(_depth < other._depth) {
                return 1;
            }
            if(_depth == other._depth) {
                return 0;
            }
            
            return -1;
        }

        public void SetDirty() {
            _dirty = true;
            _root.SetDirty();
        }
        
        public void RefreshChildCache() {
            _children.Clear();
            
            int childCount = transform.childCount;

            // only reallocate layoutItems array if child count has grown (or first refresh)
            if(_layoutItems == null || childCount > _layoutItems.Length) {
                _layoutItems = new LayoutItem[childCount];
            }
            else {
                for(int i = 0; i < _layoutItems.Length; i++) {
                    _layoutItems[i] = null;
                }
            }
            
            for(int i = 0; i < childCount; i++) {
                RectTransform rt = transform.GetChild(i).GetComponent<RectTransform>();
                
                LayoutItem li = rt.GetComponent<LayoutItem>();
                _layoutItems[i] = li;
                
                _children.Add(
                    new ChildInfo {
                        index = i,
                        rect = rt,
                        size = rt.rect.size,
                        enabled = rt.gameObject.activeInHierarchy,
                        ignoreLayout = li && li.IgnoreLayout
                    }
                );
            }

            _dirty = true;
        }
    }
}
