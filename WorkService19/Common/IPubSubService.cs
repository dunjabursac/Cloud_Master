using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [ServiceContract]
    public interface IPubSubService
    {
        [OperationContract]
        Task<bool> PublishActive(List<CurrentWork> currentWorks);
        [OperationContract]
        Task<bool> PublishHistory(List<CurrentWork> currentWorks);
        [OperationContract]
        Task<List<CurrentWork>> GetHistoryData();
        [OperationContract]
        Task<List<CurrentWork>> GetActiveData();

    }
}
