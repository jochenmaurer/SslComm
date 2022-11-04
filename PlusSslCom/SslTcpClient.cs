using System;
using System.Collections;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace PlusSslCom
{
    public static class SslTcpClient
    {
        private static Hashtable certificateErrors = new Hashtable();

        // The following method is invoked by the RemoteCertificateValidationDelegate.
        public static bool ValidateServerCertificate(
              object sender,
              X509Certificate certificate,
              X509Chain chain,
              SslPolicyErrors sslPolicyErrors)
        {
            //if (sslPolicyErrors == SslPolicyErrors.None) { return true; }

            //if (sslPolicyErrors.ToString() ==
            //    "SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors")
            //    return true;


            Console.WriteLine("*** SSL Error: " + sslPolicyErrors.ToString());
            return true;
            //return false;
        }

        public static string RunClient(string machineName, int port, string serverName, string message)
        {
            // Create a TCP/IP client socket.
            // machineName is the host running the server application.
            using (var client = new TcpClient(machineName, port))
            {
                Console.WriteLine("Client connected.");
                // Create an SSL stream that will close the client's stream.
                var sslStream = new SslStream(
                    client.GetStream(),
                    false,
                    new RemoteCertificateValidationCallback(ValidateServerCertificate),
                    null
                );
                // The server name must match the name on the server certificate.
                try
                {
                    sslStream.AuthenticateAsClient(serverName);
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

                // Encode a test message into a byte array.
                // Signal the end of the message using the "<EOF>".
                var bytes = Encoding.GetEncoding(Program.Encoding).GetBytes(message);
                // Send hello message to the server.
                sslStream.Write(bytes);
                sslStream.Flush();
                // Read message from the server.
                var serverMessage = ReadMessage(sslStream);
                Console.WriteLine("Server says: {0}", serverMessage);
                // Close the client connection.
                Console.WriteLine("Client closed.");
                return serverMessage;
            }
        }

        public static string ReadMessage(SslStream sslStream)
        {
            // Read the  message sent by the server.
            // The end of the message is signaled using the
            // "<EOF>" marker.
            var buffer = new byte[Program.BufferSize];
            var messageData = new StringBuilder();
            var bytes = -1;
            //do
            //{
            bytes = sslStream.Read(buffer, 0, buffer.Length);

            // Use Decoder class to convert from bytes to UTF8
            // in case a character spans two buffers.
            var decoder = Encoding.GetEncoding(Program.Encoding).GetDecoder();
            var chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
            decoder.GetChars(buffer, 0, bytes, chars, 0);
            messageData.Append(chars);
            // Check for EOF.
            //    if (messageData.ToString().IndexOf("<EOF>") != -1)
            //    {
            //        break;
            //    }
            //} while (bytes != 0);

            return messageData.ToString();
        }
    }
}