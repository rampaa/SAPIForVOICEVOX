using SFVvCommon;
using System;
using System.IO;
using System.IO.Pipes;

namespace SFVvConsole
{
    // ReSharper disable once ClassNeverInstantiated.Global
    internal class Program
    {
        // ReSharper disable once FunctionNeverReturns
        private static void Main()
        {
            while (true)
            {
                using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(Common.PipeName))
                {
                    pipeClient.Connect();
                    using (StreamReader reader = new StreamReader(pipeClient))
                    {
                        if (pipeClient.CanRead)
                        {
                            // 有効な値が読み込めるまでループ
                            string readText = null;
                            while (pipeClient.IsConnected)
                            {
                                readText = reader.ReadLine();
                                if (readText != null)
                                {
                                    break;
                                }
                            }
                            Console.WriteLine(readText);
                        }
                    }
                }
            }
        }
    }
}
