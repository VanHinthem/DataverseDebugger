using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DataverseDebugger.Protocol;
using Microsoft.OData.Edm;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseDebugger.Runner.Conversion.Utils
{
    /// <summary>
    /// Serializable container for cached entity metadata.
    /// </summary>
    [Serializable]
    internal class EntityMetadataCache
    {
        /// <summary>Gets or sets the entity logical name.</summary>
        public string LogicalName { get; set; }

        /// <summary>Gets or sets the entity set name (Web API collection name).</summary>
        public string EntitySetName { get; set; }

        /// <summary>Gets or sets the entity object type code.</summary>
        public int ObjectTypeCode { get; set; }
    }

    /// <summary>
    /// Caches entity metadata to avoid repeated round-trips to Dataverse.
    /// </summary>
    /// <remarks>
    /// This cache stores entity metadata including logical names, set names, attributes, and relationships.
    /// It supports disk-based caching for faster initialization and can be populated from either
    /// a live IOrganizationService connection or from an OData EDM model.
    /// </remarks>
    public class MetadataCache
    {
        private IOrganizationService Service { get; }
        private EntityMetadata[] EntityMetadata { get; }
        private Dictionary<string, EntityMetadata> EntityByLogicalName { get; }
        private Dictionary<string, EntityMetadata> EntityBySetName { get; }
        private Dictionary<string, EntityMetadata> EntityMetadataWithAttributes { get; }
        private Dictionary<string, Entity> OperationRequestParameters { get; }
        private Dictionary<string, Entity> CustomApis { get; }
        private Dictionary<string, Entity> SdkMessages { get; }
        private Dictionary<string, Entity> OperationSnapshotByOperationAndParameter { get; }
        private Dictionary<string, List<Entity>> OperationSnapshotByParameter { get; }
        private Dictionary<string, OperationParameterSource> OperationSourceByOperation { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataCache"/> class from an IOrganizationService.
        /// </summary>
        /// <param name="Service">The organization service for retrieving metadata.</param>
        /// <param name="cachePath">Optional path for disk-based caching.</param>
        /// <param name="forceRefresh">If true, bypasses the disk cache and fetches fresh metadata.</param>
        public MetadataCache(IOrganizationService Service, string cachePath = null, bool forceRefresh = false)
        {
            this.Service = Service;
            
            string cacheFile = null;
            if (!string.IsNullOrEmpty(cachePath))
            {
                cacheFile = Path.Combine(cachePath, "metadata.cache");
            }

            // Try to load from cache first
            if (!forceRefresh && cacheFile != null && TryLoadFromCache(cacheFile, out var cachedMetadata))
            {
                System.Diagnostics.Trace.WriteLine($"[MetadataCache] Loaded {cachedMetadata.Length} entities from disk cache");
                this.EntityMetadata = cachedMetadata;
            }
            else
            {
                System.Diagnostics.Trace.WriteLine("[MetadataCache] Fetching metadata from Dataverse...");
                // Only retrieve basic entity info (not attributes, relationships, privileges)
                // This significantly speeds up initialization
                RetrieveAllEntitiesRequest request = new RetrieveAllEntitiesRequest
                {
                    EntityFilters = EntityFilters.Entity
                };
                var result = (RetrieveAllEntitiesResponse)this.Service.Execute(request);
                this.EntityMetadata = result.EntityMetadata;
                System.Diagnostics.Trace.WriteLine($"[MetadataCache] Fetched {this.EntityMetadata.Length} entities from Dataverse");

                // Save to cache
                if (cacheFile != null)
                {
                    TrySaveToCache(cacheFile, this.EntityMetadata);
                    System.Diagnostics.Trace.WriteLine($"[MetadataCache] Saved metadata to disk cache: {cacheFile}");
                }
            }

            this.EntityByLogicalName = this.EntityMetadata.ToDictionary(e => e.LogicalName, StringComparer.OrdinalIgnoreCase);
            this.EntityBySetName = this.EntityMetadata.ToDictionary(e => e.EntitySetName, StringComparer.OrdinalIgnoreCase);
            this.EntityMetadataWithAttributes = new Dictionary<string, EntityMetadata>();
            this.OperationRequestParameters = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            this.CustomApis = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            this.SdkMessages = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            this.OperationSnapshotByOperationAndParameter = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            this.OperationSnapshotByParameter = new Dictionary<string, List<Entity>>(StringComparer.OrdinalIgnoreCase);
            this.OperationSourceByOperation = new Dictionary<string, OperationParameterSource>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataCache"/> class from pre-loaded metadata.
        /// </summary>
        /// <param name="entityMetadata">The array of entity metadata to use.</param>
        /// <remarks>
        /// This constructor is used for testing scenarios or when metadata has already been loaded.
        /// No IOrganizationService is available, so on-demand attribute retrieval is not supported.
        /// </remarks>
        public MetadataCache(EntityMetadata[] entityMetadata)
        {
            this.Service = null;
            this.EntityMetadata = entityMetadata ?? Array.Empty<EntityMetadata>();
            this.EntityByLogicalName = new Dictionary<string, EntityMetadata>(StringComparer.OrdinalIgnoreCase);
            this.EntityBySetName = new Dictionary<string, EntityMetadata>(StringComparer.OrdinalIgnoreCase);
            foreach (var entity in this.EntityMetadata)
            {
                if (!string.IsNullOrWhiteSpace(entity.LogicalName))
                {
                    this.EntityByLogicalName[entity.LogicalName] = entity;
                }
                if (!string.IsNullOrWhiteSpace(entity.EntitySetName))
                {
                    this.EntityBySetName[entity.EntitySetName] = entity;
                }
            }
            this.EntityMetadataWithAttributes = new Dictionary<string, EntityMetadata>(StringComparer.OrdinalIgnoreCase);
            foreach (var entity in this.EntityByLogicalName.Values)
            {
                this.EntityMetadataWithAttributes[entity.LogicalName] = entity;
            }
            this.OperationRequestParameters = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            this.CustomApis = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            this.SdkMessages = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            this.OperationSnapshotByOperationAndParameter = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            this.OperationSnapshotByParameter = new Dictionary<string, List<Entity>>(StringComparer.OrdinalIgnoreCase);
            this.OperationSourceByOperation = new Dictionary<string, OperationParameterSource>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates a MetadataCache from an OData EDM model.
        /// </summary>
        /// <param name="model">The OData EDM model (typically from $metadata).</param>
        /// <returns>A new MetadataCache populated from the model.</returns>
        /// <exception cref="ArgumentNullException">Thrown when model is null.</exception>
        /// <remarks>
        /// This factory method extracts entity definitions from the EDM model and creates
        /// corresponding EntityMetadata objects with attributes and relationships.
        /// </remarks>
        public static MetadataCache CreateFromModel(IEdmModel model, string operationSnapshotPath = null)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var container = model.EntityContainer;
            var entities = new List<EntityMetadata>();
            if (container != null)
            {
                foreach (var set in container.EntitySets())
                {
                    var entityType = set.EntityType();
                    if (entityType == null)
                    {
                        continue;
                    }

                    var logicalName = entityType.Name;
                    var entity = new EntityMetadata
                    {
                        LogicalName = logicalName,
                        EntitySetName = set.Name
                    };

                    var attributes = new List<AttributeMetadata>();
                    var keyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (entityType.DeclaredKey != null)
                    {
                        foreach (var key in entityType.DeclaredKey)
                        {
                            keyNames.Add(key.Name);
                        }
                    }

                    foreach (var prop in entityType.StructuralProperties())
                    {
                        var attr = new AttributeMetadata
                        {
                            LogicalName = prop.Name
                        };
                        SetAttributeMetadata(attr, MapEdmType(prop.Type), keyNames.Contains(prop.Name));
                        attributes.Add(attr);
                    }

                    var manyToOne = new List<OneToManyRelationshipMetadata>();
                    var oneToMany = new List<OneToManyRelationshipMetadata>();
                    foreach (var nav in entityType.NavigationProperties())
                    {
                        var constraint = nav.ReferentialConstraint;
                        if (constraint == null)
                        {
                            continue;
                        }

                        var dependent = constraint.PropertyPairs.Select(p => p.DependentProperty).FirstOrDefault(p => p != null);
                        if (dependent == null)
                        {
                            continue;
                        }

                        var referencingAttribute = dependent.Name;
                        EnsureAttribute(attributes, referencingAttribute, AttributeTypeCode.Lookup, keyNames);

                        var relatedEntityType = GetNavigationEntityType(nav);
                        var relatedName = relatedEntityType != null ? relatedEntityType.Name : string.Empty;

                        var many = new OneToManyRelationshipMetadata
                        {
                            ReferencingEntity = logicalName,
                            ReferencedEntity = relatedName,
                            ReferencingAttribute = referencingAttribute,
                            SchemaName = nav.Name,
                            ReferencingEntityNavigationPropertyName = nav.Name,
                            ReferencedEntityNavigationPropertyName = nav.Partner != null ? nav.Partner.Name : null
                        };
                        manyToOne.Add(many);

                        if (nav.Type.IsCollection())
                        {
                            var one = new OneToManyRelationshipMetadata
                            {
                                ReferencedEntity = logicalName,
                                ReferencingEntity = relatedName,
                                ReferencingAttribute = referencingAttribute,
                                SchemaName = nav.Name,
                                ReferencedEntityNavigationPropertyName = nav.Name,
                                ReferencingEntityNavigationPropertyName = nav.Partner != null ? nav.Partner.Name : null
                            };
                            oneToMany.Add(one);
                        }
                    }

                    SetEntityAttributes(entity, attributes.ToArray());
                    SetEntityManyToOneRelationships(entity, manyToOne.ToArray());
                    SetEntityOneToManyRelationships(entity, oneToMany.ToArray());
                    entities.Add(entity);
                }
            }

            var cache = new MetadataCache(entities.ToArray());
            var snapshotEntities = LoadOperationSnapshot(operationSnapshotPath, out var operationSourceHints);
            cache.InitializeOperationSnapshot(snapshotEntities, operationSourceHints);
            return cache;
        }

        private static IReadOnlyList<Entity> LoadOperationSnapshot(string snapshotPath, out List<OperationSourceSnapshotItem> operationSourceHints)
        {
            operationSourceHints = new List<OperationSourceSnapshotItem>();
            if (string.IsNullOrWhiteSpace(snapshotPath) || !File.Exists(snapshotPath))
            {
                return Array.Empty<Entity>();
            }

            try
            {
                var json = File.ReadAllText(snapshotPath);
                var snapshot = JsonSerializer.Deserialize<OperationParameterSnapshot>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (snapshot == null)
                {
                    return Array.Empty<Entity>();
                }

                if (snapshot.OperationSources != null && snapshot.OperationSources.Count > 0)
                {
                    operationSourceHints = snapshot.OperationSources;
                }

                if (snapshot.Parameters == null || snapshot.Parameters.Count == 0)
                {
                    return Array.Empty<Entity>();
                }

                var entities = new List<Entity>(snapshot.Parameters.Count);
                foreach (var parameter in snapshot.Parameters)
                {
                    var entity = CreateSnapshotEntity(parameter);
                    if (entity != null)
                    {
                        entities.Add(entity);
                    }
                }
                return entities;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[MetadataCache] Custom API snapshot load failed: {ex.Message}");
                operationSourceHints = new List<OperationSourceSnapshotItem>();
                return Array.Empty<Entity>();
            }
        }

        private static Entity CreateSnapshotEntity(OperationParameterSnapshotItem parameter)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.OperationName) || string.IsNullOrWhiteSpace(parameter.PrimaryParameterName))
            {
                return null;
            }

            var entity = new Entity("operationparameter");
            entity["operationname"] = parameter.OperationName;
            entity["primaryname"] = parameter.PrimaryParameterName;

            if (!string.IsNullOrWhiteSpace(parameter.AlternateParameterName))
            {
                entity["alternatename"] = parameter.AlternateParameterName;
            }

            if (parameter.Type.HasValue)
            {
                entity["type"] = new OptionSetValue(parameter.Type.Value);
            }

            if (!string.IsNullOrWhiteSpace(parameter.EntityLogicalName))
            {
                entity["entitylogicalname"] = parameter.EntityLogicalName;
            }

            if (!string.IsNullOrWhiteSpace(parameter.LogicalEntityName))
            {
                entity["logicalentityname"] = parameter.LogicalEntityName;
            }

            if (parameter.Position.HasValue)
            {
                entity["position"] = parameter.Position.Value;
            }

            if (parameter.IsOptional.HasValue)
            {
                entity["isoptional"] = parameter.IsOptional.Value;
            }

            if (!string.IsNullOrWhiteSpace(parameter.BindingInformation))
            {
                entity["parameterbindinginformation"] = parameter.BindingInformation;
            }

            if (!string.IsNullOrWhiteSpace(parameter.Parser))
            {
                entity["parser"] = parameter.Parser;
            }

            if (!string.IsNullOrWhiteSpace(parameter.Formatter))
            {
                entity["formatter"] = parameter.Formatter;
            }

            entity["source"] = new OptionSetValue((int)parameter.Source);

            return entity;
        }

        private void InitializeOperationSnapshot(IEnumerable<Entity> snapshot, IEnumerable<OperationSourceSnapshotItem> operationSourceHints = null)
        {
            if (snapshot == null)
            {
                return;
            }

            this.OperationSnapshotByOperationAndParameter.Clear();
            this.OperationSnapshotByParameter.Clear();

            foreach (var entity in snapshot)
            {
                var operationName = entity.GetAttributeValue<string>("operationname");
                var primaryName = entity.GetAttributeValue<string>("primaryname");
                var alternateName = entity.GetAttributeValue<string>("alternatename");

                RegisterSnapshotParameter(entity, operationName, primaryName);
                if (!string.IsNullOrWhiteSpace(alternateName) && !string.Equals(primaryName, alternateName, StringComparison.OrdinalIgnoreCase))
                {
                    RegisterSnapshotParameter(entity, operationName, alternateName);
                }
            }

            if (operationSourceHints != null)
            {
                foreach (var hint in operationSourceHints)
                {
                    if (hint == null || string.IsNullOrWhiteSpace(hint.OperationName))
                    {
                        continue;
                    }

                    if (this.OperationSourceByOperation.ContainsKey(hint.OperationName))
                    {
                        continue;
                    }

                    this.OperationSourceByOperation[hint.OperationName] = hint.Source;
                }
            }
        }

        private void RegisterSnapshotParameter(Entity entity, string operationName, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName) || string.IsNullOrWhiteSpace(operationName))
            {
                return;
            }

            var key = BuildOperationParameterKey(operationName, parameterName);
            this.OperationSnapshotByOperationAndParameter[key] = entity;
            RegisterOperationSource(operationName, entity);

            if (!this.OperationSnapshotByParameter.TryGetValue(parameterName, out var list))
            {
                list = new List<Entity>();
                this.OperationSnapshotByParameter[parameterName] = list;
            }

            if (!list.Contains(entity))
            {
                list.Add(entity);
            }
        }

        private void RegisterOperationSource(string operationName, Entity entity)
        {
            if (string.IsNullOrWhiteSpace(operationName) || entity == null)
            {
                return;
            }

            if (this.OperationSourceByOperation.ContainsKey(operationName))
            {
                return;
            }

            var sourceOption = entity.GetAttributeValue<OptionSetValue>("source");
            if (sourceOption == null)
            {
                return;
            }

            var numericValue = sourceOption.Value;
            if (Enum.IsDefined(typeof(OperationParameterSource), numericValue))
            {
                this.OperationSourceByOperation[operationName] = (OperationParameterSource)numericValue;
            }
        }

        private static string BuildOperationParameterKey(string operationName, string parameterName)
        {
            return string.Concat((operationName ?? string.Empty).Trim(), "|", (parameterName ?? string.Empty).Trim());
        }

        private Entity TryGetSnapshotParameter(string operationName, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName) || string.IsNullOrWhiteSpace(operationName))
            {
                return null;
            }

            var key = BuildOperationParameterKey(operationName, parameterName);
            if (this.OperationSnapshotByOperationAndParameter.TryGetValue(key, out var entity))
            {
                return entity;
            }

            if (this.OperationSnapshotByParameter.TryGetValue(parameterName, out var list) && list.Count == 1)
            {
                return list[0];
            }

            return null;
        }

        /// <summary>
        /// Attempts to load entity metadata from a disk cache file.
        /// </summary>
        /// <param name="cacheFile">The path to the cache file.</param>
        /// <param name="metadata">The loaded metadata if successful.</param>
        /// <returns>True if the cache was loaded successfully; false otherwise.</returns>
        private bool TryLoadFromCache(string cacheFile, out EntityMetadata[] metadata)
        {
            metadata = null;
            try
            {
                if (!File.Exists(cacheFile))
                {
                    return false;
                }

                // Check if cache is older than 7 days
                var fileInfo = new FileInfo(cacheFile);
                if ((DateTime.Now - fileInfo.LastWriteTime).TotalDays > 7)
                {
                    return false;
                }

                // Read simplified cache format (just the essential fields)
                var lines = File.ReadAllLines(cacheFile);
                var metadataList = new List<EntityMetadata>();
                
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    var parts = line.Split('|');
                    if (parts.Length < 2) continue;
                    
                    // Create minimal EntityMetadata with just what we need
                    // Note: We can't set ObjectTypeCode as it's readonly, but LogicalName and EntitySetName are sufficient
                    var entity = new EntityMetadata
                    {
                        LogicalName = parts[0],
                        EntitySetName = parts[1]
                    };
                    metadataList.Add(entity);
                }

                metadata = metadataList.ToArray();
                return metadata.Length > 0;
            }
            catch
            {
                // If cache read fails, return false to fetch fresh
                return false;
            }
        }

        /// <summary>
        /// Attempts to save entity metadata to a disk cache file.
        /// </summary>
        /// <param name="cacheFile">The path to the cache file.</param>
        /// <param name="metadata">The metadata to save.</param>
        private void TrySaveToCache(string cacheFile, EntityMetadata[] metadata)
        {
            try
            {
                // Save simplified format: LogicalName|EntitySetName
                var lines = metadata.Select(e => 
                    $"{e.LogicalName}|{e.EntitySetName}");
                File.WriteAllLines(cacheFile, lines);
            }
            catch
            {
                // If cache save fails, just continue - not critical
            }
        }

        /// <summary>
        /// Gets the operation (custom API/action) request parameter definition for a given parameter name.
        /// </summary>
        /// <param name="operationName">The operation unique name.</param>
        /// <param name="parameterName">The parameter name.</param>
        /// <returns>The parameter metadata entity, or null if not found.</returns>
        public Entity GetOperationRequestParameter(string operationName, string parameterName)
        {
            var cacheKey = BuildOperationParameterKey(operationName, parameterName);

            lock (this.OperationRequestParameters)
            {
                if (this.OperationRequestParameters.TryGetValue(cacheKey, out Entity entity))
                {
                    return entity;
                }

                var snapshot = TryGetSnapshotParameter(operationName, parameterName);
                if (snapshot != null)
                {
                    this.OperationRequestParameters[cacheKey] = snapshot;
                    return snapshot;
                }

                if (this.Service == null)
                {
                    this.OperationRequestParameters[cacheKey] = null;
                    return null;
                }

                entity = TryRetrieveCustomApiParameter(operationName, parameterName)
                    ?? TryRetrieveSdkMessageParameter(operationName, parameterName);

                this.OperationRequestParameters[cacheKey] = entity;
                return entity;
            }
        }

        /// <summary>
        /// Gets the operation source (custom API vs custom action) for a given unique name.
        /// </summary>
        /// <param name="operationName">The operation unique name.</param>
        /// <returns>The detected <see cref="OperationParameterSource"/>, or null if unknown.</returns>
        public OperationParameterSource? GetOperationSource(string operationName)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                return null;
            }

            if (this.OperationSourceByOperation.TryGetValue(operationName, out var cached))
            {
                return cached;
            }

            var customApi = GetCustomApi(operationName);
            if (customApi != null)
            {
                this.OperationSourceByOperation[operationName] = OperationParameterSource.CustomApi;
                return OperationParameterSource.CustomApi;
            }

            var sdkMessage = GetSdkMessage(operationName);
            if (sdkMessage != null)
            {
                this.OperationSourceByOperation[operationName] = OperationParameterSource.CustomAction;
                return OperationParameterSource.CustomAction;
            }

            return null;
        }

        private Entity TryRetrieveCustomApiParameter(string operationName, string parameterName)
        {
            var customApi = GetCustomApi(operationName);
            if (customApi == null)
            {
                return null;
            }

            var query = new QueryExpression("customapirequestparameter")
            {
                ColumnSet = new ColumnSet("type", "logicalentityname", "entitylogicalname")
            };

            var parameterFilter = new FilterExpression(LogicalOperator.Or);
            parameterFilter.AddCondition("name", ConditionOperator.Equal, parameterName);
            parameterFilter.AddCondition("uniquename", ConditionOperator.Equal, parameterName);

            query.Criteria.AddFilter(parameterFilter);
            query.Criteria.AddCondition("customapiid", ConditionOperator.Equal, customApi.Id);

            return this.Service.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        private Entity TryRetrieveSdkMessageParameter(string operationName, string parameterName)
        {
            var message = GetSdkMessage(operationName);
            if (message == null)
            {
                return null;
            }

            var query = new QueryExpression("sdkmessageparameter")
            {
                ColumnSet = new ColumnSet("type", "logicalentityname")
            };

            var parameterFilter = new FilterExpression(LogicalOperator.Or);
            parameterFilter.AddCondition("name", ConditionOperator.Equal, parameterName);
            parameterFilter.AddCondition("publicname", ConditionOperator.Equal, parameterName);

            query.Criteria.AddFilter(parameterFilter);
            query.Criteria.AddCondition("sdkmessageid", ConditionOperator.Equal, message.Id);

            return this.Service.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        /// <summary>
        /// Retrieves the custom API definition by unique name.
        /// </summary>
        /// <param name="uniqueName">The custom API unique name.</param>
        /// <returns>The customapi entity or null if not found.</returns>
        private Entity GetCustomApi(string uniqueName)
        {
            if (string.IsNullOrWhiteSpace(uniqueName))
            {
                return null;
            }

            lock (this.CustomApis)
            {
                if (this.CustomApis.TryGetValue(uniqueName, out var cached))
                {
                    return cached;
                }

                if (this.Service == null)
                {
                    this.CustomApis[uniqueName] = null;
                    return null;
                }

                var query = new QueryExpression("customapi")
                {
                    ColumnSet = new ColumnSet("customapiid"),
                };
                query.Criteria.AddCondition("uniquename", ConditionOperator.Equal, uniqueName);
                var entity = this.Service.RetrieveMultiple(query).Entities.FirstOrDefault();
                this.CustomApis[uniqueName] = entity;
                return entity;
            }
        }

        /// <summary>
        /// Retrieves the SDK message definition by unique name (custom action).
        /// <param name="name">The SDK message name.</param>
        /// <returns>The sdkmessage entity or null if not found.</returns>
        private Entity GetSdkMessage(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            lock (this.SdkMessages)
            {
                if (this.SdkMessages.TryGetValue(name, out var cached))
                {
                    return cached;
                }

                if (this.Service == null)
                {
                    this.SdkMessages[name] = null;
                    return null;
                }

                var query = new QueryExpression("sdkmessage")
                {
                    ColumnSet = new ColumnSet("sdkmessageid")
                };
                query.Criteria.AddCondition("name", ConditionOperator.Equal, name);

                var entity = this.Service.RetrieveMultiple(query).Entities.FirstOrDefault();
                this.SdkMessages[name] = entity;
                return entity;
            }
        }

        /// <summary>
        /// Gets entity metadata by logical name.
        /// </summary>
        /// <param name="logicalName">The entity logical name (e.g., "account").</param>
        /// <returns>The entity metadata, or null if not found.</returns>
        public EntityMetadata GetEntityFromLogicalName(string logicalName)
        {
            this.EntityByLogicalName.TryGetValue(logicalName, out var entity);
            return entity;
        }

        /// <summary>
        /// Gets entity metadata by entity set name (Web API collection name).
        /// </summary>
        /// <param name="setName">The entity set name (e.g., "accounts").</param>
        /// <returns>The entity metadata, or null if not found.</returns>
        public EntityMetadata GetEntityFromSetName(string setName)
        {
            this.EntityBySetName.TryGetValue(setName, out var entity);
            return entity;
        }

        /// <summary>
        /// Gets entity metadata including all attributes and relationships.
        /// </summary>
        /// <param name="entityLogicalName">The entity logical name.</param>
        /// <returns>The complete entity metadata with attributes and relationships.</returns>
        /// <remarks>
        /// This method fetches the full metadata on-demand and caches it for subsequent calls.
        /// </remarks>
        public EntityMetadata GetEntityMetadataWithAttributes(string entityLogicalName)
        {
            lock (this.EntityMetadataWithAttributes)
            {
                if (this.EntityMetadataWithAttributes.TryGetValue(entityLogicalName, out var metadata))
                {
                    return metadata;
                }
                if (this.Service == null)
                {
                    this.EntityByLogicalName.TryGetValue(entityLogicalName, out metadata);
                    this.EntityMetadataWithAttributes[entityLogicalName] = metadata;
                    return metadata;
                }
                RetrieveEntityRequest request = new RetrieveEntityRequest()
                {
                    EntityFilters = EntityFilters.Attributes | EntityFilters.Relationships,
                    LogicalName = entityLogicalName
                };
                var result = (RetrieveEntityResponse)this.Service.Execute(request);
                this.EntityMetadataWithAttributes[entityLogicalName] = result.EntityMetadata;
                return result.EntityMetadata;
            }
        }

        private static void EnsureAttribute(List<AttributeMetadata> attributes, string logicalName, AttributeTypeCode type, HashSet<string> keyNames)
        {
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                return;
            }

            if (attributes.Any(a => string.Equals(a.LogicalName, logicalName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var attr = new AttributeMetadata
            {
                LogicalName = logicalName
            };
            SetAttributeMetadata(attr, type, keyNames.Contains(logicalName));
            attributes.Add(attr);
        }

        private static void SetAttributeMetadata(AttributeMetadata attribute, AttributeTypeCode type, bool isPrimaryId)
        {
            SetNonPublicProperty(attribute, "AttributeType", (AttributeTypeCode?)type);
            SetNonPublicProperty(attribute, "IsPrimaryId", (bool?)isPrimaryId);
        }

        private static void SetEntityAttributes(EntityMetadata entity, AttributeMetadata[] attributes)
        {
            SetNonPublicProperty(entity, "Attributes", attributes ?? Array.Empty<AttributeMetadata>());
        }

        private static void SetEntityManyToOneRelationships(EntityMetadata entity, OneToManyRelationshipMetadata[] relationships)
        {
            SetNonPublicProperty(entity, "ManyToOneRelationships", relationships ?? Array.Empty<OneToManyRelationshipMetadata>());
        }

        private static void SetEntityOneToManyRelationships(EntityMetadata entity, OneToManyRelationshipMetadata[] relationships)
        {
            SetNonPublicProperty(entity, "OneToManyRelationships", relationships ?? Array.Empty<OneToManyRelationshipMetadata>());
        }

        private static void SetNonPublicProperty(object target, string propertyName, object value)
        {
            if (target == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return;
            }

            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var setter = property?.GetSetMethod(true);
            if (setter == null)
            {
                return;
            }

            setter.Invoke(target, new[] { value });
        }

        private static AttributeTypeCode MapEdmType(IEdmTypeReference type)
        {
            if (type == null)
            {
                return AttributeTypeCode.String;
            }

            if (type.IsEnum())
            {
                return AttributeTypeCode.Picklist;
            }

            if (type.IsPrimitive())
            {
                switch (type.AsPrimitive().PrimitiveKind())
                {
                    case EdmPrimitiveTypeKind.Boolean:
                        return AttributeTypeCode.Boolean;
                    case EdmPrimitiveTypeKind.Byte:
                    case EdmPrimitiveTypeKind.SByte:
                    case EdmPrimitiveTypeKind.Int16:
                    case EdmPrimitiveTypeKind.Int32:
                        return AttributeTypeCode.Integer;
                    case EdmPrimitiveTypeKind.Int64:
                        return AttributeTypeCode.BigInt;
                    case EdmPrimitiveTypeKind.Decimal:
                        return AttributeTypeCode.Decimal;
                    case EdmPrimitiveTypeKind.Double:
                    case EdmPrimitiveTypeKind.Single:
                        return AttributeTypeCode.Double;
                    case EdmPrimitiveTypeKind.Guid:
                        return AttributeTypeCode.Uniqueidentifier;
                    case EdmPrimitiveTypeKind.Date:
                    case EdmPrimitiveTypeKind.DateTimeOffset:
                    case EdmPrimitiveTypeKind.TimeOfDay:
                        return AttributeTypeCode.DateTime;
                    case EdmPrimitiveTypeKind.String:
                        return AttributeTypeCode.String;
                    default:
                        return AttributeTypeCode.String;
                }
            }

            return AttributeTypeCode.String;
        }

        private static IEdmEntityType GetNavigationEntityType(IEdmNavigationProperty nav)
        {
            if (nav == null)
            {
                return null;
            }

            var typeRef = nav.Type;
            if (typeRef == null)
            {
                return null;
            }

            if (typeRef.IsCollection())
            {
                var collection = typeRef.Definition as IEdmCollectionType;
                return collection?.ElementType?.Definition as IEdmEntityType;
            }

            return typeRef.Definition as IEdmEntityType;
        }
    }
}
