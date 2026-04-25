using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json.Linq;
using SFVvCommon;
using StyleRegistrationTool.View;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml;
using System.Xml.Serialization;

namespace StyleRegistrationTool.ViewModel
{
    internal sealed class MainViewModel : INotifyPropertyChanged
    {

        public MainViewModel(MainWindow mainWindow)
        {
            MainWindow = mainWindow;
            OkCommand = new DelegateCommand(OkCommandExecute);
            CancelCommand = new DelegateCommand(() => MainWindow.Close());
            ChangePortCommand = new DelegateCommand(ChangePortCommandExecute);
            AddCommand = new DelegateCommand(AddCommandExecute);
            RemoveCommand = new DelegateCommand(RemoveCommandExecute);
            AllAddCommand = new DelegateCommand(AllAddCommandExecute);
            AllRemoveCommand = new DelegateCommand(AllRemoveCommandExecute);
            UpButtonCommand = new DelegateCommand(UpButtonCommandExecute);
            DownButtonCommand = new DelegateCommand(DownButtonCommandExecute);
        }

        #region INotifyPropertyChangedの実装
        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
          => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion

        /// <summary>
        /// 唯一のhttpクライアント
        /// </summary>
        private readonly HttpClient _httpClient = new HttpClient();

        #region プロパティ

        /// <summary>
        /// メインウィンドウを取得、設定します。
        /// </summary>
        private MainWindow MainWindow { get; }

        /// <summary>
        /// okボタンのコマンド
        /// </summary>
        public ICommand OkCommand { get; }

        /// <summary>
        /// キャンセルボタンのコマンド
        /// </summary>
        public ICommand CancelCommand { get; }

        /// <summary>
        /// ポート変更ボタンのコマンド
        /// </summary>
        public ICommand ChangePortCommand { get; }

        /// <summary>
        /// 追加コマンド
        /// </summary>
        public ICommand AddCommand { get; }

        /// <summary>
        /// 削除コマンド
        /// </summary>
        public ICommand RemoveCommand { get; }

        /// <summary>
        /// 全て追加コマンド
        /// </summary>
        public ICommand AllAddCommand { get; }

        /// <summary>
        /// 全て削除コマンド
        /// </summary>
        public ICommand AllRemoveCommand { get; }

        /// <summary>
        /// 並び替え上ボタンコマンド
        /// </summary>
        public ICommand UpButtonCommand { get; }

        /// <summary>
        /// 並び替え下ボタンコマンド
        /// </summary>
        public ICommand DownButtonCommand { get; }

        /// <summary>
        /// VOICEVOX側リストの選択されてるアイテム一覧
        /// </summary>
        internal IEnumerable<VoicevoxStyle> VoicevoxStyleSelectedItems { get; set; } = Enumerable.Empty<VoicevoxStyle>();

        /// <summary>
        /// SAPI側リストの選択されているアイテム一覧
        /// </summary>
        internal IEnumerable<SapiStyle> SapiStyleSelectedItems { get; set; } = Enumerable.Empty<SapiStyle>();

        /// <summary>
        /// ソート済みSAPI側リストの選択されているアイテム一覧
        /// </summary>
        private IEnumerable<SapiStyle> SapiStyleSortedSelectedItems => SapiStyleSelectedItems.OrderBy(x => SapiStyles.IndexOf(x)).ToArray();

        /// <summary>
        /// ソート済みSAPI側リストの選択されているアイテム一覧
        /// </summary>
        private IEnumerable<SapiStyle> SapiStyleSortedSelectedItemsReverse => SapiStyleSelectedItems.OrderByDescending(x => SapiStyles.IndexOf(x)).ToArray();

        /// <summary>
        /// ポート番号
        /// </summary>
        private int Port { get; set; } = 50021;

        #region NotifyProperty

        private string _appName = "VOICEVOX";
        /// <summary>
        /// スタイルを取得するアプリ名
        /// </summary>
        public string AppName
        {
            get => _appName;
            private set
            {
                if (_appName == value)
                {
                    return;
                }

                _appName = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(ConnectingMessage));
            }
        }

        /// <summary>
        /// 接続中を示す文字列
        /// </summary>
        public string ConnectingMessage => AppName + "へ接続中";

        private ObservableCollection<VoicevoxStyle> _voicevoxStyles;
        /// <summary>
        /// VOICEVOX側のスタイル一覧
        /// </summary>
        public ObservableCollection<VoicevoxStyle> VoicevoxStyles
        {
            get => _voicevoxStyles;
            private set
            {
                if (_voicevoxStyles == value)
                {
                    return;
                }

                _voicevoxStyles = value;
                RaisePropertyChanged();
            }
        }

        private ObservableCollection<SapiStyle> _sapiStyles = new ObservableCollection<SapiStyle>();
        /// <summary>
        /// SAPI側のスタイル一覧
        /// </summary>
        public ObservableCollection<SapiStyle> SapiStyles
        {
            get => _sapiStyles;
            set
            {
                if (_sapiStyles == value)
                {
                    return;
                }

                _sapiStyles = value;
                RaisePropertyChanged();
            }
        }

        private bool _isMainWindowEnabled;
        /// <summary>
        /// メイン画面が有効かどうか
        /// </summary>
        public bool IsMainWindowEnabled
        {
            get => _isMainWindowEnabled;
            set
            {
                if (_isMainWindowEnabled == value)
                {
                    return;
                }

                _isMainWindowEnabled = value;
                RaisePropertyChanged();
            }
        }

        private Visibility _waitCircleVisibility = Visibility.Visible;
        /// <summary>
        /// 待機ぐるぐる画面の表示状態
        /// </summary>
        public Visibility WaitCircleVisibility
        {
            get => _waitCircleVisibility;
            set
            {
                if (_waitCircleVisibility == value)
                {
                    return;
                }

                _waitCircleVisibility = value;
                RaisePropertyChanged();
            }
        }
        #endregion

        #endregion

        #region イベント

        /// <summary>
        /// mainWindowのLoadedイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = (MainWindow)sender;

            //コマンドラインを見て、インストーラから起動された場合、専用のダイアログを表示する。
            string[] commandline = Environment.GetCommandLineArgs();
            commandline = commandline.Select(str => str.ToLower()).ToArray();
            bool shouldContinue;
            if (commandline.Contains("/install"))
            {
                if (File.Exists(Common.GetStyleRegistrationSettingFileName()))
                {
                    SapiStyles = new ObservableCollection<SapiStyle>(LoadStylesToLocalFile());
                    _ = MessageBox.Show("スタイル情報を復元しました。", "SAPI For VOICEVOX", MessageBoxButton.OK, MessageBoxImage.Information);
                    OkCommandExecute();
                    return;
                }

                InstallerDialogResult dialogResult = ShowStartedInstallerDialog(mainWindow);
                switch (dialogResult)
                {
                    case InstallerDialogResult.SelectStyle:
                        //何もしない
                        break;
                    case InstallerDialogResult.AllStyle:
                        await AllStyleRegistration();
                        return;
                    // ReSharper disable once RedundantEnumCaseLabelForDefaultSection
                    case InstallerDialogResult.DefaultStyle:
                    default:
                        mainWindow.Close();
                        return;
                }
            }
            else
            {
                shouldContinue = ShowVoicevoxConnectionDialog(mainWindow);
                if (!shouldContinue)
                {
                    mainWindow.Close();
                    return;
                }
            }

            //VOICEVOXスタイルリストの更新
            shouldContinue = await UpdateVoicevoxStyles(false);
            if (!shouldContinue)
            {
                mainWindow.Close();
                return;
            }

            //SAPI側の情報取得
            SapiStyle[] sapiStyles = GetSapiStyles();
            SapiStyles = new ObservableCollection<SapiStyle>(sapiStyles);
        }

        /// <summary>
        /// VOICEVOXスタイルの更新を行います。
        /// </summary>
        /// <param name="isAllStyleRegistration">初回インストール時の全て登録ボタンが押されたときの処理を行うかどうか。</param>
        /// <returns></returns>
        private async Task<bool> UpdateVoicevoxStyles(bool isAllStyleRegistration)
        {
            //VOICEVOXから話者情報取得
            VoicevoxStyle[] voicevoxStyles;
            while (true)
            {
                try
                {
                    voicevoxStyles = await GetVoicevoxStyles();
                    break;
                }
                catch (HttpRequestException)
                {
                    bool shouldContinue = ShowVoicevoxConnectionDialog(MainWindow);
                    if (shouldContinue)
                    {
                        continue;
                    }

                    return false;
                }
            }
            if (voicevoxStyles == null)
            {
                return false;
            }

            if (isAllStyleRegistration)
            {
                IsMainWindowEnabled = false;
                WaitCircleVisibility = Visibility.Visible;
            }
            else
            {
                IsMainWindowEnabled = true;
                WaitCircleVisibility = Visibility.Collapsed;
            }

            VoicevoxStyles = new ObservableCollection<VoicevoxStyle>(voicevoxStyles);
            return true;
        }

        private void OkCommandExecute()
        {
            RegistrationToWindowsRegistry();
            SaveStylesToLocalFile();
            MainWindow.Close();
        }

        /// <summary>
        /// ポート変更ボタン
        /// </summary>
        private async void ChangePortCommandExecute()
        {
            try
            {
                int prevPort = Port;
                if (!ShowChangePortWindow())
                {
                    return;
                }

                IsMainWindowEnabled = false;
                WaitCircleVisibility = Visibility.Visible;

                bool isSuccess = await UpdateVoicevoxStyles(false);
                if (!isSuccess)
                {
                    Port = prevPort;
                }
                IsMainWindowEnabled = true;
                WaitCircleVisibility = Visibility.Collapsed;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// 追加ボタンの処理
        /// </summary>
        private void AddCommandExecute()
        {
            foreach (VoicevoxStyle item in VoicevoxStyleSelectedItems)
            {
                SapiStyle sapiStyle = new SapiStyle(item, Common.CLSID);
                if (!SapiStyles.Contains(sapiStyle))
                {
                    SapiStyles.Add(sapiStyle);
                }
            }
        }

        /// <summary>
        /// 削除ボタンの処理
        /// </summary>
        private void RemoveCommandExecute()
        {
            List<SapiStyle> sapiStyles = new List<SapiStyle>(SapiStyleSelectedItems);
            foreach (SapiStyle item in sapiStyles)
            {
                _ = SapiStyles.Remove(item);
            }
        }

        /// <summary>
        /// 全て追加ボタンの処理
        /// </summary>
        private void AllAddCommandExecute()
        {
            foreach (VoicevoxStyle item in VoicevoxStyles)
            {
                SapiStyle sapiStyle = new SapiStyle(item, Common.CLSID);
                if (!SapiStyles.Contains(sapiStyle))
                {
                    SapiStyles.Add(sapiStyle);
                }
            }
        }

        /// <summary>
        /// 全て削除ボタンの処理
        /// </summary>
        private void AllRemoveCommandExecute()
        {
            SapiStyles.Clear();
        }

        /// <summary>
        /// 並び替え上ボタンの処理
        /// </summary>
        private void UpButtonCommandExecute()
        {
            foreach (SapiStyle item in SapiStyleSortedSelectedItems)
            {
                int index = SapiStyles.IndexOf(item);
                if (index == 0)
                {
                    return;
                }
                SapiStyles.Move(index, index - 1);
            }
        }

        /// <summary>
        /// 並び替え下ボタンの処理
        /// </summary>
        private void DownButtonCommandExecute()
        {
            foreach (SapiStyle item in SapiStyleSortedSelectedItemsReverse)
            {
                int index = SapiStyles.IndexOf(item);
                if (index == SapiStyles.Count - 1)
                {
                    return;
                }
                SapiStyles.Move(index, index + 1);
            }
        }

        #endregion

        #region メソッド

        /// <summary>
        /// インストーラから起動された時に表示するDialogを表示する。
        /// </summary>
        /// <param name="window">親ウィンドウ</param>
        /// <returns>
        /// true:処理継続
        /// false:処理中止
        /// </returns>
        private InstallerDialogResult ShowStartedInstallerDialog(MainWindow window)
        {
            InstallerDialogResult dialogResult = InstallerDialogResult.SelectStyle;

            TaskDialog dialog = new TaskDialog
            {
                OwnerWindowHandle = window.Handle,
                Caption = "話者とスタイルの登録",
                InstructionText = "話者とスタイルの登録を行います。",
                Text = "後で登録することもできます。\n後で登録する場合、スタートの全てのプログラムから起動できます。"
            };

            TaskDialogCommandLink link1 = new TaskDialogCommandLink("link1", "登録する話者とスタイルを選択", "VOICEVOX(または派生アプリ)の起動が必要");
            link1.Click += (sender1, e1) =>
            {
                dialog.Close();
                dialogResult = InstallerDialogResult.SelectStyle;
            };
            link1.Default = true;
            dialog.Controls.Add(link1);

            TaskDialogCommandLink link2 = new TaskDialogCommandLink("link2", "全ての話者とスタイルを登録", "VOICEVOX(または派生アプリ)の起動が必要");
            link2.Click += (sender1, e1) =>
            {
                dialog.Close();
                dialogResult = InstallerDialogResult.AllStyle;
            };
            dialog.Controls.Add(link2);

            TaskDialogCommandLink link3 = new TaskDialogCommandLink("link3", "ポート変更", "COEIROINK等のVOICEVOX派生アプリを登録します");
            link3.Click += (sender1, e1) =>
            {
                _ = ShowChangePortWindow();
            };
            dialog.Controls.Add(link3);

            TaskDialogCommandLink link4 = new TaskDialogCommandLink("link4", "後で行う", "デフォルトの話者とスタイルが登録されます。");
            link4.Click += (sender1, e1) =>
            {
                dialog.Close();
                dialogResult = InstallerDialogResult.DefaultStyle;
            };
            dialog.Controls.Add(link4);

            _ = dialog.Show();
            return dialogResult;
        }

        /// <summary>
        /// VOICEVOXを起動したかどうかの確認ダイアログを表示します。中止が押された場合、親ウィンドウを閉じます。
        /// </summary>
        /// <param name="window">親ウィンドウ</param>
        /// <returns>
        /// true:処理継続
        /// false:処理中止
        /// </returns>
        private bool ShowVoicevoxConnectionDialog(MainWindow window)
        {
            bool shouldContinue = true;

            TaskDialog dialog = new TaskDialog
            {
                OwnerWindowHandle = window.Handle,
                //dialog.Icon = TaskDialogStandardIcon.Information;
                Caption = "VOICEVOX起動の確認",
                InstructionText = "VOICEVOXを起動しましたか？",
                Text = "話者とスタイル登録には、VOICEVOX(または派生アプリ)の起動が必要です。"
            };

            TaskDialogCommandLink link1 = new TaskDialogCommandLink("link1", "VOICEVOXを起動した");
            link1.Click += (sender1, e1) => dialog.Close();
            link1.Default = true;
            dialog.Controls.Add(link1);

            TaskDialogCommandLink link2 = new TaskDialogCommandLink("link2", "ポート変更");
            link2.Click += (sender1, e1) =>
            {
                dialog.Close();
                _ = ShowChangePortWindow();
            };
            dialog.Controls.Add(link2);

            TaskDialogCommandLink link3 = new TaskDialogCommandLink("link3", "中止");
            link3.Click += (sender1, e1) =>
            {
                dialog.Close();
                shouldContinue = false;
            };
            dialog.Controls.Add(link3);

            _ = dialog.Show();

            return shouldContinue;
        }

        /// <summary>
        /// ポート変更ダイアログを表示し、ユーザーの選択に応じて、Portプロパティを更新します。
        /// </summary>
        private bool ShowChangePortWindow()
        {
            ChangePortWindow portWindow = new ChangePortWindow(AppName, Port)
            {
                Owner = MainWindow
            };
            bool? portWindowResult = portWindow.ShowDialog();
            if (portWindowResult ?? false)
            {
                AppName = portWindow.AppName;
                Port = portWindow.Port;
                return true;
            }

            return false;
        }

        /// <summary>
        /// VOICEVOXから話者とスタイル情報を取得します。
        /// </summary>
        /// <returns></returns>
        private async Task<VoicevoxStyle[]> GetVoicevoxStyles()
        {
            List<VoicevoxStyle> voicevoxStyles = new List<VoicevoxStyle>();
            using (HttpResponseMessage resultSpeakers = await _httpClient.GetAsync($"http://127.0.0.1:{Port}/speakers"))
            {
                //戻り値を文字列にする
                string resBodyStr = await resultSpeakers.Content.ReadAsStringAsync();
                JArray jsonObj = JArray.Parse(resBodyStr);
                foreach (JToken speaker in jsonObj)
                {
                    JToken speakerNameJToken = speaker["name"];
                    Debug.Assert(speakerNameJToken != null);
                    string name = speakerNameJToken.ToString();

                    JToken styleNamesJToken = speaker["styleName"];
                    Debug.Assert(styleNamesJToken != null);
                    foreach (JToken style in styleNamesJToken)
                    {
                        JToken styleNameJToken = style["name"];
                        Debug.Assert(styleNameJToken != null);
                        string styleName = styleNameJToken.ToString();
                        int id = style.Value<int>("id");
                        voicevoxStyles.Add(new VoicevoxStyle(AppName, name, styleName, id, Port));
                    }
                }
            }
            return Common.SortStyle(voicevoxStyles).OfType<VoicevoxStyle>().ToArray();
        }

        /// <summary>
        /// 全てのスタイルを登録します。
        /// </summary>
        private async Task AllStyleRegistration()
        {
            bool shouldContinue = await UpdateVoicevoxStyles(true);
            if (!shouldContinue)
            {
                MainWindow.Close();
                return;
            }
            AllAddCommandExecute();
            OkCommandExecute();
        }

        /// <summary>
        /// スタイル情報をローカルXMLファイルとして保存します。
        /// </summary>
        private void SaveStylesToLocalFile()
        {
            // シリアライズする
            XmlSerializer serializerStyleRegistrationSetting = new XmlSerializer(typeof(SapiStyle[]));
            using (StreamWriter streamWriter = new StreamWriter(Common.GetStyleRegistrationSettingFileName(), false, Encoding.UTF8))
            {
                serializerStyleRegistrationSetting.Serialize(streamWriter, SapiStyles.ToArray());
            }
        }

        /// <summary>
        /// スタイル情報をローカルXMLファイルから読み込みます。
        /// </summary>
        private static SapiStyle[] LoadStylesToLocalFile()
        {
            string settingFileName = Common.GetStyleRegistrationSettingFileName();

            //ファイル存在確認
            if (!File.Exists(settingFileName))
            {
                //無い場合はそのまま返す。
                return Array.Empty<SapiStyle>();
            }

            XmlSerializer serializerGeneralSetting = new XmlSerializer(typeof(SapiStyle[]));
            XmlReaderSettings xmlSettings = new XmlReaderSettings
            {
                CheckCharacters = false
            };
            using (StreamReader streamReader = new StreamReader(settingFileName, Encoding.UTF8))
            using (XmlReader xmlReader = XmlReader.Create(streamReader, xmlSettings))
            {
                //結果上書き
                return (SapiStyle[])serializerGeneralSetting.Deserialize(xmlReader);
            }
        }

        #region レジストリ関連

        /// <summary>
        /// Windowsのレジストリにスタイルを登録します。
        /// </summary>
        private void RegistrationToWindowsRegistry()
        {
            Common.ClearStyleFromWindowsRegistry();

            using (RegistryKey regTokensKey = Registry.LocalMachine.OpenSubKey(Common.TokensRegKey, true))
            {
                Debug.Assert(regTokensKey != null);
                for (int i = 0; i < SapiStyles.Count; i++)
                {
                    using (RegistryKey voiceVoxRegkey = regTokensKey.CreateSubKey("VOICEVOX" + i.ToString("000")))
                    {
                        Debug.Assert(voiceVoxRegkey != null);
                        voiceVoxRegkey.SetValue("", SapiStyles[i].SpaiName);
                        voiceVoxRegkey.SetValue("411", SapiStyles[i].SpaiName);
                        voiceVoxRegkey.SetValue(Common.RegClsid, SapiStyles[i].CLSID.ToString(Common.RegClsidFormatString));
                        voiceVoxRegkey.SetValue(Common.RegSpeakerNumber, SapiStyles[i].ID);
                        voiceVoxRegkey.SetValue(Common.RegName, SapiStyles[i].Name);
                        voiceVoxRegkey.SetValue(Common.RegStyleName, SapiStyles[i].StyleName);
                        voiceVoxRegkey.SetValue(Common.RegPort, SapiStyles[i].Port);
                        voiceVoxRegkey.SetValue(Common.RegAppName, SapiStyles[i].AppName);

                        using (RegistryKey attributesRegkey = voiceVoxRegkey.CreateSubKey(Common.RegAttributes))
                        {
                            Debug.Assert(attributesRegkey != null);
                            attributesRegkey.SetValue("Age", "Teen");
                            attributesRegkey.SetValue("Vendor", "Hiroshiba Kazuyuki");
                            attributesRegkey.SetValue("Language", "411");
                            attributesRegkey.SetValue("Gender", "Female");
                            attributesRegkey.SetValue("Name", SapiStyles[i].SpaiName);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// レジストリに登録されているSAPIの話者情報を取得します。
        /// </summary>
        /// <returns></returns>
        private static SapiStyle[] GetSapiStyles()
        {
            List<SapiStyle> sapiStyles = new List<SapiStyle>();

            using (RegistryKey regTokensKey = Registry.LocalMachine.OpenSubKey(Common.TokensRegKey, true))
            {
                Debug.Assert(regTokensKey != null);
                string[] tokenNames = regTokensKey.GetSubKeyNames();
                foreach (string tokenName in tokenNames)
                {
                    using (RegistryKey tokenKey = regTokensKey.OpenSubKey(tokenName))
                    {
                        Debug.Assert(tokenKey != null);
                        string clsid = (string)tokenKey.GetValue(Common.RegClsid);
                        string name = (string)tokenKey.GetValue(Common.RegName);
                        if (clsid == Common.CLSID.ToString(Common.RegClsidFormatString) &&
                            name != null)
                        {
                            string styleName = (string)tokenKey.GetValue(Common.RegStyleName);
                            int id = (int)tokenKey.GetValue(Common.RegSpeakerNumber, 0);
                            int port = (int)tokenKey.GetValue(Common.RegPort, 50021);
                            string appName = (string)tokenKey.GetValue(Common.RegAppName, "VOICEVOX");
                            SapiStyle sapiStyle = new SapiStyle(appName, name, styleName, id, port, new Guid(clsid));
                            sapiStyles.Add(sapiStyle);
                        }
                    }
                }
            }
            return sapiStyles.ToArray();
        }

        #endregion レジストリ関連

        #endregion

        #region コマンドクラス

        /// <summary>
        /// プリズムのコードを参考に、デリゲートコマンドを作成。
        /// </summary>
        private sealed class DelegateCommand : ICommand
        {
#pragma warning disable CS0067
            public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067

            public DelegateCommand(Action executeMethod)
            {
                ExecuteMethod = executeMethod;
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                ExecuteMethod();
            }

            private Action ExecuteMethod { get; }
        }

        #endregion

        /// <summary>
        /// インストール時に表示されるダイアログの押されたボタンを表す列挙型
        /// </summary>
        private enum InstallerDialogResult
        {
            SelectStyle,
            AllStyle,
            DefaultStyle
        }
    }
}
