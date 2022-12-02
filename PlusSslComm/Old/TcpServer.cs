using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PlusSslComm.Old
{
    public static class TcpServer
    {
        public static void RunServer(int port)
        {
            //der server läuft permanent und lauscht auf den port
            //bei empfang einer nachricht zeigt er diese an
            TcpListener server = null;
            try
            {
                // Set the TcpListener on port 13000.
                var localAddr = IPAddress.Parse("127.0.0.1");

                // TcpListener server = new TcpListener(port);
                server = new TcpListener(localAddr, port);

                // Start listening for client requests.
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
                            data = Encoding.GetEncoding(Program.CurrentEncoding).GetString(bytes, 0, i);
                            Console.WriteLine("Received: {0}", data);

                            // Process the data sent by the client.
                            data = data.ToUpper();

                            var msg = Encoding.GetEncoding(Program.CurrentEncoding).GetBytes(data);

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
    }
}
