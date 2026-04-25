using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Serialization;
using Setting.Model;
using Setting.View;
using SFVvCommon;

namespace Setting.ViewModel
{
    public sealed class ViewModel : INotifyPropertyChanged
    {
        #region INotifyPropertyChangedの実装
        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
          => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion

        /// <summary>
        ///
        /// </summary>
        internal ViewModel(MainWindow mainWindow)
        {
            PropertyChanged += ViewModel_PropertyChanged;
            Owner = mainWindow;
            LoadData();
        }

        #region プロパティとか

        private MainWindow Owner { get; }

        /// <summary>
        /// Model
        /// </summary>
        private GeneralSetting _generalSetting;

        /// <summary>
        /// 句点で分割するかどうかを取得、設定します。
        /// </summary>
        public bool? IsSplitKuten
        {
            get => _generalSetting.isSplitKuten;
            set
            {
                if (_generalSetting.isSplitKuten == value)
                {
                    return;
                }

                _generalSetting.isSplitKuten = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// 読点で分割するかどうかを取得、設定します。
        /// </summary>
        public bool? IsSplitTouten
        {
            get => _generalSetting.isSplitTouten;
            set
            {
                if (_generalSetting.isSplitTouten == value)
                {
                    return;
                }

                _generalSetting.isSplitTouten = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// 読点で分割するかどうかを取得、設定します。
        /// </summary>
        public bool? IsSplitNewLine
        {
            get => _generalSetting.isSplitNewLine;
            set
            {
                if (_generalSetting.isSplitNewLine == value)
                {
                    return;
                }

                _generalSetting.isSplitNewLine = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// 調声設定モードを取得、設定します。
        /// </summary>
        public SynthesisSettingMode SynthesisSettingMode
        {
            get => _generalSetting.synthesisSettingMode;
            set
            {
                if (_generalSetting.synthesisSettingMode == value)
                {
                    return;
                }

                _generalSetting.synthesisSettingMode = value;
                RaisePropertyChanged();
            }
        }


        private SynthesisParameter _batchParameter = new SynthesisParameter();
        /// <summary>
        /// 一括調声設定
        /// </summary>
        public SynthesisParameter BatchParameter
        {
            get => _batchParameter;
            set
            {
                if (_batchParameter.Equals(value))
                {
                    return;
                }

                _batchParameter = value;
                RaisePropertyChanged();
            }
        }


        private List<SynthesisParameter> _speakerParameter = new List<SynthesisParameter>();
        /// <summary>
        /// 各キャラクター調声設定
        /// </summary>
        public List<SynthesisParameter> SpeakerParameter
        {
            get => _speakerParameter;
            set
            {
                if (_speakerParameter == value)
                {
                    return;
                }

                _speakerParameter = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// エンジンエラーを通知するかどうかを取得、設定します。
        /// </summary>
        public bool? ShouldNotifyEngineError
        {
            get => _generalSetting.shouldNotifyEngineError;
            set
            {
                if (_generalSetting.shouldNotifyEngineError == value)
                {
                    return;
                }

                _generalSetting.shouldNotifyEngineError = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// SAPIイベントを通知するかどうかを取得、設定します。
        /// </summary>
        public bool? UseSapiEvent
        {
            get => _generalSetting.useSspiEvent;
            set
            {
                if (_generalSetting.useSspiEvent == value)
                {
                    return;
                }

                _generalSetting.useSspiEvent = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// 疑問文を自動調声するかどうかを取得、設定します。
        /// </summary>
        public bool? UseInterrogativeAutoAdjustment
        {
            get => _generalSetting.useInterrogativeAutoAdjustment;
            set
            {
                if (_generalSetting.useInterrogativeAutoAdjustment == value)
                {
                    return;
                }

                _generalSetting.useInterrogativeAutoAdjustment = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        #region イベントとか

        //コマンドの使い方がいまいちわからないので、普通にイベントを使う。

        /// <summary>
        /// OKボタン押下イベント
        /// </summary>
        internal void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SaveData();
            Window window = Window.GetWindow((Button)sender);
            window?.Close();
        }

        /// <summary>
        /// 適用ボタン押下イベント
        /// </summary>
        internal void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            //ボタン連打防止ようにボタン無効化
            Button button = (Button)sender;
            button.IsEnabled = false;
            SaveData();
        }

        /// <summary>
        /// リセットボタン押下イベント
        /// </summary>
        internal void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow((Button)sender);
            if (window != null)
            {
                MessageBoxResult result = MessageBox.Show(window, "各キャラクターの調声パラメータも含めて全て初期値にリセットします。" + Environment.NewLine + "よろしいですか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.No)
                {
                    return;
                }

                _generalSetting = new GeneralSetting();
                //null指定で全てのプロパティ。
                //propertyName引数はオプション引数だがCallerMemberName属性が付いてるので、明示的に指定が必要。多分
                RaisePropertyChanged(null);

                BatchParameter = new SynthesisParameter();
                for (int i = 0; i < SpeakerParameter.Count; i++)
                {
                    SpeakerParameter[i] = new SynthesisParameter();
                }
                RaisePropertyChanged(nameof(SpeakerParameter));

                //適応ボタン有効化のための、プロパティ変更通知登録
                BatchParameter.PropertyChanged += ViewModel_PropertyChanged;
                foreach (SynthesisParameter item in SpeakerParameter)
                {
                    item.PropertyChanged += ViewModel_PropertyChanged;
                }
            }
        }

        /// <summary>
        /// バージョン情報ボタン押下イベント
        /// </summary>
        internal void VersionInfoButton_Click(object sender, RoutedEventArgs e)
        {
            VersionInfoWindow versionInfoWindow = new VersionInfoWindow { Owner = Owner };
            _ = versionInfoWindow.ShowDialog();
        }

        /// <summary>
        /// プロパティ変更の通知受取り
        /// </summary>
        internal void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            //適応ボタンの有効化
            Owner.ApplyButton.IsEnabled = true;
        }

        #endregion

        #region 定数

        /*
                private const int CharacterCount = 100;
        */

#if x64
        private const string MutexName = "SAPIForVOICEVOX64bit";
#else
        private const string MutexName = "SAPIForVOICEVOX32bit";
#endif

        #endregion

        #region メソッド

        /// <summary>
        /// 保存します。
        /// </summary>
        private void SaveData()
        {
            // シリアライズする
            XmlSerializer serializerGeneralSeting = new XmlSerializer(typeof(GeneralSetting));
            using (StreamWriter streamWriter = new StreamWriter(Common.GetGeneralSettingFileName(), false, Encoding.UTF8))
            {
                serializerGeneralSeting.Serialize(streamWriter, _generalSetting);
            }

            BatchParameter.Version = Common.GetCurrentVersion().ToString();
            XmlSerializer serializerBatchParameter = new XmlSerializer(typeof(SynthesisParameter));
            using (StreamWriter streamWriter = new StreamWriter(Common.GetBatchParameterSettingFileName(), false, Encoding.UTF8))
            {
                serializerBatchParameter.Serialize(streamWriter, BatchParameter);
            }

            foreach (SynthesisParameter param in SpeakerParameter)
            {
                param.Version = Common.GetCurrentVersion().ToString();
            }
            XmlSerializer serializerSpeakerParameter = new XmlSerializer(typeof(List<SynthesisParameter>));
            using (StreamWriter streamWriter = new StreamWriter(Common.GetSpeakerParameterSettingFileName(), false, Encoding.UTF8))
            {
                serializerSpeakerParameter.Serialize(streamWriter, SpeakerParameter);
            }
        }

        /// <summary>
        /// 設定を読み込みます。
        /// </summary>
        private void LoadData()
        {
            _generalSetting = LoadGeneralSetting();
            BatchParameter = LoadBatchSynthesisParameter();
            SpeakerParameter = LoadSpeakerSynthesisParameter();

            //適応ボタン有効化のための、プロパティ変更通知登録
            BatchParameter.PropertyChanged += ViewModel_PropertyChanged;
            foreach (SynthesisParameter item in SpeakerParameter)
            {
                item.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        /// <summary>
        /// 一般設定を読み込みます。
        /// </summary>
        /// <returns>一般設定</returns>
        public static GeneralSetting LoadGeneralSetting()
        {
            GeneralSetting result = new GeneralSetting();
            string settingFileName = Common.GetGeneralSettingFileName();

            //ファイル存在確認
            if (!File.Exists(settingFileName))
            {
                //無い場合はそのまま返す。
                return result;
            }

            // デシリアライズする
            Mutex mutex = new Mutex(false, MutexName);
            try
            {
                //ミューテックス取得
                _ = mutex.WaitOne();

                XmlSerializer serializerGeneralSetting = new XmlSerializer(typeof(GeneralSetting));
                XmlReaderSettings xmlSettings = new XmlReaderSettings
                {
                    CheckCharacters = false
                };
                using (StreamReader streamReader = new StreamReader(settingFileName, Encoding.UTF8))
                using (XmlReader xmlReader = XmlReader.Create(streamReader, xmlSettings))
                {
                    //結果上書き
                    result = (GeneralSetting)serializerGeneralSetting.Deserialize(xmlReader);
                }
                return result;
            }
            catch (Exception)
            {
                return result;
            }
            finally
            {
                //ミューテックス開放
                mutex.Dispose();
            }
        }

        /// <summary>
        /// 一括の調声設定を取得します。
        /// </summary>
        /// <returns>調声設定</returns>
        public static SynthesisParameter LoadBatchSynthesisParameter()
        {
            SynthesisParameter result = new SynthesisParameter();
            string settingFileName = Common.GetBatchParameterSettingFileName();

            //ファイル存在確認
            if (!File.Exists(settingFileName))
            {
                //無い場合はそのまま返す。
                return result;
            }

            // デシリアライズする
            Mutex mutex = new Mutex(false, MutexName);
            try
            {
                //ミューテックス取得
                _ = mutex.WaitOne();

                XmlSerializer serializerSynthesisParameter = new XmlSerializer(typeof(SynthesisParameter));
                XmlReaderSettings xmlSettings = new XmlReaderSettings
                {
                    CheckCharacters = false
                };
                using (StreamReader streamReader = new StreamReader(settingFileName, Encoding.UTF8))
                using (XmlReader xmlReader = XmlReader.Create(streamReader, xmlSettings))
                {
                    //結果上書き
                    result = (SynthesisParameter)serializerSynthesisParameter.Deserialize(xmlReader);
                }
                return result;
            }
            catch (Exception)
            {
                return result;
            }
            finally
            {
                //ミューテックス開放
                mutex.Dispose();
            }
        }

        /// <summary>
        /// キャラ調声設定を読み込みます。
        /// </summary>
        /// <returns>キャラ調声設定配列</returns>
        public static List<SynthesisParameter> LoadSpeakerSynthesisParameter()
        {
            string settingFileName = Common.GetSpeakerParameterSettingFileName();

            //戻り値を作成、初期化
            List<SynthesisParameter> result = new List<SynthesisParameter>();

            //ファイル存在確認
            if (!File.Exists(settingFileName))
            {
                return result;
            }

            // デシリアライズする
            Mutex mutex = new Mutex(false, MutexName);
            try
            {
                //同じファイルを同時に操作しないために、ミューテックスを使用
                _ = mutex.WaitOne();

                XmlSerializer serializerSynthesisParameter = new XmlSerializer(typeof(List<SynthesisParameter>));
                XmlReaderSettings xmlSettings = new XmlReaderSettings
                {
                    CheckCharacters = false
                };
                using (StreamReader streamReader = new StreamReader(settingFileName, Encoding.UTF8))
                using (XmlReader xmlReader = XmlReader.Create(streamReader, xmlSettings))
                {
                    //結果上書き
                    result = (List<SynthesisParameter>)serializerSynthesisParameter.Deserialize(xmlReader);
                }
                //データがバージョン１の場合
                if (result.Count != 0 && new Version(result.First().Version).Major == 1)
                {
                    result = new List<SynthesisParameter>();
                }
                return result;
            }
            catch (Exception)
            {
                return result;
            }
            finally
            {
                //ミューテックス開放
                mutex.Dispose();
            }
        }

        #endregion
    }
}
