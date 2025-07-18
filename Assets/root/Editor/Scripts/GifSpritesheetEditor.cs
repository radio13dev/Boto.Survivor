using System.Collections.Generic;
using System.IO;
using SkiaSharp;
using UnityEditor;
using UnityEngine;

public static class GifSpritesheetEditor
{
    [MenuItem("Tools/Create Spritesheet From Gif")]
    public static void CreateSpritesheetFromGif()
    {
        // Open a file selection dialog to select a .gif file.
        string gifPath = EditorUtility.OpenFilePanel("Select GIF file", "", "gif");
        if (string.IsNullOrEmpty(gifPath))
        {
            Debug.Log("No file selected.");
            return;
        }

        // Extract frames using the GifFrameExtractor in the project.
        List<SKBitmap> frames = GifFrameExtractor.ExtractFrames(gifPath);
        if (frames == null || frames.Count == 0)
        {
            Debug.LogError("Failed to extract frames from the selected GIF.");
            return;
        }
        
        // Open a save file panel to choose where to output the spritesheet.
        string outputPath = EditorUtility.SaveFilePanel("Save Spritesheet", "Assets/root/Runtime/Materials/Spritesheets", Path.GetFileNameWithoutExtension(gifPath) + "_spritesheet_" + frames.Count + ".png", "png");
        if (string.IsNullOrEmpty(outputPath))
        {
            Debug.Log("Save operation cancelled.");
            return;
        }

        // Create the spritesheet with each sprite scaled to 64x64.
        SpriteSheetCreator.CreateSpriteSheet(frames, outputPath, 64, 64);
        Debug.Log("Spritesheet created at: " + outputPath);
    }
}