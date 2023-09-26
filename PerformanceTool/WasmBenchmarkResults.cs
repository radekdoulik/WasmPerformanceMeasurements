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

        public GraphPointData(string commitTime, int flavorId, KeyValuePair<int, double> pair, string hash)
        {
            this.commitTime = commitTime;
            taskMeasurementNameId = pair.Key;
            minTime = pair.Value;
            this.flavorId = flavorId;
            commitHash = hash;
        }

        public override string ToString()
        {
            return commitTime + " " + flavorId + " " + taskMeasurementNameId;
        }

    }

    public partial class LatestData
    {
        public DateTimeOffset FirstDate;
        public DateTimeOffset SliceStartDate;
        public DateTimeOffset SliceEndDate;
        public Index Index;

        public static LatestData Load(string json)
        {
            var context = new DataSerializerContext(new JsonSerializerOptions()
            {
                IncludeFields = true,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
                Converters = { new Index.IdMap.Converter() }
            });

            return JsonSerializer.Deserialize<LatestData>(json, context.LatestData);
        }
    }

    [JsonSourceGenerationOptions(IncludeFields = true)]
    [JsonSerializable(typeof(LatestData))]
    [JsonSerializable(typeof(RequiredData))]
    partial class DataSerializerContext : JsonSerializerContext { }

    public partial class RequiredData
    {
        public Index.IdMap flavorsMap;
        public Index.IdMap taskNamesMap;
        public DateTimeOffset firstDate;
        public DateTimeOffset latestStartDate;
        public DateTimeOffset latestEndDate;

        public string Save()
        {
            var context = new DataSerializerContext(new JsonSerializerOptions()
            {
                IncludeFields = true,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
                Converters = { new Index.IdMap.Converter() }
            });

            return JsonSerializer.Serialize<RequiredData>(this, context.RequiredData);
        }
    }
}
