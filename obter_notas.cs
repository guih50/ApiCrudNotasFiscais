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
using Npgsql;
using System;
using System.Collections.Generic;


namespace Company.Function
{
    public class obter_notas
    {
        private readonly ILogger<obter_notas> _logger;
        private static string FormatarQuerrySelect(string ReferenceMonth, string ReferenceYear, string Document, string OrderBy)
        {
            string command = "SELECT * FROM \"invoice\" WHERE \"IsActive\" = 'True'";
            command = string.IsNullOrEmpty(ReferenceMonth)
                ? command
                : command + String.Format(" AND \"ReferenceMonth\" = '{0}'", ReferenceMonth);
            command = string.IsNullOrEmpty(ReferenceYear)
                ? command
                : command + String.Format(" AND \"ReferenceYear\" = '{0}'", ReferenceYear);
            command = string.IsNullOrEmpty(Document)
                ? command
                : command + String.Format(" AND \"Document\" = '{0}'", Document);
            command = string.IsNullOrEmpty(OrderBy)
                ? command
                : command + String.Format(" ORDER BY \"{0}\"", OrderBy);
            return command;
        }

        public obter_notas(ILogger<obter_notas> log)
        {
            _logger = log;
        }
        public List<Invoice> invoices = new List<Invoice>();
        const string connectionString = "Server=projetostone.postgres.database.azure.com,5432;Initial Catalog=notas_fiscais;Username=guilhermelima;Password=projetostone2022!;";
        // Linhas abaixo são referentes a funções do azure functions
        [FunctionName("obter_notas")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "name" })]
        [OpenApiParameter(name: "ReferenceMonth", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "O mês de referência da nota fiscal")]
        [OpenApiParameter(name: "ReferenceYear", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "O ano de referência da nota fiscal")]
        [OpenApiParameter(name: "Document", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "O Documento da nota fiscal")]
        [OpenApiParameter(name: "OrderBy", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "O campo para ordenar a lista")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "text/plain", bodyType: typeof(string), Description = "The BadRequest response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "text/plain", bodyType: typeof(string), Description = "The InternalServerError response")]

        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req)
        {
            string connString = "Server=projetostone.postgres.database.azure.com;Username=guilhermelima;Database=notas_fiscais;Port=5432;Password=projetostone2022!;SSLMode=Prefer";
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string ReferenceMonth = req.Query["ReferenceMonth"];
            string ReferenceYear = req.Query["ReferenceYear"];
            string Document = req.Query["Document"];
            string OrderBy = req.Query["OrderBy"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            ReferenceMonth = ReferenceMonth ?? data?.ReferenceMonth;
            ReferenceYear = ReferenceYear ?? data?.ReferenceYear;
            Document = Document ?? data?.Document;
            OrderBy = OrderBy ?? data?.OrderBy;
            if (OrderBy != null && OrderBy != "ReferenceMonth" && OrderBy != "ReferenceYear" && OrderBy != "Document" && OrderBy != "Amount" && OrderBy != "CreatedAt" && OrderBy != "DeactivatedAt")
            {
                return new BadRequestObjectResult("OrderBy não é válido");
            }

            string command = FormatarQuerrySelect(ReferenceMonth, ReferenceYear, Document, OrderBy);

            using (var conn = new NpgsqlConnection(connString))
            {

                conn.Open();

                using (var cmd = new NpgsqlCommand(command, conn))
                {
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        invoices.Add(new Invoice(
                            reader.GetString(0),
                            reader.GetString(1),
                            reader.GetString(2),
                            reader.GetString(3),
                            reader.GetString(4),
                            reader.GetString(5),
                            reader.GetValue(6) == null ? "True" : "False"
                            ));
                    }
                    reader.Close();
                }
            }

            return new OkObjectResult(invoices);
        }
    
        
    }

    public class adicionar_notas
    {
        private readonly ILogger<obter_notas> _logger;
        private static string FormatarQuerryInsercao(string ReferenceMonth, string ReferenceYear, string Document, string Description, string Amount, string CreatedAt, string DeactivatedAt, string IsActive)
        {
            string command = String.Format("INSERT INTO \"invoice\"(\"ReferenceMonth\", \"ReferenceYear\", \"Document\", \"Description\", \"Amount\", \"CreatedAt\", \"DeactivatedAt\", \"IsActive\") VALUES ('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}')", ReferenceMonth, ReferenceYear, Document, Description, Amount, CreatedAt, DeactivatedAt, IsActive);
            
            return command;
        }

        public adicionar_notas(ILogger<obter_notas> log)
        {
            _logger = log;
        }
        public List<Invoice> invoices = new List<Invoice>();
        const string connectionString = "Server=projetostone.postgres.database.azure.com,5432;Initial Catalog=notas_fiscais;Username=guilhermelima;Password=projetostone2022!;";
        // Linhas abaixo são referentes a funções do azure functions
        [FunctionName("adicionar_notas")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "name" })]
        //Parametros da funcao, com obrigatoriedade ou não
        [OpenApiParameter(name: "ReferenceMonth", In = ParameterLocation.Query, Required = true, Type = typeof(int), Description = "O mês de referência da nota fiscal")]
        [OpenApiParameter(name: "ReferenceYear", In = ParameterLocation.Query, Required = true, Type = typeof(int), Description = "O ano de referência da nota fiscal")]
        [OpenApiParameter(name: "Document", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "O Documento da nota fiscal")]
        [OpenApiParameter(name: "Description", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "A descrição da nota fiscal")]
        [OpenApiParameter(name: "Amount", In = ParameterLocation.Query, Required = true, Type = typeof(float), Description = "O valor da nota fiscal")]
        [OpenApiParameter(name: "CreatedAt", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "A data de criação da nota fiscal")]
        [OpenApiParameter(name: "DeactivatedAt", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "A data de desativação da nota fiscal")]
        //respostas possiveis (mostrar no swagger)
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "text/plain", bodyType: typeof(string), Description = "The BadRequest response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "text/plain", bodyType: typeof(string), Description = "The InternalServerError response")]

        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req)
        {
            string connString = "Server=projetostone.postgres.database.azure.com;Username=guilhermelima;Database=notas_fiscais;Port=5432;Password=projetostone2022!;SSLMode=Prefer";
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string ReferenceMonth = req.Query["ReferenceMonth"];
            string ReferenceYear = req.Query["ReferenceYear"];
            string Document = req.Query["Document"];
            string Description = req.Query["Description"];
            string Amount = req.Query["Amount"];
            string CreatedAtEntrada = req.Query["CreatedAt"];
            string DeactivatedAtEntrada = req.Query["DeactivatedAt"];
            string IsActive = "True";

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            ReferenceMonth = ReferenceMonth ?? data?.ReferenceMonth;
            ReferenceYear = ReferenceYear ?? data?.ReferenceYear;
            Document = Document ?? data?.Document;
            Description = Description ?? data?.Description;
            Amount = Amount ?? data?.Amount;
            CreatedAtEntrada = CreatedAtEntrada ?? data?.CreatedAt;
            DeactivatedAtEntrada = DeactivatedAtEntrada ?? data?.DeactivatedAt;

            if (string.IsNullOrEmpty(ReferenceMonth) || string.IsNullOrEmpty(ReferenceYear) || string.IsNullOrEmpty(Document) || string.IsNullOrEmpty(Description) || string.IsNullOrEmpty(Amount))
            {
                return new BadRequestObjectResult("Por favor, preencha todos os campos.");
            }
            string CreatedAt = !string.IsNullOrEmpty(CreatedAtEntrada) ? CreatedAtEntrada : DateTime.Now.ToString("yyyy/MM/dd");
            string DeactivatedAt = !string.IsNullOrEmpty(DeactivatedAtEntrada) ? DeactivatedAtEntrada : null;
            string command = FormatarQuerryInsercao(ReferenceMonth, ReferenceYear, Document, Description, Amount, CreatedAt, DeactivatedAt, IsActive);

            using (var conn = new NpgsqlConnection(connString))
            {

                Console.Out.WriteLine("Opening connection");
                conn.Open();
                using (var cmd = new NpgsqlCommand(command, conn))
                {
                    cmd.ExecuteNonQuery();
                }
                
            }

            return new OkObjectResult("Nota adicionada com sucesso.");
        }
    
        
    }

    public class alterar_nota
    {
        private readonly ILogger<obter_notas> _logger;
        private static string FormatarQuerryAlteracao(string ReferenceMonth, string ReferenceYear, string Document, string Description, string Amount, string CreatedAt, string DeactivatedAt, string IsActive)
        {
            string command = String.Format("INSERT INTO \"invoice\"(\"ReferenceMonth\", \"ReferenceYear\", \"Document\", \"Description\", \"Amount\", \"CreatedAt\", \"DeactivatedAt\", \"IsActive\") VALUES ('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}')", ReferenceMonth, ReferenceYear, Document, Description, Amount, CreatedAt, DeactivatedAt, IsActive);
            
            return command;
        }

        public alterar_nota(ILogger<obter_notas> log)
        {
            _logger = log;
        }
        public List<Invoice> invoices = new List<Invoice>();
        const string connectionString = "Server=projetostone.postgres.database.azure.com,5432;Initial Catalog=notas_fiscais;Username=guilhermelima;Password=projetostone2022!;";
        // Linhas abaixo são referentes a funções do azure functions
        [FunctionName("alterar_nota")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "name" })]
        //Parametros da funcao, com obrigatoriedade ou não
        [OpenApiParameter(name: "ReferenceMonth", In = ParameterLocation.Query, Required = true, Type = typeof(int), Description = "O mês de referência da nota fiscal")]
        [OpenApiParameter(name: "ReferenceYear", In = ParameterLocation.Query, Required = true, Type = typeof(int), Description = "O ano de referência da nota fiscal")]
        [OpenApiParameter(name: "Document", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "O Documento da nota fiscal")]
        [OpenApiParameter(name: "Description", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "A descrição da nota fiscal")]
        [OpenApiParameter(name: "Amount", In = ParameterLocation.Query, Required = true, Type = typeof(float), Description = "O valor da nota fiscal")]
        [OpenApiParameter(name: "CreatedAt", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "A data de criação da nota fiscal")]
        [OpenApiParameter(name: "DeactivatedAt", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "A data de desativação da nota fiscal")]
        //respostas possiveis (mostrar no swagger)
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "text/plain", bodyType: typeof(string), Description = "The BadRequest response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "text/plain", bodyType: typeof(string), Description = "The InternalServerError response")]

        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = null)] HttpRequest req)
        {
            string connString = "Server=projetostone.postgres.database.azure.com;Username=guilhermelima;Database=notas_fiscais;Port=5432;Password=projetostone2022!;SSLMode=Prefer";
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string ReferenceMonth = req.Query["ReferenceMonth"];
            string ReferenceYear = req.Query["ReferenceYear"];
            string Document = req.Query["Document"];
            string Description = req.Query["Description"];
            string Amount = req.Query["Amount"];
            string CreatedAt = req.Query["CreatedAt"];
            string DeactivatedAt = req.Query["DeactivatedAt"];
            string IsActive = "True";

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            ReferenceMonth = ReferenceMonth ?? data?.ReferenceMonth;
            ReferenceYear = ReferenceYear ?? data?.ReferenceYear;
            Document = Document ?? data?.Document;
            Description = Description ?? data?.Description;
            Amount = Amount ?? data?.Amount;
            CreatedAt = CreatedAt ?? data?.CreatedAt;
            DeactivatedAt = DeactivatedAt ?? data?.DeactivatedAt;

            string command = FormatarQuerryAlteracao(ReferenceMonth, ReferenceYear, Document, Description, Amount, CreatedAt, DeactivatedAt, IsActive);

            using (var conn = new NpgsqlConnection(connString))
            {

                Console.Out.WriteLine("Opening connection");
                conn.Open();
                using (var cmd = new NpgsqlCommand(command, conn))
                {
                    cmd.ExecuteNonQuery();
                }
                
            }

            return new OkObjectResult("Nota adicionada com sucesso.");
        }
    
        
    }

}

