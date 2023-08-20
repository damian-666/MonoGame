﻿// Copyright (C)2023 Nick Kastellanos

using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DX = SharpDX;
using DXGI = SharpDX.DXGI;
using D3D11 = SharpDX.Direct3D11;
using MonoGame.Framework.Utilities;

#if WINDOWS_UAP
using System.Runtime.InteropServices;
using Windows.Graphics.Display;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using SharpDX.Mathematics.Interop;
#endif


namespace Microsoft.Xna.Platform.Graphics
{
    internal sealed class ConcreteGraphicsDevice : GraphicsDeviceStrategy
    {
        // Core Direct3D Objects
        internal D3D11.Device _d3dDevice;
        internal D3D11.RenderTargetView _renderTargetView;
        internal D3D11.DepthStencilView _depthStencilView;

#if WINDOWS
        internal DXGI.SwapChain _swapChain;
#endif

#if WINDOWS_UAP
        // The swap chain resources.
        internal DXGI.SwapChain1 _swapChain;
        internal SharpDX.Direct2D1.Bitmap1 _bitmapTarget;

        internal SwapChainPanel _swapChainPanel;

        // Declare Direct2D Objects
        internal SharpDX.Direct2D1.Factory1 _d2dFactory;
        internal SharpDX.Direct2D1.Device _d2dDevice;
        internal SharpDX.Direct2D1.DeviceContext _d2dContext;

        // Declare DirectWrite & Windows Imaging Component Objects
        internal SharpDX.DirectWrite.Factory _dwriteFactory;
        internal SharpDX.WIC.ImagingFactory2 _wicFactory;

        // Tearing (disabling V-Sync) support
        internal bool _isTearingSupported;

        internal float _dpi;
#endif

        internal D3D11.Device D3DDevice { get { return _d3dDevice; } }
   

        internal ConcreteGraphicsDevice(GraphicsAdapter adapter, GraphicsProfile graphicsProfile, bool preferHalfPixelOffset, PresentationParameters presentationParameters)
            : base(adapter, graphicsProfile, false, presentationParameters)
        {
        }


        public override void Reset(PresentationParameters presentationParameters)
        {
            PresentationParameters = presentationParameters;
            Reset();
        }

        public override void Reset()
        {
#if WINDOWS
            CorrectBackBufferSize();
#endif

#if WINDOWS
            if (PresentationParameters.DeviceWindowHandle == IntPtr.Zero)
                throw new ArgumentException("PresentationParameters.DeviceWindowHandle must not be null.");
#endif
#if WINDOWS_UAP
            if (PresentationParameters.DeviceWindowHandle == IntPtr.Zero && PresentationParameters.SwapChainPanel == null)
                throw new ArgumentException("PresentationParameters.DeviceWindowHandle or PresentationParameters.SwapChainPanel must not be null.");
#endif
        }

        public override void Present(Rectangle? sourceRectangle, Rectangle? destinationRectangle, IntPtr overrideWindowHandle)
        {
            throw new NotImplementedException();
        }

        public override void Present()
        {
            base.Present();

            try
            {
                lock (((ConcreteGraphicsContext)_mainContext.Strategy).D3dContext)
                {
                    int syncInterval = 0;
                    DXGI.PresentFlags presentFlags = DXGI.PresentFlags.None;

#if WINDOWS
                    // The first argument instructs DXGI to block n VSyncs before presenting.
                    syncInterval = GraphicsExtensions.ToDXSwapInterval(PresentationParameters.PresentationInterval);
#endif

#if WINDOWS_UAP
                    // The first argument instructs DXGI to block until VSync, putting the application
                    // to sleep until the next VSync. This ensures we don't waste any cycles rendering
                    // frames that will never be displayed to the screen.
                    if (_isTearingSupported && PresentationParameters.PresentationInterval == PresentInterval.Immediate)
                    {
                        syncInterval = 0;
                        presentFlags = DXGI.PresentFlags.AllowTearing;
                    }
                    else
                    {
                        syncInterval = 1;
                    }
#endif

                    _swapChain.Present(syncInterval, presentFlags);
                }
            }
            catch (SharpDX.SharpDXException ex)
            {
                // TODO: How should we deal with a device lost case here?

#if WINDOWS_UAP
                /*               
                // If the device was removed either by a disconnect or a driver upgrade, we 
                // must completely reinitialize the renderer.
                if (ex.ResultCode == DXGI.DXGIError.DeviceRemoved ||
                    ex.ResultCode == DXGI.DXGIError.DeviceReset)
                    this.Initialize();
                else
                    throw;
                */
#endif
            }
        }

        public override void GetBackBufferData<T>(Rectangle? rect, T[] data, int startIndex, int elementCount)
        {
            // TODO share code with Texture2D.GetData
            // first set up a staging texture
            const SurfaceFormat format = SurfaceFormat.Color;
            //You can't Map the BackBuffer surface, so we copy to another texture
            using (D3D11.Texture2D backBufferTexture = D3D11.Resource.FromSwapChain<D3D11.Texture2D>(_swapChain, 0))
            {
                D3D11.Texture2DDescription desc = backBufferTexture.Description;
                desc.SampleDescription = new DXGI.SampleDescription(1, 0);
                desc.BindFlags = D3D11.BindFlags.None;
                desc.CpuAccessFlags = D3D11.CpuAccessFlags.Read;
                desc.Usage = D3D11.ResourceUsage.Staging;
                desc.OptionFlags = D3D11.ResourceOptionFlags.None;

                using (D3D11.Texture2D stagingTex = new D3D11.Texture2D(this.D3DDevice, desc))
                {
                    lock (((ConcreteGraphicsContext)_mainContext.Strategy).D3dContext)
                    {
                        // Copy the data from the GPU to the staging texture.
                        // if MSAA is enabled we need to first copy to a resource without MSAA
                        if (backBufferTexture.Description.SampleDescription.Count > 1)
                        {
                            desc.Usage = D3D11.ResourceUsage.Default;
                            desc.CpuAccessFlags = D3D11.CpuAccessFlags.None;
                            using (D3D11.Texture2D noMsTex = new D3D11.Texture2D(this.D3DDevice, desc))
                            {
                                ((ConcreteGraphicsContext)_mainContext.Strategy).D3dContext.ResolveSubresource(backBufferTexture, 0, noMsTex, 0, desc.Format);
                                if (rect.HasValue)
                                {
                                    Rectangle r = rect.Value;
                                    ((ConcreteGraphicsContext)_mainContext.Strategy).D3dContext.CopySubresourceRegion(noMsTex, 0,
                                        new D3D11.ResourceRegion(r.Left, r.Top, 0, r.Right, r.Bottom, 1), stagingTex,
                                        0);
                                }
                                else
                                    ((ConcreteGraphicsContext)_mainContext.Strategy).D3dContext.CopyResource(noMsTex, stagingTex);
                            }
                        }
                        else
                        {
                            if (rect.HasValue)
                            {
                                Rectangle r = rect.Value;
                                ((ConcreteGraphicsContext)_mainContext.Strategy).D3dContext.CopySubresourceRegion(backBufferTexture, 0,
                                    new D3D11.ResourceRegion(r.Left, r.Top, 0, r.Right, r.Bottom, 1), stagingTex, 0);
                            }
                            else
                                ((ConcreteGraphicsContext)_mainContext.Strategy).D3dContext.CopyResource(backBufferTexture, stagingTex);
                        }

                        // Copy the data to the array.
                        DX.DataStream stream = null;
                        try
                        {
                            DX.DataBox databox = ((ConcreteGraphicsContext)_mainContext.Strategy).D3dContext.MapSubresource(stagingTex, 0, D3D11.MapMode.Read, D3D11.MapFlags.None, out stream);

                            int elementsInRow, rows;
                            if (rect.HasValue)
                            {
                                elementsInRow = rect.Value.Width;
                                rows = rect.Value.Height;
                            }
                            else
                            {
                                elementsInRow = stagingTex.Description.Width;
                                rows = stagingTex.Description.Height;
                            }
                            int elementSize = format.GetSize();
                            int rowSize = elementSize * elementsInRow;
                            if (rowSize == databox.RowPitch)
                                stream.ReadRange(data, startIndex, elementCount);
                            else
                            {
                                // Some drivers may add pitch to rows.
                                // We need to copy each row separately and skip trailing zeroes.
                                stream.Seek(0, SeekOrigin.Begin);

                                int elementSizeInByte = ReflectionHelpers.SizeOf<T>();
                                for (int row = 0; row < rows; row++)
                                {
                                    int i;
                                    for (i = row * rowSize / elementSizeInByte; i < (row + 1) * rowSize / elementSizeInByte; i++)
                                        data[i + startIndex] = stream.Read<T>();

                                    if (i >= elementCount)
                                        break;

                                    stream.Seek(databox.RowPitch - rowSize, SeekOrigin.Current);
                                }
                            }
                        }
                        finally
                        {
                            SharpDX.Utilities.Dispose( ref stream);
                        }
                    }
                }
            }

        }



        /// <summary>
        /// Creates resources not tied the active graphics device.
        /// </summary>
        internal void CreateDeviceIndependentResources()
        {
#if WINDOWS_UAP

            SharpDX.Direct2D1.DebugLevel debugLevel = SharpDX.Direct2D1.DebugLevel.None;

#if DEBUG && WINDOWS_UAP
            debugLevel |= SharpDX.Direct2D1.DebugLevel.Information;
#endif

            // Dispose previous references.
            if (_d2dFactory != null)
                _d2dFactory.Dispose();
            if (_dwriteFactory != null)
                _dwriteFactory.Dispose();
            if (_wicFactory != null)
                _wicFactory.Dispose();

            // Allocate new references
            _d2dFactory = new SharpDX.Direct2D1.Factory1(SharpDX.Direct2D1.FactoryType.SingleThreaded, debugLevel);
            _dwriteFactory = new SharpDX.DirectWrite.Factory(SharpDX.DirectWrite.FactoryType.Shared);
            _wicFactory = new SharpDX.WIC.ImagingFactory2();
#endif
        }


#if WINDOWS
        internal void CorrectBackBufferSize()
        {
            // Window size can be modified when we're going full screen, we need to take that into account
            // so the back buffer has the right size.
            if (PresentationParameters.IsFullScreen)
            {
                int newWidth, newHeight;
                if (PresentationParameters.HardwareModeSwitch)
                    GetModeSwitchedSize(out newWidth, out newHeight);
                else
                    GetDisplayResolution(out newWidth, out newHeight);

                PresentationParameters.BackBufferWidth = newWidth;
                PresentationParameters.BackBufferHeight = newHeight;
            }
        }

        internal void GetModeSwitchedSize(out int width, out int height)
        {
            DXGI.Output output = null;
            if (_swapChain == null)
            {
                // get the primary output
                using (DXGI.Factory1 factory = new DXGI.Factory1())
                using (DXGI.Adapter1 adapter = factory.GetAdapter1(0))
                    output = adapter.Outputs[0];
            }
            else
            {
                try
                {
                    output = _swapChain.ContainingOutput;
                }
                catch (DX.SharpDXException) { /* ContainingOutput fails on a headless device */ }
            }

            DXGI.Format format = GraphicsExtensions.ToDXFormat(PresentationParameters.BackBufferFormat);
            DXGI.ModeDescription target = new DXGI.ModeDescription
            {
                Format = format,
                Scaling = DXGI.DisplayModeScaling.Unspecified,
                Width = PresentationParameters.BackBufferWidth,
                Height = PresentationParameters.BackBufferHeight,
            };

            if (output == null)
            {
                width = PresentationParameters.BackBufferWidth;
                height = PresentationParameters.BackBufferHeight;
            }
            else
            {
                DXGI.ModeDescription closest;
                output.GetClosestMatchingMode(this.D3DDevice, target, out closest);
                width = closest.Width;
                height = closest.Height;
                output.Dispose();
            }
        }

        internal void GetDisplayResolution(out int width, out int height)
        {
            width = Adapter.CurrentDisplayMode.Width;
            height = Adapter.CurrentDisplayMode.Height;
        }

#endif

#if WINDOWS
        internal void SetHardwareFullscreen()
        {
            _swapChain.SetFullscreenState(PresentationParameters.IsFullScreen && PresentationParameters.HardwareModeSwitch, null);
        }

        internal void ClearHardwareFullscreen()
        {
            _swapChain.SetFullscreenState(false, null);
        }
#endif


#if WINDOWS
        internal void ResizeTargets()
        {
            DXGI.Format format = GraphicsExtensions.ToDXFormat(PresentationParameters.BackBufferFormat);
            DXGI.ModeDescription descr = new DXGI.ModeDescription
            {
                Format = format,
                Scaling = DXGI.DisplayModeScaling.Unspecified,
                Width = PresentationParameters.BackBufferWidth,
                Height = PresentationParameters.BackBufferHeight,
            };

            _swapChain.ResizeTarget(ref descr);
        }
#endif

#if WINDOWS
        internal void RefreshAdapter()
        {
            if (_swapChain == null)
                return;

            DXGI.Output output = null;
            try
            {
                output = _swapChain.ContainingOutput;
            }
            catch (DX.SharpDXException) { /* ContainingOutput fails on a headless device */ }

            if (output != null)
            {
                foreach (GraphicsAdapter adapter in GraphicsAdapter.Adapters)
                {
                    if (adapter.DeviceName == output.Description.DeviceName)
                    {
                        Adapter = adapter;
                        break;
                    }
                }

                output.Dispose();
            }
        }
#endif

#if WINDOWS_UAP
        internal void SetMultiSamplingToMaximum(PresentationParameters presentationParameters, out int quality)
        {
            quality = (int)D3D11.StandardMultisampleQualityLevels.StandardMultisamplePattern;
        }
#endif


        internal void CreateSizeDependentResources()
        {
            // Clamp MultiSampleCount
            PresentationParameters.MultiSampleCount =
                GetClampedMultisampleCount(PresentationParameters.MultiSampleCount);

            ((ConcreteGraphicsContext)_mainContext.Strategy).D3dContext.OutputMerger.SetTargets((D3D11.DepthStencilView)null,
                                                                                                (D3D11.RenderTargetView)null);

            if (_renderTargetView != null)
            {
                _renderTargetView.Dispose();
                _renderTargetView = null;
            }
            if (_depthStencilView != null)
            {
                _depthStencilView.Dispose();
                _depthStencilView = null;
            }

#if WINDOWS_UAP
            if (_bitmapTarget != null)
            {
                _bitmapTarget.Dispose();
                _bitmapTarget = null;
            }
            _d2dContext.Target = null;
#endif

            // Clear the current render targets.
            ((ConcreteGraphicsContext)_mainContext.Strategy)._currentDepthStencilView = null;
            Array.Clear(((ConcreteGraphicsContext)_mainContext.Strategy)._currentRenderTargets, 0, ((ConcreteGraphicsContext)_mainContext.Strategy)._currentRenderTargets.Length);
            Array.Clear(_mainContext.Strategy._currentRenderTargetBindings, 0, _mainContext.Strategy._currentRenderTargetBindings.Length);
            _mainContext.Strategy._currentRenderTargetCount = 0;

            // Make sure all pending rendering commands are flushed.
            ((ConcreteGraphicsContext)_mainContext.Strategy).D3dContext.Flush();

#if WINDOWS
            // We need presentation parameters to continue here.
            if (PresentationParameters == null
            ||  (PresentationParameters.DeviceWindowHandle == IntPtr.Zero)
               )
            {
                if (_swapChain != null)
                {
                    _swapChain.Dispose();
                    _swapChain = null;
                }

                return;
            }

            DXGI.Format format = GraphicsExtensions.ToDXFormat(PresentationParameters.BackBufferFormat);
            DXGI.SampleDescription multisampleDesc = GetSupportedSampleDescription(
                format,
                PresentationParameters.MultiSampleCount);

            DXGI.SwapChainFlags swapChainFlags = DXGI.SwapChainFlags.None;

            swapChainFlags = DXGI.SwapChainFlags.AllowModeSwitch;

            // If the swap chain already exists... update it.
            if (_swapChain != null
                // check if multisampling hasn't changed
                && _swapChain.Description.SampleDescription.Count == multisampleDesc.Count
                && _swapChain.Description.SampleDescription.Quality == multisampleDesc.Quality
               )
            {
                _swapChain.ResizeBuffers(2,
                                         PresentationParameters.BackBufferWidth,
                                         PresentationParameters.BackBufferHeight,
                                         format,
                                         swapChainFlags);
            }

            // Otherwise, create a new swap chain.
            else
            {
                bool wasFullScreen = false;
                // Dispose of old swap chain if exists
                if (_swapChain != null)
                {
                    wasFullScreen = _swapChain.IsFullScreen;
                    // Before releasing a swap chain, first switch to windowed mode
                    _swapChain.SetFullscreenState(false, null);
                    _swapChain.Dispose();
                }

                // SwapChain description
                DXGI.SwapChainDescription desc = new DXGI.SwapChainDescription()
                {
                    ModeDescription =
                    {
                        Format = format,
                        Scaling = DXGI.DisplayModeScaling.Unspecified,
                        Width = PresentationParameters.BackBufferWidth,
                        Height = PresentationParameters.BackBufferHeight,
                    },

                    OutputHandle = PresentationParameters.DeviceWindowHandle,
                    IsWindowed = true,

                    SampleDescription = multisampleDesc,
                    Usage = DXGI.Usage.RenderTargetOutput,
                    BufferCount = 2,
                    SwapEffect = GraphicsExtensions.ToDXSwapEffect(PresentationParameters.PresentationInterval),
                    Flags = swapChainFlags
                };

                // Once the desired swap chain description is configured, it must be created on the same adapter as our D3D Device

                // First, retrieve the underlying DXGI Device from the D3D Device.
                // Creates the swap chain 
                using (DXGI.Device1 dxgiDevice = this.D3DDevice.QueryInterface<DXGI.Device1>())
                using (DXGI.Adapter dxgiAdapter = dxgiDevice.Adapter)
                using (DXGI.Factory1 dxgiFactory = dxgiAdapter.GetParent<DXGI.Factory1>())
                {
                    _swapChain = new DXGI.SwapChain(dxgiFactory, dxgiDevice, desc);
                    RefreshAdapter();
                    dxgiFactory.MakeWindowAssociation(PresentationParameters.DeviceWindowHandle, DXGI.WindowAssociationFlags.IgnoreAll);
                    // To reduce latency, ensure that DXGI does not queue more than one frame at a time.
                    // Docs: https://msdn.microsoft.com/en-us/library/windows/desktop/ff471334(v=vs.85).aspx
                    dxgiDevice.MaximumFrameLatency = 1;
                }
                // Preserve full screen state, after swap chain is re-created 
                if (PresentationParameters.HardwareModeSwitch
                    && wasFullScreen)
                    SetHardwareFullscreen();
            }
#endif

#if WINDOWS_UAP
            // We need presentation parameters to continue here.
            if (PresentationParameters == null ||
                   (PresentationParameters.DeviceWindowHandle == IntPtr.Zero && PresentationParameters.SwapChainPanel == null)
               )
            {
                if (_swapChain != null)
                {
                    _swapChain.Dispose();
                    _swapChain = null;
                }

                return;
            }

            // Did we change swap panels?
            if (PresentationParameters.SwapChainPanel != _swapChainPanel)
            {
                _swapChainPanel = null;

                if (_swapChain != null)
                {
                    _swapChain.Dispose();
                    _swapChain = null;
                }
            }

            DXGI.Format format = GraphicsExtensions.ToDXFormat(PresentationParameters.BackBufferFormat);
            DXGI.SampleDescription multisampleDesc = GetSupportedSampleDescription(
                format,
                PresentationParameters.MultiSampleCount);

            DXGI.SwapChainFlags swapChainFlags = DXGI.SwapChainFlags.None;

            _isTearingSupported = IsTearingSupported();
            if (_isTearingSupported)
            {
                swapChainFlags = DXGI.SwapChainFlags.AllowTearing;
            }

            // If the swap chain already exists... update it.
            if (_swapChain != null
               )
            {
                _swapChain.ResizeBuffers(2,
                                         PresentationParameters.BackBufferWidth,
                                         PresentationParameters.BackBufferHeight,
                                         format,
                                         swapChainFlags);
            }

            // Otherwise, create a new swap chain.
            else
            {
                // SwapChain description
                DXGI.SwapChainDescription1 desc = new DXGI.SwapChainDescription1()
                {
                    // Automatic sizing
                    Width = PresentationParameters.BackBufferWidth,
                    Height = PresentationParameters.BackBufferHeight,
                    Format = format,
                    Stereo = false,
                    // By default we scale the backbuffer to the window 
                    // rectangle to function more like a WP7 game.
                    Scaling = DXGI.Scaling.Stretch,

                    SampleDescription = multisampleDesc,
                    Usage = DXGI.Usage.RenderTargetOutput,
                    BufferCount = 2,
                    SwapEffect = GraphicsExtensions.ToDXSwapEffect(PresentationParameters.PresentationInterval),
                    Flags = swapChainFlags
                };

                // Once the desired swap chain description is configured, it must be created on the same adapter as our D3D Device

                // First, retrieve the underlying DXGI Device from the D3D Device.
                // Creates the swap chain 
                using (DXGI.Device2 dxgiDevice2 = this.D3DDevice.QueryInterface<DXGI.Device2>())
                using (DXGI.Adapter dxgiAdapter = dxgiDevice2.Adapter)
                using (DXGI.Factory2 dxgiFactory2 = dxgiAdapter.GetParent<DXGI.Factory2>())
                {
                    if (PresentationParameters.DeviceWindowHandle != IntPtr.Zero)
                    {
                        // Creates a SwapChain from a CoreWindow pointer.
                        CoreWindow coreWindow = Marshal.GetObjectForIUnknown(PresentationParameters.DeviceWindowHandle) as CoreWindow;
                        using (DX.ComObject comWindow = new DX.ComObject(coreWindow))
                            _swapChain = new DXGI.SwapChain1(dxgiFactory2, dxgiDevice2, comWindow, ref desc);
                    }
                    else
                    {
                        _swapChainPanel = PresentationParameters.SwapChainPanel;
                        using (DXGI.ISwapChainPanelNative nativePanel = DX.ComObject.As<DXGI.ISwapChainPanelNative>(PresentationParameters.SwapChainPanel))
                        {
                            _swapChain = new DXGI.SwapChain1(dxgiFactory2, dxgiDevice2, ref desc, null);
                            nativePanel.SwapChain = _swapChain;

                            // update swapChain2.MatrixTransform on SizeChanged of SwapChainPanel
                            // sometimes window.SizeChanged and SwapChainPanel.SizeChanged are not synced
                            PresentationParameters.SwapChainPanel.SizeChanged += (sender, e) =>
                            {
                                try
                                {
                                    using (DXGI.SwapChain2 swapChain2 = _swapChain.QueryInterface<DXGI.SwapChain2>())
                                    {
                                        RawMatrix3x2 inverseScale = new RawMatrix3x2();
                                        inverseScale.M11 = (float)PresentationParameters.SwapChainPanel.ActualWidth / PresentationParameters.BackBufferWidth;
                                        inverseScale.M22 = (float)PresentationParameters.SwapChainPanel.ActualHeight / PresentationParameters.BackBufferHeight;
                                        swapChain2.MatrixTransform = inverseScale;
                                    };
                                }
                                catch (Exception) { }
                            };
                        }
                    }

                    // Ensure that DXGI does not queue more than one frame at a time. This both reduces 
                    // latency and ensures that the application will only render after each VSync, minimizing 
                    // power consumption.
                    dxgiDevice2.MaximumFrameLatency = 1;
                }
            }

            _swapChain.Rotation = DXGI.DisplayModeRotation.Identity;

            // Counter act the composition scale of the render target as 
            // we already handle this in the platform window code. 
            if (PresentationParameters.SwapChainPanel != null)
            {
                Windows.Foundation.IAsyncAction asyncResult = PresentationParameters.SwapChainPanel.Dispatcher.RunIdleAsync((e) =>
                {
                    RawMatrix3x2 inverseScale = new RawMatrix3x2();
                    inverseScale.M11 = (float)PresentationParameters.SwapChainPanel.ActualWidth  / PresentationParameters.BackBufferWidth;
                    inverseScale.M22 = (float)PresentationParameters.SwapChainPanel.ActualHeight / PresentationParameters.BackBufferHeight;
                    using (DXGI.SwapChain2 swapChain2 = _swapChain.QueryInterface<DXGI.SwapChain2>())
                        swapChain2.MatrixTransform = inverseScale;
                });
            }
#endif

            // Obtain the backbuffer for this window which will be the final 3D rendertarget.
            Point targetSize;
            using (D3D11.Texture2D backBuffer = D3D11.Texture2D.FromSwapChain<D3D11.Texture2D>(_swapChain, 0))
            {
                // Create a view interface on the rendertarget to use on bind.
                _renderTargetView = new D3D11.RenderTargetView(this.D3DDevice, backBuffer);

                // Get the rendertarget dimensions for later.
                D3D11.Texture2DDescription backBufferDesc = backBuffer.Description;
                targetSize = new Point(backBufferDesc.Width, backBufferDesc.Height);
            }

            // Create the depth buffer if we need it.
            if (PresentationParameters.DepthStencilFormat != DepthFormat.None)
            {
                DXGI.Format depthFormat = GraphicsExtensions.ToDXFormat(PresentationParameters.DepthStencilFormat);

                // Allocate a 2-D surface as the depth/stencil buffer.
                using (D3D11.Texture2D depthBuffer = new D3D11.Texture2D(this.D3DDevice, new D3D11.Texture2DDescription()
                    {
                        Format = depthFormat,
                        ArraySize = 1,
                        MipLevels = 1,
                        Width = targetSize.X,
                        Height = targetSize.Y,
                        SampleDescription = multisampleDesc,
                        Usage = D3D11.ResourceUsage.Default,
                        BindFlags = D3D11.BindFlags.DepthStencil,
                    }))
                {
                    // Create a DepthStencil view on this surface to use on bind.
                    _depthStencilView = new D3D11.DepthStencilView(this.D3DDevice, depthBuffer);
                }

            }

            // Set the current viewport.
            _mainContext.Viewport = new Viewport
            {
                X = 0,
                Y = 0,
                Width = targetSize.X,
                Height = targetSize.Y,
                MinDepth = 0.0f,
                MaxDepth = 1.0f
            };

#if WINDOWS_UAP
            // Now we set up the Direct2D render target bitmap linked to the swapchain. 
            // Whenever we render to this bitmap, it will be directly rendered to the 
            // swapchain associated with the window.
            SharpDX.Direct2D1.BitmapProperties1 bitmapProperties = new SharpDX.Direct2D1.BitmapProperties1(
                new SharpDX.Direct2D1.PixelFormat(format, SharpDX.Direct2D1.AlphaMode.Premultiplied),
                _dpi, _dpi,
                SharpDX.Direct2D1.BitmapOptions.Target | SharpDX.Direct2D1.BitmapOptions.CannotDraw);

            // Direct2D needs the dxgi version of the backbuffer surface pointer.
            // Get a D2D surface from the DXGI back buffer to use as the D2D render target.
            using (DXGI.Surface dxgiBackBuffer = _swapChain.GetBackBuffer<DXGI.Surface>(0))
                _bitmapTarget = new SharpDX.Direct2D1.Bitmap1(_d2dContext, dxgiBackBuffer, bitmapProperties);

            // So now we can set the Direct2D render target.
            _d2dContext.Target = _bitmapTarget;

            // Set D2D text anti-alias mode to Grayscale to 
            // ensure proper rendering of text on intermediate surfaces.
            _d2dContext.TextAntialiasMode = SharpDX.Direct2D1.TextAntialiasMode.Grayscale;
#endif
        }



        /// <summary>
        /// Get highest multisample quality level for specified format and multisample count.
        /// Returns 0 if multisampling is not supported for input parameters.
        /// </summary>
        /// <param name="format">The texture format.</param>
        /// <param name="multiSampleCount">The number of samples during multisampling.</param>
        /// <returns>
        /// Higher than zero if multiSampleCount is supported. 
        /// Zero if multiSampleCount is not supported.
        /// </returns>
        private int GetMultiSamplingQuality(DXGI.Format format, int multiSampleCount)
        {
            // The valid range is between zero and one less than the level returned by CheckMultisampleQualityLevels
            // https://msdn.microsoft.com/en-us/library/windows/desktop/bb173072(v=vs.85).aspx
            int quality = this.D3DDevice.CheckMultisampleQualityLevels(format, multiSampleCount) - 1;
            // NOTE: should we always return highest quality?
            return Math.Max(quality, 0); // clamp minimum to 0
        }

        internal DXGI.SampleDescription GetSupportedSampleDescription(DXGI.Format format, int multiSampleCount)
        {
            DXGI.SampleDescription multisampleDesc = new DXGI.SampleDescription(1, 0);

            if (multiSampleCount > 1)
            {
                int quality = GetMultiSamplingQuality(format, multiSampleCount);

                multisampleDesc.Count = multiSampleCount;
                multisampleDesc.Quality = quality;
            }

            return multisampleDesc;
        }


#if WINDOWS_UAP

        internal void Trim()
        {
            using (DXGI.Device3 dxgiDevice3 = this.D3DDevice.QueryInterface<DXGI.Device3>())
                dxgiDevice3.Trim();
        }

        internal float Dpi
        {
            get { return _dpi; }
            set
            {
                if (_dpi == value)
                    return;

                _dpi = value;
                _d2dContext.DotsPerInch = new DX.Size2F(_dpi, _dpi);

                //if (OnDpiChanged != null)
                //    OnDpiChanged(this);
            }
        }

        internal bool IsTearingSupported()
        {
            RawBool allowTearing;
            using (DXGI.Factory2 dxgiFactory2 = new DXGI.Factory2())
            {
                unsafe
                {
                    DXGI.Factory5 factory5 = dxgiFactory2.QueryInterface<DXGI.Factory5>();
                    try
                    {
                        factory5.CheckFeatureSupport(DXGI.Feature.PresentAllowTearing, new IntPtr(&allowTearing), sizeof(RawBool));

                        return allowTearing;
                    }
                    catch (DX.SharpDXException ex)
                    {
                        // can't request feature
                    }
                }
            }

            return false;
        }

#endif


        internal override GraphicsContextStrategy CreateGraphicsContextStrategy(GraphicsDevice device)
        {
            // Get Direct3D 11.1 context
#if WINDOWS
            D3D11.DeviceContext d3dContext = _d3dDevice.ImmediateContext.QueryInterface<D3D11.DeviceContext>();
#endif
#if WINDOWS_UAP
            D3D11.DeviceContext1 d3dContext = _d3dDevice.ImmediateContext.QueryInterface<D3D11.DeviceContext1>();
#endif

            return new ConcreteGraphicsContext(device, d3dContext);
        }

        internal override TextureCollectionStrategy CreateTextureCollectionStrategy(GraphicsDevice device, GraphicsContext context, int capacity)
        {
            return new ConcreteTextureCollection(device, context, capacity);
        }

        internal override SamplerStateCollectionStrategy CreateSamplerStateCollectionStrategy(GraphicsDevice device, GraphicsContext context, int capacity)
        {
            return new ConcreteSamplerStateCollection(device, context, capacity);
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
