using System;
using Microsoft.UI.Xaml;
using SaferCode.Pages;

namespace SaferCode
{
    /// <summary>
    /// ���� ���� �� ���������.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public static new MainWindow Current { get; private set; }

        public MainWindow()
        {
            this.InitializeComponent();
            Current = this;

            // ����� ��� ����� 
            ContentFrame.Navigate(typeof(MainPage));
        }
    }
}
