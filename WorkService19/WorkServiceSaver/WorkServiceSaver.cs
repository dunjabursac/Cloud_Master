using Common;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Fabric;
using System.Fabric.Description;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Client;

namespace WorkServiceSaver
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class WorkServiceSaver : StatefulService
    {
        Saver mySaver;

        public WorkServiceSaver(StatefulServiceContext context)
            : base(context)
        {
            mySaver = new Saver(this.StateManager);
        }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new[] { new ServiceReplicaListener(context => this.CreateInternalListener(context)) };
        }
        private ICommunicationListener CreateInternalListener(ServiceContext context)
        {

            EndpointResourceDescription internalEndpoint = context.CodePackageActivationContext.GetEndpoint("ProcessingServiceEndpoint");
            string uriPrefix = String.Format(
                   "{0}://+:{1}/{2}/{3}-{4}/",
                   internalEndpoint.Protocol,
                   internalEndpoint.Port,
                   context.PartitionId,
                   context.ReplicaOrInstanceId,
                   Guid.NewGuid());

            string nodeIP = FabricRuntime.GetNodeContext().IPAddressOrFQDN;

            string uriPublished = uriPrefix.Replace("+", nodeIP);
            return new WcfCommunicationListener<ISaver>(context, mySaver, WcfUtility.CreateTcpListenerBinding(), uriPrefix);
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            var myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, long>>("myDictionary");
            var CurrentWorkActiveData = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, CurrentWork>>("CurrentWorkActiveData");

            await ReadFromTable();

            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            await SendDataToBroker(cancellationToken);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var tx = this.StateManager.CreateTransaction())
                {
                    var result = await myDictionary.TryGetValueAsync(tx, "Counter");

                    ServiceEventSource.Current.ServiceMessage(this.Context, "Current Counter Value: {0}",
                        result.HasValue ? result.Value.ToString() : "Value does not exist.");

                    await myDictionary.AddOrUpdateAsync(tx, "Counter", 0, (key, value) => ++value);

                    // If an exception is thrown before calling CommitAsync, the transaction aborts, all changes are 
                    // discarded, and nothing is saved to the secondary replicas.
                    await tx.CommitAsync();
                }

                AddToTableStorage();


                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }


        public async Task ReadFromTable()
        {
            try
            {
                CloudStorageAccount _storageAccount;
                CloudTable _table;
                string a = ConfigurationManager.AppSettings["DataConnectionString"];
                _storageAccount = CloudStorageAccount.Parse(a);
                CloudTableClient tableClient = new CloudTableClient(new Uri(_storageAccount.TableEndpoint.AbsoluteUri), _storageAccount.Credentials);
                _table = tableClient.GetTableReference("CurrentWorkDataStorage");

                var results = from g in _table.CreateQuery<CurrentWorkTable>() where g.PartitionKey == "CurrentWorkData" && !g.HistoryData select g;

                if (results.ToList().Count > 0)
                {
                    var CurrentWorkActiveData = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, CurrentWork>>("CurrentWorkActiveData");
                    using (var tx = this.StateManager.CreateTransaction())
                    {
                        foreach (CurrentWorkTable currentWorkEntity in results.ToList())
                        {
                            await CurrentWorkActiveData.TryAddAsync(tx, currentWorkEntity.RowKey, new CurrentWork(currentWorkEntity.RowKey, currentWorkEntity.Location, currentWorkEntity.StartDate, currentWorkEntity.EndDate, currentWorkEntity.Description, currentWorkEntity.WeatherDescription, currentWorkEntity.Temp, currentWorkEntity.WindSpeed, currentWorkEntity.Clouds));
                        }
                        await tx.CommitAsync();
                    }
                }
            }
            catch
            {
                ServiceEventSource.Current.Message("Cloud is NOT created!");
            }
        }

        public async Task AddToTableStorage()
        {
            List<CurrentWorkTable> currentWorkTableEntities = new List<CurrentWorkTable>();
            var CurrentWorkActiveData = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, CurrentWork>>("CurrentWorkActiveData");

            using (var tx = this.StateManager.CreateTransaction())
            {
                var enumerator = (await CurrentWorkActiveData.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                {
                    CurrentWork currentWork = (await CurrentWorkActiveData.TryGetValueAsync(tx, enumerator.Current.Key)).Value;
                    currentWorkTableEntities.Add(new CurrentWorkTable(currentWork.IdCurrentWork, currentWork.Location, currentWork.StartDate, currentWork.EndDate, currentWork.Description, false, currentWork.WeatherDescription, currentWork.Temp, currentWork.WindSpeed, currentWork.Clouds));
                }
            }

            try
            {
                CloudStorageAccount _storageAccount;
                CloudTable _table;
                string a = ConfigurationManager.AppSettings["DataConnectionString"];
                _storageAccount = CloudStorageAccount.Parse(a);
                CloudTableClient tableClient = new CloudTableClient(new Uri(_storageAccount.TableEndpoint.AbsoluteUri), _storageAccount.Credentials);
                _table = tableClient.GetTableReference("CurrentWorkDataStorage");
                foreach (CurrentWorkTable currentWorkEntity in currentWorkTableEntities)
                {
                    TableOperation insertOperation = TableOperation.InsertOrReplace(currentWorkEntity);
                    _table.Execute(insertOperation);
                }
            }
            catch
            {
                ServiceEventSource.Current.Message("Cloud is NOT created!");
            }
        }




        public async Task SendDataToBroker(CancellationToken cancellationToken)
        {
            try
            {
                bool tempPublish = false;
                List<CurrentWork> currentWorks = new List<CurrentWork>();
                var CurrentWorkDict = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, CurrentWork>>("CurrentWorkActiveData");
                using (var tx = this.StateManager.CreateTransaction())
                {
                    var enumerator = (await CurrentWorkDict.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                    while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                    {
                        currentWorks.Add(enumerator.Current.Value);
                    }
                }
                FabricClient fabricClient1 = new FabricClient();
                int partitionsNumber1 = (await fabricClient1.QueryManager.GetPartitionListAsync(new Uri("fabric:/WorkService19/PubSub"))).Count;
                var binding1 = WcfUtility.CreateTcpClientBinding();
                int index1 = 0;
                for (int i = 0; i < partitionsNumber1; i++)
                {
                    ServicePartitionClient<WcfCommunicationClient<IPubSubService>> servicePartitionClient1 = new ServicePartitionClient<WcfCommunicationClient<IPubSubService>>(
                        new WcfCommunicationClientFactory<IPubSubService>(clientBinding: binding1),
                        new Uri("fabric:/WorkService19/PubSub"),
                        new ServicePartitionKey(index1 % partitionsNumber1));
                    while (!tempPublish)
                    {
                        tempPublish = await servicePartitionClient1.InvokeWithRetryAsync(client => client.Channel.PublishActive(currentWorks));
                        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    }
                    index1++;
                }
            }
            catch (Exception e)
            {
                string err = e.Message;
                ServiceEventSource.Current.Message(err);
            }
        }
    }
}
