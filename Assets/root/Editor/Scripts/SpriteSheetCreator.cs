using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

public static class SpriteSheetCreator
{
    /// <summary>
    /// Creates a PNG spritesheet from a list of SKBitmaps.
    /// Each sprite is scaled to 64x64 and arranged horizontally.
    /// </summary>
    /// <param name="frames">List of bitmaps representing the frames.</param>
    /// <param name="outputFilePath">The file path to save the PNG spritesheet.</param>
    public static void CreateSpriteSheet(List<SKBitmap> frames, string outputFilePath, int spriteWidth = -1, int spriteHeight = -1)
    {
        if (spriteWidth == -1 || spriteHeight == -1)
        {
            spriteWidth = frames[0].Width;
            spriteHeight = frames[0].Height;
            
            if (spriteWidth > 64 || spriteHeight > 64)
            {
                if (spriteWidth > spriteHeight)
                {
                    float heightScale = 64.0f/spriteWidth;
                    spriteWidth = 64;
                    spriteHeight = (int)Math.Floor(spriteHeight*heightScale);
                }
                else
                {
                    float widthScale = 64.0f/spriteHeight;
                    spriteHeight = 64;
                    spriteWidth = (int)Math.Floor(spriteWidth*widthScale);
                }
            }
        }
        
        int frameCount = frames.Count;
        int sheetWidth = frameCount * spriteWidth;
        int sheetHeight = spriteHeight;

        var sheetInfo = new SKImageInfo(sheetWidth, sheetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var surface = SKSurface.Create(sheetInfo))
        {
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            for (int i = 0; i < frameCount; i++)
            {
                SKBitmap frame = frames[i];
                // Destination rectangle: sprite's position and size on the spritesheet.
                var destRect = new SKRect(i * spriteWidth, 0, (i + 1) * spriteWidth, spriteHeight);
                // Source rectangle: full original bitmap
                var sourceRect = new SKRect(0, 0, frame.Width, frame.Height);

                canvas.DrawBitmap(frame, sourceRect, destRect);
            }

            canvas.Flush();
            using (var image = surface.Snapshot())
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            {
                using (var stream = File.OpenWrite(outputFilePath))
                {
                    data.SaveTo(stream);
                }
            }
        }
    }
}