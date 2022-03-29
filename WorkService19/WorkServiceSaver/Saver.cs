using Common;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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
    }
}
