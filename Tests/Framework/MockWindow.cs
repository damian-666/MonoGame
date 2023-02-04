﻿using System;
using Microsoft.Xna.Framework;

namespace MonoGame.Tests.Framework
{
    internal class MockWindow : GameWindow
    {
        public override bool AllowUserResizing { get; set; }

        public override Rectangle ClientBounds
        {
            get { throw new NotImplementedException(); }
        }

        // TODO: Make this common so that all platforms have it!
#if (WINDOWS && !WINDOWS_UAP) || DESKTOPGL
        public override Point Position { get; set; }
#endif

        public override DisplayOrientation CurrentOrientation
        {
            get { throw new NotImplementedException(); }
        }

        public override IntPtr Handle
        {
            get { throw new NotImplementedException(); }
        }

        public override string ScreenDeviceName
        {
            get { throw new NotImplementedException(); }
        }

        protected internal override void SetSupportedOrientations(DisplayOrientation orientations)
        {
            throw new NotImplementedException();
        }

        protected override void SetTitle(string title)
        {
            throw new NotImplementedException();
        }
    }
}
