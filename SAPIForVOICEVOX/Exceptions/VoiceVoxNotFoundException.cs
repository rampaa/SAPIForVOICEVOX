using System;
using System.IO;

namespace SAPIForVOICEVOX.Exceptions
{
    /// <summary>
    /// ボイスボックスが起動中プロセス一覧から見つからなかった場合に投げられます。
    /// </summary>
    [Serializable]
    public class VoiceVoxNotFoundException : VoiceNotificationException
    {
        private const string VoiceVoxNotFoundExceptionMessage = "VOICEVOXが見つかりません";

        public VoiceVoxNotFoundException() : this(null) { }

        public VoiceVoxNotFoundException(Exception innerException) : base(VoiceVoxNotFoundExceptionMessage, innerException)
        {
            Stream stream = Properties.Resources.ボイスボックスが見つかりません;
            ErrorVoice = new byte[stream.Length];
            _ = stream.Read(ErrorVoice, 0, (int)stream.Length);
        }
    }
}
