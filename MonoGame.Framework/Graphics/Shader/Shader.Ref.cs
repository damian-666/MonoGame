// Copyright (C)2022 Nick Kastellanos

using System;
using System.IO;
using System.Diagnostics;


namespace Microsoft.Xna.Framework.Graphics
{
    internal partial class Shader
    {

        private static int PlatformProfile()
        {
            throw new PlatformNotSupportedException();
        }

        private void PlatformConstruct(ShaderStage stage, byte[] shaderBytecode)
        {
            throw new PlatformNotSupportedException();
        }

        private void PlatformGraphicsDeviceResetting()
        {
            throw new PlatformNotSupportedException();
        }

    }
}
