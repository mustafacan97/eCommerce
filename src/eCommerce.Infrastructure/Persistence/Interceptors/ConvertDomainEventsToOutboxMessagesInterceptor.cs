﻿using eCommerce.Core.Entities.Directory;
using eCommerce.Core.Primitives;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;

namespace eCommerce.Infrastructure.Persistence.Interceptors;

public sealed class ConvertDomainEventsToOutboxMessagesInterceptor : SaveChangesInterceptor
{
    #region Public Methods

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;

        if (dbContext is null)
        {
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        var outboxMessages = dbContext.ChangeTracker
            .Entries<BaseEntity>()
            .Select(x => x.Entity)
            .SelectMany(x =>
            {
                var domainEvents = x.GetDomainEvents();
                x.ClearDomainEvents();
                return domainEvents;
            })
            .Select(domainEvent => new OutboxMessage()
            {
                CreatedOnUtc = DateTime.UtcNow,
                Type = domainEvent.GetType().AssemblyQualifiedName,
                Content = JsonSerializer.Serialize<object>(
                    domainEvent, 
                    new JsonSerializerOptions 
                    {
                        WriteIndented = true
                    })
            })
            .ToList();

        await dbContext.Set<OutboxMessage>().AddRangeAsync(outboxMessages, cancellationToken);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    #endregion
}
