using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Newtonsoft.Json;

class Program
{
    static void Main(string[] args)
    {
        // Check if two file paths are provided
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: dotnet run <impressions_file_path> <clicks_file_path>");
            return;
        }

        string impressionsFilePath = args[0];
        string clicksFilePath = args[1];

        Console.WriteLine("Process Started. "+ DateTime.Now);

        // Read and parse impressions and clicks data
        List<ImpressionEvent> impressions = ReadJsonFile<List<ImpressionEvent>>(impressionsFilePath);
        List<ClickEvent> clicks = ReadJsonFile<List<ClickEvent>>(clicksFilePath);

        // Calculate metrics
        List<MetricResult> metricResults = CalculateMetrics(impressions, clicks);

        // Write metrics to JSON file
        WriteJsonFile(metricResults, "metrics.json");

        // Make recommendations
        List<RecommendationResult> recommendations = MakeRecommendations(metricResults, impressions, clicks);

        // Write recommendations to JSON file
        WriteJsonFile(recommendations, "recommendations.json");


        Console.WriteLine("Process Finished. " + DateTime.Now);
    }

    // Function to read and parse JSON file
    static T ReadJsonFile<T>(string filePath)
    {
        string jsonContent = File.ReadAllText(filePath);
        JsonSerializerSettings settings = new JsonSerializerSettings();
        settings.FloatParseHandling = FloatParseHandling.Double;
        settings.MissingMemberHandling = MissingMemberHandling.Ignore;
        settings.Error = HandleDeserializationError;
        return JsonConvert.DeserializeObject<T>(jsonContent, settings);
    }

    /// <summary>
    /// handle errors in json file
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void HandleDeserializationError(object? sender, Newtonsoft.Json.Serialization.ErrorEventArgs e)
    {
        Console.WriteLine("Ignore error parsing record: {0}", e.ErrorContext.Error.Message);
        e.ErrorContext.Handled = true; // Ignore this record
    }



    // Function to calculate metrics
    static List<MetricResult> CalculateMetrics(List<ImpressionEvent> impressions, List<ClickEvent> clicks)
    {
        // Group by app_id and country_code
        var groupedData = impressions.GroupBy(i => new { i.app_id, i.country_code })
            .Select(group => new MetricResult
            {
                app_id = group.Key.app_id,
                country_code = group.Key.country_code,
                impressions = group.Count(),
                clicks = clicks.Count(c => group.Any(i => i.id == c.impression_id)),
                revenue = clicks.Where(c => group.Any(i => i.id == c.impression_id))
                                .Sum(c => c.revenue)
            }).ToList();

        return groupedData;
    }

    // Function to make recommendations
    static List<RecommendationResult> MakeRecommendations(List<MetricResult> metricResults, List<ImpressionEvent> impressions, List<ClickEvent> clicks)
    {
        var joinedData = from click in clicks
                         join impression in impressions on click.impression_id equals impression.id 
                         select new { click.impression_id, click.revenue,impression.advertiser_id,impression.country_code,impression.app_id };

        var recommendations = metricResults.Select(result => new RecommendationResult
        {
            app_id = result.app_id,
            country_code = result.country_code,
            recommended_advertiser_ids = joinedData
                .Where(c => c.app_id == result.app_id && c.country_code == result.country_code && result.clicks > 0)
                .GroupBy(c => c.advertiser_id)
                .Select(group => new
                {
                    AdvertiserId = group.Key,
                    RevenuePerImpression = group.Sum(c => c.revenue) / metricResults
                                                .First(m => m.app_id == result.app_id && m.country_code == result.country_code)
                                                .clicks
                })
                .OrderByDescending(a => a.RevenuePerImpression)
                .Take(5)
                .Select(a => a.AdvertiserId)
                .ToList()
        }).Where(x => x.recommended_advertiser_ids.Count > 0).ToList();

        return recommendations;
    }

    // Function to write JSON file
    static void WriteJsonFile<T>(T data, string filePath)
    {
        string jsonContent = JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText(filePath, jsonContent);
    }
}

// Define classes for impression and click events
public class ImpressionEvent
{
    public Guid id { get; set; }
    public int app_id { get; set; }
    public string country_code { get; set; }
    public int advertiser_id { get; set; }
}

public class ClickEvent
{
    public Guid impression_id { get; set; }
    public double revenue { get; set; }
}

// Define class for metric result
public class MetricResult
{
    public int app_id { get; set; }
    public string country_code { get; set; }
    public int impressions { get; set; }
    public int clicks { get; set; }
    public double revenue { get; set; }
}

// Define class for recommendation result
public class RecommendationResult
{
    public int app_id { get; set; }
    public string country_code { get; set; }
    public List<int> recommended_advertiser_ids { get; set; }
}
