using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;
using Image = UnityEngine.UIElements.Image;

public class SdfGeneratorEditor : EditorWindow
{
    [SerializeField] private SdfGenerator m_Generator;
    [SerializeField] private Sprite[] m_Bulk = Array.Empty<Sprite>();
    Texture2D m_Generated;
    Image m_Image;

    [SerializeField] private VisualTreeAsset m_VisualTreeAsset = default;

    [MenuItem("Window/UI Toolkit/SdfGeneratorEditor")]
    public static void ShowExample()
    {
        SdfGeneratorEditor wnd = GetWindow<SdfGeneratorEditor>();
        wnd.titleContent = new GUIContent("SdfGeneratorEditor");
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // VisualElements objects can contain other VisualElement following a tree hierarchy.
        VisualElement label = new Label("Hello World! From C#");
        root.Add(label);

        var so = new SerializedObject(this);

        {
            var property = so.FindProperty("m_Generator");
            var propertyBinding = new PropertyField(property, "Property binding:");
            root.Add(propertyBinding);
            propertyBinding.BindProperty(property);
        }

        root.Add(new Button(Generate) { text = "Generate" });
        root.Add(new Button(Save) { text = "Save" });

        {
            var property = so.FindProperty("m_Bulk");
            var propertyBinding = new PropertyField(property, "Bulk:");
            root.Add(propertyBinding);
            propertyBinding.BindProperty(property);
        }
        root.Add(new Button(BulkGenAndSave) { text = "Bulk Generate and Save" });

        root.Add(new Image() { image = m_Generator.Texture, scaleMode = ScaleMode.ScaleToFit });
        root.Add(m_Image = new Image());
        m_Image.scaleMode = ScaleMode.ScaleToFit;

        // Instantiate UXML
        VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
        root.Add(labelFromUXML);
    }

    void Generate()
    {
        {
            if (m_Generator == null || m_Generator.Texture == null)
            {
                Debug.LogWarning("No source texture assigned on SdfGenerator.");
                return;
            }

            EditorUtility.DisplayProgressBar("Generating sdf", "Starting sdf gen", 0f);

            var oldImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(m_Generator.Texture)) as TextureImporter;
            if (!oldImporter.isReadable)
            {
                EditorUtility.DisplayProgressBar("Generating sdf", "Making texture readable", 0f);
                oldImporter.isReadable = true;
                oldImporter.SaveAndReimport();
            }

            var src = m_Generator.Texture;
            var srcSize = new int2(src.width, src.height);
            int2 dest2 = new int2((int)math.ceil(src.width * m_Generator.OutputScale), (int)math.ceil(src.height * m_Generator.OutputScale));
            int2 padding = (int2)(m_Generator.Padding * (float2)dest2);
            m_Generated = new Texture2D(dest2.x + padding.x * 2, dest2.y + padding.y * 2);

            try
            {
                var srcPixels = src.GetPixels();

                float threshold = 0.5f;

                var outPixels = new Color[m_Generated.width * m_Generated.height];

                var spiral = Spiral(srcSize).ToArray();

                for (int yIt = 0; yIt < m_Generated.height; yIt++)
                {
                    int y = yIt;
                    EditorUtility.DisplayProgressBar("Generating sdf", $"Writing pixel: ({0},{y})", (float)y / m_Generated.width);
                    Parallel.For(0, m_Generated.width, x =>
                    {
                        int2 idp = new int2((int)math.floor((x - padding.x) / m_Generator.OutputScale), (int)math.floor((y - padding.y) / m_Generator.OutputScale));

                        Color c;
                        if (idp.y < 0 || idp.y >= srcSize.y || idp.x < 0 || idp.x >= srcSize.x)
                            c = Color.clear;
                        else
                        {
                            int inId = idp.y * srcSize.x + idp.x;
                            c = srcPixels[inId];
                        }

                        bool isInside = c.a > threshold;

                        float minDistSq = float.MaxValue;
                        float maxSearchD = float.MaxValue;
                        for (int i = 0; i < spiral.Length; i++)
                        {
                            bool isInside2;
                            var p = idp + spiral[i];
                            int idx2 = p.y * srcSize.x + p.x;
                            if (p.x < 0 || p.x >= srcSize.x || p.y < 0 || p.y >= srcSize.y || idx2 < 0 || idx2 >= srcPixels.Length)
                            {
                                isInside2 = false;
                            }
                            else
                            {
                                var c2 = srcPixels[idx2];
                                isInside2 = c2.a > threshold;
                            }

                            if (isInside2 == isInside) continue;

                            var d = math.lengthsq(spiral[i]);
                            if (d < minDistSq)
                            {
                                minDistSq = d;
                                // Assume this is the 'corner' of a square. Search out until we hit the radius that a circle would encapsulate that square
                                maxSearchD = d * 2;
                            }
                            else if (d >= maxSearchD)
                            {
                                break;
                            }
                        }


                        float val;
                        if (isInside)
                        {
                            val = (m_Generator.InsideDistance > 0)
                                ? 0.5f + Mathf.Clamp01(math.sqrt(minDistSq) / m_Generator.OutsideDistance) / 2
                                : 1f;
                        }
                        else
                        {
                            val = (m_Generator.OutsideDistance > 0)
                                ? 0.5f - Mathf.Clamp01(math.sqrt(minDistSq) / m_Generator.OutsideDistance) / 2
                                : 0f;
                        }

                        int outId = y * m_Generated.width + x;
                        outPixels[outId] = new Color(val, val, val, 1f);
                    });
                }

                m_Generated.SetPixels(outPixels);
                m_Generated.Apply();
            }
            catch (UnityException)
            {
                Debug.LogWarning("Source texture is not readable; created an empty texture with the same size.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"Created new texture {m_Generated.width}x{m_Generated.height}");
            m_Image.image = m_Generated;
        }
    }

    void Save()
    {
        // Open a save prompt to save the image to an asset
        if (m_Generated == null)
        {
            Debug.LogWarning("No generated texture to save.");
            return;
        }

        var defaultPath = AssetDatabase.GetAssetPath(m_Generator.Texture);
        var defaultFile = Path.GetFileNameWithoutExtension(defaultPath);
        var defaultFilePlusExtension = Path.GetFileName(defaultPath);
        var joinedPath = Path.Join(defaultPath.Substring(0, defaultPath.Length - defaultFilePlusExtension.Length), "_sdf");
        Debug.Log($"Path: {defaultPath}");
        Debug.Log($"File: {defaultFile}");
        Debug.Log($"JoinedPath: {joinedPath}");

        if (!Directory.Exists(joinedPath))
        {
            Debug.Log($"Creating directory...");
            Directory.CreateDirectory(joinedPath);
        }

        string path = EditorUtility.SaveFilePanelInProject(
            "Save SDF Texture",
            defaultFile + "_sdf.png",
            "png",
            "Choose location to save generated SDF",
            joinedPath);

        if (string.IsNullOrEmpty(path))
        {
            if (Directory.GetFiles(joinedPath).Length == 0) Directory.Delete(joinedPath);
            return;
        }

        try
        {
            byte[] png = m_Generated.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path);


            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            var oldImporter = AssetImporter.GetAtPath(defaultPath) as TextureImporter;
            if (importer != null && oldImporter != null)
            {
                TextureImporterSettings settings = new();
                oldImporter.ReadTextureSettings(settings);
                importer.SetTextureSettings(settings);
                importer.filterMode = FilterMode.Trilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.compressionQuality = 100;
                importer.spritePixelsPerUnit = 1000.0f * m_Generated.width / (512.0f * (1 + m_Generator.Padding * 2));
                importer.SaveAndReimport();
            }

            Debug.Log($"Saved SDF texture to `{path}`");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save texture: {e.Message}");
        }
    }


    void BulkGenAndSave()
    {
        var old = m_Generator.Texture;
        var oldGen = m_Generated;
        List<Texture2D> bulkGenerated = new();
        foreach (var tex in m_Bulk)
        {
            m_Generator.Texture = tex.texture;
            Generate();
            bulkGenerated.Add(m_Generated);
        }

        if (bulkGenerated.Count > 0)
        {
            string folderPath;
            TextureImporter oldImporter;
            {
                var defaultPath = AssetDatabase.GetAssetPath(m_Generator.Texture);
                var defaultFile = Path.GetFileNameWithoutExtension(defaultPath);
                var defaultFilePlusExtension = Path.GetFileName(defaultPath);
                var joinedPath = Path.Join(defaultPath.Substring(0, defaultPath.Length - defaultFilePlusExtension.Length), "_sdf");
                Debug.Log($"Path: {defaultPath}");
                Debug.Log($"File: {defaultFile}");
                Debug.Log($"JoinedPath: {joinedPath}");

                if (!Directory.Exists(joinedPath))
                {
                    Debug.Log($"Creating directory...");
                    Directory.CreateDirectory(joinedPath);
                }

                folderPath = EditorUtility.SaveFolderPanel(
                    "Save SDF Texture",
                    joinedPath,
                    "_sdf");
                    
                if (Directory.GetFiles(joinedPath).Length == 0) Directory.Delete(joinedPath);
                
                oldImporter = AssetImporter.GetAtPath(defaultPath) as TextureImporter;
            }

            
            if (string.IsNullOrEmpty(folderPath))
                return;
                

            for (int i = 0; i < bulkGenerated.Count; i++)
            {
                try
                {
                    var defaultPath = AssetDatabase.GetAssetPath(m_Bulk[i].texture);
                    var defaultFile = Path.GetFileNameWithoutExtension(defaultPath);
                    Debug.Log($"Path: {defaultPath}");
                    Debug.Log($"File: {defaultFile}");
                    Debug.Log($"Folder path: {folderPath}");
                    
                    string path = Path.Join(folderPath, defaultFile + "_sdf.png");
                    Debug.Log($"Full path: {path}");
                    var relPath = Path.Join("Assets", Path.GetRelativePath(Path.GetFullPath("Assets"), path));
                    Debug.Log($"Relative: {relPath}");
                    
                    byte[] png = bulkGenerated[i].EncodeToPNG();
                    System.IO.File.WriteAllBytes(relPath, png);
                    AssetDatabase.ImportAsset(relPath);

                    var importer = AssetImporter.GetAtPath(relPath) as TextureImporter;
                    if (importer != null && oldImporter != null)
                    {
                        TextureImporterSettings settings = new();
                        oldImporter.ReadTextureSettings(settings);
                        importer.SetTextureSettings(settings);
                        importer.filterMode = FilterMode.Trilinear;
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.compressionQuality = 100;
                        importer.spritePixelsPerUnit = 1000.0f * bulkGenerated[i].width / (512.0f * (1 + m_Generator.Padding * 2));
                        importer.SaveAndReimport();
                    }
                    else
                        Debug.LogWarning($"Did not update importer for {relPath}");

                    Debug.Log($"Saved SDF texture to `{relPath}`");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to save texture: {e.Message}");
                }
            }
        }


        m_Generated = oldGen;
        m_Generator.Texture = old;
    }

    IEnumerable<int2> Spiral(int2 bounds)
    {
        int2 dir;
        int2 curr;
        int2 h = 1;
        curr = default;
        dir = new int2(1, 0);

        while (math.all(math.abs(curr) < bounds / 2))
        {
            yield return curr;
            var next = curr + dir;
            if (next.x >= h.x)
            {
                dir = new int2(0, -1);
                h++;
                h = math.clamp(h, 0, bounds);
                if (math.all(h == bounds)) yield break;
            }
            else if (next.y <= -h.y)
            {
                dir = new int2(-1, 0);
            }
            else if (next.x <= -h.x)
            {
                dir = new int2(0, 1);
            }
            else if (next.y >= h.y)
            {
                dir = new int2(1, 0);
            }

            curr = curr + dir;
        }
    }
}

[Serializable]
public class SdfGenerator
{
    public Texture2D Texture;
    public int InsideDistance = 50;
    public int OutsideDistance = 50;
    [Range(0.01f, 1f)] public float OutputScale = 0.05f;
    [Range(0, 1f)] public float Padding = 0.07f;

    [NonSerialized] public Texture2D Generated;
    [NonSerialized] public Image TexturePreview;
    [NonSerialized] public Image GeneratedPreview;
}