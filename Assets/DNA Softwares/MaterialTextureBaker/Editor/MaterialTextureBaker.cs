using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;


namespace DNASoftwares.MaterialTextureBaker
{
    public struct BakedTextureInfo
    {
        public string TextureFileName;
        public string TextureKey;
        public string ColorKey;
    }
    public class TextureBakeInformation
    {
        public bool VisibleInEditor;
        public Material MaterialReference;
        public MeshRenderer MeshReference;
        public string Guid;
        public List<BakedTextureInfo> BakedTextures = new List<BakedTextureInfo>();
        public Dictionary<Material, Material> MaterialSubstitutes= new Dictionary<Material, Material>(); 

        public TextureBakeInformation()
        {

        }

        public TextureBakeInformation(Material mat,string guid)
        {
            MaterialReference = mat;
            Guid = guid;
        }

        public TextureBakeInformation(MeshRenderer mesh)
        {
            MeshReference = mesh;
        }
    }

    public class MaterialTextureBaker : EditorWindow
    {
        internal static Dictionary<string, BakerShaderConfig> BakerConfigs;
        protected List<TextureBakeInformation> SelectedBakeCandidate;
        private Vector2 _scrollPosition;
        private bool _isProcessing;
        private IEnumerator _bakerProcess;
        private string _subStatus;
        private float _percentage;

        // Add menu item to the Window menu
        [MenuItem("Window/D.N.A. Softwares/Material Texture Baker")]
        private static void Init()
        {
            // Get existing open window or if none, make a new one:
            if (BakerConfigs == null)
            {
                ReBuildBakerConfig();
            }
            GetWindow<MaterialTextureBaker>(false, "MatTexBaker");
        }

        private static void ReBuildBakerConfig()
        {
            BakerConfigs = new Dictionary<string, BakerShaderConfig>();
            string[] guids = AssetDatabase.FindAssets("t:BakerShaderConfig");
            foreach (var g in guids)
            {
                string name = AssetDatabase.GUIDToAssetPath(g);
                var cfg = AssetDatabase.LoadAssetAtPath<BakerShaderConfig>(name);
                if (cfg.TargetShader != null)
                    BakerConfigs.Add(cfg.TargetShaderName, cfg);
            }
        }

        void OnEnable()
        {
            SelectedBakeCandidate = new List<TextureBakeInformation>();
        }

        void Update()
        {
            if (_isProcessing)
            {
                if (!_bakerProcess.MoveNext())
                {
                    EditorUtility.ClearProgressBar();
                    _isProcessing = false;
                }
            }
        }

        // Implement your own editor GUI here.
        private void OnGUI()
        {
            if (_isProcessing)
            {
                EditorUtility.DisplayProgressBar("Baking Textures...", _subStatus, _percentage);
            }
            EditorGUI.BeginDisabledGroup(_isProcessing);
            EditorGUILayout.BeginHorizontal();
            if (BakerConfigs == null)
            {
                ReBuildBakerConfig();
            }
            EditorGUILayout.LabelField(string.Format("{0} shader settings found in project", BakerConfigs.Count));

            if (GUILayout.Button("Rescan"))
            {
                ReBuildBakerConfig();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Select material(s)\nfrom project view")))
            {
                foreach (string guid in Selection.assetGUIDs)
                {
                    Object obj = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid));
                    Debug.Log("Selected:" + obj.ToString());
                    if (obj is Material)
                    {
                        SelectedBakeCandidate.Add(new TextureBakeInformation(obj as Material,guid));
                    }
                }
            }
            EditorGUILayout.BeginVertical();
            if (GUILayout.Button(new GUIContent("Select mesh(es)\nfrom Hierarchy view")))
            {
                foreach (var gobj in Selection.gameObjects)
                {
                    var mrend = gobj.GetComponent<MeshRenderer>();
                    if (mrend != null)
                    {
                        SelectedBakeCandidate.Add(new TextureBakeInformation(mrend));
                    }
                }
            }
            if (GUILayout.Button(new GUIContent("+ Children")))
            {
                foreach (var gobj in Selection.gameObjects)
                {
                    AddBakeCandidateRecursive(gobj);
                }
            }
            EditorGUILayout.EndVertical();
            if (GUILayout.Button(new GUIContent("Clear\nlist")))
            {
                SelectedBakeCandidate.Clear();
            }
            EditorGUILayout.EndHorizontal();
            if (SelectedBakeCandidate.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No material / mesh are selected.\n" +
                    "Select material(s) in project view,and push\"Select material(s)\"button, " +
                    "or select mesh(es) in hierarchy view,and push\"Select mesh(es)\"button.",
                    MessageType.Info);
                return;
            }
            EditorGUILayout.Separator();
            if (GUILayout.Button(new GUIContent("Bake!")))
            {
                _isProcessing = true;
                _bakerProcess = DoBakeTexture();
            }
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (var v in SelectedBakeCandidate)
            {
                string treename = v.MaterialReference == null
                    ? string.Format("[MESH] {0}",v.MeshReference.name)
                    : string.Format("[MAT] {0}",v.MaterialReference.name);
                v.VisibleInEditor = EditorGUILayout.Foldout(v.VisibleInEditor,
                    treename);
                if (v.VisibleInEditor)
                {
                    if (v.MaterialReference == null)
                    {
                        EditorGUILayout.LabelField(string.Format("contains {0} material:",v.MeshReference.sharedMaterials.Length));
                        for(int i=0;i<v.MeshReference.sharedMaterials.Length;i++)
                        {
                            DrawSingleMaterial(v.MeshReference.sharedMaterials[i],
                                string.Format("Material #{0}",i));
                        }
                    }
                    else
                    {
                        DrawSingleMaterial(v.MaterialReference);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUI.EndDisabledGroup();
        }

        private void AddBakeCandidateRecursive(GameObject gobj)
        {
            var mrend = gobj.GetComponent<MeshRenderer>();
            if(mrend!=null)
                SelectedBakeCandidate.Add(new TextureBakeInformation(mrend));
            for (int i=0;i<gobj.transform.childCount;i++)
            {
                AddBakeCandidateRecursive(gobj.transform.GetChild(i).gameObject);
            }
        }

        private void DrawSingleMaterial(Material mat,string header="")
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical();
            if(header.Length>0)
                EditorGUILayout.LabelField(header,EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.Format("Shader:{0}", mat.shader.name));
            BakerShaderConfig cfg = BakerConfigs[mat.shader.name];
            if (cfg == null)
            {
                EditorGUILayout.HelpBox(string.Format("No shader settings found \n" +
                                                      "for shader '{0}'.", mat.shader.name),
                    MessageType.Error);
            }
            else
            {
                int cnt = 1;
                foreach (var pair in cfg.Textures)
                {
                    DrawTexColPair(cnt, mat, pair);
                    cnt++;
                }
            }
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }

        private void DrawTexColPair(int cnt, Material mat, TextureColorKeyPair pair)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PrefixLabel(string.Format("Pair #{0}", cnt));
            var tex = mat.GetTexture(pair.TextureKey);
            var col = mat.GetColor(pair.ColorKey);
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField(string.Format("Texture:{0}",pair.TextureKey));
                if (tex == null)
                {
                    EditorGUILayout.LabelField("(undefined)");
                }
                else
                {
                    Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(80));
                    EditorGUI.DrawPreviewTexture(
                        r, tex);
                }
                EditorGUILayout.EndVertical();
            }
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField(string.Format("Color:{0}",pair.ColorKey));
                var r =
                    EditorGUILayout.GetControlRect(GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorGUI.DrawRect(r, col);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private IEnumerator DoBakeTexture()
        {
            _subStatus = "";
            _percentage = 0;

            foreach (var v in SelectedBakeCandidate)
            {
                if(v.MaterialReference!=null)
                    yield return BakeMaterialC(v.MaterialReference);
                if (v.MeshReference != null)
                    yield return BakeMeshMaterial(v);
            }
        }

        private IEnumerator BakeMeshMaterial(TextureBakeInformation textureBakeInformation)
        {
            var MaterialSubstitutes = new Dictionary<Material, Material>();
            var rend = textureBakeInformation.MeshReference;
            foreach (var mat in rend.sharedMaterials)
            {
                var newmat = BakeMaterial(mat);
                if(newmat!=null) MaterialSubstitutes.Add(mat,newmat);
            }
            Undo.RecordObject(textureBakeInformation.MeshReference,"Bake Mesh Material's Texture");
            var sms = rend.sharedMaterials;
            for (int i = 0; i < sms.Length; i++)
            {
                Material newmat;
                if (MaterialSubstitutes.TryGetValue(sms[i], out newmat))
                {
                    Debug.Log(
                        string.Format("{0}:{1} -> {2}.", rend.name,sms[i].name,newmat.name),
                        this);
                    sms[i] = newmat;
                }
            }
            rend.sharedMaterials = sms;
            if (MaterialSubstitutes.Count > 0)
            {
                Debug.Log(
                    string.Format("{0}:Successfully baked.", rend.name),
                    this);
            }
            else
            {
                Debug.Log(
                    string.Format("{0}:No material to bake.", rend.name),
                    this);
            }
            return null;
        }

        private Material BakeMaterial(Material mat)
        {
            var cfg = BakerConfigs[mat.shader.name];
            List<BakedTextureInfo> bakedTextures = new List<BakedTextureInfo>();

            if (cfg == null)
            {
                Debug.LogWarning(
                    string.Format("No shader settings found for shader '{0}'.", mat.shader.name),
                    this);
                return null;
            }
            var needtoclone = false;
            foreach (var pair in cfg.Textures)
            {
                Texture2D sourceTexture = mat.GetTexture(pair.TextureKey) as Texture2D;
                if (sourceTexture == null)
                {
                    continue;
                }
                var col = mat.GetColor(pair.ColorKey);
                if (col.r == 1 && col.g == 1 && col.b == 1)
                {
                    //if Color is Perfectly White(1.0,1.0,1.0),skip baking.
                    continue;
                }
                string assetPath = AssetDatabase.GetAssetPath(sourceTexture);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                }
                var bakedTexture = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.ARGB32, false);

                var pixels = sourceTexture.GetPixels();
                for (long i = 0; i < pixels.LongLength; i++)
                {
                    var c = pixels[i];
                    var dc = new Color(c.r * col.r, c.g * col.g, c.b * col.b, c.a);
                    pixels[i] = dc;
                }
                bakedTexture.SetPixels(pixels);
                var png = bakedTexture.EncodeToPNG();
                string createAssetPath = AssetDatabase.GenerateUniqueAssetPath(
                    Path.Combine(Path.GetDirectoryName(assetPath),
                    Path.GetFileNameWithoutExtension(assetPath) + pair.TextureKey + ".png"));
                bakedTextures.Add(new BakedTextureInfo
                {
                    TextureFileName = createAssetPath,
                    TextureKey = pair.TextureKey,
                    ColorKey = pair.ColorKey
                });
                Unity.FileIO.WriteAllBytes(createAssetPath, png);
                AssetDatabase.ImportAsset(createAssetPath);
                var ioutput = AssetImporter.GetAtPath(createAssetPath) as TextureImporter;
                if (!ioutput.isReadable)
                {
                    ioutput.isReadable = true;
                    ioutput.SaveAndReimport();
                }
                needtoclone = true;
            }
            if (needtoclone)
            {
                var origMaterialPath = AssetDatabase.GetAssetPath(mat);
                var createMaterialPath = AssetDatabase.GenerateUniqueAssetPath(
                    Path.Combine(Path.GetDirectoryName(origMaterialPath),
                        Path.GetFileNameWithoutExtension(origMaterialPath) + "_baked.mat"));
                var newmat = new Material(mat);
                foreach (var baked in bakedTextures)
                {
                    newmat.SetColor(baked.ColorKey, Color.white);
                    newmat.SetTexture(baked.TextureKey, AssetDatabase.LoadAssetAtPath<Texture>(baked.TextureFileName));
                }
                AssetDatabase.CreateAsset(newmat, createMaterialPath);
                return newmat;
            }
            return null;
        }

        private IEnumerator BakeMaterialC(Material mat)
        {
            var cfg = BakerConfigs[mat.shader.name];
            var bakedTextures=new List<BakedTextureInfo>();

            if (cfg == null)
            {
                Debug.LogWarning(
                    string.Format("No shader settings found for shader '{0}'.", mat.shader.name),
                    this);
                yield break;
            }
            var needtoclone = false;
            foreach (var pair in cfg.Textures)
            {
                var sourceTexture = mat.GetTexture(pair.TextureKey) as Texture2D;
                if (sourceTexture == null)
                {
                    continue;
                }
                var col = mat.GetColor(pair.ColorKey);
                if (col.r == 1 && col.g == 1 && col.b == 1)
                {
                    //if Color is Perfectly White(1.0,1.0,1.0),skip baking.
                    continue;
                }
                var assetPath = AssetDatabase.GetAssetPath(sourceTexture);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    _subStatus = "Changing Setting for " + System.IO.Path.GetFileName(assetPath);
                    yield return null;
                    importer.SaveAndReimport();
                    sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                }
                var BakedTexture = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.ARGB32, false);

                var pixels = sourceTexture.GetPixels();
                _subStatus = "Baking color to" + System.IO.Path.GetFileName(assetPath);
                yield return null;
                for (long i = 0; i < pixels.LongLength; i++)
                {
                    Color c = pixels[i];
                    Color dc = new Color(c.r * col.r, c.g * col.g, c.b * col.b, c.a);
                    pixels[i] = dc;
                }
                BakedTexture.SetPixels(pixels);
                byte[] png = BakedTexture.EncodeToPNG();
                string createAssetPath = AssetDatabase.GenerateUniqueAssetPath(
                    Path.Combine(Path.GetDirectoryName(assetPath),
                    Path.GetFileNameWithoutExtension(assetPath) + ".png"));
                bakedTextures.Add(new BakedTextureInfo()
                {
                    TextureFileName = createAssetPath,
                    TextureKey = pair.TextureKey,
                    ColorKey = pair.ColorKey
                });
                DNASoftwares.Unity.FileIO.WriteAllBytes(createAssetPath, png);
                AssetDatabase.ImportAsset(createAssetPath);
                needtoclone = true;
            }
            if (needtoclone)
            {
                var origMaterialPath = AssetDatabase.GetAssetPath(mat);
                var createMaterialPath = AssetDatabase.GenerateUniqueAssetPath(
                    Path.Combine(Path.GetDirectoryName(origMaterialPath),
                        Path.GetFileNameWithoutExtension(origMaterialPath) + "_baked.mat"));
                _subStatus = "Saving New material" + System.IO.Path.GetFileName(createMaterialPath);
                yield return null;
                var newmat = new Material(mat);
                foreach (var baked in bakedTextures)
                {
                    newmat.SetColor(baked.ColorKey, Color.white);
                    newmat.SetTexture(baked.TextureKey, AssetDatabase.LoadAssetAtPath<Texture>(baked.TextureFileName));
                }
                AssetDatabase.CreateAsset(newmat, createMaterialPath);
                Debug.Log(
                    string.Format("{1}:Successfully baked,saved in file '{0}'.", createMaterialPath,newmat.name),
                    this);
            }
            else
            {
                Debug.Log(
                    string.Format("{0}:No textures to bake.", mat.name),
                    this);

            }
        }

        // Called whenever the project has changed.
        void OnProjectChange()
        {
        }
    }
}