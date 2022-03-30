using Common;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WorkServiceSaver
{
    public class Saver : ISaver
    {
        IReliableDictionary<string, CurrentWork> CurrentWorkDict;
        IReliableStateManager StateManager;


        public Saver()
        {

        }

        public Saver(IReliableStateManager stateManager)
        {
            StateManager = stateManager;
        }


        public async Task<bool> AddCurrentWork(string idCurrentWork, string location, DateTime startDate, DateTime endDate, string description)
        {
            string url = string.Format("https://api.openweathermap.org/data/2.5/weather?q={0}&appid=ee89cb80a57b008a5ca9b94bd300f41b", location);

            JArray dataArray;
            JToken token;

            string weatherDescription = "none";
            double temp = 0;
            double windSpeed = 0;
            double clouds = 0;
            try
            {
                var client = new WebClient();
                var content = client.DownloadString(url);
                dynamic data = JObject.Parse(content);

                var obj = JsonConvert.DeserializeObject<JObject>(content);

                dataArray = data.weather;
                token = dataArray[0];
                weatherDescription = token["description"].ToString();

                double tempInKelvin = Convert.ToDouble(obj["main"]["temp"]);
                temp = tempInKelvin - 273.15;

                windSpeed = Convert.ToDouble(obj["wind"]["speed"]);
                clouds = Convert.ToDouble(obj["clouds"]["all"]);
            }
            catch
            {
                ServiceEventSource.Current.Message("Not connected to OpenWeather!");
            }


            bool result = true;

            CurrentWorkDict = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, CurrentWork>>("CurrentWorkActiveData");
            using (var tx = this.StateManager.CreateTransaction())
            {
                result = await CurrentWorkDict.TryAddAsync(tx, idCurrentWork, new CurrentWork(idCurrentWork, location, startDate, endDate, description, weatherDescription, temp, windSpeed, clouds));
                await tx.CommitAsync();
            }


            List<CurrentWork> currentWorks = await GetAllData();
            FabricClient fabricClient = new FabricClient();
            int partitionsNumber = (await fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/WorkService19/PubSub"))).Count;
            var binding = WcfUtility.CreateTcpClientBinding();
            int index = 0;
            for (int i = 0; i < partitionsNumber; i++)
            {
                ServicePartitionClient<WcfCommunicationClient<IPubSubService>> servicePartitionClient = new ServicePartitionClient<WcfCommunicationClient<IPubSubService>>(
                    new WcfCommunicationClientFactory<IPubSubService>(clientBinding: binding),
                    new Uri("fabric:/WorkService19/PubSub"),
                    new ServicePartitionKey(index % partitionsNumber));
                bool tempPublish = await servicePartitionClient.InvokeWithRetryAsync(client => client.Channel.PublishActive(currentWorks));
                index++;
            }


            return result;
        }

        
        public async Task<List<CurrentWork>> GetAllData()
        {
            List<CurrentWork> currentWorks = new List<CurrentWork>();
            CurrentWorkDict = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, CurrentWork>>("CurrentWorkActiveData");
            using (var tx = this.StateManager.CreateTransaction())
            {
                var enumerator = (await CurrentWorkDict.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                {
                    currentWorks.Add(enumerator.Current.Value);
                }
            }

            return currentWorks;
        }


        public async Task<bool> DeleteAllActiveData()
        {
            CurrentWorkDict = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, CurrentWork>>("CurrentWorkActiveData");
            using (var tx = this.StateManager.CreateTransaction())
            {
                var enumerator = (await CurrentWorkDict.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                {
                    if (enumerator.Current.Value.EndDate < DateTime.Now)
                    {
                        await CurrentWorkDict.TryRemoveAsync(tx, enumerator.Current.Key);
                    }
                }
                await tx.CommitAsync();
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
                bool tempPublish = await servicePartitionClient1.InvokeWithRetryAsync(client => client.Channel.PublishActive(new List<CurrentWork>()));
                index1++;
            }

            await SendDataToBroker();

            return true;
        }


        public async Task<List<CurrentWork>> GetAllHistoricalData()
        {
            List<CurrentWork> currentWorks = new List<CurrentWork>();

            CurrentWorkDict = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, CurrentWork>>("CurrentWorkActiveData");
            using (var tx = this.StateManager.CreateTransaction())
            {
                var enumerator = (await CurrentWorkDict.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                {
                    if(enumerator.Current.Value.EndDate < DateTime.Now)
                    {
                        currentWorks.Add(enumerator.Current.Value);
                    }
                }
            }


            return currentWorks;
        }



        public async Task SendDataToBroker()
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
