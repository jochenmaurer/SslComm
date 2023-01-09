using System;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;

namespace PlusSslComm
{
    public class SslRemoteListener
    {
        public SslStream RemoteStream { get; set; }
        public TcpClient Client { get; set; }
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
            while (_started)
            {
                if (RemoteStream != null && Client.GetStream().CanWrite)
                {
                    var buffer = new byte[BufferSize];
                    var bytesTransferred = RemoteStream.Read(buffer, 0, buffer.Length);
                    if (DoLogging)
                        Console.WriteLine("Received from remote: {0}", bytesTransferred);

                    var localBuffer = buffer.Take(bytesTransferred).ToArray();

                    if (bytesTransferred < BufferSize && localBuffer.Last() != 0)
                    {
                        var newBuffer = new byte[bytesTransferred + 1];
                        Buffer.BlockCopy(localBuffer, 0, newBuffer, 0, localBuffer.Length);
                        localBuffer = newBuffer;
                        bytesTransferred = localBuffer.Length;
                    }


                    Client.GetStream().Write(localBuffer, 0, bytesTransferred);
                    Client.GetStream().Flush();
                    if (DoLogging)
                        Console.WriteLine("Sent to local: {0}", bytesTransferred);
                }
            }
        }
    }
}
