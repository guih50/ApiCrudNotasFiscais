using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using Npgsql;
using System;
public static class DictionaryExtensions
{
    public static void AddOrUpdate(this Dictionary<string, List<string>> targetDictionary, string key, string entry)
    {
        if (!targetDictionary.ContainsKey(key))
            targetDictionary.Add(key, new List<string>());

        targetDictionary[key].Add(entry);
    }
}

namespace Company.Function
{
    public class obter_notas
    {
        private readonly ILogger<obter_notas> _logger;

        public obter_notas(ILogger<obter_notas> log)
        {
            _logger = log;
        }
        
        const string connectionString = "Server=projetostone.postgres.database.azure.com,5432;Initial Catalog=notas_fiscais;Username=guilhermelima;Password=projetostone2022!;";
        [FunctionName("obter_notas")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "name" })]
        // Essas linhas abaixo que definem quais parametros são recebidos
        [OpenApiParameter(name: "invoiceNumber", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The **invoiceNumber** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req)
        {
            string connString = "Server=projetostone.postgres.database.azure.com;Username=guilhermelima;Database=notas_fiscais;Port=5432;Password=projetostone2022!;SSLMode=Prefer";
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string invoiceNumber = req.Query["invoiceNumber"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            invoiceNumber = invoiceNumber ?? data?.invoiceNumber;

            string command = string.IsNullOrEmpty(invoiceNumber)
                ? "SELECT * FROM \"invoice\""
                : "SELECT * FROM \"invoice\" WHERE \"InvoiceNumber\" = " + invoiceNumber;
            var invoice = new Dictionary<string, List<string>>();
            
            using (var conn = new NpgsqlConnection(connString))
            {

                Console.Out.WriteLine("Opening connection");
                conn.Open();


                using (var cmd = new NpgsqlCommand(command, conn))
                {
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        invoice.AddOrUpdate("InvoiceNumber", reader.GetInt32(0).ToString());
                        invoice.AddOrUpdate("ReferenceMonth", reader.GetValue(1).ToString());
                        invoice.AddOrUpdate("ReferenceYear", reader.GetValue(2).ToString());
                        invoice.AddOrUpdate("Document", reader.GetValue(3).ToString());
                        invoice.AddOrUpdate("Description", reader.GetValue(4).ToString());
                        invoice.AddOrUpdate("Amount", reader.GetValue(5).ToString());
                        invoice.AddOrUpdate("CreatedAt", reader.GetValue(6).ToString());
                        invoice.AddOrUpdate("DeactivatedAt", reader.GetValue(7).ToString());
                        invoice.AddOrUpdate("IsActive", reader.GetValue(8).ToString());
                        Console.Out.WriteLine(invoice);
                    }
                    reader.Close();
                }
            }
            
            return new OkObjectResult(invoice);
        }
    }
}

