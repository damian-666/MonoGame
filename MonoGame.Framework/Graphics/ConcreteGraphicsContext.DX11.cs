﻿// Copyright (C)2022 Nick Kastellanos

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Framework.Utilities;
using SharpDX.Direct3D;
using SharpDX.Mathematics.Interop;
using D3D11 = SharpDX.Direct3D11;


namespace Microsoft.Xna.Platform.Graphics
{
    internal sealed class ConcreteGraphicsContext : GraphicsContextStrategy
    {
        private D3D11.DeviceContext _d3dContext;
        internal int _vertexBufferSlotsUsed;

        internal PrimitiveType _lastPrimitiveType = (PrimitiveType)(-1);

        // The active render targets.
        internal readonly D3D11.RenderTargetView[] _currentRenderTargets = new D3D11.RenderTargetView[8];

        // The active depth view.
        internal D3D11.DepthStencilView _currentDepthStencilView;

        internal readonly Dictionary<VertexDeclaration, DynamicVertexBuffer> _userVertexBuffers = new Dictionary<VertexDeclaration, DynamicVertexBuffer>();
        internal DynamicIndexBuffer _userIndexBuffer16;
        internal DynamicIndexBuffer _userIndexBuffer32;


        internal D3D11.DeviceContext D3dContext { get { return _d3dContext; } }

        public override Viewport Viewport
        {
            get { return base.Viewport; }
            set
            {
                base.Viewport = value;
                PlatformApplyViewport();
            }
        }

        internal ConcreteGraphicsContext(GraphicsDevice device, D3D11.DeviceContext d3dContext)
            : base(device)
        {
            _d3dContext = d3dContext;

        }

        public override void Clear(ClearOptions options, Vector4 color, float depth, int stencil)
        {
            // Clear options for depth/stencil buffer if not attached.
            if (_currentDepthStencilView != null)
            {
                if (_currentDepthStencilView.Description.Format != SharpDX.DXGI.Format.D24_UNorm_S8_UInt)
                    options &= ~ClearOptions.Stencil;
            }
            else
            {
                options &= ~ClearOptions.DepthBuffer;
                options &= ~ClearOptions.Stencil;
            }

            lock (D3dContext)
            {
                // Clear the diffuse render buffer.
                if ((options & ClearOptions.Target) == ClearOptions.Target)
                {
                    foreach (D3D11.RenderTargetView view in _currentRenderTargets)
                    {
                        if (view != null)
                            D3dContext.ClearRenderTargetView(view, new RawColor4(color.X, color.Y, color.Z, color.W));
                    }
                }

                // Clear the depth/stencil render buffer.
                D3D11.DepthStencilClearFlags flags = 0;
                if ((options & ClearOptions.DepthBuffer) == ClearOptions.DepthBuffer)
                    flags |= D3D11.DepthStencilClearFlags.Depth;
                if ((options & ClearOptions.Stencil) == ClearOptions.Stencil)
                    flags |= D3D11.DepthStencilClearFlags.Stencil;

                if (flags != 0)
                    D3dContext.ClearDepthStencilView(_currentDepthStencilView, flags, depth, (byte)stencil);
            }
        }

        internal void PlatformApplyState()
        {
            Debug.Assert(this.D3dContext != null, "The d3d context is null!");

            {
                PlatformApplyBlend();
            }

            if (_depthStencilStateDirty)
            {
                _actualDepthStencilState.PlatformApplyState(this);
                _depthStencilStateDirty = false;
            }

            if (_rasterizerStateDirty)
            {
                _actualRasterizerState.PlatformApplyState(this);
                _rasterizerStateDirty = false;
            }

            if (_scissorRectangleDirty)
            {
                PlatformApplyScissorRectangle();
                _scissorRectangleDirty = false;
            }
        }

        private void PlatformApplyBlend()
        {
            if (_blendStateDirty || _blendFactorDirty)
            {
                D3D11.BlendState blendState = _actualBlendState.GetDxState(this);
                var blendFactor = ConcreteGraphicsContext.ToDXColor(BlendFactor);
                D3dContext.OutputMerger.SetBlendState(blendState, blendFactor);

                _blendStateDirty = false;
                _blendFactorDirty = false;
            }
        }

        private void PlatformApplyScissorRectangle()
        {
            // NOTE: This code assumes CurrentD3DContext has been locked by the caller.

            D3dContext.Rasterizer.SetScissorRectangle(
                _scissorRectangle.X,
                _scissorRectangle.Y,
                _scissorRectangle.Right,
                _scissorRectangle.Bottom);
            _scissorRectangleDirty = false;
        }

        internal static SharpDX.Mathematics.Interop.RawColor4 ToDXColor(Color blendFactor)
        {
            return new SharpDX.Mathematics.Interop.RawColor4(
                    blendFactor.R / 255.0f,
                    blendFactor.G / 255.0f,
                    blendFactor.B / 255.0f,
                    blendFactor.A / 255.0f);
        }

        internal void PlatformApplyViewport()
        {
            lock (this.D3dContext)
            {
                if (this.D3dContext != null)
                {
                    var viewport = new RawViewportF
                    {
                        X = _viewport.X,
                        Y = _viewport.Y,
                        Width = _viewport.Width,
                        Height = _viewport.Height,
                        MinDepth = _viewport.MinDepth,
                        MaxDepth = _viewport.MaxDepth
                    };
                    this.D3dContext.Rasterizer.SetViewport(viewport);
                }
            }
        }

        internal void PlatformApplyIndexBuffer()
        {
            // NOTE: This code assumes CurrentD3DContext has been locked by the caller.

            if (_indexBufferDirty)
            {
                if (_indexBuffer != null)
                {
                    this.D3dContext.InputAssembler.SetIndexBuffer(
                        _indexBuffer.Buffer,
                        _indexBuffer.IndexElementSize == IndexElementSize.SixteenBits ?
                            SharpDX.DXGI.Format.R16_UInt : SharpDX.DXGI.Format.R32_UInt,
                        0);
                }
                _indexBufferDirty = false;
            }
        }

        internal void PlatformApplyVertexBuffers()
        {
            // NOTE: This code assumes CurrentD3DContext has been locked by the caller.

            if (_vertexBuffersDirty)
            {
                if (_vertexBuffers.Count > 0)
                {
                    for (int slot = 0; slot < _vertexBuffers.Count; slot++)
                    {
                        VertexBufferBinding vertexBufferBinding = _vertexBuffers.Get(slot);
                        VertexBuffer vertexBuffer = vertexBufferBinding.VertexBuffer;
                        VertexDeclaration vertexDeclaration = vertexBuffer.VertexDeclaration;
                        int vertexStride = vertexDeclaration.VertexStride;
                        int vertexOffsetInBytes = vertexBufferBinding.VertexOffset * vertexStride;
                        this.D3dContext.InputAssembler.SetVertexBuffers(
                            slot, new D3D11.VertexBufferBinding(vertexBuffer.Buffer, vertexStride, vertexOffsetInBytes));
                    }
                    _vertexBufferSlotsUsed = _vertexBuffers.Count;
                }
                else
                {
                    for (int slot = 0; slot < _vertexBufferSlotsUsed; slot++)
                        this.D3dContext.InputAssembler.SetVertexBuffers(slot, new D3D11.VertexBufferBinding());

                    _vertexBufferSlotsUsed = 0;
                }
            }
        }

        internal void PlatformApplyShaders()
        {
            // NOTE: This code assumes CurrentD3DContext has been locked by the caller.

            if (_vertexShaderDirty)
            {
                this.D3dContext.VertexShader.Set(_vertexShader.VertexShader);

                unchecked { this.Device.CurrentContext._graphicsMetrics._vertexShaderCount++; }
            }
            if (_vertexShaderDirty || _vertexBuffersDirty)
            {
                this.D3dContext.InputAssembler.InputLayout = _vertexShader.InputLayouts.GetOrCreate(_vertexBuffers);
                _vertexShaderDirty = false;
                _vertexBuffersDirty = false;
            }

            if (_pixelShaderDirty)
            {
                this.D3dContext.PixelShader.Set(_pixelShader.PixelShader);
                _pixelShaderDirty = false;

                unchecked { this.Device.CurrentContext._graphicsMetrics._pixelShaderCount++; }
            }
        }

        internal void PlatformApplyShaderBuffers()
        {
            _vertexConstantBuffers.Apply(this);
            _pixelConstantBuffers.Apply(this);

            this.VertexTextures.PlatformApply(this);
            this.VertexSamplerStates.PlatformApply(this);
            this.Textures.PlatformApply(this);
            this.SamplerStates.PlatformApply(this);
        }

        internal void PlatformApplyPrimitiveType(PrimitiveType primitiveType)
        {
            if (_lastPrimitiveType == primitiveType)
                return;

            this.D3dContext.InputAssembler.PrimitiveTopology = ConcreteGraphicsContext.ToPrimitiveTopology(primitiveType);
            _lastPrimitiveType = primitiveType;
        }

        internal static PrimitiveTopology ToPrimitiveTopology(PrimitiveType primitiveType)
        {
            switch (primitiveType)
            {
                case PrimitiveType.LineList:
                    return PrimitiveTopology.LineList;
                case PrimitiveType.LineStrip:
                    return PrimitiveTopology.LineStrip;
                case PrimitiveType.TriangleList:
                    return PrimitiveTopology.TriangleList;
                case PrimitiveType.TriangleStrip:
                    return PrimitiveTopology.TriangleStrip;
                case PrimitiveType.PointList:
                    return PrimitiveTopology.PointList;

                default:
                    throw new ArgumentException();
            }
        }

        internal int SetUserVertexBuffer<T>(T[] vertexData, int vertexOffset, int vertexCount, VertexDeclaration vertexDecl)
            where T : struct
        {
            DynamicVertexBuffer buffer;

            if (!_userVertexBuffers.TryGetValue(vertexDecl, out buffer) || buffer.VertexCount < vertexCount)
            {
                // Dispose the previous buffer if we have one.
                if (buffer != null)
                    buffer.Dispose();

                int requiredVertexCount = Math.Max(vertexCount, 4 * 256);
                requiredVertexCount = (requiredVertexCount + 255) & (~255); // grow in chunks of 256.
                buffer = new DynamicVertexBuffer(this.Device, vertexDecl, requiredVertexCount, BufferUsage.WriteOnly);
                _userVertexBuffers[vertexDecl] = buffer;
            }

            int startVertex = buffer.UserOffset;


            if ((vertexCount + buffer.UserOffset) < buffer.VertexCount)
            {
                buffer.UserOffset += vertexCount;
                buffer.SetData(startVertex * vertexDecl.VertexStride, vertexData, vertexOffset, vertexCount, vertexDecl.VertexStride, SetDataOptions.NoOverwrite);
            }
            else
            {
                buffer.UserOffset = vertexCount;
                buffer.SetData(vertexData, vertexOffset, vertexCount, SetDataOptions.Discard);
                startVertex = 0;
            }

            SetVertexBuffer(buffer);

            return startVertex;
        }

        internal int SetUserIndexBuffer<T>(T[] indexData, int indexOffset, int indexCount)
            where T : struct
        {
            DynamicIndexBuffer buffer;

            int indexSize = ReflectionHelpers.SizeOf<T>();
            IndexElementSize indexElementSize = indexSize == 2 ? IndexElementSize.SixteenBits : IndexElementSize.ThirtyTwoBits;

            int requiredIndexCount = Math.Max(indexCount, 6 * 512);
            requiredIndexCount = (requiredIndexCount + 511) & (~511); // grow in chunks of 512.
            if (indexElementSize == IndexElementSize.SixteenBits)
            {
                if (_userIndexBuffer16 == null || _userIndexBuffer16.IndexCount < requiredIndexCount)
                {
                    if (_userIndexBuffer16 != null)
                        _userIndexBuffer16.Dispose();

                    _userIndexBuffer16 = new DynamicIndexBuffer(this.Device, indexElementSize, requiredIndexCount, BufferUsage.WriteOnly);
                }

                buffer = _userIndexBuffer16;
            }
            else
            {
                if (_userIndexBuffer32 == null || _userIndexBuffer32.IndexCount < requiredIndexCount)
                {
                    if (_userIndexBuffer32 != null)
                        _userIndexBuffer32.Dispose();

                    _userIndexBuffer32 = new DynamicIndexBuffer(this.Device, indexElementSize, requiredIndexCount, BufferUsage.WriteOnly);
                }

                buffer = _userIndexBuffer32;
            }

            int startIndex = buffer.UserOffset;

            if ((indexCount + buffer.UserOffset) < buffer.IndexCount)
            {
                buffer.UserOffset += indexCount;
                buffer.SetData(startIndex * indexSize, indexData, indexOffset, indexCount, SetDataOptions.NoOverwrite);
            }
            else
            {
                startIndex = 0;
                buffer.UserOffset = indexCount;
                buffer.SetData(indexData, indexOffset, indexCount, SetDataOptions.Discard);
            }

            Indices = buffer;

            return startIndex;
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThrowIfDisposed();

                if (_d3dContext != null)
                    _d3dContext.Dispose();
                _d3dContext = null;
            }

            base.Dispose(disposing);
        }

        internal void PlatformResolveRenderTargets()
        {
            for (int i = 0; i < _currentRenderTargetCount; i++)
            {
                RenderTargetBinding renderTargetBinding = _currentRenderTargetBindings[i];

                // Resolve MSAA render targets
                RenderTarget2D renderTarget = renderTargetBinding.RenderTarget as RenderTarget2D;
                if (renderTarget != null && renderTarget.MultiSampleCount > 1)
                    renderTarget.ResolveSubresource();

                // Generate mipmaps.
                if (renderTargetBinding.RenderTarget.LevelCount > 1)
                {
                    lock (this.D3dContext)
                        this.D3dContext.GenerateMips(renderTargetBinding.RenderTarget.GetShaderResourceView());
                }
            }
        }

        internal void PlatformApplyDefaultRenderTarget()
        {
            // Set the default swap chain.
            Array.Clear(_currentRenderTargets, 0, _currentRenderTargets.Length);
            _currentRenderTargets[0] = this.Device._renderTargetView;
            _currentDepthStencilView = this.Device._depthStencilView;

            lock (this.D3dContext)
                this.D3dContext.OutputMerger.SetTargets(_currentDepthStencilView, _currentRenderTargets);
        }

        internal IRenderTarget PlatformApplyRenderTargets()
        {
            // Clear the current render targets.
            Array.Clear(_currentRenderTargets, 0, _currentRenderTargets.Length);
            _currentDepthStencilView = null;

            // Make sure none of the new targets are bound
            // to the device as a texture resource.
            lock (this.D3dContext)
            {
                VertexTextures.ClearTargets(this);
                Textures.ClearTargets(this);
            }

            for (int i = 0; i < _currentRenderTargetCount; i++)
            {
                var binding = _currentRenderTargetBindings[i];
                var targetDX = (IRenderTargetDX11)binding.RenderTarget;
                _currentRenderTargets[i] = targetDX.GetRenderTargetView(binding.ArraySlice);
            }

            // Use the depth from the first target.
            var renderTargetDX = (IRenderTargetDX11)_currentRenderTargetBindings[0].RenderTarget;
            _currentDepthStencilView = renderTargetDX.GetDepthStencilView(_currentRenderTargetBindings[0].ArraySlice);

            // Set the targets.
            lock (this.D3dContext)
                this.D3dContext.OutputMerger.SetTargets(_currentDepthStencilView, _currentRenderTargets);

            return (IRenderTarget)_currentRenderTargetBindings[0].RenderTarget;
        }

    }
}
