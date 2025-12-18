using System;
using System.Linq;
using DataverseDebugger.Protocol;
using DataverseDebugger.Runner.Conversion.Utils;
using DataverseDebugger.Runner.Conversion.Model;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using System.Web;
using Microsoft.Crm.Sdk.Messages;

namespace DataverseDebugger.Runner.Conversion.Converters
{
    /// <summary>
    /// Converts Dataverse Web API requests to SDK OrganizationRequest objects.
    /// </summary>
    /// <remarks>
    /// This partial class handles the main conversion logic, routing HTTP requests to the appropriate
    /// SDK request types based on HTTP method and OData path structure. Specific conversion methods
    /// are implemented in separate partial class files (CRUD, Batch, CustomApi).
    /// </remarks>
    public partial class RequestConverter
    {
        private DataverseContext Context { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestConverter"/> class.
        /// </summary>
        /// <param name="context">The Dataverse context containing metadata and services.</param>
        /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
        public RequestConverter(DataverseContext context)
        {
            this.Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Converts a Web API request to an SDK OrganizationRequest.
        /// </summary>
        /// <param name="request">The Web API request to convert.</param>
        /// <returns>
        /// A RequestConversionResult containing the converted OrganizationRequest,
        /// or an error message if conversion failed. Returns null if the request
        /// should be passed through without conversion (e.g., metadata requests).
        /// </returns>
        /// <remarks>
        /// This method parses the OData URI and routes to the appropriate conversion method
        /// based on HTTP method (GET, POST, PATCH, DELETE) and path segment count.
        /// Certain system functions (GetClientMetadata, RetrieveMetadataChanges, etc.) are
        /// skipped and allowed to pass through without plugin emulation.
        /// </remarks>
        public RequestConversionResult Convert(WebApiRequest request)
        {
            if (request == null)
                return null;
                
            // Skip conversion for system metadata functions that don't need plugin emulation
            var skipFunctions = new[] 
            { 
                "GetClientMetadata", 
                "RetrieveMetadataChanges", 
                "RetrieveAllEntities",
                "RetrieveEntity",
                "RetrieveAttribute"
            };
            
            if (skipFunctions.Any(func => request.LocalPathWithQuery.IndexOf($"/{func}(", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                // Return null to indicate no conversion needed - let the request pass through
                return null;
            }
            
            RequestConversionResult result = new RequestConversionResult()
            {
                SrcRequest = request
            };

            void Log(string message)
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    result.DebugLog.Add(message);
                }
            }

            Log($"Converting {request.Method} {request.LocalPathWithQuery}");
            ODataUriParser parser;
            ODataPath path;
            try
            {
                var apiRelativeUri = request.LocalPathWithQuery;
                if (apiRelativeUri.StartsWith("/api"))
                    apiRelativeUri = apiRelativeUri.Substring(15);
                parser = new ODataUriParser(this.Context.Model, new Uri(apiRelativeUri, UriKind.Relative))
                {
                    Resolver = new AlternateKeysODataUriResolver(this.Context.Model)
                };
                path = parser.ParsePath();
                var firstSegment = path?.FirstOrDefault()?.Identifier ?? "(none)";
                Log($"Parsed OData path segments={path?.Count ?? 0}, first='{firstSegment}'");
            }
            catch (Exception ex)
            {
                result.ConvertFailureMessage = "Unable to parse: " + ex.Message;
                Log(result.ConvertFailureMessage);
                return result;
            }
            try
            {
                switch (request.Method)
                {
                    case "POST":
                        Log($"Routing POST request with {path.Count} segments");
                        switch (path.Count)
                        {
                            case 1:
                                Log("POST -> ManagePost1Segment");
                                ManagePost1Segment();
                                break;
                            case 2:
                                Log("POST -> ManagePost2Segment");
                                ManagePost2Segment();
                                break;
                            case 3:
                                Log("POST -> ManagePost3Segment");
                                ManagePost3Segment();
                                break;
                            default:
                                throw new NotImplementedException("POST is not implemented for: " + path.Count + " segments");
                        }

                        break;
                    case "PATCH":
                        if (path.Count != 2)
                        {
                            throw new NotImplementedException("PATCH is not implemented for: " + path.Count + " segments");
                        }
                        if (path.FirstSegment.EdmType?.TypeKind != EdmTypeKind.Collection)
                        {
                            throw new NotImplementedException("PATCH is not implemented for: " + path.FirstSegment.EdmType?.TypeKind);
                        }
                        Log("PATCH -> ConvertToCreateUpdateRequest");
                        ConvertToCreateUpdateRequest(result, path);
                        break;
                    case "GET":
                        Log($"Routing GET request with {path.Count} segments");
                        switch (path.Count)
                        {
                            case 1:
                                throw new NotImplementedException("Retrievemultiple are not implemented");
                            case 2:
                                Log("GET -> ConvertToRetrieveRequest");
                                ConvertToRetrieveRequest(result, parser, path);
                                break;
                            case 3:
                                if (path.LastSegment is OperationSegment operationSegment)
                                {
                                    Log($"GET -> ConvertToBoundFunction ({operationSegment.Identifier})");
                                    ConvertToBoundFunction(result, path, operationSegment);
                                    break;
                                }
                                throw new NotSupportedException("Unexpected number of segments:" + path.Count);
                            default:
                                throw new NotSupportedException("Unexpected number of segments:" + path.Count);
                        }
                        break;
                    case "DELETE":
                        if (path.Count != 2)
                        {
                            throw new NotSupportedException("Unexpected number of segments:" + path.Count);
                        }
                        Log("DELETE -> ConvertToDeleteRequest");
                        ConvertToDeleteRequest(result, path);
                        break;
                    default:
                        result.ConvertFailureMessage = "method not implemented";
                        Log(result.ConvertFailureMessage);
                        break;
                }
            }
            catch (Exception ex)
            {
                result.ConvertFailureMessage = ex.Message;
                Log("Conversion threw: " + ex.Message);
            }

            if (result.ConvertedRequest != null)
            {
                var requestName = result.ConvertedRequest.RequestName ?? result.ConvertedRequest.GetType().Name;
                Log($"Conversion produced {requestName} ({result.ConvertedRequest.GetType().Name})");
            }
            else if (!string.IsNullOrWhiteSpace(result.ConvertFailureMessage))
            {
                Log("Conversion ended without request: " + result.ConvertFailureMessage);
            }

            return result;

            void ManagePost3Segment()
            {
                if (!(path.LastSegment is OperationSegment))
                {
                    throw new NotImplementedException("Post with 3 segments are implemented only for custom api!");
                }
                var entity = this.Context.MetadataCache.GetEntityFromSetName(path.FirstSegment.Identifier) ?? throw new NotSupportedException($"Entity: {path.FirstSegment.Identifier} not found!");
                var keySegment = path.Skip(1).First() as KeySegment ?? throw new NotSupportedException("2nd segment should be of type identifier");
                EntityReference target = GetEntityReferenceFromKeySegment(entity, keySegment);
                string identifier = path.LastSegment.Identifier;
                var declaredOperation = this.Context.Model.FindDeclaredOperations(identifier).Single();

                var source = ResolveOperationSource(declaredOperation);
                Log($"POST bound {DescribeOperationSource(source)} -> {identifier}");
                ConvertOperationBySource(source, declaredOperation, target);
            }

            void ManagePost2Segment()
            {
                if (!(path.LastSegment is OperationSegment))
                {
                    throw new NotImplementedException("Post with 3 segments are implemented only for custom api!");
                }
                string identifier = path.LastSegment.Identifier;
                var declaredOperation = this.Context.Model.FindDeclaredOperations(identifier).Single();

                var source = ResolveOperationSource(declaredOperation);
                Log($"POST {DescribeOperationSource(source)} -> {identifier}");
                ConvertOperationBySource(source, declaredOperation, null);
            }

            void ManagePost1Segment()
            {
                if (path.FirstSegment.EdmType?.TypeKind == EdmTypeKind.Collection)
                {
                    string identifier = path.FirstSegment.Identifier;
                    var entity = this.Context.MetadataCache.GetEntityFromSetName(path.FirstSegment.Identifier);
                    if (entity != null)
                    {
                        Log("POST collection -> ConvertToCreateUpdateRequest");
                        ConvertToCreateUpdateRequest(result, path);
                    }
                    else
                    {
                        //Custom api returning only one collection have a first segment of type collection
                        var declaredOperation = this.Context.Model.FindDeclaredOperationImports(identifier).FirstOrDefault() ?? throw new NotImplementedException("Identifier unknown:" + identifier);
                        var source = ResolveOperationSource(declaredOperation.Operation);
                        Log($"POST collection operation import ({DescribeOperationSource(source)}) -> {identifier}");
                        ConvertOperationBySource(source, declaredOperation.Operation, null);
                    }
                }
                else if (path.FirstSegment.Identifier == "$batch")
                {
                    Log("POST batch -> ConvertToExecuteMultipleRequest");
                    ConvertToExecuteMultipleRequest(result);
                }
                else if (path.FirstSegment is OperationImportSegment)
                {
                    string identifier = path.FirstSegment.Identifier;
                    var declaredOperation = this.Context.Model.FindDeclaredOperationImports(identifier).Single();
                    var source = ResolveOperationSource(declaredOperation.Operation);
                    Log($"POST operation import ({DescribeOperationSource(source)}) -> {identifier}");
                    ConvertOperationBySource(source, declaredOperation.Operation, null);
                }
                else
                {
                    throw new NotImplementedException("POST is not implemented for: " + path.FirstSegment.EdmType?.TypeKind);
                }
            }

            OperationParameterSource? ResolveOperationSource(IEdmOperation operation)
            {
                if (operation == null)
                {
                    return null;
                }

                return this.Context.MetadataCache.GetOperationSource(operation.Name);
            }

            void ConvertOperationBySource(OperationParameterSource? source, IEdmOperation operation, EntityReference target)
            {
                if (source == OperationParameterSource.CustomApi)
                {
                    ConvertToCustomApi(operation, result, target);
                }
                
                if(source == OperationParameterSource.CustomAction)
                {
                    ConvertToCustomAction(operation, result, target);
                }
            }

            string DescribeOperationSource(OperationParameterSource? source)
            {
                switch (source)
                {
                    case OperationParameterSource.CustomAction:
                        return "custom action";
                    case OperationParameterSource.CustomApi:
                        return "custom API";
                    default:
                        return "operation";
                }
            }
        }

        private static EntityReference GetEntityReferenceFromKeySegment(EntityMetadata entity, KeySegment keySegment)
        {
            GetIdFromKeySegment(keySegment, out var id, out var keys);

            EntityReference target;
            if (id == Guid.Empty)
            {
                target = new EntityReference(entity.LogicalName, keys);
            }
            else
            {
                target = new EntityReference(entity.LogicalName, id);
            }

            return target;
        }

        private static void GetIdFromKeySegment(KeySegment keySegment, out Guid id, out KeyAttributeCollection keys)
        {
            if (keySegment.Keys.Count() == 1)
            {
                var key = keySegment.Keys.First();
                if (key.Value is Guid keyId)
                {
                    id = keyId;
                    keys = null;
                    return;
                }
            }
            id = Guid.Empty;
            keys = new KeyAttributeCollection();
            foreach (var key in keySegment.Keys)
            {
                keys[key.Key] = key.Value;
            }
        }

        private void ConvertToBoundFunction(RequestConversionResult conversionResult, ODataPath path, OperationSegment operationSegment)
        {
            // Support for RetrievePrincipalAccess bound function
            if (string.Equals(operationSegment.Identifier, "Microsoft.Dynamics.CRM.RetrievePrincipalAccess", StringComparison.OrdinalIgnoreCase))
            {
                var entitySegment = path.FirstSegment as EntitySetSegment ?? throw new NotSupportedException("First segment should be entity set");
                var keySegment = path.Skip(1).First() as KeySegment ?? throw new NotSupportedException("Second segment should be key");

                // The entity in the URL path is the PRINCIPAL (systemuser or team)
                var principalEntity = this.Context.MetadataCache.GetEntityFromSetName(entitySegment.Identifier) ?? throw new NotSupportedException("Entity not found: " + entitySegment.Identifier);
                var principalRef = GetEntityReferenceFromKeySegment(principalEntity, keySegment);

                var uri = new Uri("http://localhost" + conversionResult.SrcRequest.LocalPathWithQuery);
                var query = HttpUtility.ParseQueryString(uri.Query);
                
                // The Target parameter contains the target entity reference
                var targetRaw = query["@Target"] ?? query["Target"];
                
                if (string.IsNullOrEmpty(targetRaw))
                {
                    throw new NotSupportedException("RetrievePrincipalAccess missing Target parameter");
                }

                // Parse the Target JSON: {"@odata.id":"entityset(guid)"}
                EntityReference targetRef;
                try
                {
                    var targetJson = System.Text.Json.JsonDocument.Parse(targetRaw);
                    var odataId = targetJson.RootElement.GetProperty("@odata.id").GetString();
                    
                    // Parse "entityset(guid)" format
                    var match = System.Text.RegularExpressions.Regex.Match(odataId, @"^([^(]+)\(([^)]+)\)$");
                    if (!match.Success)
                    {
                        throw new NotSupportedException($"Invalid @odata.id format: {odataId}");
                    }
                    
                    var entitySetName = match.Groups[1].Value;
                    var guidStr = match.Groups[2].Value;
                    
                    var targetEntity = this.Context.MetadataCache.GetEntityFromSetName(entitySetName);
                    if (targetEntity == null)
                    {
                        throw new NotSupportedException($"Entity not found for set name: {entitySetName}");
                    }
                    
                    if (!Guid.TryParse(guidStr, out var targetId))
                    {
                        throw new NotSupportedException($"Invalid GUID in Target: {guidStr}");
                    }
                    
                    targetRef = new EntityReference(targetEntity.LogicalName, targetId);
                }
                catch (Exception ex)
                {
                    throw new NotSupportedException($"Failed to parse Target parameter: {ex.Message}");
                }

                conversionResult.ConvertedRequest = new RetrievePrincipalAccessRequest
                {
                    Principal = principalRef,
                    Target = targetRef
                };
                return;
            }

            // Support for RetrievePrincipalAccessInfo bound function (legacy)
            if (string.Equals(operationSegment.Identifier, "Microsoft.Dynamics.CRM.RetrievePrincipalAccessInfo", StringComparison.OrdinalIgnoreCase))
            {

            var entitySegment = path.FirstSegment as EntitySetSegment ?? throw new NotSupportedException("First segment should be entity set");
            var keySegment = path.Skip(1).First() as KeySegment ?? throw new NotSupportedException("Second segment should be key");

            var principalEntity = this.Context.MetadataCache.GetEntityFromSetName(entitySegment.Identifier) ?? throw new NotSupportedException("Entity not found: " + entitySegment.Identifier);
            var principalRef = GetEntityReferenceFromKeySegment(principalEntity, keySegment);

            var uri = new Uri("http://localhost" + conversionResult.SrcRequest.LocalPathWithQuery);
            var query = HttpUtility.ParseQueryString(uri.Query);
            var objectIdRaw = query["@ObjectId"] ?? query["ObjectId"];
            var entityName = TrimQuotes(query["@EntityName"] ?? query["EntityName"]);
            if (string.IsNullOrEmpty(objectIdRaw) || string.IsNullOrEmpty(entityName))
            {
                throw new NotSupportedException("RetrievePrincipalAccessInfo missing parameters");
            }

            var targetEntity = this.Context.MetadataCache.GetEntityFromSetName(entityName) ?? this.Context.MetadataCache.GetEntityFromLogicalName(entityName);
            if (targetEntity == null)
            {
                throw new NotSupportedException("Entity not found: " + entityName);
            }

            if (!Guid.TryParse(objectIdRaw, out var targetId))
            {
                throw new NotSupportedException("RetrievePrincipalAccessInfo ObjectId is not a Guid: " + objectIdRaw);
            }
            var targetRef = new EntityReference(targetEntity.LogicalName, targetId);

            conversionResult.ConvertedRequest = new RetrievePrincipalAccessRequest
            {
                Principal = principalRef,
                Target = targetRef
            };
                return;
            }

            throw new NotSupportedException("Bound function not supported: " + operationSegment.Identifier);
        }

        private static string TrimQuotes(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            if (value.Length >= 2 && value.StartsWith("'") && value.EndsWith("'"))
            {
                return value.Substring(1, value.Length - 2);
            }
            return value;
        }


    }
}
