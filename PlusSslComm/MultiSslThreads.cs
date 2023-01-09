using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Dcx.Plus.Infrastructure;

namespace PlusSslComm
{
    public class MultiSslThreads
    {
        #region Properties

        public int InputBufferSize { get; set; } = 32768;
        public int OutputBufferSize { get; set; } = 1024;

        public TcpClient LocalClient { get; set; }

        public bool MessageContainsApplicationSystem { get; set; }

        public CccomConnectionInformation BaseConnectionInformation { get; set; }

        #endregion
        
        #region Members/Constants

        //private NetworkStream _localStream;
        private TcpListener _listener;
        private Thread _threadListenLocal;
        private string _localIp;
        private int _localPort;
        private bool _started;
        private bool _localListenerStarted;
        private bool _doLogging;
        //private bool _locked;
        private readonly List<LocalToRemote> _openConnections = new List<LocalToRemote>();
        private readonly Dictionary<string, ConnectionParameters> _connectionParametersMap;
        private const string RelativeCccomGlobal = @"..\Config\CCCommGlobal.cfg";
        private const string FallbackCccomGlobal = @"C:\Program Files\PlusClient\Global\Config\CCCommGlobal.cfg";
        private const int ApplicationSystemLength = 40;

        #endregion

        #region Construction

        public MultiSslThreads()
        {
            _doLogging = true;
            var cccomGlobal = RelativeCccomGlobal;
            if (!File.Exists(cccomGlobal))
            {
                cccomGlobal = FallbackCccomGlobal;
            }

            Console.WriteLine("CCCom config taken from: {0}", cccomGlobal);

            _connectionParametersMap = new ConfigHandling().GetConnectionParametersMap(cccomGlobal);
        }

        #endregion

        #region Start/Listening

        public void Start(string localIp, int localPort)
        {
            _started = true;
            _localIp = localIp;
            _localPort = localPort;
            StartListener();
        }

        private void StartListener()
        {
            while (true)
            {
                try
                {
                    _openConnections.Clear();
                    _listener = new TcpListener(IPAddress.Parse(_localIp), _localPort);
                    _listener.Start();

                    _localListenerStarted = true;
                    _threadListenLocal = new Thread(ListenLocal);
                    _threadListenLocal.Start();
                    _threadListenLocal.Join();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Other exception: {0}", e);
                }
                finally
                {
                    _localListenerStarted = false;
                    foreach (var openConnection in _openConnections)
                    {
                        openConnection.IsActive = false;
                    }

                    Thread.Sleep(100);
                    _threadListenLocal = null;
                    LocalClient.Dispose();
                    _listener?.Stop();
                    _listener = null;
                }

                if (!_started)
                    break;
            }
        }

        private void ListenLocal()
        {
            var inputBufferSize = InputBufferSize;

            if (MessageContainsApplicationSystem)
                inputBufferSize += ApplicationSystemLength;

            var bytes = new byte[inputBufferSize];

            try
            {

                while (_localListenerStarted)
                {
                    Console.Write("Waiting for a connection on address {0}:{1}... ", _localIp,
                        _localPort);

                    LocalClient = _listener.AcceptTcpClient();

                    Console.WriteLine("Connected!");

                    int bytesToWrite;

                    //Lese Eingangsmessage
                    while ((bytesToWrite = LocalClient.GetStream().Read(bytes, 0, bytes.Length)) != 0)
                    {
                        HandleIncomingLocalMessage(bytes, bytesToWrite);
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

        #endregion

        #region Message handling local to remote

        private void HandleIncomingLocalMessage(byte[] bytes, int bytesToWrite)
        {
            if (MessageContainsApplicationSystem)
            {
                var appSystem = GetApplicationSystem(bytes.Take(ApplicationSystemLength).ToArray());
                if (GetConnectionInformation(_connectionParametersMap, appSystem) is CccomConnectionInformation information)
                {
                    var buffer = bytes.Skip(ApplicationSystemLength).Take(bytesToWrite - ApplicationSystemLength)
                        .ToArray();
                    bytesToWrite -= ApplicationSystemLength;

                    SendMessageToRemote(information, buffer, bytesToWrite);
                }
                else
                {
                    Console.Out.WriteLine("Application {0} system not in config", appSystem);
                }
            }
            else
            {
                SendMessageToRemote(BaseConnectionInformation, bytes, bytesToWrite);
            }
        }

        private void SendMessageToRemote(CccomConnectionInformation information, byte[] buffer, int bytesToWrite)
        {
            try
            {
                var encoding = Encoding.GetEncoding(information.Encoding);
                var message = PlusEncoding.GetString(encoding, buffer, bytesToWrite);
                var newBuffer = PlusEncoding.GetBytes(encoding, message, ref bytesToWrite);


                //Schreiben nach remote
                if (_doLogging)
                    Console.WriteLine("Received from local: {0}", bytesToWrite);
                var sslStream = GetSslStreamFromDictionary(information.ToAddress, information.ToPort);
                var chunks = Split(newBuffer, bytesToWrite, InputBufferSize);
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

        #endregion

        #region Message handling remote to local

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
                    RemoteStream = GetSecuredStream(new TcpClient(mappingToAddress, mappingToPort), ""),
                    BufferSize = OutputBufferSize,
                    IsActive = true
                };

                localToRemote.Listener = new Thread(() => HandleIncomingRemoteMessage(localToRemote));

                _openConnections.Add(localToRemote);

                localToRemote.Listener.Start();
            }

            return _openConnections.FirstOrDefault(t => t.FullAddress == fullAddress)?.RemoteStream;
        }

        private void HandleIncomingRemoteMessage(LocalToRemote localToRemote)
        {
            while (localToRemote.IsActive)
            {
                if (localToRemote.RemoteStream != null && LocalClient.GetStream().CanWrite)
                {
                    var buffer = new byte[localToRemote.BufferSize];
                    var bytesTransferred = localToRemote.RemoteStream.Read(buffer, 0, buffer.Length);
                    if (_doLogging)
                        Console.WriteLine("Received from remote: {0}", bytesTransferred);

                    var localBuffer = buffer.Take(bytesTransferred).ToArray();

                    if (bytesTransferred < localToRemote.BufferSize && localBuffer.Last() != 0)
                    {
                        var newBuffer = new byte[bytesTransferred + 1];
                        Buffer.BlockCopy(localBuffer, 0, newBuffer, 0, localBuffer.Length);
                        localBuffer = newBuffer;
                        bytesTransferred = localBuffer.Length;
                    }


                    LocalClient.GetStream().Write(localBuffer, 0, bytesTransferred);
                    LocalClient.GetStream().Flush();
                    if (_doLogging)
                        Console.WriteLine("Sent to local: {0}", bytesTransferred);
                }
            }

            localToRemote.RemoteStream?.Close();
        }

        #endregion



        #region Helpers

        public static SslStream GetSecuredStream(TcpClient client, string authName)
        {
            var sslStream = new SslStream(
                client.GetStream(),
                false,
                new RemoteCertificateValidationCallback(ValidateServerCertificate),
                null
            );// The server name must match the name on the server certificate.
            try
            {
                //sslStream.ReadTimeout = 50;

                sslStream.AuthenticateAsClient("TEST Server");
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                }

                Console.WriteLine("Authentication failed - closing the connection.");
                return null;
            }

            return sslStream;
        }

        private static bool ValidateServerCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None) { return true; }

            if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors)
                return true;

            Console.WriteLine("*** SSL Error: " + sslPolicyErrors.ToString());
            return false;
        }

        private static string GetApplicationSystem(byte[] bufferPart)
        {
            return Encoding.ASCII.GetString(bufferPart).Trim();
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

        public static IEnumerable<byte[]> Split(byte[] value, int inputBufferLength, int outputBufferLength)
        {
            var totalBytesToWrite = inputBufferLength;
            var countOfArray = inputBufferLength / outputBufferLength;
            if (inputBufferLength % outputBufferLength > 0)
                countOfArray++;
            for (var i = 0; i < countOfArray; i++)
            {
                if (totalBytesToWrite <= outputBufferLength)
                {
                    outputBufferLength = totalBytesToWrite;
                }
                totalBytesToWrite -= outputBufferLength;
                yield return value.Skip(i * outputBufferLength).Take(outputBufferLength).ToArray();
            }
        }

        public static CccomConnectionInformation GetConnectionInformation(Dictionary<string, ConnectionParameters> connectionParametersMap, string appSystem)
        {
            if (connectionParametersMap.TryGetValue(GetBaseConnection(appSystem),
                    out var connectionParameters))
            {
                var result = new CccomConnectionInformation
                {
                    FromAddress = connectionParameters.ProtocolParameters["RemoteHost"].ToString(),
                    FromPort = int.Parse(
                        connectionParameters.ProtocolParameters["RemotePort"].ToString()),

                    ToAddress = connectionParameters.ProtocolParameters["SslRemoteHost"].ToString(),
                    ToPort = int.Parse(
                        connectionParameters.ProtocolParameters["SslRemotePort"].ToString()),

                    Encoding = connectionParameters.ConnectionParameter["Encoding"].ToString()
                };

                return result;
            }

            return null;
        }

        #endregion
    }

    public class LocalToRemote
    {
        public string FullAddress { get; set; }

        public SslStream RemoteStream { get; set; }

        public int BufferSize { get; set; }

        public Thread Listener { get; set; }

        public bool IsActive { get; set; }
    }

    public class CccomConnectionInformation
    {
        public string ToAddress { get; set; }

        public int ToPort { get; set; }

        public string FromAddress { get; set; }

        public int FromPort { get; set; }

        public string Encoding { get; set; }
    }
}