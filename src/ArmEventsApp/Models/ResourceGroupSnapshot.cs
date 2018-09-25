using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.EventGrid.Models;
using Newtonsoft.Json;

namespace ArmEventsApp.Models
{
    public class ResourceGroupSnapshot
    {
        #region Properties

        [JsonProperty(PropertyName = "gridEvent")]
        public EventGridEvent GridEvent { get; set; }

        [JsonProperty(PropertyName = "resources")]
        public List<ResourceStatus> Resources { get; set; }

        #endregion
    }
}
