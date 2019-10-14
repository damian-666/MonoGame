// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

	using System;
using Microsoft.Xna.Framework;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.ApplicationModel.Activation;
#if WP81
using Windows.Phone.UI.Input;
using Microsoft.Xna.Framework.Input;
#endif

namespace MonoGame.Framework
{
    /// <summary>
    /// Static class for initializing a Game object for a XAML application.
    /// </summary>
    /// <typeparam name="T">A class derived from Game.</typeparam>
    [CLSCompliant(false)]
    public static class XamlGame<T>
        where T : Game, new()
    {
        /// <summary>
        /// Creates your Game class initializing it to work within a XAML application window.
        /// </summary>
        /// <param name="launchParameters">The command line arguments from launch.</param>
        /// <param name="window">The core window object.</param>
        /// <param name="swapChainPanel">The XAML SwapChainBackgroundPanel to which we render the scene and recieve input events.</param>
        /// <returns></returns>
        [Obsolete("Use Create(string launchParameters, CoreWindow window, SwapChainPanel swapChainPanel) instead. In future versions this method can be removed.")]
        static public T Create(string launchParameters, CoreWindow window, SwapChainBackgroundPanel swapChainPanel)
        {
            return Create(launchParameters, window, new GenericSwapChainPanel(swapChainPanel));
        }

        /// <summary>
        /// Creates your Game class initializing it to work within a XAML application window.
        /// </summary>
        /// <param name="launchParameters">The command line arguments from launch.</param>
        /// <param name="window">The core window object.</param>
        /// <param name="swapChainPanel">The XAML SwapChainPanel to which we render the scene and receive input events.</param>
        /// <returns></returns>
        static public T Create(string launchParameters, CoreWindow window, SwapChainPanel swapChainPanel)
        {
            return Create(launchParameters, window, new GenericSwapChainPanel(swapChainPanel));
        }
        
        static private T Create(string launchParameters, CoreWindow window, GenericSwapChainPanel swapChainPanel)
        {
            if (launchParameters == null)
                throw new NullReferenceException("The launch parameters cannot be null!");
            if (window == null)
                throw new NullReferenceException("The window cannot be null!");
            if (swapChainPanel == null)
                throw new NullReferenceException("The swap chain panel cannot be null!");

            // Save any launch parameters to be parsed by the platform.
            MetroGamePlatform.LaunchParameters = launchParameters;

            // Setup the window class.
            MetroGameWindow.Instance.Initialize(window, swapChainPanel, MetroGamePlatform.TouchQueue);

            // Construct the game.
            var game = new T();

            // Set the swap chain panel on the graphics mananger.
            if (game.graphicsDeviceManager == null)
                throw new NullReferenceException("You must create the GraphicsDeviceManager in the Game constructor!");
            game.graphicsDeviceManager.GetStrategy<Microsoft.Xna.Platform.ConcreteGraphicsDeviceManager>().SwapChainPanel = swapChainPanel;

            // Start running the game.
            game.Run(GameRunBehavior.Asynchronous);

#if WP81
            HardwareButtons.BackPressed += HardwareButtons_BackPressed;
#endif

            // Return the created game object.
            return game;
        }

#if WP81
        static void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            if (!e.Handled)
            {
                GamePad.Back = true;
                e.Handled = true;
            }
        }
#endif
        
        /// <summary>
        /// Preserves the previous execution state in MetroGamePlatform and returns the constructed game object initialized with the given window.
        /// </summary>
        /// <param name="args">The command line arguments from launch.</param>
        /// <param name="window">The core window object.</param>
        /// <param name="swapChainPanel">The XAML SwapChainBackgroundPanel to which we render the scene and recieve input events.</param>
        /// <returns></returns>
        static public T Create(LaunchActivatedEventArgs args, CoreWindow window, SwapChainBackgroundPanel swapChainPanel)
        {
            MetroGamePlatform.PreviousExecutionState = args.PreviousExecutionState;

            return Create(args.Arguments, window, new GenericSwapChainPanel(swapChainPanel));
        }

        static public T Create(ProtocolActivatedEventArgs args, CoreWindow window, SwapChainBackgroundPanel swapChainPanel)
        {
            MetroGamePlatform.PreviousExecutionState = args.PreviousExecutionState;

            return Create(args.Uri.AbsoluteUri, window, new GenericSwapChainPanel(swapChainPanel));
        }
    }
}
