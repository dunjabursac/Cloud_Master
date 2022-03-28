using Common;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
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
            bool result = true;

            CurrentWorkDict = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, CurrentWork>>("CurrentWorkActiveData");
            using (var tx = this.StateManager.CreateTransaction())
            {
                result = await CurrentWorkDict.TryAddAsync(tx, idCurrentWork, new CurrentWork(idCurrentWork, location, startDate, endDate, description));
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
                    await CurrentWorkDict.TryRemoveAsync(tx, enumerator.Current.Key);
                }
                await tx.CommitAsync();
            }

            return true;
        }
    }
}
