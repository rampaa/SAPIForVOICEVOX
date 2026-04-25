using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace Setting.View
{
    /// <summary>
    /// VersionInfoWindow.xaml の相互作用ロジック
    /// </summary>
    internal sealed partial class VersionInfoWindow
    {
        public VersionInfoWindow()
        {
            InitializeComponent();

            System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
            System.Version ver = asm.GetName().Version;
            VersionString.Text = $"Version {ver}";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.ToString());
        }
    }
}
