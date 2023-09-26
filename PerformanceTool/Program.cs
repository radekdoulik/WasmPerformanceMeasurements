using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text;
using WasmBenchmarkResults;
using System.Text.Json.Serialization;

public partial class Program
{
    readonly static string gitLogFile = "/git-log.txt";
    readonly static string fileName = "index.json";
    static WasmBenchmarkResults.Index data;
    static bool latestLoaded = false;
    static List<string> flavors;
    static Dictionary<string, List<GraphPointData>> graphDataByHash = new();
    static string origIndexUrl;
    static string urlBase;

    readonly static JsonSerializerOptions serializerOptions = new()
    {
        IncludeFields = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new WasmBenchmarkResults.Index.IdMap.Converter() }
    };
    readonly static CommonSerializerContext serializerContext = new(serializerOptions);

    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(List<GraphPointData>))]
    public partial class CommonSerializerContext : JsonSerializerContext { }

    static List<GraphPointData> list = new();

    public static string CreateDelimiter(int alignmentLength)
    {
        return $"|{new string('-', alignmentLength)}:";
    }

    private static async Task<WasmBenchmarkResults.Index> LoadIndex()
    {
        return WasmBenchmarkResults.Index.Load(await GetIndexContent(origIndexUrl));
    }

    static async Task<string> GetIndexContent(string indexUrl)
    {
        DataDownloader dataDownloader = new();
        using var memoryStream = new MemoryStream(await dataDownloader.downloadAsBytes(indexUrl));
        using var archive = new ZipArchive(memoryStream);
        var entry = archive.GetEntry(fileName);
        using Stream readStream = entry.Open(); 
        using var reader = new StreamReader(readStream);

        return await reader.ReadToEndAsync();
    }

    private static async Task<WasmBenchmarkResults.LatestData> LoadLatestIndex()
    {
        var url = urlBase + "/slices/last.zip";

        return WasmBenchmarkResults.LatestData.Load(await GetIndexContent(url));
    }

    [JSExport]
    internal static string GetLogUrl(string hash, string flavor)
    {
        return urlBase + hash + "/" + flavor.Replace('.', '/') + gitLogFile;
    }

    internal static async Task<string> LoadTests()
    {
        var idx = origIndexUrl.LastIndexOf('/');
        DateTimeOffset firstDate;
        DateTimeOffset startDate;
        DateTimeOffset endDate;
        urlBase = origIndexUrl;
        if (idx >= 0)
            urlBase = origIndexUrl.Substring(0, idx);

        try {
            var latest = await LoadLatestIndex();
            firstDate = latest.FirstDate;
            startDate = latest.SliceStartDate;
            endDate = latest.SliceEndDate;
            data = latest.Index;
            latestLoaded = true;
        } catch {
            data = await LoadIndex();
            firstDate = data.Data[0].commitTime;
            endDate = data.Data[data.Data.Count - 1].commitTime;
            startDate = endDate.AddDays(-14);
        }

        flavors = data.FlavorMap.Keys.ToList<string>();

        var neededData = new RequiredData { flavorsMap = data.FlavorMap, taskNamesMap = data.MeasurementMap, firstDate = firstDate, latestStartDate = startDate, latestEndDate = endDate };
        var jsonData = neededData.Save();

        return jsonData;
    }

    [JSExport]
    internal static async Task<string> GetHashesForRange(string date1, string date2)
    {
        var startDate = DateTimeOffset.Parse(JsonSerializer.Deserialize<string>(date1, serializerContext.String));
        var endDate = DateTimeOffset.Parse(JsonSerializer.Deserialize<string>(date2, serializerContext.String));
        var hashSet = new HashSet<string>();

        if (latestLoaded && startDate < data.Data[0].commitTime)
            data = await LoadIndex();

        foreach (var d in data.Data)
        {
            if (d.commitTime >= startDate && d.commitTime <= endDate)
                hashSet.Add(d.hash);
        }

        return JsonSerializer.Serialize(hashSet.ToList(), serializerContext.ListString);
    }

    [JSExport]
    internal static string GetSubFlavors(string jsonFlavors)
    {
        HashSet<string> subFlavors = new();
        flavors.ForEach(flavor => subFlavors.UnionWith(flavor.Split('.')));

        return JsonSerializer.Serialize(subFlavors.ToList(), serializerContext.ListString);
    }

    [JSExport]
    internal static string GetGraphPoints(string hash)
    {
        if (!graphDataByHash.TryGetValue(hash, out list))
        {

            list = CreateGraphPoints(hash);
            graphDataByHash[hash] = list;
        }

        return JsonSerializer.Serialize(list, serializerContext.ListGraphPointData);
    }

    internal static void AddGraphPoints(List<GraphPointData> list, WasmBenchmarkResults.Index.Item item)
    {
        var flavorId = item.flavorId;

        foreach (var pair in item.minTimes)
        {
            list.Add(new GraphPointData(item.commitTime.ToString(CultureInfo.InvariantCulture), flavorId, new KeyValuePair<int, double>(pair.Key, pair.Value), item.hash));
        }

        if (item.sizes != null)
        {
            foreach (var pair in item.sizes)
            {
                list.Add(new GraphPointData(item.commitTime.ToString(CultureInfo.InvariantCulture), flavorId, new KeyValuePair<int, double>(pair.Key, (double)pair.Value), item.hash));
            }
        }
    }

    internal static List<GraphPointData> CreateGraphPoints(string hash)
    {
        var list = new List<GraphPointData>();
        int idx = -1;
        while ((idx = data.Data.FindIndex(idx + 1, d => d.hash == hash)) >= 0) {
            AddGraphPoints(list, data.Data[idx]);
        }

        return list;
    }

    static GraphPointData GetDataForHash(List<GraphPointData> list, string hash)
    {
        return list.Find(data => data.commitHash == hash);
    }

    [JSExport]
    internal static string CreateMarkdownText(string date1, string date2, string jsonTests, string jsonFlavors)
    {
        var startDate = DateTimeOffset.Parse(JsonSerializer.Deserialize<string>(date1, serializerContext.String));
        var endDate = DateTimeOffset.Parse(JsonSerializer.Deserialize<string>(date2, serializerContext.String));
        var availableTests = JsonSerializer.Deserialize<List<string>>(jsonTests, serializerContext.ListString);
        var availableFlavors = JsonSerializer.Deserialize<List<string>>(jsonFlavors, serializerContext.ListString);
        var availableData = list.FindAll(point => DateTimeOffset.Parse(point.commitTime) >= startDate
                        && DateTimeOffset.Parse(point.commitTime) <= endDate);
        HashSet<string> commitSet = new();
        foreach (var item in availableData)
        {
            commitSet.Add(item.commitHash);
        }
        var commits = commitSet.ToList();
        var commitLen = commits.Count;
        StringBuilder markdown = new();

        for (int i = 0; i < availableTests.Count; i++)
        {
            var tableCorner = string.Format("|{0, -30}{1, 10}|", availableTests[i], commits[0].Substring(0, 7));
            markdown.Append(tableCorner);

            for (int j = 1; j < commitLen; j++)
            {
                var commitCell = string.Format("{0, -10}|", commits[j].Substring(0, 7));
                markdown.Append(commitCell);
            }

            markdown.Append("\n");
            markdown.Append(CreateDelimiter(39));
            for (int j = 1; j < commitLen; j++)
            {
                markdown.Append(CreateDelimiter(9));
            }
            markdown.Append("|\n");

            for (int j = 0; j < availableFlavors.Count; j++)
            {
                var rowName = string.Format("|{0, -40}|", availableFlavors[j]);
                var filteredData = availableData.FindAll(d => data.MeasurementMap[d.taskMeasurementNameId] == availableTests[i]
                               && data.FlavorMap[d.flavorId] == availableFlavors[j]
                );
                markdown.Append(rowName);
                var prevData = GetDataForHash(filteredData, commits[0]);
                for (int k = 1; k < commitLen; k++)
                {
                    string percentageCell;
                    var currentData = GetDataForHash(filteredData, commits[k]);
                    if (currentData != null && prevData != null)
                    {
                        percentageCell = string.Format("{0, 10:N3}|", (currentData.minTime/prevData.minTime - 1)*100);
                    }
                    else
                    {
                        percentageCell = string.Format("{0, 10}|", "N/A");
                    }
                    markdown.Append(percentageCell);
                    if (currentData != null)
                        prevData = currentData;
                }
                markdown.Append("\n");
            }
            markdown.Append("\n");
        }
        return markdown.ToString();
    }

    [JSExport]
    internal static Task<string> LoadData(string indexUrl)
    {
        origIndexUrl = indexUrl;
        return LoadTests();
    }

    static int Main() => 0;
}
