using Common;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Fabric;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WorkServiceSaver;

namespace HistoryWorkSaver
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class HistoryWorkSaver : StatelessService
    {
        public HistoryWorkSaver(StatelessServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new[] { new ServiceInstanceListener(context => this.CreateInternalListener(context)) };
        }


        private ICommunicationListener CreateInternalListener(StatelessServiceContext context)
        {
            string host = context.NodeContext.IPAddressOrFQDN;

            var endpointConfig = context.CodePackageActivationContext.GetEndpoint("HistoryWorkSaverEndpoint");
            int port = endpointConfig.Port;
            var scheme = endpointConfig.Protocol.ToString();
            string uri = string.Format(CultureInfo.InvariantCulture, "net.{0}://{1}:{2}/HistoryWorkSaverEndpoint", scheme, host, port);

            var listener = new WcfCommunicationListener<IHistoryService>(
                serviceContext: context,
                wcfServiceObject: new HistoryService(),
                listenerBinding: WcfUtility.CreateTcpListenerBinding(maxMessageSize: 1024 * 1024 * 1024),
                address: new System.ServiceModel.EndpointAddress(uri)
                );

            ServiceEventSource.Current.Message("Listener created!");
            return listener;
        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.


            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);


            long iterations = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ServiceEventSource.Current.ServiceMessage(this.Context, "Working-{0}", ++iterations);

                await GetDataFromCurrentWork();

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }




        async Task<bool> GetDataFromCurrentWork()
        {
            FabricClient fabricClient = new FabricClient();
            int partitionsNumber = (await fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/WorkService19/WorkServiceSaver"))).Count;
            var binding = WcfUtility.CreateTcpClientBinding();
            int index = 0;
            int index2 = 0;
            List<CurrentWork> currentWorks = new List<CurrentWork>();
            for (int i = 0; i < partitionsNumber; i++)
            {
                ServicePartitionClient<WcfCommunicationClient<ISaver>> servicePartitionClient = new ServicePartitionClient<WcfCommunicationClient<ISaver>>(
                    new WcfCommunicationClientFactory<ISaver>(clientBinding: binding),
                    new Uri("fabric:/WorkService19/WorkServiceSaver"),
                    new ServicePartitionKey(index % partitionsNumber));
                currentWorks = await servicePartitionClient.InvokeWithRetryAsync(client => client.Channel.GetAllHistoricalData());
                index++;
            }

            if (currentWorks.Count > 0)
            {
                try
                {
                    CloudStorageAccount _storageAccount;
                    CloudTable _table;
                    string a = ConfigurationManager.AppSettings["DataConnectionString"];
                    _storageAccount = CloudStorageAccount.Parse(a);
                    CloudTableClient tableClient = new CloudTableClient(new Uri(_storageAccount.TableEndpoint.AbsoluteUri), _storageAccount.Credentials);
                    _table = tableClient.GetTableReference("CurrentWorkDataStorage");
                    foreach (CurrentWork currentWork in currentWorks)
                    {
                        CurrentWorkTable currentWorkTable = new CurrentWorkTable(currentWork.IdCurrentWork, currentWork.Location, currentWork.StartDate, currentWork.EndDate, currentWork.Description, true);
                        TableOperation insertOperation = TableOperation.InsertOrReplace(currentWorkTable);
                        _table.Execute(insertOperation);
                    }
                    bool tempBool = false;
                    for (int i = 0; i < partitionsNumber; i++)
                    {
                        ServicePartitionClient<WcfCommunicationClient<ISaver>> servicePartitionClient2 = new ServicePartitionClient<WcfCommunicationClient<ISaver>>(
                            new WcfCommunicationClientFactory<ISaver>(clientBinding: binding),
                            new Uri("fabric:/WorkService19/WorkServiceSaver"),
                            new ServicePartitionKey(index2 % partitionsNumber));
                        tempBool = await servicePartitionClient2.InvokeWithRetryAsync(client => client.Channel.DeleteAllActiveData());
                        index2++;
                    }



                    //List<CurrentWork> historyData = GetAllHistoricalData();
                    //FabricClient fabricClient1 = new FabricClient();
                    //int partitionsNumber1 = (await fabricClient1.QueryManager.GetPartitionListAsync(new Uri("fabric:/WorkService19/PubSub"))).Count;
                    //var binding1 = WcfUtility.CreateTcpClientBinding();
                    //int index1 = 0;
                    //for (int i = 0; i < partitionsNumber1; i++)
                    //{
                    //    ServicePartitionClient<WcfCommunicationClient<IPubSubService>> servicePartitionClient1 = new ServicePartitionClient<WcfCommunicationClient<IPubSubService>>(
                    //        new WcfCommunicationClientFactory<IPubSubService>(clientBinding: binding1),
                    //        new Uri("fabric:/CloudProjekatSistemUcitavanjaElektricnogBrojila/Broker"),
                    //        new ServicePartitionKey(index1 % partitionsNumber1));
                    //    bool tempPublish = await servicePartitionClient1.InvokeWithRetryAsync(client => client.Channel.PublishHistory(historyData));
                    //    index1++;
                    //}

                }
                catch
                {
                    ServiceEventSource.Current.Message("Cloud is NOT created!");
                }
            }
            return true;
        }



        
    }
}
