﻿// Copyright (C)2023 Nick Kastellanos

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace Microsoft.Xna.Platform.Graphics
{
    public class GraphicsResourceStrategy : IGraphicsResourceStrategy
    {
        private GraphicsDeviceStrategy _deviceStrategy;

        public GraphicsDevice GraphicsDevice
        { 
            get 
            {
                if (_deviceStrategy == null)
                    return null;
                return _deviceStrategy.Device;
            }
        }

        public event EventHandler<EventArgs> Disposing;

        public event EventHandler<EventArgs> ContextLost;
        public event EventHandler<EventArgs> DeviceDisposing;

        internal GraphicsResourceStrategy()
        {
        }

        public GraphicsResourceStrategy(GraphicsContextStrategy contextStrategy)
        {
            BindGraphicsDevice(contextStrategy.Context.DeviceStrategy);
        }

        public GraphicsResourceStrategy(GraphicsResourceStrategy source)
        {
            BindGraphicsDevice(source._deviceStrategy);
        }

        internal void BindGraphicsDevice(GraphicsDeviceStrategy deviceStrategy)
        {
            _deviceStrategy = deviceStrategy;

            _deviceStrategy.ContextLost += GraphicsDeviceStrategy_ContextLost;
            _deviceStrategy.Disposing += GraphicsDeviceStrategy_Disposing;
        }

        internal void UnbindGraphicsDevice()
        {
            if (_deviceStrategy != null)
            {
                _deviceStrategy.ContextLost -= GraphicsDeviceStrategy_ContextLost;
                _deviceStrategy.Disposing -= GraphicsDeviceStrategy_Disposing;
                _deviceStrategy = null;
            }
        }

        private void GraphicsDeviceStrategy_ContextLost(object sender, EventArgs e)
        {
            OnContextLost(e);
            PlatformGraphicsContextLost();
        }

        private void GraphicsDeviceStrategy_Disposing(object sender, EventArgs e)
        {
            OnDeviceDisposing(e);
        }

        private void OnContextLost(EventArgs e)
        {
            var handler = ContextLost;
            if (handler != null)
                handler(this, e);
        }

        private void OnDeviceDisposing(EventArgs e)
        {
            var handler = DeviceDisposing;
            if (handler != null)
                handler(this, e);
        }

        internal virtual void PlatformGraphicsContextLost()
        {

        }


        #region IDisposable Members

        ~GraphicsResourceStrategy()
        {
            OnDisposing(EventArgs.Empty);
            Dispose(false);
        }

        public void Dispose()
        {
            OnDisposing(EventArgs.Empty);
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal void OnDisposing(EventArgs e)
        {
            var handler = Disposing;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnbindGraphicsDevice();
                _deviceStrategy = null;
            }

        }

        #endregion IDisposable Members

    }
}
