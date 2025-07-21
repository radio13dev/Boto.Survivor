using System.Collections.Generic;
using System.IO;
using SkiaSharp;

public static class GifFrameExtractor
{
    /// <summary>
    /// Extracts the frames of a GIF file as SKBitmaps.
    /// </summary>
    /// <param name="filePath">The path to the GIF file.</param>
    /// <returns>A list of SKBitmaps, each representing one frame.</returns>
    public static List<SKBitmap> ExtractFrames(string filePath)
    {
        var frames = new List<SKBitmap>();
        using (var stream = File.OpenRead(filePath))
        {
            using (var codec = SKCodec.Create(stream))
            {
                int frameCount = codec.FrameCount;
                var imageInfo = codec.Info;

                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    // Create a new bitmap for the frame.
                    var bitmap = new SKBitmap(imageInfo.Width, imageInfo.Height);
                    // Use SKCodecOptions to decode the specific frame.
                    var codecOptions = new SKCodecOptions(frameIndex);
                    SKCodecResult result = codec.GetPixels(imageInfo, bitmap.GetPixels(), codecOptions);

                    if (result == SKCodecResult.Success || result == SKCodecResult.IncompleteInput)
                    {
                        // Add the decoded bitmap (frame) to the list.
                        frames.Add(bitmap);
                    }
                }
            }
        }

        return frames;
    }
}