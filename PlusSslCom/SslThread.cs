using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;

namespace PlusSslCom
{
    public class SslThread
    {
        public int BufferSize { get; set; } = Program.SmallBufferSize;
        public TcpClient Remote { get; set; }
        private NetworkStream _localStream;
        private SslStream _sslStream;
        private bool _started;
        private bool _doLogging;

        public void Start(Mapping mapping)
        {
            _started = true;

            var getDataThread = new Thread(GetDataFromRemote);
            getDataThread.Start();

            StartListener(mapping);
        }

        public void Stop()
        {
            _started = false;
        }

        private void StartListener(Mapping mapping)
        {
            TcpListener server = null;
            try
            {
                server = new TcpListener(IPAddress.Parse(mapping.FromAddress), mapping.FromPort);
                server.Start();

                Remote = new TcpClient(mapping.ToAddress, mapping.ToPort);

                var bytes = new byte[BufferSize];

                while (_started)
                {
                    Console.Write("Waiting for a connection on address {0}:{1}... ", mapping.FromAddress,
                        mapping.FromPort);

                    using (var client = server.AcceptTcpClient())
                    {
                        Console.WriteLine("Connected!");

                        _localStream = client.GetStream();

                        int bytesToWrite;

                        //Lese Eingangsmessage
                        while ((bytesToWrite = _localStream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            SendMessageToRemote(_localStream, bytes, bytesToWrite);
                        }

                        client.Close();
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            catch (Exception e)
            {

            }
            finally
            {
                server.Stop();
            }
        }

        public string SendMessageToRemote(Stream localStream, byte[] buffer, int bytesToWrite)
        {
            try
            {
                //Schreiben nach remote
                if (_doLogging)
                    Console.WriteLine("Received from local: {0}", bytesToWrite);
                if (_sslStream == null)
                {
                    _sslStream = SslHelpers.GetSecuredPlusSslStream(Remote, "");
                }
                _sslStream.Write(buffer, 0, bytesToWrite);
                _sslStream.Flush();

                //_sslStream.Flush();
                if (_doLogging)
                    Console.WriteLine("Sent to remote: {0}", bytesToWrite);
            }
            catch (ArgumentNullException e)
            {
                Console.WriteLine("ArgumentNullException: {0}", e);
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }

            return null;
        }

        private void GetDataFromRemote()
        {
            var buffer = new byte[BufferSize];

            while (_started)
            {
                if (_sslStream != null)
                {
                    var bytesTransferred = _sslStream.Read(buffer, 0, buffer.Length);
                    if (_doLogging)
                        Console.WriteLine("Received from remote: {0}", bytesTransferred);
                    _localStream.Write(buffer, 0, bytesTransferred);
                    _localStream.Flush();
                    if (_doLogging)
                        Console.WriteLine("Sent to local: {0}", bytesTransferred);
                }

                Thread.Sleep(100);
            }
        }
    }
}
