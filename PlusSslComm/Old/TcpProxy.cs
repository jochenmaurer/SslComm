using System;
using System.Text;

namespace PlusSslComm.Old
{
    public class TcpProxy : ProxyBase
    {
        protected override void HandleRequest(Mapping mapping)
        {
            //input
            using (var inputStream = mapping.Source.GetStream())
            {
                ReadBytesFromInputStream(inputStream, out var buffer, out var bytesRead);

                if (bytesRead > 0)
                {

                    if (IsLoggingEnabled)
                    {
                        var data = Encoding.GetEncoding(mapping.Encoding).GetString(buffer, 0, bytesRead);
                        Console.WriteLine("Incoming: {0}", data);
                    }

                    //output
                    if (mapping.TargetStream == null)
                        mapping.TargetStream = mapping.Target.GetStream();

                    if (mapping.TargetStream.CanWrite)
                        mapping.TargetStream.Write(buffer, 0, bytesRead);

                    ReadBytesFromOutputStream(mapping.TargetStream, out buffer, out bytesRead);

                    if (IsLoggingEnabled)
                    {
                        var serverMessage = ReadMessage(buffer, bytesRead, mapping.Encoding);
                        Console.WriteLine("Outgoing: {0}", serverMessage);
                    }

                    inputStream.Write(buffer, 0, bytesRead);
                }
                else
                {
                    mapping.Source.Dispose();
                    mapping.Target.Dispose();

                    inputStream.Write(buffer, 0, 0);
                }
            }
        }
    }
}
