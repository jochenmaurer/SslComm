using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace PlusSslComm.Old
{
    public class SimpleTcpAsyncTransaction
    {
        public int BufferSize { get; set; } = Program.BufferSize;
        public TcpClient Remote { get; set; }
        private const int PacketSize = 2048;

        public async Task Start(Mapping mapping)
        {
            var listener = new TcpListener(IPAddress.Parse(mapping.FromAddress), mapping.FromPort);
            listener.Start();

            Remote = new TcpClient(mapping.ToAddress, mapping.ToPort);

            try
            {
                while (true)
                {
                    Console.Write("Waiting for a connection on address {0}:{1}... ", mapping.FromAddress,
                        mapping.FromPort);
                    await Accept(await listener.AcceptTcpClientAsync());
                }
            }
            finally
            {
                listener.Stop();
                Remote.Dispose();
            }
        }

        private async Task Accept(TcpClient client)
        {
            await Task.Yield();
            try
            {
                using (client)
                using (var localStream = client.GetStream())
                {
                    Console.WriteLine("Connected!");
                    //var chunkSize = 1;
                    var remoteStream = Remote.GetStream();
                    var running = true;
                    var totalBytesSend = 0;

                    do
                    {
                        var bytesRead = 0;
                        var localBuffer = new byte[PacketSize];
                        while (bytesRead < localBuffer.Length)// && chunkSize > 0)
                        {
                            if (localStream.DataAvailable)
                            {
                                bytesRead = await localStream.ReadAsync(localBuffer, bytesRead, localBuffer.Length - bytesRead);

                                //bytesRead += chunkSize = read;
                                if (localBuffer.All(t => t != 0))
                                    continue;
                                running = false;
                                break;
                            }

                            running = false;
                            break;
                        }

                        Console.WriteLine("Received from local: {0}", bytesRead);

                        // Hier erfolgt der Wechsel zur Tandem
                        if (bytesRead > 0)
                        {
                            await remoteStream.WriteAsync(localBuffer, 0, bytesRead);
                            totalBytesSend += bytesRead;
                            Console.WriteLine("Sent to remote: {0}", bytesRead);
                        }
                    } while (running);

                    //await remoteStream.FlushAsync();

                    Console.WriteLine("Total bytes {0} sent to remote!", totalBytesSend);

                    // receive and send the result to client
                    if (totalBytesSend > 0)
                    {
                        if (remoteStream.CanRead)
                        {
                            do
                            {
                                try
                                {
                                    var remoteBuffer = new byte[Program.BufferSize];
                                    var bytesTransferred = await remoteStream.ReadAsync(remoteBuffer, 0, remoteBuffer.Length);
                                    Console.WriteLine("Received from remote: {0}", bytesTransferred);
                                    await localStream.WriteAsync(remoteBuffer, 0, bytesTransferred);
                                    Console.WriteLine("Sent to local: {0}", bytesTransferred);

                                    if (remoteBuffer.Any(t => t == 0))
                                    {
                                        break;
                                    }
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e);
                                }
                            } while (remoteStream.DataAvailable);
                        }
                    }
                    else
                    {
                        var remoteBuffer = new byte[PacketSize];
                        await localStream.WriteAsync(remoteBuffer, 0, 0);
                        Console.WriteLine("Sent to local: {0}", 0);
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
        }
    }
}
