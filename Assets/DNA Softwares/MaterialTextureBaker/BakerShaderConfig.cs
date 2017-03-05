using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace DNASoftwares.MaterialTextureBaker
{
    [Serializable]
    public struct TextureColorKeyPair
    {
        public string TextureKey;
        public string ColorKey;
    }
    public class BakerShaderConfig : ScriptableObject
    {
        public Shader TargetShader;

        public string TargetShaderName
        {
            get { return TargetShader == null?"":TargetShader.name; }
        }
        [SerializeField]
        public TextureColorKeyPair[] Textures;
    }
}