using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace PlusSslComm.Old
{
    public class SimpleSslCycleOld
    {
        public int BufferSize { get; set; } = Program.BufferSize;


        private string _address;
        private int _port;

        private Timer _timer;
        private NetworkStream _localStream;
        private SslStream _remoteStream;

        public SimpleSslCycleOld()
        {
            _timer = new Timer(2000);
            _timer.Elapsed += TimerOnElapsed;
        }

        private void TimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            var stream = _localStream;
            GetDataFromRemote(stream);
        }

        public void Start(Mapping mapping)
        {
            _address = mapping.ToAddress;
            _port = mapping.ToPort;

            TcpListener server = null;
            try
            {
                server = new TcpListener(IPAddress.Parse(mapping.FromAddress), mapping.FromPort);
                server.Start();
                
                var bytes = new byte[BufferSize];

                while (true)
                {
                    Console.Write("Waiting for a connection on address {0}:{1}... ", mapping.FromAddress, mapping.FromPort);

                    _timer.Start();

                    var client = server.AcceptTcpClient();
                
                    Console.WriteLine("Connected!");

                    _localStream = client.GetStream();

                    int bytesToWrite;

                    //Lese Eingangsmessage
                    while ((bytesToWrite = _localStream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        SendMessageToRemote(_localStream, bytes, bytesToWrite);
                    }

                    //client.Close();

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

            _timer.Enabled = false;

            Console.WriteLine("\nHit enter to continue...");
            Console.Read();
        }

        public string SendMessageToRemote(Stream localStream, byte[] buffer, int bytesToWrite)
        {
            Console.WriteLine("SendMessageToRemote");
            try
            {
                Console.WriteLine("Received from local: {0}", bytesToWrite);
                if (_remoteStream == null)
                    _remoteStream = SslHelpers.GetSecuredStream(new TcpClient(_address, _port), "");
                _remoteStream.Write(buffer, 0, bytesToWrite);
                _remoteStream.Flush();

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

        private void GetDataFromRemote(Stream localStream)
        {
            bool result;

            do
            {
                result = Task.Run(async () => await GetDataFromRemoteAsync(localStream)).Wait(200);
            } while (!result);
        }

        private async Task<bool> GetDataFromRemoteAsync(Stream localStream)
        {
            if (localStream == null)
                return false;

            Console.WriteLine("GetDataFromRemoteAsync");
            try
            {
                var buffer = new byte[BufferSize];
                if (_remoteStream == null)
                {
                    var client = new TcpClient(_address, _port);
                    _remoteStream = SslHelpers.GetSecuredStream(client, "");
                }
                
                var bytesTransferred = await _remoteStream.ReadAsync(buffer, 0, buffer.Length);
                Console.WriteLine("Received from remote: {0}", bytesTransferred);
                await localStream.WriteAsync(buffer, 0, bytesTransferred);
                await localStream.FlushAsync();
                Console.WriteLine("Sent to local: {0}", bytesTransferred);
                
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}
