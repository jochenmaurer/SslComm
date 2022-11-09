using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Timers;

namespace PlusSslCom
{
    public class SimpleTcpCycle
    {
        public int BufferSize { get; set; } = Program.BufferSize;
        public TcpClient Remote { get; set; }
        private Timer _timer;
        private NetworkStream _localStream;
        private bool _lock;

        public SimpleTcpCycle()
        {
            _timer = new Timer(100);
            _timer.Elapsed += TimerOnElapsed;
        }

        private void TimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            GetDataFromRemote(Remote.GetStream(), _localStream);
        }

        public void Start(Mapping mapping)
        {
            TcpListener server = null;
            try
            {
                server = new TcpListener(IPAddress.Parse(mapping.FromAddress), mapping.FromPort);
                server.Start();

                Remote = new TcpClient(mapping.ToAddress, mapping.ToPort);

                var bytes = new byte[BufferSize];

                while (true)
                {
                    Console.Write("Waiting for a connection on address {0}:{1}... ", mapping.FromAddress, mapping.FromPort);

                    using (var client = server.AcceptTcpClient())
                    {
                        Console.WriteLine("Connected!");

                        _localStream = client.GetStream();

                        int bytesToWrite;

                        if (!_timer.Enabled)
                            _timer.Start();

                        //Lese Eingangsmessage
                        while ((bytesToWrite = _localStream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            SendMessageToRemote(_localStream, bytes, bytesToWrite);
                        }

                        client.Close();
                    }

                    //Thread.Sleep(100);
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                server.Stop();
            }

            Console.WriteLine("\nHit enter to continue...");
            Console.Read();
        }

        public string SendMessageToRemote(Stream localStream, byte[] buffer, int bytesToWrite)
        {
            try
            {
                //Schreiben nach remote
                _lock = true;
                Console.WriteLine("Received from local: {0}", bytesToWrite);
                var remoteStream = Remote.GetStream();
                remoteStream.Write(buffer, 0, bytesToWrite);
                remoteStream.Flush();
                Console.WriteLine("Sent to remote: {0}", bytesToWrite);
                _lock = false;
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

        private void GetDataFromRemote(NetworkStream remoteStream, Stream localStream)
        {
            if (_lock || !remoteStream.DataAvailable)
                return;

            var buffer = new byte[BufferSize];

            do
            {
                var bytesTransferred = remoteStream.Read(buffer, 0, buffer.Length);
                Console.WriteLine("Received from remote: {0}", bytesTransferred);
                localStream.Write(buffer, 0, bytesTransferred);
                localStream.Flush();
                Console.WriteLine("Sent to local: {0}", bytesTransferred);
            } while (remoteStream.DataAvailable);
        }
    }
}
