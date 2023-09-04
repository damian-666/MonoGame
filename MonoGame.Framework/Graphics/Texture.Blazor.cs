// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Diagnostics;
using Microsoft.Xna.Platform.Graphics;

namespace Microsoft.Xna.Framework.Graphics
{
    public abstract partial class Texture
    {

        private void PlatformGraphicsDeviceResetting()
        {
            if (GetTextureStrategy<ConcreteTexture>()._glTexture != null)
                GetTextureStrategy<ConcreteTexture>()._glTexture.Dispose();
            GetTextureStrategy<ConcreteTexture>()._glTexture = null;
            GetTextureStrategy<ConcreteTexture>()._glLastSamplerState = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (GetTextureStrategy<ConcreteTexture>()._glTexture != null)
                    GetTextureStrategy<ConcreteTexture>()._glTexture.Dispose();
                GetTextureStrategy<ConcreteTexture>()._glTexture = null;
                GetTextureStrategy<ConcreteTexture>()._glLastSamplerState = null;
            }

            base.Dispose(disposing);
        }

    }

}

