using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace xmpp_test
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private void Textbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ConnectButton.IsEnabled = false;
            if (UsernameTextbox.Text.Length != 0 && ServerUrlTextbox.Text.Length != 0 && PortTextbox.Text.Length != 0 && PasswordTextbox.Password.Length != 0)
                ConnectButton.IsEnabled = true;
        }

        private void Textbox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ConnectButton.IsEnabled = false;
            if (UsernameTextbox.Text.Length != 0 && ServerUrlTextbox.Text.Length != 0 && PortTextbox.Text.Length != 0 && PasswordTextbox.Password.Length != 0)
                ConnectButton.IsEnabled = true;
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            ConnectButton.IsEnabled = false;
            Loading.IsActive = true;
            try
            {
                Xmpp.Connection conn = await Xmpp.Connection.Open(ServerUrlTextbox.Text, PortTextbox.Text, UsernameTextbox.Text, PasswordTextbox.Password);
                this.Frame.Navigate(typeof(ChatPage), conn);
            }
            finally
            {
                Loading.IsActive = false;
            }
        }
    }
}
