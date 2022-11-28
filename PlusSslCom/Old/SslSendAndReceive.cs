using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PlusSslCom
{
    public static class SslSendAndReceive
    {
        public static void Run(Mapping mapping)
        {
            var buffer = Encoding.GetEncoding(Program.CurrentEncoding).GetBytes(Program.FirstRequest);
            using (var client = new TcpClient(mapping.ToAddress, mapping.ToPort))
            {
                Console.WriteLine("Client connected.");
                var sslStream = SslHelpers.GetSecuredPlusSslStream(client, "");
                
                sslStream.Write(buffer);
                sslStream.Flush();
                // Read message from the server.
                var serverMessage = ReadMessage(sslStream);
                Console.WriteLine("Server says: {0}", serverMessage);
                // Close the client connection.
                Console.WriteLine("Client closed.");
            }
        }

        public static string ReadMessage(PlusSslStream sslStream)
        {
            var buffer = new byte[Program.BufferSize];
            var messageData = new StringBuilder();
            do
            {
                var bytes = sslStream.Read(buffer, 0, buffer.Length);
                var decoder = Encoding.GetEncoding(Program.CurrentEncoding).GetDecoder();
                var chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
                decoder.GetChars(buffer, 0, bytes, chars, 0);
                messageData.Append(chars);
            } while (sslStream.NetworkStream.DataAvailable);

            return messageData.ToString();
        }
    }

    public class PlusSslStream : SslStream
    {
        public PlusSslStream(Stream innerStream) : base(innerStream)
        {
        }

        public PlusSslStream(Stream innerStream, bool leaveInnerStreamOpen) : base(innerStream, leaveInnerStreamOpen)
        {
        }

        public PlusSslStream(Stream innerStream, bool leaveInnerStreamOpen, RemoteCertificateValidationCallback userCertificateValidationCallback) : base(innerStream, leaveInnerStreamOpen, userCertificateValidationCallback)
        {
        }

        public PlusSslStream(Stream innerStream, bool leaveInnerStreamOpen, RemoteCertificateValidationCallback userCertificateValidationCallback, LocalCertificateSelectionCallback userCertificateSelectionCallback) : base(innerStream, leaveInnerStreamOpen, userCertificateValidationCallback, userCertificateSelectionCallback)
        {
        }

        public PlusSslStream(Stream innerStream, bool leaveInnerStreamOpen, RemoteCertificateValidationCallback userCertificateValidationCallback, LocalCertificateSelectionCallback userCertificateSelectionCallback, EncryptionPolicy encryptionPolicy) : base(innerStream, leaveInnerStreamOpen, userCertificateValidationCallback, userCertificateSelectionCallback, encryptionPolicy)
        {
        }

        public NetworkStream NetworkStream
        {
            get
            {
                return base.InnerStream as NetworkStream;
            }
        }
    }
}
