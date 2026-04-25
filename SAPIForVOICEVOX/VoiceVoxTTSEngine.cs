using Microsoft.Win32;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SFVvCommon;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using SAPIForVOICEVOX.EnglishToKana;
using SAPIForVOICEVOX.Exceptions;
using Setting.Model;
using Setting.ViewModel;
using TTSEngineLib;

namespace SAPIForVOICEVOX
{
    [Guid(Common.GuidString)]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
#pragma warning disable IDE0079 // Remove unnecessary suppression
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
#pragma warning restore IDE0079 // Remove unnecessary suppression
    public class VoiceVoxTTSEngine : ISpTTSEngine, ISpObjectWithToken
    {
        #region ネイティブ

        private const ushort WAVE_FORMAT_PCM = 1;

        //SPDFID_WaveFormatExの値は、ヘッダーファイルで定義さていなくて、不明である。
        //したがって、C++コードで値を返す関数を定義しDLLとして出力し、使用することにした。
        [DllImport("SAPIGetStaticValueLib.dll")]
        private static extern Guid GetSPDFIDWaveFormatEx();
        //同上
        [DllImport("SAPIGetStaticValueLib.dll")]
        private static extern Guid GetSPDFIDText();

        /// <summary>
        /// SPVESACTIONSは、ISpTTSEngineSite :: GetActions呼び出しによって返される値をリストします。これらの値から、TTSエンジンは、アプリケーションによって行われたリアルタイムのアクション要求を受信します。
        /// </summary>
        [Flags]
        private enum SPVESACTIONS
        {
            SPVES_CONTINUE = 0,
            SPVES_ABORT = 1 << 0,
            SPVES_SKIP = 1 << 1,
            SPVES_RATE = 1 << 2,
            SPVES_VOLUME = 1 << 3
        }

        /// <summary>
        /// SPEVENTENUMは、SAPIから可能なイベントを一覧表示します。
        /// </summary>
        private enum SPEVENTENUM
        {
            SPEI_UNDEFINED = 0,
            SPEI_START_INPUT_STREAM = 1,
            SPEI_END_INPUT_STREAM = 2,
            SPEI_VOICE_CHANGE = 3,
            SPEI_TTS_BOOKMARK = 4,
            SPEI_WORD_BOUNDARY = 5,
            SPEI_PHONEME = 6,
            SPEI_SENTENCE_BOUNDARY = 7,
            SPEI_VISEME = 8,
            SPEI_TTS_AUDIO_LEVEL = 9,
            SPEI_TTS_PRIVATE = 15,
            SPEI_MIN_TTS = 1,
            SPEI_MAX_TTS = 15,
            SPEI_END_SR_STREAM = 34,
            SPEI_SOUND_START = 35,
            SPEI_SOUND_END = 36,
            SPEI_PHRASE_START = 37,
            SPEI_RECOGNITION = 38,
            SPEI_HYPOTHESIS = 39,
            SPEI_SR_BOOKMARK = 40,
            SPEI_PROPERTY_NUM_CHANGE = 41,
            SPEI_PROPERTY_STRING_CHANGE = 42,
            SPEI_FALSE_RECOGNITION = 43,
            SPEI_INTERFERENCE = 44,
            SPEI_REQUEST_UI = 45,
            SPEI_RECO_STATE_CHANGE = 46,
            SPEI_ADAPTATION = 47,
            SPEI_START_SR_STREAM = 48,
            SPEI_RECO_OTHER_CONTEXT = 49,
            SPEI_SR_AUDIO_LEVEL = 50,
            SPEI_SR_RETAINEDAUDIO = 51,
            SPEI_SR_PRIVATE = 52,
            SPEI_ACTIVE_CATEGORY_CHANGED = 53,
            SPEI_RESERVED5 = 54,
            SPEI_RESERVED6 = 55,
            SPEI_MIN_SR = 34,
            SPEI_MAX_SR = 55,
            SPEI_RESERVED1 = 30,
            SPEI_RESERVED2 = 33,
            SPEI_RESERVED3 = 63
        }

        //SPEVENTENUMはフラグを直接定義しているのではなく、フラグの位置を定義してるらしい？
        //SPFEIマクロを使用して変換する必要がある？
        private const ulong SPFEI_FLAGCHECK = (1ul << (int)SPEVENTENUM.SPEI_RESERVED1) | (1ul << (int)SPEVENTENUM.SPEI_RESERVED2);
        private const ulong SPFEI_ALL_TTS_EVENTS = 0x000000000000FFFEul | SPFEI_FLAGCHECK;
        private const ulong SPFEI_ALL_SR_EVENTS = 0x003FFFFC00000000ul | SPFEI_FLAGCHECK;
        private const ulong SPFEI_ALL_EVENTS = 0xEFFFFFFFFFFFFFFFul;

#pragma warning disable IDE1006 // Naming Styles
        private ulong SPFEI(SPEVENTENUM SPEI_ord)
#pragma warning restore IDE1006 // Naming Styles
        {
            return (1ul << (int)SPEI_ord) | SPFEI_FLAGCHECK;
        }

        private enum SPEVENTLPARAMTYPE
        {
            SPET_LPARAM_IS_UNDEFINED = 0,
            SPET_LPARAM_IS_TOKEN = SPET_LPARAM_IS_UNDEFINED + 1,
            SPET_LPARAM_IS_OBJECT = SPET_LPARAM_IS_TOKEN + 1,
            SPET_LPARAM_IS_POINTER = SPET_LPARAM_IS_OBJECT + 1,
            SPET_LPARAM_IS_STRING = SPET_LPARAM_IS_POINTER + 1
        }

        #endregion


        /// <summary>
        /// キャラ番号
        /// </summary>
        private int SpeakerNumber { get; set; }

        /// <summary>
        /// ポート番号
        /// </summary>
        private int Port { get; set; } = 50021;

        /// <summary>
        /// トークン
        /// </summary>
        private ISpObjectToken Token { get; set; }

        /// <summary>
        /// 唯一のhttpクライアント
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// 英語カナ辞書
        /// </summary>
        private readonly EnglishKanaDictionary _engKanaDict;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public VoiceVoxTTSEngine()
        {
            _httpClient = new HttpClient();
            _engKanaDict = new EnglishKanaDictionary();
        }

        /// <summary>
        /// スピークメソッド。
        /// 読み上げ指示が来ると呼ばれる。
        /// </summary>
        /// <param name="dwSpeakFlags"></param>
        /// <param name="rguidFormatId"></param>
        /// <param name="pWaveFormatEx"></param>
        /// <param name="pTextFragList"></param>
        /// <param name="pOutputSite"></param>
        public void Speak(uint dwSpeakFlags, ref Guid rguidFormatId, ref WAVEFORMATEX pWaveFormatEx, ref SPVTEXTFRAG pTextFragList, ISpTTSEngineSite pOutputSite)
        {
            //SPDFIDTextは非対応
            if (rguidFormatId == GetSPDFIDText())
            {
                return;
            }

            //SAPIの情報取得
            pOutputSite.GetRate(out int tempInt);
            //SAPIは0が真ん中

#pragma warning disable IDE1006 // Naming Styles
            double SAPIspeed = tempInt < 0
                ? Map(tempInt, -10, 0, 0.5, 1.0)
                : Map(tempInt, 0, 10, 1.0, 2.0);
#pragma warning restore IDE1006 // Naming Styles

            pOutputSite.GetVolume(out ushort tempUshort);
            double sapiVolume = Map(tempUshort, 0, 100, 0.0, 1.0);

            //設定アプリのデータ取得
            GetSettingData(SpeakerNumber, out GeneralSetting generalSetting, out SynthesisParameter synthesisParameter);
            double speed;
            double volume;
            if (synthesisParameter.ValueMode == ParameterValueMode.SAPI)
            {
                speed = SAPIspeed;
                volume = sapiVolume;
            }
            else
            {
                speed = synthesisParameter.Speed;
                volume = synthesisParameter.Volume;
            }
            double pitch = synthesisParameter.Pitch;
            double intonation = synthesisParameter.Intonation;
            double prePhonemeLength = synthesisParameter.PrePhonemeLength;
            double postPhonemeLength = synthesisParameter.PostPhonemeLength;
            bool enableInterrogativeUpspeak = generalSetting.useInterrogativeAutoAdjustment ?? false;

            //区切り文字設定
            List<string> charSeparators = new List<string>();
            if (generalSetting.isSplitKuten ?? false)
            {
                charSeparators.Add("。");
            }
            if (generalSetting.isSplitTouten ?? false)
            {
                charSeparators.Add("、");
            }
            if (generalSetting.isSplitNewLine ?? false)
            {
                charSeparators.Add(Environment.NewLine);
            }

            try
            {
                ulong writtenWavLength = 0;
                SPVTEXTFRAG currentTextList = pTextFragList;
                while (true)
                {
                    //不明なXMLタグが含まれていた場合、スキップ
                    if (currentTextList.State.eAction == SPVACTIONS.SPVA_ParseUnknownTag)
                    {
                        goto SetNextData;
                    }

                    //指定の範囲抽出
                    string text = currentTextList.pTextStart;
                    text = text.Substring(0, (int)currentTextList.ulTextLen);

                    SendToDebugConsole(text);

                    //分割
                    string[] splitString = charSeparators.Count == 0
                        ? new[] { text }
                        : text.Split(charSeparators.ToArray(), StringSplitOptions.RemoveEmptyEntries);

                    foreach (string str in splitString)
                    {
                        //アクションを確認し、アボートの場合は終了
                        SPVESACTIONS spveActions = (SPVESACTIONS)pOutputSite.GetActions();
                        if (spveActions.HasFlag(SPVESACTIONS.SPVES_ABORT))
                        {
                            return;
                        }

                        //SAPIイベント
                        if (generalSetting.useSspiEvent ?? false)
                        {
                            AddEventToSAPI(pOutputSite, currentTextList.pTextStart, str, writtenWavLength);
                        }

                        //英単語をカナへ置換
                        string replaceString = _engKanaDict.ReplaceEnglishToKana(str);

                        //VOICEVOXへ送信
                        //asyncメソッドにはref引数を指定できないらしいので、awaitも使用できない。awaitを使用しない実装にした。
                        Task<byte[]> waveDataTask = SendToVoiceVox(replaceString, SpeakerNumber, speed, pitch, intonation, volume, prePhonemeLength, postPhonemeLength, enableInterrogativeUpspeak);
                        byte[] waveData;
                        try
                        {
                            waveDataTask.Wait();
                            waveData = waveDataTask.Result;
                        }
                        catch (AggregateException ex) when (ex.InnerException is VoiceVoxEngineException voiceNotification)
                        {
                            //エンジンエラーを通知するかどうか
                            if (generalSetting.shouldNotifyEngineError ?? false)
                            {
                                waveData = voiceNotification.ErrorVoice;
                            }
                            else
                            {
                                waveData = Array.Empty<byte>();
                            }
                        }

                        //リサンプリング
                        using (MemoryStream stream = new MemoryStream(waveData))
                        using (WaveFileReader reader = new WaveFileReader(stream))
                        {
                            WaveFormat waveFormat = new WaveFormat((int)pWaveFormatEx.nSamplesPerSec, pWaveFormatEx.wBitsPerSample, pWaveFormatEx.nChannels);
                            using (MediaFoundationResampler resampler = new MediaFoundationResampler(reader, waveFormat))
                            {
                                //書き込み
                                writtenWavLength += OutputSiteWriteSafe(pOutputSite, resampler);
                            }
                        }
                    }

                    //次のデータを設定
                SetNextData:
                    if (currentTextList.pNext == IntPtr.Zero)
                    {
                        break;
                    }

                    currentTextList = Marshal.PtrToStructure<SPVTEXTFRAG>(currentTextList.pNext);
                }
            }
            //Task.Waitは例外をまとめてAggregateExceptionで投げる。
            catch (AggregateException ex) when (ex.InnerException is VoiceNotificationException voiceNotification)
            {
                byte[] waveData = voiceNotification.ErrorVoice;
                using (MemoryStream stream = new MemoryStream(waveData))
                using (WaveFileReader reader = new WaveFileReader(stream))
                {
                    WaveFormat waveFormat = new WaveFormat((int)pWaveFormatEx.nSamplesPerSec, pWaveFormatEx.wBitsPerSample, pWaveFormatEx.nChannels);
                    using (MediaFoundationResampler resampler = new MediaFoundationResampler(reader, waveFormat))
                    {
                        //書き込み
                        _ = OutputSiteWriteSafe(pOutputSite, resampler);
                    }
                }
            }
        }

        /// <summary>
        /// SAPI音声出力へ、安全な書き込みを行います。
        /// </summary>
        /// <param name="pOutputSite">TTSEngineSiteオブジェクト</param>
        /// <param name="waveProvider">音声データ</param>
        /// <returns>書き込んだバイト数</returns>
        private uint OutputSiteWriteSafe(ISpTTSEngineSite pOutputSite, IWaveProvider waveProvider)
        {
            uint writtenByte = 0;
            byte[] buffer = new byte[waveProvider.WaveFormat.AverageBytesPerSecond * 4];
            while (true)
            {
                int bytesRead = waveProvider.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    // end of source provider
                    break;
                }
                if (bytesRead < buffer.Length)
                {
                    Array.Resize(ref buffer, bytesRead);
                }
                writtenByte += OutputSiteWriteSafe(pOutputSite, buffer);
            }
            return writtenByte;
        }

        /// <summary>
        /// SAPI音声出力へ、安全な書き込みを行います。
        /// </summary>
        /// <param name="pOutputSite">TTSEngineSiteオブジェクト</param>
        /// <param name="data">音声データ</param>
        private uint OutputSiteWriteSafe(ISpTTSEngineSite pOutputSite, byte[] data)
        {
            if (data is null)
            {
                data = Array.Empty<byte>();
            }

            //受け取った音声データをpOutputSiteへ書き込む
            IntPtr pWavData = IntPtr.Zero;
            try
            {
                //メモリが確実に確保され、確実に代入されるためのおまじない。
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                { }
                finally
                {
                    pWavData = Marshal.AllocCoTaskMem(data.Length);
                }
                Marshal.Copy(data, 0, pWavData, data.Length);
                pOutputSite.Write(pWavData, (uint)data.Length, out uint written);
                return written;
            }
            finally
            {
                if (pWavData != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(pWavData);
                }
            }
        }

        /// <summary>
        /// SAPIへイベントを追加します。
        /// </summary>
        /// <param name="outputSite"></param>
        /// <param name="allText"></param>
        /// <param name="speakTargetText"></param>
        /// <param name="writtenWavLength"></param>
        private void AddEventToSAPI(ISpTTSEngineSite outputSite, string allText, string speakTargetText, ulong writtenWavLength)
        {
            outputSite.GetEventInterest(out ulong ulongValue);
            List<SPEVENT> spEventList = new List<SPEVENT>();
            //プラットフォームのビット数に応じて、wParamとlParamの型が異なるので、分岐
#if x64
            ulong wParam = (ulong)speakTargetText.Length;
            long lParam = allText.IndexOf(speakTargetText);
#else
            uint wParam = (uint)speakTargetText.Length;
            int lParam = allText.IndexOf(speakTargetText, StringComparison.Ordinal);
#endif
            //SPEI_SENTENCE_BOUNDARYとWORD_BOUNDARY_EVENTにのみ対応
            if ((ulongValue & SPFEI(SPEVENTENUM.SPEI_SENTENCE_BOUNDARY)) == SPFEI(SPEVENTENUM.SPEI_SENTENCE_BOUNDARY))
            {
#pragma warning disable IDE1006 // Naming Styles
                SPEVENT SENTENCE_BOUNDARY_EVENT = new SPEVENT
                {
                    eEventId = (ushort)SPEVENTENUM.SPEI_SENTENCE_BOUNDARY,
                    elParamType = (ushort)SPEVENTLPARAMTYPE.SPET_LPARAM_IS_UNDEFINED,
                    wParam = wParam,
                    lParam = lParam,
                    ullAudioStreamOffset = writtenWavLength
                };
#pragma warning restore IDE1006 // Naming Styles

                spEventList.Add(SENTENCE_BOUNDARY_EVENT);
            }
            if ((ulongValue & SPFEI(SPEVENTENUM.SPEI_WORD_BOUNDARY)) == SPFEI(SPEVENTENUM.SPEI_WORD_BOUNDARY))
            {
#pragma warning disable IDE1006 // Naming Styles
                SPEVENT WORD_BOUNDARY_EVENT = new SPEVENT
                {
                    eEventId = (ushort)SPEVENTENUM.SPEI_WORD_BOUNDARY,
                    elParamType = (ushort)SPEVENTLPARAMTYPE.SPET_LPARAM_IS_UNDEFINED,
                    wParam = wParam,
                    lParam = lParam,
                    ullAudioStreamOffset = writtenWavLength
                };
#pragma warning restore IDE1006 // Naming Styles
                spEventList.Add(WORD_BOUNDARY_EVENT);
            }
            if (spEventList.Count > 0)
            {
                SPEVENT[] spEvent = spEventList.ToArray();
                outputSite.AddEvents(ref spEvent[0], (uint)spEvent.Length);
            }
        }

        private const ushort Channels = 1;
        private const uint SamplesPerSec = 24000;
        private const ushort BitsPerSample = 16;

        /// <summary>
        /// 読み上げ指示の前に呼ばれるはず。
        /// 音声データの形式を指定する。
        /// </summary>
        /// <param name="pTargetFmtId"></param>
        /// <param name="pTargetWaveFormatEx"></param>
        /// <param name="pOutputFormatId"></param>
        /// <param name="ppCoMemOutputWaveFormatEx"></param>
        public void GetOutputFormat(ref Guid pTargetFmtId, ref WAVEFORMATEX pTargetWaveFormatEx, out Guid pOutputFormatId, IntPtr ppCoMemOutputWaveFormatEx)
        {
            //comインターフェースのラップクラス自動生成がうまく行かなかったので、unsafeでポインタを直接使用する
            unsafe
            {
                pOutputFormatId = GetSPDFIDWaveFormatEx();

                WAVEFORMATEX waveForMatex = new WAVEFORMATEX
                {
                    wFormatTag = WAVE_FORMAT_PCM,
                    nChannels = Channels,
                    cbSize = 0
                };
                try
                {
                    //所望のサンプリング周波数が指定の範囲に有るときは、そのまま使う。それ以外は24k固定。
                    waveForMatex.nSamplesPerSec = pTargetWaveFormatEx.nSamplesPerSec >= 24000 && pTargetWaveFormatEx.nSamplesPerSec <= 192000
                        ? pTargetWaveFormatEx.nSamplesPerSec
                        : SamplesPerSec;

                    //所望のビット数が16か24の場合は、そのまま。それ以外は16固定。
                    waveForMatex.wBitsPerSample = pTargetWaveFormatEx.wBitsPerSample == 16 || pTargetWaveFormatEx.wBitsPerSample == 24
                        ? pTargetWaveFormatEx.wBitsPerSample
                        : BitsPerSample;
                }
                catch (Exception)
                {
                    waveForMatex.nSamplesPerSec = SamplesPerSec;
                    waveForMatex.wBitsPerSample = BitsPerSample;
                }
                waveForMatex.nBlockAlign = (ushort)(waveForMatex.nChannels * waveForMatex.wBitsPerSample / 8);
                waveForMatex.nAvgBytesPerSec = waveForMatex.nSamplesPerSec * waveForMatex.nBlockAlign;
                IntPtr intPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf(waveForMatex));
                Marshal.StructureToPtr(waveForMatex, intPtr, false);

                WAVEFORMATEX** ppFormat = (WAVEFORMATEX**)ppCoMemOutputWaveFormatEx.ToPointer();
                *ppFormat = (WAVEFORMATEX*)intPtr.ToPointer();
            }
        }

        #region トークン関連

        /// <summary>
        /// ここでトークンを使用し、初期化を行う。
        /// </summary>
        /// <param name="pToken"></param>
        public void SetObjectToken(ISpObjectToken pToken)
        {
            Token = pToken;
            //初期化
            //話者番号を取得し、プロパティに設定。
            Token.GetDWORD(Common.RegSpeakerNumber, out uint value);
            unchecked //オーバーフローのチェックを行わず、そのまま代入。
            {
                SpeakerNumber = (int)value;
            }

            Token.GetDWORD(Common.RegPort, out value);
            Port = (int)value;
        }

        /// <summary>
        /// トークンを取得します。
        /// </summary>
        /// <param name="ppToken"></param>
        public void GetObjectToken(out ISpObjectToken ppToken)
        {
            ppToken = Token;
        }

        #endregion

        #region レジストリ関連

        private const string RegName1 = "VOICEVOX1";
        private const string RegName2 = "VOICEVOX2";

        /// <summary>
        /// レジストリ登録されるときに呼ばれます。
        /// </summary>
        /// <param name="key">よくわからん。不使用。リファレンスに書いてあったから定義しただけ。</param>
        [ComRegisterFunction]
#pragma warning disable IDE0060 // Remove unused parameter
        public static void RegisterClass(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            //四国めたん
            using (RegistryKey registryKey = Registry.LocalMachine.CreateSubKey(Common.TokensRegKey + RegName1))
            {
                Debug.Assert(registryKey != null);
                registryKey.SetValue("", "VOICEVOX 四国めたん");
                registryKey.SetValue("411", "VOICEVOX 四国めたん");
                registryKey.SetValue("CLSID", Common.CLSID.ToString(Common.RegClsidFormatString));
                registryKey.SetValue(Common.RegSpeakerNumber, 0);
            }
            using (RegistryKey registryKey = Registry.LocalMachine.CreateSubKey(Common.TokensRegKey + RegName1 + @"\" + Common.RegAttributes))
            {
                Debug.Assert(registryKey != null);
                registryKey.SetValue("Age", "Teen");
                registryKey.SetValue("Vendor", "Hiroshiba Kazuyuki");
                registryKey.SetValue("Language", "411");
                registryKey.SetValue("Gender", "Female");
                registryKey.SetValue("Name", "VOICEVOX Shikoku Metan");
            }

            //ずんだもん
            using (RegistryKey registryKey = Registry.LocalMachine.CreateSubKey(Common.TokensRegKey + RegName2))
            {
                Debug.Assert(registryKey != null);
                registryKey.SetValue("", "VOICEVOX ずんだもん");
                registryKey.SetValue("411", "VOICEVOX ずんだもん");
                registryKey.SetValue("CLSID", Common.CLSID.ToString(Common.RegClsidFormatString));
                registryKey.SetValue(Common.RegSpeakerNumber, 1);
            }
            using (RegistryKey registryKey = Registry.LocalMachine.CreateSubKey(Common.TokensRegKey + RegName2 + @"\" + Common.RegAttributes))
            {
                Debug.Assert(registryKey != null);
                registryKey.SetValue("Age", "Child");
                registryKey.SetValue("Vendor", "Hiroshiba Kazuyuki");
                registryKey.SetValue("Language", "411");
                registryKey.SetValue("Gender", "Female");
                registryKey.SetValue("Name", "VOICEVOX Zundamon");
            }
        }

        /// <summary>
        /// レジストリ解除されるときに呼ばれます。
        /// </summary>
        /// <param name="key">よくわからん。不使用。リファレンスに書いてあったから定義しただけ。</param>
        [ComUnregisterFunction]
#pragma warning disable IDE0060 // Remove unused parameter
        public static void UnregisterClass(string key)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            Common.ClearStyleFromWindowsRegistry();
        }

        #endregion

        private const string WavMediaType = "audio/wav";

        /// <summary>
        /// VOICEVOXへ音声データ作成の指示を送ります。
        /// </summary>
        /// <param name="text">セリフ</param>
        /// <param name="speakerNum">話者番号</param>
        /// <param name="speedScale">話速 0.5~2.0 中央=1</param>
        /// <param name="pitchScale">音高 -0.15~0.15 中央=0</param>
        /// <param name="intonation">抑揚 0~2 中央=1</param>
        /// <param name="volumeScale">音量 0.0~1.0</param>
        /// <param name="prePhonemeLength">開始無音</param>
        /// <param name="postPhonemeLength">終了無音</param>
        /// <param name="enableInterrogativeUpspeak">疑問形にするかどうか</param>
        /// <returns>waveデータ</returns>
        private async Task<byte[]> SendToVoiceVox(string text, int speakerNum, double speedScale, double pitchScale, double intonation, double volumeScale, double prePhonemeLength, double postPhonemeLength, bool enableInterrogativeUpspeak)
        {
            //SendToDebugConsole(text);

            //エンジンが起動中か確認を行う
            Process[] ps = Process.GetProcessesByName("run");
            if (ps.Length == 0)
            {
                throw new VoiceVoxNotFoundException();
            }

            string speakerString = speakerNum.ToString();

            //audio_queryのためのデータ
            Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "text", text },
                { "speaker", speakerString }
            };
            //データのエンコード。日本語がある場合、エンコードが必要。
            string encodedParameters = await new FormUrlEncodedContent(parameters).ReadAsStringAsync();

            try
            {
                //audio_queryを送る
                string url = $"http://127.0.0.1:{Port}/";
                using (HttpResponseMessage resultAudioQuery = await _httpClient.PostAsync($"{url}audio_query?{encodedParameters}", null))
                {
                    //戻り値を文字列にする
                    string resBodyStr = await resultAudioQuery.Content.ReadAsStringAsync();

                    //jsonの値変更
                    JObject jsonObj = JObject.Parse(resBodyStr);
                    SetValueJObjectSafe(jsonObj, "speedScale", speedScale);
                    SetValueJObjectSafe(jsonObj, "pitchScale", pitchScale);
                    SetValueJObjectSafe(jsonObj, "intonationScale", intonation);
                    SetValueJObjectSafe(jsonObj, "volumeScale", volumeScale);
                    SetValueJObjectSafe(jsonObj, "prePhonemeLength", prePhonemeLength);
                    SetValueJObjectSafe(jsonObj, "postPhonemeLength", postPhonemeLength);

                    string jsonString = JsonConvert.SerializeObject(jsonObj, Formatting.None);

                    //jsonコンテンツに変換
                    StringContent content = new StringContent(jsonString, Encoding.UTF8, @"application/json");
                    //synthesis送信
                    using (HttpResponseMessage resultSynthesis = await _httpClient.PostAsync($"{url}synthesis?speaker={speakerString}&enable_interrogative_upspeak={enableInterrogativeUpspeak}", content))
                    {
                        HttpContent httpContent = resultSynthesis.Content;

                        //戻り値をストリームで受け取る
                        Stream stream = await httpContent.ReadAsStreamAsync();
                        //byte配列に変換
                        byte[] wavData = new byte[stream.Length];
                        _ = await stream.ReadAsync(wavData, 0, (int)stream.Length);

                        // データが本当にWAVEかどうか確認
                        byte[] waveHeder = Encoding.ASCII.GetBytes("RIFF    WAVE");
                        if (wavData.Length < waveHeder.Length)
                        {
                            throw new VoiceVoxEngineException();
                        }
                        for (int i = 0; i < waveHeder.Length; i++)
                        {
                            if (3 < i && i < 8)
                            {
                                continue;
                            }

                            if (wavData[i] != waveHeder[i])
                            {
                                throw new VoiceVoxEngineException();
                            }
                        }

                        return wavData;
                    }
                }
            }
            catch (VoiceVoxEngineException)
            {
                //エンジンエラーはそのまま呼び出し元へ投げる。
                throw;
            }
            catch (Exception ex)
            {
                throw new VoiceVoxConnectionException(ex);
            }
        }

        /// <summary>
        ///  数値をある範囲から別の範囲に変換します。
        /// </summary>
        /// <param name="x">変換したい数値</param>
        /// <param name="in_min">現在の範囲の下限</param>
        /// <param name="in_max">現在の範囲の上限</param>
        /// <param name="out_min">変換後の範囲の下限</param>
        /// <param name="out_max">変換後の範囲の上限</param>
        /// <returns>変換結果</returns>
        private double Map(double x, double in_min, double in_max, double out_min, double out_max)
        {
            return ((x - in_min) * (out_max - out_min) / (in_max - in_min)) + out_min;
        }

        /// <summary>
        /// JObjectへ、プロパティの存在確認を行ってから、値を代入します。プロパティが存在しない場合は、代入されません。
        /// </summary>
        /// <param name="jObject">対象JObject</param>
        /// <param name="propertyName">プロパティ名</param>
        /// <param name="value">値</param>
        private void SetValueJObjectSafe(JObject jObject, string propertyName, double value)
        {
            if (jObject.ContainsKey(propertyName))
            {
                jObject[propertyName] = value;
            }
        }

        #region 設定データ取得関連

        /// <summary>
        /// 設定データを取得します。
        /// </summary>
        /// <param name="speakerNum">話者番号</param>
        /// <param name="generalSetting">全般設定</param>
        /// <param name="synthesisParameter">調声設定</param>
        private void GetSettingData(int speakerNum, out GeneralSetting generalSetting, out SynthesisParameter synthesisParameter)
        {
            generalSetting = ViewModel.LoadGeneralSetting();
            switch (generalSetting.synthesisSettingMode)
            {
                case SynthesisSettingMode.Batch:
                    synthesisParameter = ViewModel.LoadBatchSynthesisParameter();
                    break;
                case SynthesisSettingMode.EachCharacter:
                    List<SynthesisParameter> parameters = ViewModel.LoadSpeakerSynthesisParameter();
                    synthesisParameter = parameters.FirstOrDefault(x => x.ID == speakerNum && x.Port == Port) ?? new SynthesisParameter();
                    break;
                default:
                    synthesisParameter = new SynthesisParameter();
                    break;
            }
        }

        #endregion

        /// <summary>
        /// デバッグコンソールへテキストを送信
        /// </summary>
        /// <param name="text">送信するテキスト</param>
        private void SendToDebugConsole(string text)
        {
            // デバッグコンソールが起動中か確認して、パイプを作成
            if (Process.GetProcessesByName("SFVvConsole").Length > 0)
            {
                using (NamedPipeServerStream pipeServer = new NamedPipeServerStream(Common.PipeName))
                {
                    pipeServer.WaitForConnection();
                    using (StreamWriter writer = new StreamWriter(pipeServer))
                    {
                        writer.AutoFlush = true;
                        writer.WriteLine(text);
                    }
                }
            }
        }
    }
}
