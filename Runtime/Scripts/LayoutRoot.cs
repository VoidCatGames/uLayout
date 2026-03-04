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
using System.Collections.Generic;
using UnityEngine;

namespace Poke.UI
{
    [
        ExecuteAlways,
        RequireComponent(typeof(RectTransform))
    ]
    public class LayoutRoot : MonoBehaviour
    {
        [SerializeField] private bool m_log;
        
        private readonly SortedBucket<Layout, int, Layout> _layouts = new (l => l, l => l.GetInstanceID());
        private readonly Stack<Layout> _reverse = new ();
        private bool _dirty;

        private void OnEnable() {
            _dirty = true;
        }

        private void Start() {
            UpdateLayout();
        }

        public void SetDirty() {
            _dirty = true;
        }
        
        public void LateUpdate() {
            if(_dirty) {
                UpdateLayout();
            }
        }

        public void UpdateLayout() {
            if(m_log) Debug.Log($"[Root]: Update Layout ({Time.unscaledTime:f5})");
            
            _reverse.Clear();
                
            // fit sizing pass (0)
            if(m_log) Debug.Log($"[Root]: Fit Size Pass");
            foreach(Layout l in _layouts) {
                if(l.NeedsRefresh) {
                    l.ComputeFitSize();
                    _reverse.Push(l);
                }
            }

            // grow sizing pass (1)
            if(m_log) Debug.Log($"[Root]: Grow Size Pass");
            foreach(Layout l in _reverse) {
                if(l.NeedsRefresh) {
                    l.GrowChildren();
                }
            }
                
            // layout pass (2)
            if(m_log) Debug.Log($"[Root]: Layout Pass");
            foreach(Layout l in _reverse) {
                l.ComputeLayout();
            }
            
            if(m_log) Debug.Log($"[Root]: Refreshed {_reverse.Count} layouts");
            
            _dirty = false;
        }

        public void RegisterLayout(Layout layout) {
            if(m_log) Debug.Log($"[Root]: Registered \"{layout.name}\" at depth [{layout.Depth}]");
            
            layout.OnLayoutChanged += SetDirty;
            _layouts.Add(layout);
            
            SetDirty();
        }

        public void UnregisterLayout(Layout layout) {
            if(_layouts.Remove(layout)) {
                layout.OnLayoutChanged -= SetDirty;
                
                SetDirty();
                if(m_log) Debug.Log($"[Root]: Removed \"{layout.name}\"");
            }
            else {
                Debug.LogError($"[Root]: Failed to remove \"{layout.name}\" (not found)");
            }
        }
    }
}