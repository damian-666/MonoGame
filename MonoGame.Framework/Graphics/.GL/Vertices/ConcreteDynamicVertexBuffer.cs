﻿// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

// Copyright (C)2023 Nick Kastellanos

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Framework.Utilities;
using MonoGame.OpenGL;


namespace Microsoft.Xna.Platform.Graphics
{
    public class ConcreteDynamicVertexBuffer : ConcreteVertexBuffer
    {

        internal ConcreteDynamicVertexBuffer(GraphicsContextStrategy contextStrategy, VertexDeclaration vertexDeclaration, int vertexCount, BufferUsage usage)
            : base(contextStrategy, vertexDeclaration, vertexCount, usage, isDynamic:true)
        {
            PlatformConstructDynamicVertexBuffer();
        }

        private void PlatformConstructDynamicVertexBuffer()
        {
            base.PlatformConstructVertexBuffer();
        }
        
    }

}
