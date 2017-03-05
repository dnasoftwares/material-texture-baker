using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using UnityEditorInternal;
using System;

namespace DNASoftwares.MaterialTextureBaker
{
    [CustomEditor(typeof(DNASoftwares.MaterialTextureBaker.BakerShaderConfig))]
    public class BakerShaderConfigEditor : Editor {

        private ReorderableList _pairsList;
        private List<string> _texturenames;
        private List<string> _colornames;
        // Implement this function tomake a custom
        // inspector. 
        public override void OnInspectorGUI () {
            serializedObject.Update();
            DrawProperties();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProperties()
        {
            var obj = target as BakerShaderConfig;
            if(obj==null)return;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetShader"),
                new GUIContent("Shader"));
            if(EditorGUI.EndChangeCheck())
            {
                RebuildNames();
            }
            _pairsList.DoLayoutList();
        }

        // This function is called when the object
	    // is loaded.
	    void OnEnable ()
	    {
            if (_texturenames == null || _colornames == null)
                RebuildNames();
            _pairsList = new ReorderableList(serializedObject, serializedObject.FindProperty("Textures"))
	        {
	            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Texture and Color Pairs"),
                drawElementCallback = (rect, index, active, focused) =>
                {
                    var texture_w = rect.width/2;
                    var color_w = rect.width - texture_w;
                    var element = _pairsList.serializedProperty.GetArrayElementAtIndex(index);
                    EditorGUI.BeginChangeCheck();
                    var newtexindex =
                        EditorGUI.Popup(new Rect(rect.x, rect.y, texture_w, EditorGUIUtility.singleLineHeight),
                            _texturenames.IndexOf(element.FindPropertyRelative("TextureKey").stringValue),
                            _texturenames.ToArray());
                    var newcolorindex =
                        EditorGUI.Popup(
                            new Rect(rect.x + texture_w, rect.y, color_w, EditorGUIUtility.singleLineHeight),
                            _colornames.IndexOf(element.FindPropertyRelative("ColorKey").stringValue),
                            _colornames.ToArray());
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (newtexindex >= 0)
                            element.FindPropertyRelative("TextureKey").stringValue = _texturenames[newtexindex];
                        if (newcolorindex >= 0)
                            element.FindPropertyRelative("ColorKey").stringValue = _colornames[newcolorindex];
                    }
                }
	        };

	    }

        private void RebuildNames()
        {
            Shader s = serializedObject.FindProperty("TargetShader").objectReferenceValue as Shader;
            if (s == null) return;
            _texturenames=new List<string>();
            _colornames=new List<string>();
            for (var i = 0; i < ShaderUtil.GetPropertyCount(s); i++)
            {
                switch (ShaderUtil.GetPropertyType(s, i))
                {
                    case ShaderUtil.ShaderPropertyType.Color:
                        _colornames.Add(ShaderUtil.GetPropertyName(s,i));
                        break;
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        _texturenames.Add(ShaderUtil.GetPropertyName(s,i));
                        break;
                }
            }
            if (_texturenames.Count == 0)
            {
                _texturenames.Add("NO TEXTURE PROPERTY");
            }
            if (_colornames.Count == 0)
            {
                _colornames.Add("NO COLOR PROPERTY");
            }
        }
    }
}
