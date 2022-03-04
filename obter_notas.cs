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
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;

namespace Company.Function
{
    public class obter_notas
    {
        private readonly ILogger<obter_notas> _logger;
        private static string FormatarQuerrySelect(int InvoiceId, string ReferenceMonth, string ReferenceYear, string Document, string OrderBy, string Offset, string Limit)
        {
            //transform id in string
            string id_string = InvoiceId.ToString();
            string command = "SELECT * FROM \"invoice\" WHERE \"IsActive\" = 'True'";
            command = id_string == "0"
                ? command
                : command + String.Format(" AND \"InvoiceId\" = '{0}'", Convert.ToString(id_string));
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
            command = command + String.Format(" OFFSET {0} LIMIT {1}", Offset, Limit);
            return command;
        }

        public obter_notas(ILogger<obter_notas> log)
        {
            _logger = log;
        }
        public List<Invoice> invoices = new List<Invoice>();

        // Linhas abaixo são referentes a funções do azure functions
        [FunctionName("obter_notas")]
        [OpenApiOperation(operationId: "Run_obter_notas", tags: new[] { "Invoice" } , Summary = "Obter as notas fiscais do servidor", Description = "Metodo usado para recuperar todas notas fiscais, ou parcialmente filtradas.", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiSecurity("apikey",SecuritySchemeType.ApiKey, In = OpenApiSecurityLocationType.Query, Name = "code")]
        [OpenApiParameter(name: "InvoiceId", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "O id da nota fiscal")]
        [OpenApiParameter(name: "ReferenceMonth", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "O mês de referência da nota fiscal")]
        [OpenApiParameter(name: "ReferenceYear", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "O ano de referência da nota fiscal")]
        [OpenApiParameter(name: "Document", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "O Documento da nota fiscal")]
        [OpenApiParameter(name: "OrderBy", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "O campo para ordenar a lista")]
        [OpenApiParameter(name: "Offset", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Paginação da api.")]
        [OpenApiParameter(name: "Limit", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Quantidade de registros para retornar, limitado a 50.")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(Invoice), Description = "The OK response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "text/plain", bodyType: typeof(string), Description = "The BadRequest response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "text/plain", bodyType: typeof(string), Description = "The InternalServerError response")]

        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req)
        {
            string connString = System.Environment.GetEnvironmentVariable("PATH_TO_PROJECT_STONE_DATABASE");
            _logger.LogInformation("C# HTTP trigger function processou uma requisição get.");

            int InvoiceId = Convert.ToInt32(req.Query["InvoiceId"]);
            string ReferenceMonth = req.Query["ReferenceMonth"];
            string ReferenceYear = req.Query["ReferenceYear"];
            string Document = req.Query["Document"];
            string OrderBy = req.Query["OrderBy"];
            string OffsetEntrada = req.Query["Offset"];
            string LimitEntrada = req.Query["Limit"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            ReferenceMonth = ReferenceMonth ?? data?.ReferenceMonth;
            ReferenceYear = ReferenceYear ?? data?.ReferenceYear;
            Document = Document ?? data?.Document;
            OrderBy = OrderBy ?? data?.OrderBy;
            OffsetEntrada = OffsetEntrada ?? data?.OffsetEntrada;
            LimitEntrada = LimitEntrada ?? data?.LimitEntrada;
            int LimitEntradaConvertido = Convert.ToInt32(LimitEntrada);
            if (LimitEntradaConvertido > 50)
            {
                LimitEntrada = "50";
            }

            string Offset = !string.IsNullOrEmpty(OffsetEntrada) ? OffsetEntrada : "0";
            string Limit = !string.IsNullOrEmpty(LimitEntrada) ? LimitEntrada : "50";
            if (OrderBy != null && OrderBy != "ReferenceMonth" && OrderBy != "ReferenceYear" && OrderBy != "Document" && OrderBy != "Amount" && OrderBy != "CreatedAt" && OrderBy != "DeactivatedAt")
            {
                return new BadRequestObjectResult("OrderBy não é válido");
            }

            string command = FormatarQuerrySelect(InvoiceId, ReferenceMonth, ReferenceYear, Document, OrderBy, Offset, Limit);
            using (var conn = new NpgsqlConnection(connString))
            {
                try{
                    conn.Open();
                }
                catch (Exception)
                {
                    //return server erro
                    return new StatusCodeResult(500);
                }
                try{
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
                                reader.GetString(6),
                                Convert.ToInt32(reader.GetValue(8))
                                ));
                        }
                        reader.Close();
                    }
                }
                catch (Exception)
                {
                    return new BadRequestObjectResult("Parametros invalidos.");
                }
            }

            return new OkObjectResult(invoices);
        }
    
        
    }

    public class adicionar_notas
    {
        private readonly ILogger<adicionar_notas> _logger;
        private static string FormatarQuerryInsercao(string ReferenceMonth, string ReferenceYear, string Document, string Description, string Amount, string CreatedAt, string DeactivatedAt, string IsActive)
        {
            string command = String.Format("INSERT INTO \"invoice\"(\"ReferenceMonth\", \"ReferenceYear\", \"Document\", \"Description\", \"Amount\", \"CreatedAt\", \"DeactivatedAt\", \"IsActive\") VALUES ('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}')", ReferenceMonth, ReferenceYear, Document, Description, Amount, CreatedAt, DeactivatedAt, IsActive);
            
            return command;
        }

        public adicionar_notas(ILogger<adicionar_notas> log)
        {
            _logger = log;
        }
        public List<Invoice> invoices = new List<Invoice>();
        [FunctionName("adicionar_notas")]
        [OpenApiOperation(operationId: "Run_adicionar_notas", tags: new[] { "Invoice" } , Summary = "Adicionar as notas fiscais do servidor", Description = "Metodo usado para Adicionar notas fiscais no servidor.", Visibility = OpenApiVisibilityType.Important)]
        //Parametros da funcao, com obrigatoriedade ou não
        [OpenApiSecurity("apikey",SecuritySchemeType.ApiKey, In = OpenApiSecurityLocationType.Query, Name = "code")]
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

        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req)
        {
            string connString = System.Environment.GetEnvironmentVariable(variable : "PATH_TO_PROJECT_STONE_DATABASE");
            _logger.LogInformation("C# HTTP trigger function precessou uma requisição de adicionar notas fiscais");

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

            if (string.IsNullOrEmpty(ReferenceMonth) || string.IsNullOrEmpty(ReferenceYear) || string.IsNullOrEmpty(Document) || string.IsNullOrEmpty(Description) || string.IsNullOrEmpty(Amount))
            {
                return new BadRequestObjectResult("Por favor, preencha todos os campos.");
            }
            CreatedAt = !string.IsNullOrEmpty(CreatedAt) ? CreatedAt : DateTime.Now.ToString("yyyy/MM/dd");
            DeactivatedAt = !string.IsNullOrEmpty(DeactivatedAt) ? DeactivatedAt : null;
            string command = FormatarQuerryInsercao(ReferenceMonth, ReferenceYear, Document, Description, Amount, CreatedAt, DeactivatedAt, IsActive);

            using (var conn = new NpgsqlConnection(connString))
            {

                try{
                    conn.Open();
                }
                catch (Exception)
                {
                    return new StatusCodeResult(500);
                }
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
        private readonly ILogger<alterar_nota> _logger;
        private static string FormatarQuerryAlteracao(int InvoiceId, string ReferenceMonth, string ReferenceYear, string Document, string Description, string Amount, string CreatedAt, string DeactivatedAt, string IsActive)
        {
            DeactivatedAt = DeactivatedAt == null ? "" : DeactivatedAt;
            string command = String.Format("UPDATE \"invoice\" SET \"ReferenceMonth\" = '{1}', \"ReferenceYear\" = '{2}', \"Document\" = '{3}', \"Description\" = '{4}', \"Amount\" = '{5}', \"CreatedAt\" = '{6}', \"DeactivatedAt\" = '{7}', \"IsActive\" = '{8}' WHERE \"InvoiceId\" = '{0}'", InvoiceId, ReferenceMonth, ReferenceYear, Document, Description, Amount, CreatedAt, DeactivatedAt, IsActive);
            
            return command;
        }

        public alterar_nota(ILogger<alterar_nota> log)
        {
            _logger = log;
        }
        public List<Invoice> invoices = new List<Invoice>();
        [FunctionName("alterar_nota")]
        [OpenApiOperation(operationId: "Run_alterar_nota", tags: new[] { "Invoice" } , Summary = "Alterar as notas fiscais do servidor", Description = "Metodo usado para modificar notas fiscais no servidor.", Visibility = OpenApiVisibilityType.Important)]
        //Parametros da funcao, com obrigatoriedade ou não
        [OpenApiSecurity("apikey",SecuritySchemeType.ApiKey, In = OpenApiSecurityLocationType.Query, Name = "code")]
        [OpenApiParameter(name: "InvoiceId", In = ParameterLocation.Query, Required = true, Type = typeof(int), Description = "O id da nota fiscal")]
        [OpenApiParameter(name: "ReferenceMonth", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "O mês de referência da nota fiscal")]
        [OpenApiParameter(name: "ReferenceYear", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "O ano de referência da nota fiscal")]
        [OpenApiParameter(name: "Document", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "O Documento da nota fiscal")]
        [OpenApiParameter(name: "Description", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "A descrição da nota fiscal")]
        [OpenApiParameter(name: "Amount", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "O valor da nota fiscal")]
        [OpenApiParameter(name: "CreatedAt", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "A data de criação da nota fiscal")]
        [OpenApiParameter(name: "DeactivatedAt", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "A data de desativação da nota fiscal")]
        //respostas possiveis (mostrar no swagger)
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "text/plain", bodyType: typeof(string), Description = "The BadRequest response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "text/plain", bodyType: typeof(string), Description = "The InternalServerError response")]

        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "put", Route = null)] HttpRequest req)
        {
            string connString = System.Environment.GetEnvironmentVariable(variable : "PATH_TO_PROJECT_STONE_DATABASE");
            _logger.LogInformation("C# HTTP trigger function processou uma requisição de alteração de nota fiscal.");

            string ReferenceMonth = req.Query["ReferenceMonth"];
            string ReferenceYear = req.Query["ReferenceYear"];
            string Document = req.Query["Document"];
            string Description = req.Query["Description"];
            string Amount = req.Query["Amount"];
            string CreatedAt = req.Query["CreatedAt"];
            string DeactivatedAt = req.Query["DeactivatedAt"];
            string IsActive = "True";
            int InvoiceId = Convert.ToInt32(req.Query["InvoiceId"]);

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            ReferenceMonth = ReferenceMonth ?? data?.ReferenceMonth;
            ReferenceYear = ReferenceYear ?? data?.ReferenceYear;
            Document = Document ?? data?.Document;
            Description = Description ?? data?.Description;
            Amount = Amount ?? data?.Amount;
            CreatedAt = CreatedAt ?? data?.CreatedAt;
            DeactivatedAt = DeactivatedAt ?? data?.DeactivatedAt;

            DeactivatedAt = !string.IsNullOrEmpty(DeactivatedAt) ? DeactivatedAt : null;
            if (string.IsNullOrEmpty(ReferenceMonth) || string.IsNullOrEmpty(ReferenceYear) || string.IsNullOrEmpty(Document) || string.IsNullOrEmpty(Description) || string.IsNullOrEmpty(Amount) || string.IsNullOrEmpty(CreatedAt))
            {
                return new BadRequestObjectResult("Por favor, preencha todos os campos.");
            }

            string command = FormatarQuerryAlteracao(InvoiceId, ReferenceMonth, ReferenceYear, Document, Description, Amount, CreatedAt, DeactivatedAt, IsActive);

            using (var conn = new NpgsqlConnection(connString))
            {

                try{
                    conn.Open();
                }
                catch (Exception)
                {
                    return new StatusCodeResult(500);
                }
                try{
                    using (var cmd = new NpgsqlCommand(command, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception)
                {
                    return new BadRequestObjectResult("Parametros invalidos.");
                }
                
            }

            return new OkObjectResult("Nota alterada com sucesso.");
        }
    
        
    }

    public class alterar_informacao_de_nota
    {
        private readonly ILogger<alterar_informacao_de_nota> _logger;
        private static string FormatarQuerryAlteracao(int InvoiceId, string ReferenceMonth, string ReferenceYear, string Document, string Description, string Amount, string CreatedAt, string DeactivatedAt, string IsActive)
        {
            string command = "UPDATE \"invoice\" SET ";
            command = string.IsNullOrEmpty(ReferenceMonth)
                ? command
                : command + String.Format("\"ReferenceMonth\" = '{0}', ", ReferenceMonth);
            command = string.IsNullOrEmpty(ReferenceYear)
                ? command
                : command + String.Format("\"ReferenceYear\" = '{0}', ", ReferenceYear);
            command = string.IsNullOrEmpty(Document)
                ? command
                : command + String.Format("\"Document\" = '{0}', ", Document);
            command = string.IsNullOrEmpty(Amount)
                ? command
                : command + String.Format("\"Amount\" = '{0}', ", Amount);
            command = string.IsNullOrEmpty(CreatedAt)
                ? command
                : command + String.Format("\"CreatedAt\" = '{0}', ", CreatedAt);
            command = string.IsNullOrEmpty(DeactivatedAt)
                ? command
                : command + String.Format("\"DeactivatedAt\" = '{0}', ", DeactivatedAt);
            //retirar as ultimas 4 letras de command
            command = command.Remove(command.Length - 2);
            command = command + String.Format(" WHERE \"InvoiceId\" = {0};", InvoiceId);
            return command;
        }

        public alterar_informacao_de_nota(ILogger<alterar_informacao_de_nota> log)
        {
            _logger = log;
        }
        public List<Invoice> invoices = new List<Invoice>();
        [FunctionName("alterar_informacao_de_nota")]
        [OpenApiOperation(operationId: "Run_alterar_informacao_de_nota", tags: new[] { "Invoice" } , Summary = "Alterar as notas fiscais do servidor massivamente", Description = "Metodo usado para modificar notas fiscais no servidor de modo massivo.", Visibility = OpenApiVisibilityType.Important)]
        //Parametros da funcao, com obrigatoriedade ou não
        [OpenApiSecurity("apikey",SecuritySchemeType.ApiKey, In = OpenApiSecurityLocationType.Query, Name = "code")]
        [OpenApiParameter(name: "InvoiceId", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "O id da nota fiscal")]
        [OpenApiParameter(name: "ReferenceMonth", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "O mês de referência da nota fiscal")]
        [OpenApiParameter(name: "ReferenceYear", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "O ano de referência da nota fiscal")]
        [OpenApiParameter(name: "Document", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "O Documento da nota fiscal")]
        [OpenApiParameter(name: "Description", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "A descrição da nota fiscal")]
        [OpenApiParameter(name: "Amount", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "O valor da nota fiscal")]
        [OpenApiParameter(name: "CreatedAt", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "A data de criação da nota fiscal")]
        [OpenApiParameter(name: "DeactivatedAt", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "A data de desativação da nota fiscal")]
        //respostas possiveis (mostrar no swagger)
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "text/plain", bodyType: typeof(string), Description = "The BadRequest response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "text/plain", bodyType: typeof(string), Description = "The InternalServerError response")]

        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "patch", Route = "alterar_notas_em_massa")] HttpRequest req)
        {
            string connString = System.Environment.GetEnvironmentVariable(variable : "PATH_TO_PROJECT_STONE_DATABASE");
            _logger.LogInformation("C# HTTP trigger function processou uma requisição de alteração de dados especificos em notas fiscais.");

            string ReferenceMonth = req.Query["ReferenceMonth"];
            string ReferenceYear = req.Query["ReferenceYear"];
            string Document = req.Query["Document"];
            string Description = req.Query["Description"];
            string Amount = req.Query["Amount"];
            string CreatedAt = req.Query["CreatedAt"];
            string DeactivatedAt = req.Query["DeactivatedAt"];
            string IsActive = "True";
            int InvoiceId = Convert.ToInt32(req.Query["InvoiceId"]);

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            ReferenceMonth = ReferenceMonth ?? data?.ReferenceMonth;
            ReferenceYear = ReferenceYear ?? data?.ReferenceYear;
            Document = Document ?? data?.Document;
            Description = Description ?? data?.Description;
            Amount = Amount ?? data?.Amount;
            CreatedAt = CreatedAt ?? data?.CreatedAt;
            DeactivatedAt = DeactivatedAt ?? data?.DeactivatedAt;

            if (!string.IsNullOrEmpty(ReferenceMonth) && !string.IsNullOrEmpty(ReferenceYear) && !string.IsNullOrEmpty(Document) && !string.IsNullOrEmpty(Description) && !string.IsNullOrEmpty(Amount) && !string.IsNullOrEmpty(CreatedAt) && !string.IsNullOrEmpty(DeactivatedAt))
            {
                return new BadRequestObjectResult("Por favor, preencha ao menos um campo para fazer a alteração.");
            }

            string command = FormatarQuerryAlteracao(InvoiceId, ReferenceMonth, ReferenceYear, Document, Description, Amount, CreatedAt, DeactivatedAt, IsActive);

            using (var conn = new NpgsqlConnection(connString))
            {

                try{
                    conn.Open();
                }
                catch (Exception)
                {
                    //return server erro
                    return new StatusCodeResult(500);
                }
                try{
                    using (var cmd = new NpgsqlCommand(command, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception)
                {
                    //return server erro
                    return new BadRequestObjectResult("Parametros invalidos.");
                }
                
            }

            return new OkObjectResult("Notas modificada com sucesso.");
        }
    
        
    }

    public class deletar_nota
    {
        private readonly ILogger<deletar_nota> _logger;
        private static string FormatarQuerrydeletar(int InvoiceId, string ReferenceMonth, string ReferenceYear, string Document, string Description, string Amount, string CreatedAt, string DeactivatedAt)
        {
            string command = "UPDATE \"invoice\" SET \"IsActive\" = 'False' WHERE ";
            string id_string = InvoiceId.ToString();
            command = string.IsNullOrEmpty(id_string)
                ? command
                : command + String.Format("\"InvoiceId\" = '{0}' AND ", Convert.ToString(id_string));
            command = string.IsNullOrEmpty(ReferenceMonth)
                ? command
                : command + String.Format("\"ReferenceMonth\" = '{0}' AND ", ReferenceMonth);
            command = string.IsNullOrEmpty(ReferenceYear)
                ? command
                : command + String.Format("\"ReferenceYear\" = '{0}' AND ", ReferenceYear);
            command = string.IsNullOrEmpty(Document)
                ? command
                : command + String.Format("\"Document\" = '{0}' AND ", Document);
            command = string.IsNullOrEmpty(Amount)
                ? command
                : command + String.Format("\"Amount\" = '{0}' AND ", Amount);
            command = string.IsNullOrEmpty(CreatedAt)
                ? command
                : command + String.Format("\"CreatedAt\" = '{0}' AND ", CreatedAt);
            command = string.IsNullOrEmpty(DeactivatedAt)
                ? command
                : command + String.Format("\"DeactivatedAt\" = '{0}' AND ", DeactivatedAt);
            //retirar as ultimas 4 letras de command
            command = command.Remove(command.Length - 4);
            command = command + ';';
            return command;
        }

        public deletar_nota(ILogger<deletar_nota> log)
        {
            _logger = log;
        }
        public List<Invoice> invoices = new List<Invoice>();
        [FunctionName("deletar_nota")]
        [OpenApiOperation(operationId: "Run_deletar_nota", tags: new[] { "Invoice" } , Summary = "Deletar as notas fiscais do servidor", Description = "Metodo usado para deletar notas fiscais no servidor.", Visibility = OpenApiVisibilityType.Important)]
        //Parametros da funcao, com obrigatoriedade ou não
        [OpenApiSecurity("apikey",SecuritySchemeType.ApiKey, In = OpenApiSecurityLocationType.Query, Name = "code")]
        [OpenApiParameter(name: "InvoiceId", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "O id da nota fiscal")]
        [OpenApiParameter(name: "ReferenceMonth", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "O mês de referência da nota fiscal")]
        [OpenApiParameter(name: "ReferenceYear", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "O ano de referência da nota fiscal")]
        [OpenApiParameter(name: "Document", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "O Documento da nota fiscal")]
        [OpenApiParameter(name: "Description", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "A descrição da nota fiscal")]
        [OpenApiParameter(name: "Amount", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "O valor da nota fiscal")]
        [OpenApiParameter(name: "CreatedAt", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "A data de criação da nota fiscal")]
        [OpenApiParameter(name: "DeactivatedAt", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "A data de desativação da nota fiscal")]
        //respostas possiveis (mostrar no swagger)
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "text/plain", bodyType: typeof(string), Description = "The BadRequest response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "text/plain", bodyType: typeof(string), Description = "The InternalServerError response")]

        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "delete", Route = null)] HttpRequest req)
        {
            string connString = System.Environment.GetEnvironmentVariable(variable : "PATH_TO_PROJECT_STONE_DATABASE");
            _logger.LogInformation("C# HTTP trigger function processou uma requisição de deletar notas fiscais.");

            string ReferenceMonth = req.Query["ReferenceMonth"];
            string ReferenceYear = req.Query["ReferenceYear"];
            string Document = req.Query["Document"];
            string Description = req.Query["Description"];
            string Amount = req.Query["Amount"];
            string CreatedAt = req.Query["CreatedAt"];
            string DeactivatedAt = req.Query["DeactivatedAt"];
            int InvoiceId = Convert.ToInt32(req.Query["InvoiceId"]);

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            ReferenceMonth = ReferenceMonth ?? data?.ReferenceMonth;
            ReferenceYear = ReferenceYear ?? data?.ReferenceYear;
            Document = Document ?? data?.Document;
            Description = Description ?? data?.Description;
            Amount = Amount ?? data?.Amount;
            CreatedAt = CreatedAt ?? data?.CreatedAt;
            DeactivatedAt = DeactivatedAt ?? data?.DeactivatedAt;

            string command = FormatarQuerrydeletar(InvoiceId, ReferenceMonth, ReferenceYear, Document, Description, Amount, CreatedAt, DeactivatedAt);

            using (var conn = new NpgsqlConnection(connString))
            {

                try{
                    conn.Open();
                }
                catch (Exception)
                {
                    //return server erro
                    return new StatusCodeResult(500);
                }
                using (var cmd = new NpgsqlCommand(command, conn))
                {
                    cmd.ExecuteNonQuery();
                }
                
            }

            return new OkObjectResult("Nota deletada com sucesso.");
        }
    
        
    }

}

