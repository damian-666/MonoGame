// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;

namespace Microsoft.Xna.Framework
{
    partial class GamePlatform
    {
        internal static GamePlatform PlatformCreate(Game game)
        {
#if BLAZOR
            return new MonoGame.Framework.BlazorGamePlatform(game);
#else
            return new WebGamePlatform(game);
#endif
        }
    }
}
