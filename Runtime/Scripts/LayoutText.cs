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

namespace Poke.UI
{
    [RequireComponent(typeof(TMP_Text))]
    public class LayoutText : LayoutItem
    {
        private TMP_Text _text;
        private Vector2 _preferredSize;
        private float _fontSize;
        private string _str;
        
        protected override void Awake() {
            base.Awake();
            _text = GetComponent<TMP_Text>();
        }

        protected override void OnEnable() {
            base.OnEnable();
            Log("enable");
            
            _str = _text.text;
            _fontSize = _text.fontSize;
            _text.ForceMeshUpdate(true, true);

            _preferredSize = _text.GetPreferredValues();
            Log($"preferred size: {_preferredSize}, {_text.textInfo.lineCount} lines");
        }

        public override void Update() {
            base.Update();
            
            _text.textWrappingMode = m_sizing.x != SizingMode.FitContent ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            
            if(String.CompareOrdinal(_str, _text.text) != 0) {
                _str = _text.text;
                _dirty = true;
            }

            if(!Mathf.Approximately(_text.fontSize, _fontSize)) {
                _fontSize = _text.fontSize;
                _dirty = true;
            }
                
            if(_dirty) {
                _text.ForceMeshUpdate(true, true);
                _preferredSize = _text.GetPreferredValues();
            }

            if(_dirty && m_sizing.x == SizingMode.FitContent && m_sizing.y != SizingMode.Grow) {
                Log("Fit Size X");
                _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _preferredSize.x);
            }
            if(_dirty && m_sizing.y == SizingMode.FitContent && m_sizing.x != SizingMode.Grow) {
                Log("Fit Size Y");
                _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _preferredSize.y);
            }

            if(_dirty) {
                if(_parent) {
                    _parent.SetDirty();
                }
                LayoutRebuilder.MarkLayoutForRebuild(_rect);
                _dirty = false;
            }
        }

        public override void GrowSizingXCallback(float x) {
            if(m_sizing.y == SizingMode.FitContent) {
                _text.ForceMeshUpdate(true, true);
                _preferredSize = _text.GetPreferredValues();
                _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _preferredSize.y);
            }
        }

        public override void GrowSizingYCallback(float y) {
            if(m_sizing.x == SizingMode.FitContent) {
                _text.ForceMeshUpdate(true, true);
                _preferredSize = _text.GetPreferredValues();
                _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _preferredSize.x);
            }
        }

        private void Log(object msg) {
            if(m_log) Debug.Log($"[LT:{gameObject.name}]: {msg}");
        }
    }
}
