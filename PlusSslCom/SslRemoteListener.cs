using System;
using System.Net.Security;
using System.Net.Sockets;

namespace PlusSslCom
{
    public class SslRemoteListener
    {
        public NetworkStream LocalStream { get; set; }
        public SslStream RemoteStream { get; set; }
        
        public int BufferSize { get; set; }
        public bool DoLogging { get; set; }

        private bool _started;

        public void Start()
        {
            _started = true;
            GetDataFromRemote();
        }

        public void Stop()
        {
            _started = false;
        }
        
        private void GetDataFromRemote()
        {
            var buffer = new byte[BufferSize];

            while (_started)
            {
                if (RemoteStream != null && LocalStream.CanWrite)
                {
                    var bytesTransferred = RemoteStream.Read(buffer, 0, buffer.Length);
                    if (DoLogging)
                        Console.WriteLine("Received from remote: {0}", bytesTransferred);
                    LocalStream.Write(buffer, 0, bytesTransferred);
                    LocalStream.Flush();
                    if (DoLogging)
                        Console.WriteLine("Sent to local: {0}", bytesTransferred);
                }
            }
        }
    }
}
