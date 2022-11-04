using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlusSslCom
{
    public abstract class ProxyBase
    {
        public Mapping Mapping { get; set; }

        public bool Running { get; set; }

        public bool IsLoggingEnabled { get; set; } = true;

        public async Task Run()
        {
            Running = true;
            Console.WriteLine("Proxy is started!");

            try
            {
                while (Running)
                {
                    Mapping.Listener.Start();

                    if (IsLoggingEnabled)
                    {
                        Console.WriteLine("Listening to {0}!", Mapping.FromAddress + ":" + Mapping.FromPort);
                    }


                    Mapping.Source = await Mapping.Listener.AcceptTcpClientAsync();
                    await Task.Run(() => HandleRequest(Mapping));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }

        protected abstract void HandleRequest(Mapping mapping);

        protected static void ReadBytesFromInputStream(NetworkStream inputStream, out byte[] buffer, out int bytesRead)
        {
            buffer = new byte[Program.BufferSize];
            bytesRead = 0;

            if (inputStream.CanRead)
            {
                try
                {
                    inputStream.ReadTimeout = 500;
                    bytesRead = inputStream.Read(buffer, 0, buffer.Length);
                }
                catch (IOException ioException)
                {
                    Console.WriteLine(ioException);
                }
            }

            if (bytesRead == 0)
            {
                Console.WriteLine("0 bytes read. Error?");
            }
        }

        protected static void ReadBytesFromOutputStream(Stream outputStream, out byte[] buffer, out int bytesRead)
        {
            bytesRead = 0;
            buffer = new byte[Program.BufferSize];
            if (outputStream.CanRead)
            {
                bytesRead = outputStream.Read(buffer, 0, buffer.Length);
            }
        }
        
        public static string ReadMessage(byte[] buffer, int bytesRead, string encoding)
        {
            var messageData = new StringBuilder();
            var decoder = Encoding.GetEncoding(encoding).GetDecoder();
            var chars = new char[decoder.GetCharCount(buffer, 0, bytesRead)];
            decoder.GetChars(buffer, 0, bytesRead, chars, 0);
            messageData.Append(chars);

            return messageData.ToString();
        }

        public void Stop()
        {
            Mapping.Listener.Stop();
            
            Running = false;
            Console.WriteLine("Proxy is stopped!");
        }

        //public async Task<TcpClient> GetTcpClient(TcpListener listener, CancellationToken token)
        //{
        //    using (cts.Token.Register(listener.Stop))
        //    {
        //        try
        //        {
        //            var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
        //            return client;
        //        }
        //        catch (ObjectDisposedException ex)
        //        {
        //            // Token was canceled - swallow the exception and return null
        //            if (token.IsCancellationRequested) return null;
        //            throw ex;
        //        }
        //    }
        //}
    }
}
