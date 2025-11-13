using System;
using System.Collections.Generic;
using System.IO;
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

        var property = so.FindProperty("m_Generator");
        var propertyBinding = new PropertyField(property, "Property binding:");
        root.Add(propertyBinding);
        propertyBinding.BindProperty(property);


        root.Add(new Button(Generate) { text = "Generate" });
        root.Add(new Button(Save) { text = "Save" });
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

            var src = m_Generator.Texture;
            m_Generated = new Texture2D(src.width, src.height);

            try
            {
                var srcPixels = src.GetPixels();
                int w = src.width;
                int h = src.height;

                float threshold = 0.5f;

                var outPixels = new Color[w * h];

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int idx = y * w + x;
                        var c = srcPixels[idx];
                        bool isInside = c.a > threshold;

                        float dist;
                        {
                            
                            int maxRadius = 0;
                            if (m_Generator != null)
                                maxRadius = isInside ? m_Generator.InsideDistance : m_Generator.OutsideDistance;

                            int minX = Mathf.Max(0, x - maxRadius);
                            int maxX = Mathf.Min(w - 1, x + maxRadius);
                            int minY = Mathf.Max(0, y - maxRadius);
                            int maxY = Mathf.Min(h - 1, y + maxRadius);

                            float minDistSq = float.MaxValue;
                            for (int yy = minY; yy <= maxY; yy++)
                            {
                                for (int xx = minX; xx <= maxX; xx++)
                                {
                                    int idx2 = yy * w + xx;
                                    var c2 = srcPixels[idx2];
                                    bool isInside2 = c2.a > threshold;
                                    if (isInside2 == isInside) continue;

                                    int dx = xx - x;
                                    int dy = yy - y;
                                    float d2 = dx * dx + dy * dy;
                                    if (d2 < minDistSq) minDistSq = d2;
                                }
                            }

                            if (minDistSq == float.MaxValue)
                                dist = maxRadius; // fallback when no opposite pixel found within search area
                            else
                                dist = Mathf.Sqrt(minDistSq);
                        }


                        float val;
                        if (isInside)
                        {
                            val = (m_Generator != null && m_Generator.InsideDistance > 0)
                                ? 0.5f + Mathf.Clamp01(dist / m_Generator.InsideDistance)/2
                                : 1f;
                        }
                        else
                        {
                            val = (m_Generator != null && m_Generator.OutsideDistance > 0)
                                ? 0.5f - Mathf.Clamp01(dist / m_Generator.OutsideDistance)/2
                                : 0f;
                        }

                        outPixels[idx] = new Color(val, val, val, 1f);
                    }
                }

                m_Generated.SetPixels(outPixels);
                m_Generated.Apply();
            }
            catch (UnityException)
            {
                Debug.LogWarning("Source texture is not readable; created an empty texture with the same size.");
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
                importer.SaveAndReimport();
            }

            Debug.Log($"Saved SDF texture to `{path}`");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save texture: {e.Message}");
        }
    }
   

    IEnumerable<int2> Spiral(int2 bounds)
    {
        int2 dir;
        int2 curr;
        int2 h = 1;
        curr = default;
        dir = new int2(1, 0);

        while (math.all(math.abs(curr) < bounds*2))
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
    public int InsideDistance;
    public int OutsideDistance;
}