using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;

namespace PlusSslComm.Old
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

                    Thread.Sleep(100);
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

        public void SendMessageAndReceiveReplies(Stream localStream, byte[] buffer, int bytesToWrite)
        {
            try
            {
                //Schreiben nach remote
                Console.WriteLine("Received from local: {0}", bytesToWrite);
                if (RemoteStream == null)
                    RemoteStream = SslHelpers.GetSecuredStream(Remote, "");

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
    }
}
