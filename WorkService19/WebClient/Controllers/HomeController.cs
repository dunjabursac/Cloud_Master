using Common;
using HistoryWorkSaver;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using WebClient.Models;
using WorkServiceSaver;

namespace WebClient.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        [HttpPost]
        [Route("/HomeController/PostData")]
        public async Task<IActionResult> PostData(string idCurrentWork, string location, DateTime startDate, DateTime endDate, string description)
        {
            try
            {
                bool result = true;
                FabricClient fabricClient = new FabricClient();
                int partitionsNumber = (await fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/WorkService19/WorkServiceSaver"))).Count;
                var binding = WcfUtility.CreateTcpClientBinding();
                int index = 0;
                for (int i = 0; i < partitionsNumber; i++)
                {
                    ServicePartitionClient<WcfCommunicationClient<ISaver>> servicePartitionClient = new ServicePartitionClient<WcfCommunicationClient<ISaver>>(
                        new WcfCommunicationClientFactory<ISaver>(clientBinding: binding),
                        new Uri("fabric:/WorkService19/WorkServiceSaver"),
                        new ServicePartitionKey(index % partitionsNumber));
                    result = await servicePartitionClient.InvokeWithRetryAsync(client => client.Channel.AddCurrentWork(idCurrentWork, location, startDate, endDate, description));
                    index++;
                }

                if(result)
                {
                    ViewData["Title"] = "New work added successfully!";
                }
                else
                {
                    ViewData["Title"] = "New work NOT added successfully!";
                }
                
                return View("Index");
            }
            catch
            {
                ViewData["Title"] = "New work NOT added successfully!";
                return View("Index");
            }

        }

        public async Task<IActionResult> About()
        {
            ViewData["About"] = null;
            List<CurrentWork> currentWorks = new List<CurrentWork>();

            var myBinding = new NetTcpBinding(SecurityMode.None);
            var myEndpoint = new EndpointAddress("net.tcp://localhost:54675/HistoryWorkSaverEndpoint");

            using (var myChannelFactory = new ChannelFactory<IHistoryService>(myBinding, myEndpoint))
            {
                IHistoryService clientService = null;
                try
                {
                    clientService = myChannelFactory.CreateChannel();
                    currentWorks = clientService.GetAllHistoricalDataFromStorage();
                    ((ICommunicationObject)clientService).Close();
                    myChannelFactory.Close();

                    return View(currentWorks);
                }
                catch
                {
                    (clientService as ICommunicationObject)?.Abort();

                    ViewData["About"] = "Service is currently unavailable!";
                    return View();
                }

            }

        }

        public async Task<IActionResult> Contact()
        {
            ViewData["Contact"] = null;
            List<CurrentWork> currentWorks = new List<CurrentWork>();

            try
            {
                FabricClient fabricClient1 = new FabricClient();
                int partitionsNumber1 = (await fabricClient1.QueryManager.GetPartitionListAsync(new Uri("fabric:/WorkService19/WorkServiceSaver"))).Count;
                var binding1 = WcfUtility.CreateTcpClientBinding();
                int index1 = 0;
                for (int i = 0; i < partitionsNumber1; i++)
                {
                    ServicePartitionClient<WcfCommunicationClient<ISaver>> servicePartitionClient1 = new ServicePartitionClient<WcfCommunicationClient<ISaver>>(
                        new WcfCommunicationClientFactory<ISaver>(clientBinding: binding1),
                        new Uri("fabric:/WorkService19/WorkServiceSaver"),
                        new ServicePartitionKey(index1 % partitionsNumber1));
                    currentWorks = await servicePartitionClient1.InvokeWithRetryAsync(client => client.Channel.GetAllData());
                    index1++;
                }
                return View(currentWorks);
            }
            catch
            {
                ViewData["Contact"] = "Service is currently unavailable!";
                return View();
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
