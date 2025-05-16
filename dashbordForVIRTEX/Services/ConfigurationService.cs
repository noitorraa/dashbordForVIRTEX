
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace dashbordForVIRTEX.Services;
public class ConfigurationService : IConfigurationService
{
    public IActionResult SaveConfiguration(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return new BadRequestObjectResult("Пустая строка подключения");

        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        var json = System.IO.File.ReadAllText(filePath);
        var jObj = JObject.Parse(json);
        var connSection = jObj["ConnectionStrings"] as JObject ?? new JObject();
        connSection["DefaultConnection"] = connectionString;
        jObj["ConnectionStrings"] = connSection;
        System.IO.File.WriteAllText(filePath, jObj.ToString(Newtonsoft.Json.Formatting.Indented));
        return new OkResult();
    }

    public async Task<IActionResult> TestConnectionAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return new BadRequestObjectResult("Пустая строка подключения");

        try
        {
            if (connectionString.StartsWith("postgres://"))
            {
                var builder = new NpgsqlConnectionStringBuilder(connectionString);
                using var conn = new NpgsqlConnection(builder.ConnectionString);
                await conn.OpenAsync().ConfigureAwait(false);
                await conn.CloseAsync().ConfigureAwait(false);
            }
            else if (connectionString.StartsWith("mysql://"))
            {
                var uri = new Uri(connectionString);
                var builder = new MySqlConnectionStringBuilder
                {
                    Server = uri.Host,
                    Port = (uint)uri.Port,
                    Database = uri.AbsolutePath.Trim('/'),
                    UserID = uri.UserInfo.Split(':')[0],
                    Password = uri.UserInfo.Split(':')[1]
                };
                using var conn = new MySqlConnection(builder.ConnectionString);
                await conn.OpenAsync().ConfigureAwait(false);
                await conn.CloseAsync().ConfigureAwait(false);
            }
            else if (connectionString.StartsWith("mssql://"))
            {
                var uri = new Uri(connectionString);
                var builder = new SqlConnectionStringBuilder
                {
                    DataSource = $"{uri.Host},{uri.Port}",
                    InitialCatalog = uri.AbsolutePath.Trim('/'),
                    UserID = uri.UserInfo.Split(':')[0],
                    Password = uri.UserInfo.Split(':')[1],
                    Encrypt = uri.Query.Contains("encrypt=true")
                };
                using var conn = new SqlConnection(builder.ConnectionString);
                await conn.OpenAsync().ConfigureAwait(false);
                await conn.CloseAsync().ConfigureAwait(false);
            }
            else
            {
                return new BadRequestObjectResult("Неизвестный формат строки подключения");
            }
            return new OkObjectResult("Соединение установлено");
        }
        catch (Exception ex)
        {
            return new BadRequestObjectResult(ex.Message);
        }
    }
}

public interface IConfigurationService
{
    IActionResult SaveConfiguration(string connectionString);
    Task<IActionResult> TestConnectionAsync(string connectionString);
}
