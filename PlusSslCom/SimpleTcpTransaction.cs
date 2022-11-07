using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace PlusSslCom
{
    public class SimpleTcpTransaction
    {
        public void Start(Mapping mapping)
        {
            TcpListener server = null;
            try
            {
                server = new TcpListener(IPAddress.Parse(mapping.FromAddress), mapping.FromPort);
                server.Start();

                var bytes = new byte[Program.BufferSize];

                while (true)
                {
                    Console.Write("Waiting for a connection... ");

                    using (var client = server.AcceptTcpClient())
                    {
                        Console.WriteLine("Connected!");

                        var stream = client.GetStream();

                        int bytesToWrite;

                        //Lese Eingangsmessage
                        while ((bytesToWrite = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            SendMessageAndReceiveReplies(mapping.ToAddress, mapping.ToPort, Program.Encoding, stream, bytes, bytesToWrite);
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

        public static string SendMessageAndReceiveReplies(string server, int port, string encoding, Stream localStream, byte[] buffer, int bytesToWrite)
        {
            try
            {
                using (var client = new TcpClient(server, port))
                {
                    var message = Encoding.GetEncoding(encoding).GetString(buffer, 0, bytesToWrite);
                    Console.WriteLine("Received from local: {0}", bytesToWrite);


                    //Schreiben nach remote
                    //var data = Encoding.GetEncoding(encoding).GetBytes(message);

                    var remoteStream = client.GetStream();

                    remoteStream.Write(buffer, 0, bytesToWrite);

                    Console.WriteLine("Sent to remote: {0}", bytesToWrite);


                    //Lesen und zurück
                    buffer = new byte[Program.BufferSize];

                    if (remoteStream.CanRead)
                    {
                        do
                        {
                            var bytesTransferred = remoteStream.Read(buffer, 0, buffer.Length);

                            var remoteMessage = Encoding.GetEncoding(encoding).GetString(buffer, 0, bytesTransferred);
                            Console.WriteLine("Received from remote: {0}", bytesTransferred);

                            localStream.Write(buffer, 0, bytesTransferred);
                            Console.WriteLine("Sent to local: {0}", bytesTransferred);
                        } while (remoteStream.DataAvailable);
                    }
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
    }
}
