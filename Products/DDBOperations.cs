using Amazon.DynamoDBv2.Model;
using Amazon.DynamoDBv2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using System.Diagnostics.Metrics;

namespace Products;

public class DDBOperations
{
    private AmazonDynamoDBClient _client;
    private static string PRODUCTS_TABLE = Environment.GetEnvironmentVariable("TABLE_NAME");

    public async Task CreateNewDDBItem(Product product)
    {
        // If a warm start, reuse the existing object.
        if (_client == null)
        {
            _client = new AmazonDynamoDBClient();
        }

        var item = new Dictionary<string, AttributeValue>(4)
        {
            { "BarCode", new AttributeValue(product.BarCode) },
            { "ProductName", new AttributeValue(product.Name) },
            { "Description", new AttributeValue(product.Description) },
            { "Price", new AttributeValue(product.Price.ToString()) },
        };

        var request = new PutItemRequest
        {
            TableName = PRODUCTS_TABLE,
            Item = item,
            ConditionExpression = "attribute_not_exists(BarCode)"
        };
        await this._client.PutItemAsync(request);
    }

    public async Task UpdateExistingDDBItem(Product product)
    {
        // If a warm start, reuse the existing object.
        if (_client == null)
        {
            _client = new AmazonDynamoDBClient();
        }

        var key = new Dictionary<string, AttributeValue>(1)
        {
            { "BarCode", new AttributeValue(product.BarCode) }
        };

        var updatedAttributes = new Dictionary<string, AttributeValueUpdate>(4)
        {
            { "ProductName", new AttributeValueUpdate(new AttributeValue(product.Name), AttributeAction.PUT)  },
            { "Description", new AttributeValueUpdate(new AttributeValue(product.Description), AttributeAction.PUT)  },
            { "Price", new AttributeValueUpdate(new AttributeValue(product.Price.ToString()), AttributeAction.PUT) }
        };

        var request = new UpdateItemRequest
        {
            TableName = PRODUCTS_TABLE,
            Key = key,// Key is provided so no need for conditional expression, otherwise DDB throws an exception.
            AttributeUpdates = updatedAttributes
        };

        await this._client.UpdateItemAsync(request);
    }

    public async Task<Product> GetItemFromDDB(string barcode)
    {
        // If a warm start, reuse the existing object.
        if (_client == null)
        {
            _client = new AmazonDynamoDBClient();
        }


        var getItemResponse = await this._client.GetItemAsync(new GetItemRequest(PRODUCTS_TABLE,
                                                new Dictionary<string, AttributeValue>(1)
                                                {
                                                    {"BarCode", new AttributeValue(barcode)}
                                                }));

        if (!getItemResponse.IsItemSet) // Not found!
        {
            return null;
        }

        Product product = new Product();
        product.BarCode = barcode;
        product.Name = KeyExistsInItem(getItemResponse.Item, "ProductName") ? getItemResponse.Item["ProductName"].S : null;
        product.Description = KeyExistsInItem(getItemResponse.Item, "Description") ? getItemResponse.Item["Description"].S : null;
        product.Price = KeyExistsInItem(getItemResponse.Item, "Price") ? decimal.Parse(getItemResponse.Item["Price"].S) : decimal.Zero;

        return product;
    }

    public async Task DeleteItemFromDDB(string barCode)
    {
        // If a warm start, reuse the existing object.
        if (_client == null)
        {
            _client = new AmazonDynamoDBClient();
        }

        var delItemResponse = await this._client.DeleteItemAsync(new DeleteItemRequest(PRODUCTS_TABLE,
                                                new Dictionary<string, AttributeValue>(1)
                                                {
                                                    {"BarCode", new AttributeValue(barCode)}
                                                }));
    }

    /// <summary>
    /// As DynamoDB is schema-less, it is possible that the attribute (key) we are looking for isn't in the item that is returned.
    /// So better we check if it exists first.
    /// </summary>
    /// <param name="item"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    private bool KeyExistsInItem(Dictionary<string, AttributeValue> item, string key)
    {
        return item.ContainsKey(key);
    }

}
