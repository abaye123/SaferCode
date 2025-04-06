using System;
using Microsoft.UI.Xaml;
using SaferCode.Pages;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using WinRT.Interop;

namespace SaferCode
{
    public sealed partial class MainWindow : Window
    {
        public static new MainWindow? Current { get; private set; }

        public MainWindow()
        {
            this.InitializeComponent();
            Current = this;

            CenterWindow();

            ContentFrame.Navigate(typeof(MainPage));
        }

        private void CenterWindow()
        {
            var windowHandle = WindowNative.GetWindowHandle(this);
            var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(windowHandle));
            var displayArea = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(windowHandle), DisplayAreaFallback.Primary);

            if (appWindow != null && displayArea != null)
            {
                var width = appWindow.Size.Width;
                var height = appWindow.Size.Height;

                var centerX = (displayArea.WorkArea.Width - width) / 2;
                var centerY = (displayArea.WorkArea.Height - height) / 2;

                appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
            }
        }
    }
}