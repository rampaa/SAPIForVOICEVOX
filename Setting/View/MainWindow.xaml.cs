using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using Setting.Model;
using SFVvCommon;

namespace Setting.View
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal sealed partial class MainWindow
    {
        #region 最大化ボタン無効化

        /// <summary>
        /// ウィンドウに関するデータを取得
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="nIndex"></param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        /// <summary>
        /// ウィンドウの属性を変更
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="nIndex"></param>
        /// <param name="dwNewLong"></param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        /// <summary>
        /// ウィンドウスタイル
        /// </summary>
        private const int GWL_STYLE = -16;

        /// <summary>
        /// 最大化ボタン
        /// </summary>
        private const int WS_MAXIMIZEBOX = 0x0001_0000; // C#7より前の場合は 0x00010000

        /// <summary>
        /// 初期化時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper((Window)sender).Handle;
            int value = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, value & ~WS_MAXIMIZEBOX);
        }

        #endregion

        #region プロパティ

        /// <summary>
        /// メインのビューモデル
        /// </summary>
        private ViewModel.ViewModel MainViewModel { get; }

        #endregion

        public MainWindow()
        {
            InitializeComponent();

#if x64
            string bitStr = "64bit版";
#else
            const string bitStr = "32bit版";
#endif
            Title += bitStr;

            MainViewModel = new ViewModel.ViewModel(this);
            DataContext = MainViewModel;
            OkButton.Click += MainViewModel.OkButton_Click;
            ApplyButton.Click += MainViewModel.ApplyButton_Click;
            ResetButton.Click += MainViewModel.ResetButton_Click;
            VersionInfoButton.Click += MainViewModel.VersionInfoButton_Click;

            ApplyButton.IsEnabled = false;

            AddTabControl();
        }

        /// <summary>
        /// 各キャラクターやスタイルのタブを追加します。
        /// </summary>
        private void AddTabControl()
        {
            List<VoicevoxStyle> styles = new List<VoicevoxStyle>();

            using (RegistryKey regTokensKey = Registry.LocalMachine.OpenSubKey(Common.TokensRegKey))
            {
                Debug.Assert(regTokensKey != null);
                string[] tokenNames = regTokensKey.GetSubKeyNames();
                foreach (string tokenName in tokenNames)
                {
                    using (RegistryKey tokenKey = regTokensKey.OpenSubKey(tokenName))
                    {
                        Debug.Assert(tokenKey != null);
                        string clsid = (string)tokenKey.GetValue(Common.RegClsid);
                        if (clsid != Common.CLSID.ToString(Common.RegClsidFormatString))
                        {
                            continue;
                        }

                        string name = (string)tokenKey.GetValue(Common.RegName);
                        if (name == null)
                        {
                            AddTabDefault();
                            return;
                        }

                        string styleName = (string)tokenKey.GetValue(Common.RegStyleName);
                        int id = (int)tokenKey.GetValue(Common.RegSpeakerNumber, 0);
                        int port = (int)tokenKey.GetValue(Common.RegPort, 50021);
                        styles.Add(new VoicevoxStyle("VOICEVOX", name, styleName, id, port));
                    }
                }
            }

            styles = Common.SortStyle(styles).OfType<VoicevoxStyle>().ToList();

            foreach (VoicevoxStyle style in styles)
            {
                IEnumerable<TabItem> tabItems = MainTab.Items.OfType<TabItem>().Where(x => x.Header.ToString() == style.Name);
                //スタイル名のタブ
                TabControl tabControl;

                TabItem firstTabItem = tabItems.FirstOrDefault();
                if (firstTabItem == null)
                {
                    Binding binding = new Binding("IsChecked")
                    {
                        ElementName = nameof(ParCharacterRadioButton), Converter = new BooleanToVisibilityConverter()
                    };
                    TabItem tabItem = new TabItem { Header = style.Name };
                    tabItem.SetBinding(VisibilityProperty, binding);

                    MainTab.Items.Add(tabItem);

                    tabControl = new TabControl();
                    tabItem.Content = tabControl;
                }
                else
                {
                    tabControl = (TabControl)firstTabItem.Content;
                }

                //int index = Array.FindIndex(MainViewModel.SpeakerParameter, x => x.ID == style.ID && x.Port == style.Port);
                int index = MainViewModel.SpeakerParameter.FindIndex(x => x.ID == style.ID && x.Port == style.Port);
                if (index < 0)
                {
                    SynthesisParameter parameter = new SynthesisParameter { ID = style.ID, Port = style.Port };
                    parameter.PropertyChanged += MainViewModel.ViewModel_PropertyChanged;
                    MainViewModel.SpeakerParameter.Add(parameter);
                    index = MainViewModel.SpeakerParameter.Count - 1;
                }

                VoicevoxParameterSlider parameterSlider = new VoicevoxParameterSlider();
                parameterSlider.SetBinding(DataContextProperty, nameof(ViewModel.ViewModel.SpeakerParameter) + $"[{index}]");

                TabItem styleTabItem = new TabItem { Header = style.StyleName, Content = parameterSlider };

                tabControl.Items.Add(styleTabItem);
            }
        }

        private void AddTabDefault()
        {
            const int defaultPort = 50021;
            int id = 0;
            int index = MainViewModel.SpeakerParameter.FindIndex(x => x.ID == id && x.Port == defaultPort);
            if (index < 0)
            {
                SynthesisParameter parameter = new SynthesisParameter { ID = id };
                parameter.PropertyChanged += MainViewModel.ViewModel_PropertyChanged;
                MainViewModel.SpeakerParameter.Add(parameter);
                index = MainViewModel.SpeakerParameter.Count - 1;
            }

            VoicevoxParameterSlider parameterSlider = new VoicevoxParameterSlider();
            parameterSlider.SetBinding(DataContextProperty, nameof(ViewModel.ViewModel.SpeakerParameter) + $"[{index}]");

            Binding binding = new Binding("IsChecked")
            {
                ElementName = nameof(ParCharacterRadioButton), Converter = new BooleanToVisibilityConverter()
            };
            TabItem tabItem = new TabItem { Header = "四国めたん" };
            tabItem.SetBinding(VisibilityProperty, binding);
            tabItem.Content = parameterSlider;

            MainTab.Items.Add(tabItem);

            id = 1;
            index = MainViewModel.SpeakerParameter.FindIndex(x => x.ID == id && x.Port == defaultPort);
            if (index < 0)
            {
                SynthesisParameter parameter = new SynthesisParameter { ID = id };
                parameter.PropertyChanged += MainViewModel.ViewModel_PropertyChanged;
                MainViewModel.SpeakerParameter.Add(parameter);
                index = MainViewModel.SpeakerParameter.Count - 1;
            }

            parameterSlider = new VoicevoxParameterSlider();
            parameterSlider.SetBinding(DataContextProperty, nameof(ViewModel.ViewModel.SpeakerParameter) + $"[{index}]");

            tabItem = new TabItem { Header = "ずんだもん" };
            tabItem.SetBinding(VisibilityProperty, binding);
            tabItem.Content = parameterSlider;

            MainTab.Items.Add(tabItem);
        }

        /// <summary>
        /// キャンセルボタン押下時のイベントハンドラ
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
