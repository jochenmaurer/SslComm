using System;
using System.Collections.Generic;
using System.IO;
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
        public int InputBufferSize { get; set; } = Program.BufferSize;
        public int OutputBufferSize { get; set; } = Program.SmallBufferSize;
        //private NetworkStream _localStream;
        private TcpListener _listener;
        private TcpClient _localClient;
        private Thread _thrListener;
        private string _localIp;
        private int _localPort;
        private bool _started;
        private bool _doLogging;
        private readonly List<LocalToRemote> _openConnections = new List<LocalToRemote>();
        private readonly Dictionary<string, ConnectionParameters> _connectionParametersMap;
        private const string _relativeCccomGlobal = @"..\Config\CCCommGlobal.cfg";
        private const string _fallbackCccomGlobal = @"C:\Program Files\PlusClient\Global\Config\CCCommGlobal.cfg";

        public MultiSslThreads()
        {
            _doLogging = true;
            var cccomGlobal = _relativeCccomGlobal;
            if (!File.Exists(cccomGlobal))
            {
                cccomGlobal = _fallbackCccomGlobal;
            }

            _connectionParametersMap = new ConfigHandling().GetConnectionParametersMap(cccomGlobal);
        }

        public void Start(string localIp, int localPort)
        {
            _started = true;
            _localIp = localIp;
            _localPort = localPort;
            StartListener();
        }

        public void Stop()
        {
            foreach (var openConnection in _openConnections)
            {
                openConnection.Listener.Stop();
            }
            _started = false;
        }

        private void StartListener()
        {
            while (true)
            {
                try
                {
                    _listener = new TcpListener(IPAddress.Parse(_localIp), _localPort);
                    _listener.Start();

                    _thrListener = new Thread(Listen);
                    _thrListener.Start();
                    _thrListener.Join();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Other exception: {0}", e);
                }
                finally
                {
                    _localClient?.Dispose();
                    _listener?.Stop();
                }

                if (!_started)
                    break;
            }
        }

        private void Listen()
        {
            var lastAddress = "";
            var lastPort = 0;
            var bytes = new byte[InputBufferSize];

            while (true)
            {
                try
                {
                    Console.Write("Waiting for a connection on address {0}:{1}... ", _localIp,
                    _localPort);

                    _localClient = _listener.AcceptTcpClient();

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
                        if (bufferContent.Contains(";") && bufferContent.StartsWith("{SSL:"))
                        {
                            var bufferParts = bufferContent.Split(';');
                            if (_connectionParametersMap.TryGetValue(GetBaseConnection(GetApplicationSystem(bufferParts[0])),
                                    out var connectionParameters))
                            {
                                address = connectionParameters.ProtocolParameters["SslRemoteHost"].ToString();
                                port = int.Parse(
                                    connectionParameters.ProtocolParameters["SslRemotePort"].ToString());
                                lastAddress = address;
                                lastPort = port;
                                encoding = connectionParameters.ConnectionParameter["Encoding"].ToString();
                                buffer = Encoding.GetEncoding(encoding).GetBytes(bufferParts[1]);
                                bytesToWrite = bytesToWrite - bufferParts[0].Length - 1;

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
                catch (IOException e)
                {
                    Console.WriteLine("IOException: {0}", e);
                    break;
                }
            }
        }

        private static string GetApplicationSystem(string bufferPart)
        {
            var result = bufferPart;
            var regex = new Regex("(^\\{SSL:)([a-zA-Z0-9_-]*)(\\})");
            var match = regex.Match(bufferPart);
            if (match.Success && match.Groups.Count > 0)
                result = match.Groups[2].Value;

            return result;
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
                    BufferSize = OutputBufferSize,
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
                var chunks = Split(buffer, bytesToWrite, OutputBufferSize);
                foreach (var chunk in chunks)
                {
                    sslStream.Write(chunk, 0, chunk.Length);
                }

                sslStream.Flush();

                if (_doLogging)
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

        public static IEnumerable<byte[]> Split(byte[] value, int inputBufferLength, int outputBufferLength)
        {
            var countOfArray = inputBufferLength / outputBufferLength;
            if (inputBufferLength % outputBufferLength > 0)
                countOfArray++;
            for (var i = 0; i < countOfArray; i++)
            {
                yield return value.Skip(i * outputBufferLength).Take(outputBufferLength).ToArray();
            }
        }
    }

    public class LocalToRemote
    {
        public string FullAddress { get; set; }

        public SslStream RemoteStream { get; set; }

        public SslRemoteListener Listener { get; set; }
    }
}