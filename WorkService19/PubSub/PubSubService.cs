using Common;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PubSub
{
    public class PubSubService : IPubSubService
    {
        IReliableDictionary<string, CurrentWork> ActiveData;
        IReliableDictionary<string, CurrentWork> HistoryData;
        IReliableStateManager StateManager;

        public PubSubService(IReliableStateManager stateManager)
        {
            StateManager = stateManager;
        }

        public PubSubService()
        {

        }



        public async Task<List<CurrentWork>> GetActiveData()
        {
            List<CurrentWork> currentWorks = new List<CurrentWork>();
            ActiveData = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, CurrentWork>>("ActiveData");
            using (var tx = this.StateManager.CreateTransaction())
            {
                var enumerator = (await ActiveData.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                {
                    currentWorks.Add(enumerator.Current.Value);
                }
            }
            return currentWorks;
        }

        public async Task<List<CurrentWork>> GetHistoryData()
        {
            List<CurrentWork> currentWorks = new List<CurrentWork>();
            HistoryData = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, CurrentWork>>("HistoryData");
            using (var tx = this.StateManager.CreateTransaction())
            {
                var enumerator = (await HistoryData.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                {
                    currentWorks.Add(enumerator.Current.Value);
                }
            }
            return currentWorks;
        }

        public async Task<bool> PublishActive(List<CurrentWork> currentWorks)
        {
            try
            {
                ActiveData = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, CurrentWork>>("ActiveData");
                using (var tx = this.StateManager.CreateTransaction())
                {
                    var enumerator = (await ActiveData.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                    while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                    {
                        await ActiveData.TryRemoveAsync(tx, enumerator.Current.Key);
                    }

                    foreach (CurrentWork currentWork in currentWorks)
                    {
                        await ActiveData.TryAddAsync(tx, currentWork.IdCurrentWork, currentWork);
                    }
                    await tx.CommitAsync();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> PublishHistory(List<CurrentWork> currentWorks)
        {
            try
            {
                HistoryData = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, CurrentWork>>("HistoryData");
                using (var tx = this.StateManager.CreateTransaction())
                {
                    foreach (CurrentWork currentWork in currentWorks)
                    {
                        await HistoryData.TryAddAsync(tx, currentWork.IdCurrentWork, currentWork);
                    }
                    await tx.CommitAsync();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
