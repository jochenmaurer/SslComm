using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace PlusSslCom
{
    public class SimpleSslTransaction
    {
        public int BufferSize { get; set; } = Program.BufferSize;
        public TcpClient Remote { get; set; }
        public SslStream RemoteStream { get; set; }

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

                        var stream = client.GetStream();

                        int bytesToWrite;

                        //Lese Eingangsmessage
                        while ((bytesToWrite = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            SendMessageAndReceiveReplies(stream, bytes, bytesToWrite);
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

        private static SslStream GetSecuredStream(TcpClient client, string authName)
        {
            var sslStream = new SslStream(
                client.GetStream(),
                false,
                new RemoteCertificateValidationCallback(ValidateServerCertificate),
                null
            );
            // The server name must match the name on the server certificate.
            try
            {
                sslStream.AuthenticateAsClient(authName);
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

        public void SendMessageAndReceiveReplies(Stream localStream, byte[] buffer, int bytesToWrite)
        {
            try
            {
                //Schreiben nach remote
                Console.WriteLine("Received from local: {0}", bytesToWrite);
                if (RemoteStream == null)
                    RemoteStream = GetSecuredStream(Remote, "");

                RemoteStream.Write(buffer);
                RemoteStream.Flush();
                Console.WriteLine("Sent to remote: {0}", bytesToWrite);


                //Lesen und zurück
                buffer = new byte[BufferSize];
                if (RemoteStream.CanRead)
                {
                    int bytesTransferred;
                    do
                    {
                        bytesTransferred = RemoteStream.Read(buffer, 0, buffer.Length);
                        Console.WriteLine("Received from remote: {0}", bytesTransferred);
                        localStream.Write(buffer, 0, bytesTransferred);
                        Console.WriteLine("Sent to local: {0}", bytesTransferred);
                    } while (buffer[bytesTransferred - 1].ToString() != "0");
                }
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

        public static bool ValidateServerCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
