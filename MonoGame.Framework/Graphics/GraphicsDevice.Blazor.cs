// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

// Copyright (C)2023 Nick Kastellanos

using System;


namespace Microsoft.Xna.Framework.Graphics
{
    public partial class GraphicsDevice
    {

        private void PlatformDispose()
        {
        }

        internal void OnPresentationChanged()
        {
            _strategy._mainContext.ApplyRenderTargets(null);
        }

    }
}
