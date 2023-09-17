﻿// Copyright (C)2023 Nick Kastellanos

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace Microsoft.Xna.Platform.Graphics
{
    public sealed class ConcreteVertexShader : ConcreteShader
    {
        public override ShaderStage Stage { get { return ShaderStage.Vertex; } }

        internal ConcreteVertexShader(GraphicsContextStrategy contextStrategy, byte[] shaderBytecode, SamplerInfo[] samplers, int[] cBuffers, VertexAttribute[] attributes, ShaderProfileType profile)
            : base(contextStrategy, shaderBytecode, samplers, cBuffers, attributes, profile)
        {
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            }

            base.Dispose(disposing);
        }
    }
}
