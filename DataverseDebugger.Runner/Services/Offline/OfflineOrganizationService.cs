using System;
using System.Collections.Generic;
using System.Linq;
using DataverseDebugger.Runner.Abstractions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseDebugger.Runner.Services.Offline
{
    internal sealed class OfflineOrganizationService : IOrganizationService
    {
        internal static readonly Guid DefaultUserId = new Guid("11111111-1111-1111-1111-111111111111");
        internal static readonly Guid DefaultBusinessUnitId = new Guid("22222222-2222-2222-2222-222222222222");
        internal static readonly Guid DefaultOrganizationId = new Guid("33333333-3333-3333-3333-333333333333");

        private readonly Dictionary<string, Dictionary<Guid, Entity>> _store =
            new Dictionary<string, Dictionary<Guid, Entity>>(StringComparer.OrdinalIgnoreCase);
        private readonly Guid _userId;
        private readonly Guid _businessUnitId;
        private readonly Guid _organizationId;

        public OfflineOrganizationService()
            : this(null, null, null)
        {
        }

        public OfflineOrganizationService(
            Guid? userId = null,
            Guid? businessUnitId = null,
            Guid? organizationId = null)
        {
            _userId = userId ?? DefaultUserId;
            _businessUnitId = businessUnitId ?? DefaultBusinessUnitId;
            _organizationId = organizationId ?? DefaultOrganizationId;
        }

        internal void SeedEntity(Entity? entity)
        {
            if (entity == null)
            {
                return;
            }

            var clone = CloneEntity(entity, new ColumnSet(true));
            if (clone.Id == Guid.Empty)
            {
                clone.Id = Guid.NewGuid();
            }

            StoreEntity(clone, replaceExisting: true);
        }

        public Guid Create(Entity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (string.IsNullOrWhiteSpace(entity.LogicalName))
            {
                throw new InvalidOperationException("Create requires an entity logical name.");
            }

            if (entity.Id == Guid.Empty)
            {
                entity.Id = Guid.NewGuid();
            }

            var clone = CloneEntity(entity, new ColumnSet(true));
            StoreEntity(clone, replaceExisting: true);
            return entity.Id;
        }

        public void Update(Entity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (string.IsNullOrWhiteSpace(entity.LogicalName))
            {
                throw new InvalidOperationException("Update requires an entity logical name.");
            }
            if (entity.Id == Guid.Empty) throw new InvalidOperationException("Update requires an entity Id.");

            StoreEntity(entity, replaceExisting: false);
        }

        public void Delete(string entityName, Guid id)
        {
            if (string.IsNullOrWhiteSpace(entityName)) throw new ArgumentNullException(nameof(entityName));
            if (id == Guid.Empty) throw new ArgumentException("Delete requires a valid id.");

            if (_store.TryGetValue(entityName, out var bucket))
            {
                bucket.Remove(id);
            }
        }

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (request is WhoAmIRequest)
            {
                var response = new OrganizationResponse();
                response.Results["UserId"] = _userId;
                response.Results["BusinessUnitId"] = _businessUnitId;
                response.Results["OrganizationId"] = _organizationId;
                return response;
            }

            throw new RunnerNotSupportedException(
                "Offline",
                request.GetType().Name,
                "Only WhoAmIRequest is supported in Offline mode.");
        }

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            if (string.IsNullOrWhiteSpace(entityName)) throw new ArgumentNullException(nameof(entityName));
            if (id == Guid.Empty) throw new ArgumentException("Retrieve requires a valid id.");

            if (_store.TryGetValue(entityName, out var bucket) && bucket.TryGetValue(id, out var stored))
            {
                return CloneEntity(stored, columnSet);
            }

            return new Entity(entityName) { Id = id };
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));

            var logicalName = ResolveEntityName(query);
            var results = new EntityCollection();
            if (!string.IsNullOrWhiteSpace(logicalName))
            {
                results.EntityName = logicalName;
            }

            if (string.IsNullOrWhiteSpace(logicalName))
            {
                return results;
            }

            var resolvedName = logicalName!;
            if (!_store.TryGetValue(resolvedName, out var bucket) || bucket.Count == 0)
            {
                return results;
            }

            ColumnSet? columnSet = null;
            if (query is QueryExpression qe)
            {
                columnSet = qe.ColumnSet;
            }

            foreach (var entity in bucket.Values)
            {
                results.Entities.Add(CloneEntity(entity, columnSet ?? new ColumnSet(true)));
            }

            return results;
        }

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            throw new RunnerNotSupportedException(
                "Offline",
                "Associate",
                "Associate is not supported in Offline mode.");
        }

        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            throw new RunnerNotSupportedException(
                "Offline",
                "Disassociate",
                "Disassociate is not supported in Offline mode.");
        }

        private void StoreEntity(Entity entity, bool replaceExisting)
        {
            if (entity == null) return;
            var logicalName = entity.LogicalName;
            if (string.IsNullOrWhiteSpace(logicalName)) return;

            if (!_store.TryGetValue(logicalName, out var bucket))
            {
                bucket = new Dictionary<Guid, Entity>();
                _store[logicalName] = bucket;
            }

            if (!bucket.TryGetValue(entity.Id, out var stored) || replaceExisting)
            {
                stored = new Entity(logicalName) { Id = entity.Id };
                bucket[entity.Id] = stored;
            }

            if (replaceExisting)
            {
                stored.Attributes.Clear();
                stored.FormattedValues.Clear();
                if (stored.KeyAttributes != null)
                {
                    stored.KeyAttributes.Clear();
                }
            }

            foreach (var attr in entity.Attributes)
            {
                stored[attr.Key] = CloneAttributeValue(attr.Value);
            }

            foreach (var formatted in entity.FormattedValues)
            {
                stored.FormattedValues[formatted.Key] = formatted.Value;
            }

            if (entity.KeyAttributes != null && stored.KeyAttributes != null)
            {
                foreach (var keyAttr in entity.KeyAttributes)
                {
                    stored.KeyAttributes[keyAttr.Key] = CloneAttributeValue(keyAttr.Value);
                }
            }
        }

        private static Entity CloneEntity(Entity source, ColumnSet columnSet)
        {
            var clone = new Entity(source.LogicalName) { Id = source.Id };
            if (columnSet == null || columnSet.AllColumns)
            {
                foreach (var attr in source.Attributes)
                {
                    clone[attr.Key] = CloneAttributeValue(attr.Value);
                }

                foreach (var formatted in source.FormattedValues)
                {
                    clone.FormattedValues[formatted.Key] = formatted.Value;
                }

                if (source.KeyAttributes != null && clone.KeyAttributes != null)
                {
                    foreach (var keyAttr in source.KeyAttributes)
                    {
                        clone.KeyAttributes[keyAttr.Key] = CloneAttributeValue(keyAttr.Value);
                    }
                }

                return clone;
            }

            foreach (var column in columnSet.Columns)
            {
                if (source.Attributes.TryGetValue(column, out var value))
                {
                    clone[column] = CloneAttributeValue(value);
                }
            }

            return clone;
        }

        private static object? CloneAttributeValue(object? value)
        {
            switch (value)
            {
                case null:
                    return null;
                case string s:
                    return s;
                case Guid g:
                    return g;
                case bool b:
                    return b;
                case int i:
                    return i;
                case long l:
                    return l;
                case double d:
                    return d;
                case decimal m:
                    return m;
                case DateTime dt:
                    return dt;
                case byte[] bytes:
                    return (byte[])bytes.Clone();
                case Money money:
                    return new Money(money.Value);
                case OptionSetValue osv:
                    return new OptionSetValue(osv.Value);
                case OptionSetValueCollection options:
                    return new OptionSetValueCollection(options.Select(opt => new OptionSetValue(opt.Value)).ToList());
                case EntityReference reference:
                    return new EntityReference(reference.LogicalName, reference.Id)
                    {
                        Name = reference.Name,
                        RowVersion = reference.RowVersion
                    };
                case EntityReferenceCollection references:
                    var clonedRefs = new EntityReferenceCollection();
                    foreach (var item in references)
                    {
                        clonedRefs.Add(new EntityReference(item.LogicalName, item.Id)
                        {
                            Name = item.Name,
                            RowVersion = item.RowVersion
                        });
                    }
                    return clonedRefs;
                case Entity entity:
                    return CloneEntity(entity, new ColumnSet(true));
                case EntityCollection collection:
                    var cloned = new EntityCollection
                    {
                        EntityName = collection.EntityName,
                        MoreRecords = collection.MoreRecords,
                        TotalRecordCount = collection.TotalRecordCount,
                        PagingCookie = collection.PagingCookie,
                        MinActiveRowVersion = collection.MinActiveRowVersion
                    };
                    foreach (var item in collection.Entities)
                    {
                        cloned.Entities.Add(CloneEntity(item, new ColumnSet(true)));
                    }
                    return cloned;
                case AliasedValue aliased:
                    return new AliasedValue(
                        aliased.EntityLogicalName,
                        aliased.AttributeLogicalName,
                        CloneAttributeValue(aliased.Value));
                default:
                    return value;
            }
        }

        private static string? ResolveEntityName(QueryBase query)
        {
            switch (query)
            {
                case QueryExpression qe:
                    return qe.EntityName;
                case FetchExpression fe:
                    return ExtractEntityFromFetch(fe.Query);
                default:
                    return null;
            }
        }

        private static string? ExtractEntityFromFetch(string? fetchXml)
        {
            if (string.IsNullOrWhiteSpace(fetchXml)) return null;
            var marker = "name=\"";
            var normalized = fetchXml!;
            var index = normalized.IndexOf("<entity", StringComparison.OrdinalIgnoreCase);
            if (index < 0) return null;
            var nameIndex = normalized.IndexOf(marker, index, StringComparison.OrdinalIgnoreCase);
            if (nameIndex < 0) return null;
            nameIndex += marker.Length;
            var end = normalized.IndexOf("\"", nameIndex, StringComparison.OrdinalIgnoreCase);
            if (end < 0) return null;
            return normalized.Substring(nameIndex, end - nameIndex);
        }
    }
}
