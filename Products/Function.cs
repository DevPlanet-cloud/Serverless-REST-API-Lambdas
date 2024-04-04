using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Runtime.Internal.Util;
using Microsoft.VisualBasic;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.SourceGeneratorLambdaJsonSerializer<Products.JsonApiGatewayContext>))]


namespace Products;

[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
[JsonSerializable(typeof(Product))]
public partial class JsonApiGatewayContext : JsonSerializerContext
{
}

public class Function
{
    private DDBOperations _dBOperations;

    /// <summary>
    /// Lambda function handler to create  a new Product over POST method.
    /// </summary>
    /// <param name="apigProxyEvent"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task<APIGatewayHttpApiV2ProxyResponse> CreateProductHandler(APIGatewayHttpApiV2ProxyRequest apigProxyEvent, ILambdaContext context)
    {
        if (apigProxyEvent.RequestContext.Http.Method != "POST" || string.IsNullOrEmpty(apigProxyEvent.Body))
        {
            string errorText = "{ \"Error\" : \"Invalid HttpMethod or empty request body!\" }";
            context.Logger.LogError(errorText);
            return GenerateAndReturnResponse((int)HttpStatusCode.MethodNotAllowed, errorText);
        }

        context.Logger.LogInformation("Creating new product using info: " + apigProxyEvent.Body);

        APIGatewayHttpApiV2ProxyResponse createdProduct = await CreateProduct(apigProxyEvent.Body);

        return createdProduct;
    }


    /// <summary>
    /// Lambda function handler to get a product over GET using the barcode
    /// </summary>
    /// <param name="jsonBody"></param>
    /// <returns></returns>
    public async Task<APIGatewayHttpApiV2ProxyResponse> GetProductHandler(APIGatewayHttpApiV2ProxyRequest apigProxyEvent, ILambdaContext context)
    {
        if (apigProxyEvent.RequestContext.Http.Method != "GET" || apigProxyEvent.PathParameters.ContainsKey("barcode") == false)
        {
            context.Logger.LogError("Invalid HttpMethod or missing argument!");
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }


        context.Logger.LogInformation("Requested Product Barcode: " + apigProxyEvent.PathParameters["barcode"]);

        APIGatewayHttpApiV2ProxyResponse existingProduct = await GetProductByBarCode(apigProxyEvent.PathParameters["barcode"]);

        return existingProduct;
    }

    /// <summary>
    /// Lambda function handler to update an existing Product over PUT method.
    /// </summary>
    /// <param name="apigProxyEvent"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task<APIGatewayHttpApiV2ProxyResponse> UpdateProductHandler(APIGatewayHttpApiV2ProxyRequest apigProxyEvent, ILambdaContext context)
    {
        if (apigProxyEvent.RequestContext.Http.Method != "PUT" || string.IsNullOrEmpty(apigProxyEvent.Body))
        {
            string errorText = "{ \"Error\" : \"Invalid HttpMethod or empty request body!\" }";
            context.Logger.LogError(errorText);
            return GenerateAndReturnResponse((int)HttpStatusCode.MethodNotAllowed, errorText);
        }

        context.Logger.LogInformation("Updating existing product using info: " + apigProxyEvent.Body);

        APIGatewayHttpApiV2ProxyResponse updatedProduct = await UpdateProduct(apigProxyEvent.Body);

        return updatedProduct;
    }

    /// <summary>
    /// Lambda function to delete an existing product over DEL using barcode
    /// </summary>
    /// <param name="apigProxyEvent"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task<APIGatewayHttpApiV2ProxyResponse> DeleteProductHandler(APIGatewayHttpApiV2ProxyRequest apigProxyEvent, ILambdaContext context)
    {
        if (apigProxyEvent.RequestContext.Http.Method != "DELETE" || apigProxyEvent.PathParameters.ContainsKey("barcode") == false)
        {
            context.Logger.LogError("Invalid HttpMethod or missing argument!");
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }


        context.Logger.LogInformation("Request Deletion for Product with barcode: " + apigProxyEvent.PathParameters["barcode"]);

        APIGatewayHttpApiV2ProxyResponse deletedProductResponse = await DeleteProductByBarCode(apigProxyEvent.PathParameters["barcode"]);

        return deletedProductResponse;
    }

    private async Task<APIGatewayHttpApiV2ProxyResponse> CreateProduct(string jsonBody)
    {
        Product product = JsonSerializer.Deserialize<Product>(jsonBody);

        if (product == null || !product.IsProductValid()) 
        {
            return GenerateAndReturnResponse((int)HttpStatusCode.BadRequest, "Required information missing for creating a new product");
        }

        if (_dBOperations == null)
        {
            // Initialize the global scoped object if null, so can be reused in subsequent Warm starts.
            _dBOperations = new DDBOperations();
        }

        try
        {
            await _dBOperations.CreateNewDDBItem(product);
        }
        catch (Exception ex)
        {
            return GenerateAndReturnResponse((int)HttpStatusCode.InternalServerError, $"Exception: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
        }

        return GenerateAndReturnResponse(200, JsonSerializer.Serialize(product));
    }

    private async Task<APIGatewayHttpApiV2ProxyResponse> GetProductByBarCode(string barCode)
    {

        Product product = null;

        if (_dBOperations == null)
        {
            // Initialize the global scoped object if null, so can be reused in subsequent Warm starts.
            _dBOperations = new DDBOperations();
        }

        try
        {
             product = await _dBOperations.GetItemFromDDB(barCode);

            if (product == null) 
            {
                return GenerateAndReturnResponse((int)HttpStatusCode.NotFound, "{ \"Error\" : \"Product not found!\" }");
            }
        }
        catch (Exception ex)
        {
            return GenerateAndReturnResponse((int)HttpStatusCode.InternalServerError, $"Exception: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
        }

        return GenerateAndReturnResponse(200, JsonSerializer.Serialize(product));
    }

    private async Task<APIGatewayHttpApiV2ProxyResponse> UpdateProduct(string jsonBody)
    {
        Product product = JsonSerializer.Deserialize<Product>(jsonBody);

        if (product == null || !product.IsProductValid())
        {
            return GenerateAndReturnResponse((int)HttpStatusCode.BadRequest, "Required information missing for creating a new product");
        }

        if (_dBOperations == null)
        {
            // Initialize the global scoped object if null, so can be reused in subsequent Warm starts.
            _dBOperations = new DDBOperations();
        }

        try
        {
            await _dBOperations.UpdateExistingDDBItem(product);
        }
        catch (Exception ex)
        {
            return GenerateAndReturnResponse((int)HttpStatusCode.InternalServerError, $"Exception: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
        }

        return GenerateAndReturnResponse(200, JsonSerializer.Serialize(product));
    }

    private async Task<APIGatewayHttpApiV2ProxyResponse> DeleteProductByBarCode(string barCode)
    {

        if (_dBOperations == null)
        {
            // Initialize the global scoped object if null, so can be reused in subsequent Warm starts.
            _dBOperations = new DDBOperations();
        }

        try
        {
            await _dBOperations.DeleteItemFromDDB(barCode);
            return GenerateAndReturnResponse((int)HttpStatusCode.OK, string.Empty);
        }
        catch (Exception ex)
        {
            return GenerateAndReturnResponse((int)HttpStatusCode.InternalServerError, $"Exception: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
        }
    }


    private APIGatewayHttpApiV2ProxyResponse GenerateAndReturnResponse(int statusCode, string jsonMessage)
    {
        return new APIGatewayHttpApiV2ProxyResponse
        {
            Body = jsonMessage,
            StatusCode = statusCode,
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };
    }
}
