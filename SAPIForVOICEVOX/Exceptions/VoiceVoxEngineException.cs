using System;
using System.IO;

namespace SAPIForVOICEVOX.Exceptions
{
    /// <summary>
    /// VOICEVOXのエンジンに関するエラーを表します。
    /// </summary>
    [Serializable]
    public class VoiceVoxEngineException : VoiceNotificationException
    {
        private const string VoiceVoxEngineExceptionMessage = "エンジンエラーです";

        public VoiceVoxEngineException() : this(null) { }

        public VoiceVoxEngineException(Exception innerException) : base(VoiceVoxEngineExceptionMessage, innerException)
        {
            Stream stream = Properties.Resources.エンジンエラーです;
            ErrorVoice = new byte[stream.Length];
            _ = stream.Read(ErrorVoice, 0, (int)stream.Length);
        }
    }
}
