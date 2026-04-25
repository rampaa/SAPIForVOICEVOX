using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Setting.View
{
    /// <summary>
    /// voicevoxPropertySlider.xaml の相互作用ロジック
    /// </summary>
    internal sealed partial class VoicevoxParameterSlider
    {
        public VoicevoxParameterSlider()
        {
            InitializeComponent();
        }

        private void TextBlock_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            TextBlock textBlock = (TextBlock)sender;
            textBlock.Foreground = textBlock.IsEnabled
                ? Brushes.Black
                : Brushes.Gray;
        }
    }
}
