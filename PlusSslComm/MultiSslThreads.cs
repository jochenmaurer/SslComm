﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace PlusSslComm
{
    public class MultiSslThreads
    {
        public int BufferSize { get; set; } = Program.SmallBufferSize;
        //private NetworkStream _localStream;
        private TcpClient _localClient;
        private bool _started;
        private bool _doLogging;
        private readonly List<LocalToRemote> _openConnections = new List<LocalToRemote>();
        private readonly Dictionary<string, ConnectionParameters> _connectionParametersMap;

        public MultiSslThreads()
        {
            _doLogging = true;
            _connectionParametersMap = new ConfigHandling().GetConnectionParametersMap(
                @"..\Config\CCCommGlobal.cfg");
        }

        public void Start(string localIp, int localPort)
        {
            _started = true;

            StartListener(localIp, localPort);
        }

        public void Stop()
        {
            foreach (var openConnection in _openConnections)
            {
                openConnection.Listener.Stop();
            }
            _started = false;
        }

        private void StartListener(string localIp, int localPort)
        {
            var lastAddress = "";
            var lastPort = 0;

            TcpListener server = null;
            try
            {
                server = new TcpListener(IPAddress.Parse(localIp), localPort);
                server.Start();

                var bytes = new byte[BufferSize];

                while (_started)
                {
                    Console.Write("Waiting for a connection on address {0}:{1}... ", localIp,
                        localPort);

                    _localClient = server.AcceptTcpClient();

                    Console.WriteLine("Connected!");

                    int bytesToWrite;

                    //Lese Eingangsmessage
                    while ((bytesToWrite = _localClient.GetStream().Read(bytes, 0, bytes.Length)) != 0)
                    {
                        var address = "";
                        var port = 0;
                        var encoding = "";
                        var buffer = bytes;

                        var bufferContent = Encoding.ASCII.GetString(bytes);
                        if (bufferContent.Contains(";"))
                        {
                            var bufferParts = bufferContent.Split(';');
                            if (_connectionParametersMap.TryGetValue(GetBaseConnection(bufferParts[0]),
                                    out var connectionParameters))
                            {
                                address = connectionParameters.ProtocolParameters["SslRemoteHost"].ToString();
                                port = int.Parse(
                                    connectionParameters.ProtocolParameters["SslRemotePort"].ToString());
                                lastAddress = address;
                                lastPort = port;
                                encoding = connectionParameters.ConnectionParameter["Encoding"].ToString();
                                buffer = Encoding.GetEncoding(encoding).GetBytes(bufferParts[1]);
                                bytesToWrite = buffer.Length;

                                SendMessageToRemote(address, port, buffer, bytesToWrite);
                            }
                            else
                            {
                                Console.Out.WriteLine("Application system not in config");
                            }
                        }
                        else
                        {
                            Console.Out.WriteLine("No application system. Send it to the last one.");
                            SendMessageToRemote(lastAddress, lastPort, buffer, bytesToWrite);
                        }
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

        private static string GetBaseConnection(string connectionName)
        {
            var telConnection = connectionName;
            var regex = new Regex("(_[0-9]*$)");
            var match = regex.Match(telConnection);
            if (match.Success && match.Groups.Count > 0)
                telConnection = telConnection.Remove(telConnection.Length - match.Value.Length, match.Value.Length);

            return telConnection;
        }

        private SslStream GetSslStreamFromDictionary(string mappingToAddress, int mappingToPort)
        {
            var fullAddress = mappingToAddress + ":" + mappingToPort;
            var localToRemote = _openConnections.FirstOrDefault(t => t.FullAddress == fullAddress);
            if (localToRemote == null)
            {
                Console.Out.WriteLine("Connect to remote system {0}", fullAddress);
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