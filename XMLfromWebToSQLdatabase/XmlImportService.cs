using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace XMLfromWebToSQLdatabase.Services;

public class XmlImportService
{
    private readonly HttpClient _httpClient;
    private readonly string _connectionString;

    public XmlImportService(HttpClient httpClient, string databaseFilePath)
    {
        _httpClient = httpClient;
        _connectionString = $"Data Source={databaseFilePath}";
    }

    public async Task ImportAsync(string url)
    {
        string json;
        string downloadedAtUtc = DateTime.UtcNow.ToString("O"); // ISO 8601-compliant format => e.g. "2024-06-01T12:34:56.789Z"

        try
        {
            // Try to get the response so we can inspect status codes
            using HttpResponseMessage response = await _httpClient.GetAsync(url);

            /* Site responded with an error status (404, 500, etc.)
             Error will be written into the JSON-serialized object */
            if (!response.IsSuccessStatusCode)
            {
                var errorObj = new { Error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}" };
                json = JsonConvert.SerializeObject(errorObj, Formatting.Indented);
            }
            else
            {
                string xml = await response.Content.ReadAsStringAsync();
                XDocument doc = XDocument.Parse(xml);
                json = JsonConvert.SerializeXNode(doc, Formatting.Indented);
            }
        }
        /* Network-level error (site offline, DNS failure, connection refused, etc.)
         Error will be written into the JSON-serialized object */
        catch (HttpRequestException ex)
        {
            var errorObj = new { Error = "Network error or site unreachable", Details = ex.Message };
            json = JsonConvert.SerializeObject(errorObj, Formatting.Indented);
        }
        /* Timeout or cancellation
         Error will be written into the JSON-serialized object */
        catch (TaskCanceledException ex)
        {
            var errorObj = new { Error = "Request timed out or was canceled", Details = ex.Message };
            json = JsonConvert.SerializeObject(errorObj, Formatting.Indented);
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Create the table if it doesn't exist
        string createTableSql = @"
            CREATE TABLE IF NOT EXISTS XmlDownloads
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SourceUrl TEXT NOT NULL,
                DownloadedAtUtc TEXT NOT NULL,
                JsonData TEXT NOT NULL
            );";

        await using (var createCommand = new SqliteCommand(createTableSql, connection))
        {
            await createCommand.ExecuteNonQueryAsync();
        }

        // Insert the new record
        string insertSql = @"
            INSERT INTO XmlDownloads (SourceUrl, DownloadedAtUtc, JsonData)
            VALUES ($url, $downloadedAtUtc, $json);";

        await using (var insertCommand = new SqliteCommand(insertSql, connection))
        {
            insertCommand.Parameters.AddWithValue("$url", url);
            insertCommand.Parameters.AddWithValue("$downloadedAtUtc", downloadedAtUtc);
            insertCommand.Parameters.AddWithValue("$json", json);

            await insertCommand.ExecuteNonQueryAsync();
        }
    }
}
