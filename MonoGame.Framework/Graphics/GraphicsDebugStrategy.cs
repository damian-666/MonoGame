﻿// Copyright (C)2023 Nick Kastellanos

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace Microsoft.Xna.Platform.Graphics
{
    public abstract class GraphicsDebugStrategy
    {
        internal readonly GraphicsContext _context;

        internal GraphicsDebugStrategy(GraphicsContext context)
        {
            _context = context;

        }

        public abstract bool TryDequeueMessage(out GraphicsDebugMessage message);
    }
}
