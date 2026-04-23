namespace SFVvCommon
{
    /// <summary>
    /// VOICEVOX側のスタイル情報を表します。
    /// </summary>
    public sealed class VoicevoxStyle : StyleBase
    {
        /// <summary>
        /// スタイル情報を初期化します。
        /// </summary>
        /// <param name="appName"></param>
        /// <param name="name"></param>
        /// <param name="styleName"></param>
        /// <param name="iD"></param>
        /// <param name="port"></param>
        public VoicevoxStyle(string appName, string name, string styleName, int iD, int port) : base(appName, name, styleName, iD, port)
        {
        }
    }
}
