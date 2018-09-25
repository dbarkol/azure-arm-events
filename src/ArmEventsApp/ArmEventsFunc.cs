// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Microsoft.Rest.Azure.Authentication;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.ResourceManager.Models;
using ArmEventsApp.Models;

namespace ArmEventsApp
{
    public static class ArmEventsFunc
    {
        #region Data members

        private static ResourceManagementClient _resourceClient = null;
        private static List<Provider> _providers = null;

        #endregion

        /// <summary>
        /// This function is triggered off of an event grid event that 
        /// originates from a resource group.
        /// </summary>
        /// <param name="eventGridEvent">The event grid event</param>
        /// <param name="document">Cosmos DB output binding</param>
        /// <param name="log">Logger</param>
        [FunctionName("ArmEventsFunc")]
        public static void Run(
            [EventGridTrigger]EventGridEvent eventGridEvent, 
            [CosmosDB(
                databaseName: "armEvents",
                collectionName: "snapshots",
                ConnectionStringSetting = "CosmosDBConnection")] out dynamic document,
            ILogger log)
        {
            log.LogInformation("ArmEventsFunc triggered");            

            // Initialize the function app with any static variables
            // and settings that we can reuse for future invocations.
            Initialize().GetAwaiter().GetResult();

            // Get the resource group name from the event
            var resourceGroupName = GetResourceGroupNameFromTopic(eventGridEvent.Topic);

            // Retrieve a snapshot of all the resources in the group
            var currentSnapshot = new ResourceGroupSnapshot
            {
                Resources = GetResources(resourceGroupName),
                GridEvent = eventGridEvent
            };

            // Create a new document that includes the snapshot
            // of what is currently in the resource group
            document = new
            {
                id = Guid.NewGuid(),
                snapshot = currentSnapshot
            };
        }

        #region Private methods

        private static string GetResourceGroupNameFromTopic(string topic)
        {
            var parts = topic.Split('/');
            return parts[4];
        }

        private static async Task Initialize()
        {
            // Initialize the resource client with the service principal 
            // credentials. Save the instance into a static variable for reuse.
            if (_resourceClient == null)
            {             
                var tenantId = System.Environment.GetEnvironmentVariable("TenantId");
                var clientId = System.Environment.GetEnvironmentVariable("ClientId");
                var clientSecret = System.Environment.GetEnvironmentVariable("ClientSecret");
                var subscriptionId = System.Environment.GetEnvironmentVariable("SubscriptionId");
                var serviceCreds = await ApplicationTokenProvider.LoginSilentAsync(tenantId, clientId, clientSecret);
                _resourceClient = new ResourceManagementClient(serviceCreds) {SubscriptionId = subscriptionId};
            }

            // Retrieve a list of providers for future reference. We'll 
            // use these to look up API versions when querying the state
            // of a resource.
            if (_providers == null || _providers.Count == 0)
            {
                _providers = _resourceClient.Providers.List().ToList();
            }
        }

        private static string GetApiVersion(string resourceProvider, string resourceType)
        {
            // Retrieve the api version for the resource from the 
            // captured list of providers.
            var apiVersion = _providers
                .First(x => x.NamespaceProperty == resourceProvider)
                .ResourceTypes.First(r => r.ResourceType == resourceType)
                .ApiVersions[0];

            return apiVersion;
        }

        private static List<ResourceStatus> GetResources(string resourceGroupName)
        {
            // Initialize a list of resource statuses
            var resourceStatuses = new List<ResourceStatus>();
            
            // Get all the resources in the group
            var resources = _resourceClient.Resources.ListByResourceGroup(resourceGroupName).ToList();

            // Iterate through each resource in the group
            foreach (var r in resources)
            {
                // Split the URI into an array of strings so that we can
                // retrieve the pieces we need to get the API version and
                // resource details.
                var resourceUriParts = r.Id.Split('/');
                var resourceProvider = resourceUriParts[6];
                var resourceType = resourceUriParts[7];

                // Get the API version based on the provider and resource type
                var apiVersion = GetApiVersion(resourceProvider, resourceType);

                // Get the current state of the resource
                var resource = _resourceClient.Resources.GetById(r.Id, apiVersion);
                var resourceProperties = resource.Properties.ToString();

                // Add the status to the collection
                resourceStatuses.Add(new ResourceStatus
                {
                    Id = r.Id,
                    Name = r.Name,
                    ResourceType = r.Type,
                    ResourceProperties = resourceProperties
                });
            }

            return resourceStatuses;
        }

        #endregion
    }
}