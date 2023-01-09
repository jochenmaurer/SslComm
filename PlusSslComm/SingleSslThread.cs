using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace PlusSslComm
{
    public class SingleSslThread
    {
        private Thread _remoteListenerThread;
        private Thread _localListenerThread;
        private bool _running = true;

        public SslStream SslStream { get; private set; }

        public string LocalAddress { get; set; }

        public int LocalPort { get; set; }

        public string RemoteAddress { get; set; }

        public int RemotePort { get; set; }

        public TcpClient LocalClient { get; private set; }

        public TcpClient RemoteClient { get; private set; }

        public TcpListener LocalListener { get; private set; }

        public int InputBufferSize { get; set; } = 32768;
        public int OutputBufferSize { get; set; } = 1024;

        public void Start()
        {
            LocalListener = new TcpListener(IPAddress.Parse(LocalAddress), LocalPort);
            LocalListener.Start();
            Console.Out.WriteLine("Local tcp listener started");

            RemoteClient = new TcpClient(RemoteAddress, RemotePort);
            SslStream = MultiSslThreads.GetSecuredStream(RemoteClient, "");

            _remoteListenerThread = new Thread(HandleIncomingRemoteMessage);

            _localListenerThread = new Thread(ListenLocal);
            _localListenerThread.Start();
            Console.Out.WriteLine("Local listener thread started");
            _localListenerThread.Join();
        }
        
        public void Stop()
        {
            _running = false;

            Thread.Sleep(100);

            if (_remoteListenerThread.IsAlive)
                _remoteListenerThread.Abort();
            if (_localListenerThread.IsAlive)
                _localListenerThread.Abort();

            SslStream.Close();
            RemoteClient.Close();
            RemoteClient.Dispose();
            LocalListener.Stop();
        }

        private void ListenLocal()
        {
            var bytes = new byte[InputBufferSize];

            try
            {
                while (_running)
                {
                    Console.Write("Waiting for a connection on address {0}:{1}... ", LocalAddress,
                        LocalPort);

                    LocalClient = LocalListener.AcceptTcpClient();

                    _remoteListenerThread.Start();
                    Console.Out.WriteLine("Remote listener thread started");

                    Console.WriteLine("Connected!");

                    int bytesToWrite;

                    //Lese Eingangsmessage
                    while ((bytesToWrite = LocalClient.GetStream().Read(bytes, 0, bytes.Length)) != 0)
                    {
                        SendMessageToRemote(bytes, bytesToWrite);
                    }
                }
            }
            catch (IOException e)
            {
                Console.WriteLine("IOException: {0}", e);
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                LocalClient.Close();
                LocalClient.Dispose();
            }
        }

        private void SendMessageToRemote(byte[] buffer, int bytesToWrite)
        {
            try
            {
                //Schreiben nach remote
                Console.WriteLine("Received from local: {0}", bytesToWrite);
                var chunks = MultiSslThreads.Split(buffer, bytesToWrite, InputBufferSize);
                foreach (var chunk in chunks)
                {
                    SslStream.Write(chunk, 0, chunk.Length);
                }

                SslStream.Flush();

                Console.WriteLine("Sent {0} chunks to remote: {1}", chunks.Count(), bytesToWrite);
            }
            catch (ArgumentNullException e)
            {
                Console.WriteLine("ArgumentNullException: {0}", e);
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
        }

        private void HandleIncomingRemoteMessage()
        {
            while (_running)
            {
                var buffer = new byte[OutputBufferSize];
                var bytesTransferred = SslStream.Read(buffer, 0, buffer.Length);
                Console.WriteLine("Received from remote: {0}", bytesTransferred);

                var localBuffer = buffer.Take(bytesTransferred).ToArray();

                if (bytesTransferred < OutputBufferSize && localBuffer.Last() != 0)
                {
                    var newBuffer = new byte[bytesTransferred + 1];
                    Buffer.BlockCopy(localBuffer, 0, newBuffer, 0, localBuffer.Length);
                    localBuffer = newBuffer;
                    bytesTransferred = localBuffer.Length;
                }


                LocalClient.GetStream().Write(localBuffer, 0, bytesTransferred);
                LocalClient.GetStream().Flush();
                Console.WriteLine("Sent to local: {0}", bytesTransferred);
            }
        }
    }
}
