using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace PlusSslCom
{
    public class MultiSslThreads
    {
        public int BufferSize { get; set; } = Program.MediumBufferSize;
        //private NetworkStream _localStream;
        private TcpClient _localClient;
        private bool _started;
        private bool _doLogging;
        private readonly List<LocalToRemote> _openConnections = new List<LocalToRemote>();

        public void Start(Mapping mapping)
        {
            _started = true;

            StartListener(mapping);
        }

        public void Stop()
        {
            foreach (var openConnection in _openConnections)
            {
                openConnection.Listener.Stop();
            }
            _started = false;
        }

        private void StartListener(Mapping mapping)
        {
            TcpListener server = null;
            try
            {
                server = new TcpListener(IPAddress.Parse(mapping.FromAddress), mapping.FromPort);
                server.Start();

                var bytes = new byte[BufferSize];

                while (_started)
                {
                    Console.Write("Waiting for a connection on address {0}:{1}... ", mapping.FromAddress,
                        mapping.FromPort);

                    _localClient = server.AcceptTcpClient();
                    
                    Console.WriteLine("Connected!");

                    int bytesToWrite;

                    //Lese Eingangsmessage
                    while ((bytesToWrite = _localClient.GetStream().Read(bytes, 0, bytes.Length)) != 0)
                    {
                        var address = mapping.ToAddress;
                        var port = mapping.ToPort;
                        var buffer = bytes;

                        var bufferContent = Encoding.GetEncoding("Windows-1252").GetString(bytes);
                        if (bufferContent.Contains(";"))
                        {
                            var bufferParts = bufferContent.Split(';');
                            if (bufferParts.Length > 0)
                            {
                                var ipAndPort = bufferParts[0].Split(':');
                                if (ipAndPort.Length > 0)
                                {
                                    address = ipAndPort[0];
                                    port = int.Parse(ipAndPort[1]);
                                    buffer = Encoding.GetEncoding("Windows-1252").GetBytes(bufferParts[1]);
                                    bytesToWrite = buffer.Length;
                                }
                            }
                        }

                        SendMessageToRemote(address, port, buffer, bytesToWrite);
                    }
                    
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            catch (Exception e)
            {
                Console.WriteLine("Other exception: {0}", e);
            }
            finally
            {
                _localClient.Dispose();
                server?.Stop();
            }
        }

        private SslStream GetSslStreamFromDictionary(string mappingToAddress, int mappingToPort)
        {
            var fullAddress = mappingToAddress + ":" + mappingToPort;
            var localToRemote = _openConnections.FirstOrDefault(t => t.FullAddress == fullAddress);
            if (localToRemote == null)
            {
                localToRemote = new LocalToRemote
                {
                    FullAddress = fullAddress,
                    RemoteStream = SslHelpers.GetSecuredStream(new TcpClient(mappingToAddress, mappingToPort), ""),
                };

                localToRemote.Listener = new SslRemoteListener()
                {
                    BufferSize = BufferSize,
                    LocalStream = _localClient.GetStream(),
                    RemoteStream = localToRemote.RemoteStream,
                    DoLogging = _doLogging
                };

                _openConnections.Add(localToRemote);

                var getDataThread = new Thread(localToRemote.Listener.Start);
                getDataThread.Start();
            }

            return _openConnections.FirstOrDefault(t => t.FullAddress == fullAddress)?.RemoteStream;
        }

        private void SendMessageToRemote(string mappingToAddress, int mappingToPort,
            byte[] buffer, int bytesToWrite)
        {
            try
            {
                //Schreiben nach remote
                if (_doLogging)
                    Console.WriteLine("Received from local: {0}", bytesToWrite);
                var sslStream = GetSslStreamFromDictionary(mappingToAddress, mappingToPort);
                sslStream.Write(buffer, 0, bytesToWrite);
                sslStream.Flush();

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
        }

        public class LocalToRemote
        {
            public string FullAddress { get; set; }

            public SslStream RemoteStream { get; set; }

            public SslRemoteListener Listener { get; set; }
        }
    }
}