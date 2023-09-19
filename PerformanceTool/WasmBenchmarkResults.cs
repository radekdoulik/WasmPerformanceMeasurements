using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using WasmBenchmarkResults;
using static WasmBenchmarkResults.Index;

namespace WasmBenchmarkResults
{
    public class GraphPointData
    {
        public string commitTime;
        public int taskMeasurementNameId;
        public double minTime;
        public int flavorId;
        public string commitHash;
        public double percentage;
        public string unit;

        public GraphPointData(string commitTime, int flavorId, KeyValuePair<int, double> pair, string hash, string unit = "ms", double percentage = 0)
        {
            this.commitTime = commitTime;
            taskMeasurementNameId = pair.Key;
            minTime = pair.Value;
            this.flavorId = flavorId;
            commitHash = hash;
            this.unit = unit;
            this.percentage = percentage;
        }

        public override string ToString()
        {
            return commitTime + " " + flavorId + " " + taskMeasurementNameId;
        }

    }

    public partial class RequiredData
    {
        public Index.IdMap flavorsMap;
        public Index.IdMap taskNamesMap;

        public RequiredData(Index.IdMap flavorsMap, Index.IdMap taskNamesMap)
        {
            this.flavorsMap = flavorsMap;
            this.taskNamesMap = taskNamesMap;
        }

        [JsonSourceGenerationOptions(IncludeFields = true)]
        [JsonSerializable(typeof(RequiredData))]
        partial class RequiredDataSerializerContext : JsonSerializerContext { }

        public string Save()
        {
            var context = new RequiredDataSerializerContext(new JsonSerializerOptions()
            {
                IncludeFields = true,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
                Converters = { new Index.IdMap.Converter() }
            });
            return JsonSerializer.Serialize<RequiredData>(this, context.RequiredData);
        }
    }
}
