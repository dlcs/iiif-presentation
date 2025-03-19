using System.Text.Json;
using AWS.SQS;
using BackgroundHandler.Helpers;
using Core.Auth;
using Core.Helpers;
using IIIF.Presentation.V3.Strings;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Database.Collections;
using Models.Database.General;
using Repository;

namespace BackgroundHandler.CustomerCreation;

/// <summary>
/// Handler for customer created messages
/// </summary>
public class CustomerCreatedMessageHandler(
    PresentationContext dbContext,
    ILogger<CustomerCreatedMessageHandler> logger)
    : IMessageHandler
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken)
    {
        using (LogContextHelpers.SetServiceName(nameof(CustomerCreatedMessageHandler)))
        {
            try
            {
                var customerCreatedMessage = DeserializeMessage(message);

                await EnsureRootCollection(customerCreatedMessage, cancellationToken);

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling customer-created message {MessageId}", message.MessageId);
            }
        }

        return false;
    }

    private async Task EnsureRootCollection(CustomerCreatedMessage customerCreatedMessage, CancellationToken cancellationToken)
    {
        var customerId = customerCreatedMessage.Id;
        
        logger.LogInformation("Ensuring new customer {CustomerId} has root collection", customerId);

        if (await dbContext.Collections.AnyAsync(
                c => c.Id == KnownCollections.RootCollection && c.CustomerId == customerId,
                cancellationToken))
        {
            logger.LogInformation("Customer {CustomerId} already has root collection, no-op", customerId);
            return;
        }
        
        var dateCreated = DateTime.UtcNow;
        var collection = new Collection
        {
            Id = KnownCollections.RootCollection,
            UsePath = true,
            Created = dateCreated,
            Modified = dateCreated,
            CreatedBy = Authorizer.GetUser(),
            CustomerId = customerId,
            IsPublic = true,
            IsStorageCollection = true,
            Label = new LanguageMap("en", "IIIF Home"),
            Hierarchy =
            [
                new Hierarchy
                {
                    Slug = string.Empty,
                    Canonical = true,
                    Type = ResourceType.StorageCollection,
                }
            ]
        };

        await dbContext.Collections.AddAsync(collection, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CustomerCreatedMessage DeserializeMessage(QueueMessage message)
    {
        var deserialized = JsonSerializer.Deserialize<CustomerCreatedMessage>(message.Body, JsonSerializerOptions);
        return deserialized.ThrowIfNull(nameof(deserialized));
    }
}
