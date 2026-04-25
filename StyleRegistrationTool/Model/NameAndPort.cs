using System.Collections.Generic;
// ReSharper disable NonReadonlyMemberInGetHashCode

namespace StyleRegistrationTool.Model
{
    /// <summary>
    /// 名前とポート番号
    /// </summary>
    public sealed class NameAndPort
    {
        internal NameAndPort()
        {

        }

        internal NameAndPort(string name, int port)
        {
            Name = name ?? "";
            Port = port;
        }

        /// <summary>
        /// アプリ名
        /// </summary>
        internal string Name { get; set; } = "";

        /// <summary>
        /// ポート番号
        /// </summary>
        internal int Port { get; set; } = 50021;

        public override bool Equals(object obj)
        {
            NameAndPort port = obj as NameAndPort;
            return port != null &&
                   Name == port.Name &&
                   Port == port.Port;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = (hash * 31) + Name.GetHashCode();
            hash = (hash * 31) + Port;
            return hash;
        }

        public static bool operator ==(NameAndPort port1, NameAndPort port2) => EqualityComparer<NameAndPort>.Default.Equals(port1, port2);

        public static bool operator !=(NameAndPort port1, NameAndPort port2) => !(port1 == port2);
    }
}
