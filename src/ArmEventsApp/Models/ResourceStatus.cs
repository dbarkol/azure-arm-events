using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace ArmEventsApp.Models
{
    public class ResourceStatus
    {
        #region Properties

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "resourceType")]
        public string ResourceType { get; set; }

        [JsonProperty(PropertyName = "resourceProperties")]
        public string ResourceProperties { get; set; }

        #endregion
    }
}
