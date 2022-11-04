using System.Net;
using System.Net.Security;
using System.Net.Sockets;

namespace PlusSslCom
{
    public class Mapping
    {
        private TcpListener _listener;
        private TcpClient _target;

        public string Name { get; set; }

        public string FromAddress { get; set; } = "127.0.0.1";

        public int FromPort { get; set; }

        public string ToAddress { get; set; }

        public int ToPort { get; set; }

        public bool UseSsl { get; set; }

        public string Encoding { get; set; }

        public TcpListener Listener
        {
            get
            {
                if (_listener == null)
                {
                    _listener = new TcpListener(IPAddress.Parse(FromAddress), FromPort);
                }
                return _listener;
            }
            set { _listener = value; }
        }

        public TcpClient Source { get; set; }

        public TcpClient Target
        {
            get
            {
                if (_target == null)
                {
                    _target = new TcpClient(ToAddress, ToPort);
                }

                return _target;
            }
            set { _target = value; }
        }

        public NetworkStream TargetStream { get; set; }

        public SslStream TargetSslStream { get; set; }
    }
}
