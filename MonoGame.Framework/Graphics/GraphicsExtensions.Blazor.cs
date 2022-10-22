﻿// Copyright (C)2022 Nick Kastellanos

using System;
using System.Diagnostics;
using nkast.Wasm.Canvas.WebGL;


namespace Microsoft.Xna.Framework.Graphics
{
    static partial class GraphicsExtensions
    {
        internal static IWebGLRenderingContext GL;

        public static WebGLDepthComparisonFunc ToGLComparisonFunction(CompareFunction compare)
        {
            switch (compare)
            {
                case CompareFunction.Always:
                    return WebGLDepthComparisonFunc.ALWAYS;
                case CompareFunction.Equal:
                    return WebGLDepthComparisonFunc.EQUAL;
                case CompareFunction.Greater:
                    return WebGLDepthComparisonFunc.GREATER;
                case CompareFunction.GreaterEqual:
                    return WebGLDepthComparisonFunc.GEQUAL;
                case CompareFunction.Less:
                    return WebGLDepthComparisonFunc.LESS;
                case CompareFunction.LessEqual:
                    return WebGLDepthComparisonFunc.LEQUAL;
                case CompareFunction.Never:
                    return WebGLDepthComparisonFunc.NEVER;
                case CompareFunction.NotEqual:
                    return WebGLDepthComparisonFunc.NOTEQUAL;

                default:
                    throw new ArgumentOutOfRangeException("compare");
            }
        }

        [Conditional("DEBUG")]
        internal static void CheckGLError()
        {
            WebGLErrorCode error = GL.GetError();
            if (error != WebGLErrorCode.NO_ERROR)
            {
                Console.WriteLine(error);
                throw new InvalidOperationException("GL.GetError() returned " + error.ToString());
            }
        }

    }
}
