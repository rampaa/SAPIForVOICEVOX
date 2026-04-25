using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable MemberCanBeInternal

namespace Setting.Model
{
    /// <summary>
    /// VOICEVOXに必要なパラメータを定義します。
    /// </summary>
    public sealed class SynthesisParameter : INotifyPropertyChanged, IEquatable<SynthesisParameter>
    {
        #region INotifyPropertyChangedの実装
        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
          => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #endregion

        #region Equals

        public bool Equals(SynthesisParameter other)
        {
            return other != null && ValueMode == other.ValueMode &&
                Volume == other.Volume &&
                Speed == other.Speed &&
                Pitch == other.Pitch &&
                PrePhonemeLength == other.PrePhonemeLength &&
                PostPhonemeLength == other.PostPhonemeLength &&
                Intonation == other.Intonation;
        }

        public override bool Equals(object obj)
        {
            return obj is SynthesisParameter parameter && Equals(parameter);
        }

        public override int GetHashCode()
        {
            int hashCode = 1557109181;
            hashCode = (hashCode * -1521134295) + ValueMode.GetHashCode();
            hashCode = (hashCode * -1521134295) + Volume.GetHashCode();
            hashCode = (hashCode * -1521134295) + Speed.GetHashCode();
            hashCode = (hashCode * -1521134295) + Pitch.GetHashCode();
            hashCode = (hashCode * -1521134295) + PrePhonemeLength.GetHashCode();
            hashCode = (hashCode * -1521134295) + PostPhonemeLength.GetHashCode();
            hashCode = (hashCode * -1521134295) + Intonation.GetHashCode();
            return hashCode;
        }

        // ReSharper disable once ArrangeRedundantParentheses
        public static bool operator ==(SynthesisParameter left, SynthesisParameter right) => left?.Equals(right) ?? (right is null);
        public static bool operator !=(SynthesisParameter left, SynthesisParameter right) => !(left == right);
        #endregion

        private ParameterValueMode _valueMode = ParameterValueMode.SAPI;
        /// <summary>
        /// SAPIの値を使用するか、設定アプリの値を使用するかを取得、設定します。
        /// </summary>
        public ParameterValueMode ValueMode
        {
            get => _valueMode;
            set
            {
                if (_valueMode == value)
                {
                    return;
                }

                _valueMode = value;
                RaisePropertyChanged();
            }
        }

        private double _volume = 1;
        /// <summary>
        /// 音量を取得、設定します。
        /// </summary>
        public double Volume
        {
            get => _volume;
            set
            {
                if (_volume == value)
                {
                    return;
                }

                _volume = value;
                RaisePropertyChanged();
            }
        }

        private double _speed = 1;
        /// <summary>
        /// 話速を取得、設定します。
        /// </summary>
        public double Speed
        {
            get => _speed;
            set
            {
                if (_speed == value)
                {
                    return;
                }

                _speed = value;
                RaisePropertyChanged();
            }
        }

        private double _pitch;
        /// <summary>
        /// 音高を取得、設定します。
        /// </summary>
        public double Pitch
        {
            get => _pitch;
            set
            {
                if (_pitch == value)
                {
                    return;
                }

                _pitch = value;
                RaisePropertyChanged();
            }
        }

        private double _intonation = 1;
        /// <summary>
        /// 抑揚を取得、設定します。
        /// </summary>
        public double Intonation
        {
            get => _intonation;
            set
            {
                if (_intonation == value)
                {
                    return;
                }

                _intonation = value;
                RaisePropertyChanged();
            }
        }

        private double _prePhonemeLength = 0.1;
        /// <summary>
        /// 開始無音を取得、設定します。
        /// </summary>
        public double PrePhonemeLength
        {
            get => _prePhonemeLength;
            set
            {
                if (_prePhonemeLength == value)
                {
                    return;
                }

                _prePhonemeLength = value;
                RaisePropertyChanged();
            }
        }

        private double _postPhonemeLength = 0.1;
        /// <summary>
        /// 終了無音を取得、設定します。
        /// </summary>
        public double PostPhonemeLength
        {
            get => _postPhonemeLength;
            set
            {
                if (_postPhonemeLength == value)
                {
                    return;
                }

                _postPhonemeLength = value;
                RaisePropertyChanged();
            }
        }

        private int _port = 50021;
        /// <summary>
        /// ポートを取得、設定します。
        /// </summary>
        public int Port
        {
            get => _port;
            set
            {
                if (_port == value)
                {
                    return;
                }

                _port = value;
                RaisePropertyChanged();
            }
        }

        private int _id;
        /// <summary>
        /// 話者IDを取得、設定します。
        /// </summary>
        public int ID
        {
            get => _id;
            set
            {
                if (_id == value)
                {
                    return;
                }

                _id = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// 設定ファイルのバージョンを所得、設定します。
        /// </summary>
        internal string Version { get; set; } = new Version(1, 0, 0).ToString();
    }
}
