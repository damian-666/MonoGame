// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.IO;
using Microsoft.Xna.Framework.Media;
using MonoGame.Framework.Utilities;

namespace Microsoft.Xna.Framework.Content
{
    internal class VideoReader : ContentTypeReader<Video>
    {
        protected internal override Video Read(ContentReader input, Video existingInstance)
        {
            string path = input.ReadObject<string>();
            int durationMS = input.ReadObject<int>();
            int width = input.ReadObject<int>();
            int height = input.ReadObject<int>();
            float framesPerSecond = input.ReadObject<float>();
            int soundTrackType = input.ReadObject<int>();  // 0 = Music, 1 = Dialog, 2 = Music and dialog

            if (!String.IsNullOrEmpty(path))
            {
                // Add the ContentManager's RootDirectory
                string rootDirectoryFullPath = input.ContentManager.RootDirectoryFullPath;
                string dirPath = Path.Combine(rootDirectoryFullPath, input.AssetName);

                // Resolve the relative path
                path = FileHelpers.ResolveRelativePath(dirPath, path);
            }

            return new Video(input.GetGraphicsDevice(), path, durationMS)
            {
                Width = width,
                Height = height,
                FramesPerSecond = framesPerSecond,
                VideoSoundtrackType = (VideoSoundtrackType)soundTrackType
            };
        }
    }
}
