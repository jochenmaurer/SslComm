using System;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

namespace PlusSslCom
{
    public static class Proxy
    {
        public static void Start(Mapping mapping)
        {
            //der proxy startet einen server und lauscht
            //beim empfang einer nachricht, leitet er diese an das ziel weiter
            TcpListener server = null;
            try
            {
                server = new TcpListener(IPAddress.Parse(mapping.FromAddress), mapping.FromPort);
                server.Start();

                // Buffer for reading data
                var bytes = new byte[Program.BufferSize];
                string data = null;

                // Enter the listening loop.
                while (true)
                {
                    Console.Write("Waiting for a connection... ");

                    // Perform a blocking call to accept requests.
                    // You could also use server.AcceptSocket() here.
                    using (var client = server.AcceptTcpClient())
                    {
                        Console.WriteLine("Connected!");

                        data = null;

                        // Get a stream object for reading and writing
                        var stream = client.GetStream();

                        int i;

                        // Loop to receive all the data sent by the client.
                        while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            // Translate data bytes to a UTF8 string.
                            data = Encoding.GetEncoding(mapping.Encoding).GetString(bytes, 0, i);
                            Console.WriteLine("Received: {0}", data);

                            //send the data to the server
                            if (mapping.UseSsl)
                                data = SendSslMessageToServer(mapping.ToAddress, mapping.ToPort, data, mapping.Encoding, true);
                            else
                                data = SendNormalMessageToServer(mapping.ToAddress, mapping.ToPort, data, mapping.Encoding);

                            var msg = Encoding.GetEncoding(mapping.Encoding).GetBytes(data);

                            // Send back a response.
                            stream.Write(msg, 0, msg.Length);
                            Console.WriteLine("Sent: {0}", data);
                        }

                        // Shutdown and end the connection
                        client.Close();
                    }
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

        private static string SendSslMessageToServer(string server, int port, string message, string encoding, bool own)
        {
            if (!own)
            {
                var serverMessage = SslTcpClient.RunClient(server, port, "", message);
                Console.WriteLine("Received: {0}", serverMessage);
                return serverMessage;
            }

            try
            {
                // Prefer using declaration to ensure the instance is Disposed later.
                using (var client = new TcpClient(server, port))
                {
                    var data = Encoding.GetEncoding(encoding).GetBytes(message);

                    var sslStream = new SslStream(
                        client.GetStream(),
                        false,
                        new RemoteCertificateValidationCallback(SslTcpClient.ValidateServerCertificate),
                        null
                    );

                    // The server name must match the name on the server certificate.
                    try
                    {
                        sslStream.AuthenticateAsClient(server);
                    }
                    catch (AuthenticationException e)
                    {
                        Console.WriteLine("Exception: {0}", e.Message);
                        if (e.InnerException != null)
                        {
                            Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                        }
                        Console.WriteLine("Authentication failed - closing the connection.");
                        client.Close();
                        return null;
                    }


                    // Send the message to the connected TcpServer.
                    sslStream.Write(data);
                    sslStream.Flush();

                    Console.WriteLine("Sent: {0}", message);

                    // Receive the server response.
                    // Read message from the server.
                    var serverMessage = SslTcpClient.ReadMessage(sslStream);

                    Console.WriteLine("Received: {0}", serverMessage);
                    return serverMessage;
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

            return null;
        }

        public static string SendNormalMessageToServer(string server, int port, string message, string encoding)
        {
            try
            {
                // Prefer using declaration to ensure the instance is Disposed later.
                using (var client = new TcpClient(server, port))
                {
                    var data = Encoding.GetEncoding(encoding).GetBytes(message);

                    // Get a client stream for reading and writing.
                    var stream = client.GetStream();

                    // Send the message to the connected TcpServer.
                    stream.Write(data, 0, data.Length);

                    Console.WriteLine("Sent: {0}", message);

                    // Buffer to store the response bytes.
                    data = new byte[16];

                    // String to store the response UTF8 representation.
                    var responseData = string.Empty;

                    // Read the first batch of the TcpServer response bytes.
                    if (stream.CanRead)
                    {
                        
                        //var bytes = stream.Read(data, 0, data.Length);
                        responseData = ReadMessage(stream); //Encoding.GetEncoding(encoding).GetString(data, 0, bytes);
                    }
                    
                    Console.WriteLine("Received: {0}", responseData);
                    return responseData;
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

            return null;
        }

        public static string ReadMessage(NetworkStream networkStream)
        {
            var buffer = new byte[16];
            var messageData = new StringBuilder();
            var bytes = -1;
            do
            {
                bytes = networkStream.Read(buffer, 0, buffer.Length);

                var decoder = Encoding.GetEncoding(Program.CurrentEncoding).GetDecoder();
                var chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
                decoder.GetChars(buffer, 0, bytes, chars, 0);
                messageData.Append(chars);
                // Check for EOF.
                if (buffer.Any(t => t == 0))
                    break;
            } while (bytes != 0);

            return messageData.ToString();
        }
    }
}
