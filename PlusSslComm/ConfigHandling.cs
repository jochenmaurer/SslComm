using System.Collections;
using System.Collections.Generic;
using Dcx.Plus.Infrastructure.Config;

namespace PlusSslComm
{
    public class ConfigHandling
    {
        private const string dynamicConnectionSettingsGroupName = "DynamicConnectionSettings";
        private const string protocolGroupName = "Protocol";
        private const string connectionGroupName = "Connection";
        private const string ccCommProtocolsGroupName = "CCComm_Protocols";
        private const string ccCommConnectionsGroupName = "CCComm_Connections";
        private const string protocolTagName = "Protocol";

        public Dictionary<string, ConnectionParameters> GetConnectionParametersMap(string fileName)
        {
            var config = new PlusConfigTree
            {
                LoadIncludeFiles = true,
                LoadComments = false
            };
            config.Load(fileName);

            var connectionParameters = new Dictionary<string, ConnectionParameters>();

            var dynamicConnGroup = (PlusGroup)config.Root.GetNode(dynamicConnectionSettingsGroupName, PlusConfigNode.NodeType.Group);

            foreach (var node in dynamicConnGroup)
            {
                if (node.Type == PlusConfigNode.NodeType.Group)
                {
                    var connParameterGroup = (PlusGroup)node;
                    var protocolGroup = (PlusGroup)connParameterGroup.GetNodeOpt(protocolGroupName, PlusConfigNode.NodeType.Group);
                    var connGroup = (PlusGroup)connParameterGroup.GetNodeOpt(connectionGroupName, PlusConfigNode.NodeType.Group);

                    if (protocolGroup == null && connGroup == null)
                    {
                        //LogProvider.Log.Write(LogExtra.Default(), PlusDataTandemLocalizer.Singleton.TandemCommunicator_msgProtocolAndConnectionMissing().SetParameter(
                        //    connParameterGroup.Name));
                    }
                    else
                    {
                        if (connectionParameters.ContainsKey(connParameterGroup.Name))
                        {
                            //LogProvider.Log.Write(LogExtra.Default(), PlusDataTandemLocalizer.Singleton.TandemCommunicator_msgDuplicateEntry().SetParameter(
                            //    connParameterGroup.Name));
                        }
                        else
                        {
                            var parameters = new ConnectionParameters();
                            connectionParameters[connParameterGroup.Name] = parameters;

                            if (protocolGroup != null)
                            {
                                ExtractParameters(parameters.ProtocolParameters, protocolGroup, connParameterGroup.Name);
                            }
                            if (connGroup != null)
                            {
                                ExtractParameters(parameters.ConnectionParameter, connGroup, connParameterGroup.Name);
                            }
                        }
                    }
                }
            }

            return connectionParameters;
        }

        private void ExtractParameters(IDictionary parameterStorage, PlusGroup configGroup, string connParaName)
        {
            foreach (var node in configGroup)
            {
                if (parameterStorage.Contains(node.Name))
                {
                    //LogProvider.Log.Write(LogExtra.Default(), PlusDataTandemLocalizer.Singleton.TandemCommunicator_msgDuplicateParameter().SetParameter(
                    //    node.Name, connParaName, configGroup.Name));
                }
                else
                {
                    if (node.Type == PlusConfigNode.NodeType.TaggedConstant)
                    {
                        parameterStorage[node.Name] = ((PlusTaggedConstant)node).Value;
                    }
                    else if (node.Type == PlusConfigNode.NodeType.TaggedConstantList)
                    {
                        parameterStorage[node.Name] = ((PlusTaggedConstantList)node).ConstantList.ToArray();
                    }
                    else
                    {
                        //LogProvider.Log.Write(LogExtra.Default(), PlusDataTandemLocalizer.Singleton.TandemCommunicator_msgInvalidParameterType().SetParameter(
                        //    node.Name, node.Type.ToString(), connParaName, configGroup.Name));
                    }
                }
            }
        }
    }

    public class ConnectionParameters
    {
        #region private member

        private Hashtable protocolParameters = new Hashtable();
        private Hashtable connectionParameters = new Hashtable();

        #endregion private member

        #region properties

        /// <summary>
        /// The protocol parameters. May be empty if the connection uses a preconfigured protocol.
        /// </summary>
        public IDictionary ProtocolParameters
        {
            get
            {
                return protocolParameters;
            }
        }

        /// <summary>
        /// The connection parameters. May be empty if there are no connection specific settings as
        /// for protocols that allow only a single connection per protocol instance.
        /// </summary>
        public IDictionary ConnectionParameter
        {
            get
            {
                return connectionParameters;
            }
        }

        #endregion properties
    }
}
