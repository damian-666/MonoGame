﻿// Copyright (C)2023 Nick Kastellanos

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace Microsoft.Xna.Platform.Graphics
{
    public interface ITexture2DStrategy : ITextureStrategy
    {

        int Width { get; }
        int Height { get; }
        int ArraySize { get; }

        Rectangle Bounds { get; }
    }
}
