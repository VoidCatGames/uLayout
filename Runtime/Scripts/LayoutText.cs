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
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poke.UI {
    [RequireComponent(typeof(TMP_Text))]
    public class LayoutText : LayoutItem, ILayoutSelfController
    {
        [Header("Text")]
        [SerializeField, Min(0)] private float m_maxFontSize;
        
        private TMP_Text _text;
        private DrivenRectTransformTracker _rectTracker;
        private Vector2 _preferredSize;
        private float _fontSize;
        private bool _dirty;
        private string _str;
        
        protected override void Awake() {
            base.Awake();
            _text = GetComponent<TMP_Text>();
            _rectTracker = new DrivenRectTransformTracker();
        }

        protected override void OnValidate() {
            base.OnValidate();
            _dirty = true;
        }

        protected override void OnEnable() {
            base.OnEnable();
            Log("enable");
            
            _str = _text.text;
            _fontSize = _text.fontSize;
            _text.ForceMeshUpdate(true, true);
            _dirty = true;
            
            Log($"line count: {_text.textInfo.lineCount}");
        }

        public override void Update() {
            //if(m_log) Debug.Log($"[LT:{gameObject.name}]: update");
            
            _rectTracker.Clear();
            if(m_sizing.x == SizingMode.FitContent)
                _rectTracker.Add(this, _rect, DrivenTransformProperties.SizeDeltaX);
            if(m_sizing.y == SizingMode.FitContent)
                _rectTracker.Add(this, _rect, DrivenTransformProperties.SizeDeltaY);
            
            _text.textWrappingMode = m_sizing.x == SizingMode.Grow ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            
            if(String.CompareOrdinal(_str, _text.text) != 0) {
                _str = _text.text;
                _dirty = true;
            }

            if(!Mathf.Approximately(_fontSize, _text.fontSize)) {
                _dirty = true;
            }
            
            if(!Mathf.Approximately(_rect.rect.size.x, _preferredSize.x) && m_sizing.x != SizingMode.Grow) {
                _dirty = true;
            }
            if(!Mathf.Approximately(_rect.rect.size.y, _preferredSize.y) && m_sizing.y != SizingMode.Grow) {
                _dirty = true;
            }

            if(_dirty) {
                LayoutRebuilder.MarkLayoutForRebuild(_rect);
            }
            
            base.Update();
        }

        public void SetLayoutHorizontal() {
            if(_dirty) {
                if(m_sizing.x == SizingMode.FitContent) {
                    Log("SetLayoutHorizontal");
                    _preferredSize.x = LayoutUtility.GetPreferredWidth(_rect);
                    _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _preferredSize.x);
                }
            }
        }
        
        public void SetLayoutVertical() {
            if(_dirty) {
                if(m_sizing.y == SizingMode.FitContent) {
                    Log($"SetLayoutVertical ({_text.textInfo.lineCount} lines)");
                    _preferredSize.y = LayoutUtility.GetPreferredHeight(_rect);
                    Log($"height: {_preferredSize.y}");
                    _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _preferredSize.y);
                }

                _dirty = false;
            }
        }

        private void Log(object msg) {
            if(m_log) Debug.Log($"[LT:{gameObject.name}]: {msg}");
        }
    }
}
