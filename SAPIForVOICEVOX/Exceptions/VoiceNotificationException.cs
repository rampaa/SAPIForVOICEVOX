using System;

namespace SAPIForVOICEVOX.Exceptions
{
    /// <summary>
    /// 例外を音声で通知する、例外クラス。
    /// </summary>
    [Serializable]
    public class VoiceNotificationException : Exception
    {
        public VoiceNotificationException()
        {
        }

        public VoiceNotificationException(string message, Exception innerException = null) : base(message, innerException)
        {
        }

        /// <summary>
        /// エラー音声を取得、設定します。
        /// </summary>
        public byte[] ErrorVoice { get; protected set; }
    }
}
