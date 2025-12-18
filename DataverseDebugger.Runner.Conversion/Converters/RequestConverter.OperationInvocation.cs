using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DataverseDebugger.Protocol;
using DataverseDebugger.Runner.Conversion.Model;
using Microsoft.OData.Edm;
using Microsoft.Xrm.Sdk;

namespace DataverseDebugger.Runner.Conversion.Converters
{
    /// <summary>
    /// Partial class containing shared helpers for converting Dataverse operations to SDK requests.
    /// </summary>
    public partial class RequestConverter
    {
        private object ConvertValueToAttribute(JsonElement value, Entity parameterMetadata, IEdmTypeReference edmType)
        {
            var explicitType = parameterMetadata?.GetAttributeValue<OptionSetValue>("type")?.Value;
            if (!explicitType.HasValue)
            {
                var parserType = OperationParameterTypeMapper.FromParser(parameterMetadata?.GetAttributeValue<string>("parser"));
                if (parserType.HasValue)
                {
                    explicitType = parserType;
                }
            }

            var resolvedType = explicitType ?? GetRequestParameterTypeFromEdmType(edmType);
            if (value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            var typeName = edmType.FullName();
            switch (resolvedType)
            {
                case OperationParameterType.Boolean:
                    return value.GetBoolean();
                case OperationParameterType.DateTime:
                    return value.GetDateTime();
                case OperationParameterType.Decimal:
                    return value.GetDecimal();
                case OperationParameterType.Entity:
                    return ConvertToEntity(value, edmType, typeName);
                case OperationParameterType.EntityCollection:
                    var collection = new EntityCollection();
                    foreach (var item in value.EnumerateArray())
                    {
                        collection.Entities.Add(ConvertToEntity(item, null, null));
                    }

                    collection.EntityName = collection.Entities.FirstOrDefault()?.LogicalName;
                    if (collection.EntityName == null && parameterMetadata != null)
                    {
                        collection.EntityName = parameterMetadata.GetAttributeValue<string>("entitylogicalname");
                    }

                    return collection;
                case OperationParameterType.EntityReference:
                    return ConvertToEntity(value, edmType, typeName).ToEntityReference();
                case OperationParameterType.Float:
                    return value.GetDouble();
                case OperationParameterType.Integer:
                    return value.GetInt32();
                case OperationParameterType.Money:
                    return new Money(value.GetDecimal());
                case OperationParameterType.Picklist:
                    if (edmType is IEdmPrimitiveTypeReference primitive && primitive.PrimitiveKind() == EdmPrimitiveTypeKind.Int32)
                    {
                        return value.GetInt32();
                    }

                    return new OptionSetValue(value.GetInt32());
                case OperationParameterType.String:
                    return value.GetString();
                case OperationParameterType.StringArray:
                    var list = new List<string>();
                    foreach (var item in value.EnumerateArray())
                    {
                        list.Add(item.GetString());
                    }

                    return list.ToArray();
                case OperationParameterType.Guid:
                    return value.GetGuid();
                default:
                    throw new NotImplementedException($"Type {typeName}({resolvedType}) is not implemented!");
            }
        }

        private int GetRequestParameterTypeFromEdmType(IEdmTypeReference edmType)
        {
            switch (edmType.FullName())
            {
                case "Edm.Boolean":
                    return OperationParameterType.Boolean;
                case "Edm.Byte":
                    return OperationParameterType.Integer;
                case "Edm.DateTime":
                    return OperationParameterType.DateTime;
                case "Edm.Decimal":
                    return OperationParameterType.Decimal;
                case "Edm.Double":
                    return OperationParameterType.Float;
                case "Edm.Single":
                    return OperationParameterType.Float;
                case "Edm.Guid":
                    return OperationParameterType.Guid;
                case "Edm.Int16":
                    return OperationParameterType.Integer;
                case "Edm.Int32":
                    return OperationParameterType.Integer;
                case "Edm.Int64":
                    return OperationParameterType.Integer;
                case "Edm.SByte":
                    return OperationParameterType.Integer;
                case "Edm.String":
                    return OperationParameterType.String;
                default:
                    if (edmType.TypeKind() == EdmTypeKind.Entity)
                    {
                        return OperationParameterType.EntityReference;
                    }

                    if (edmType.TypeKind() == EdmTypeKind.Collection)
                    {
                        return OperationParameterType.EntityCollection;
                    }

                    throw new NotImplementedException($"Type {edmType.TypeKind()} is not implemented!");
            }
        }

        private Entity ConvertToEntity(JsonElement value, IEdmTypeReference edmType, string typeName)
        {
            if (!value.TryGetProperty("@odata.type", out var dataType))
            {
                throw new NotSupportedException("@odata.type property must be set!");
            }

            if (typeName != null && dataType.GetString() != typeName)
            {
                throw new NotSupportedException($"@odata.type property is of type {dataType.GetString()} whereas {typeName} was expected!");
            }

            typeName = dataType.GetString();
            var definition = (edmType == null ? this.Context.Model.FindType(typeName) : edmType.Definition) as IEdmEntityType;
            if (definition == null)
            {
                throw new NotSupportedException($"IEdmEntityType was expected but not found for type {typeName}!");
            }

            var record = new Entity(definition.Name);
            var entityMetadata = this.Context.MetadataCache.GetEntityMetadataWithAttributes(definition.Name);
            var key = definition.DeclaredKey.FirstOrDefault()?.Name;
            ReadEntityFromJson(entityMetadata, record, value, key);
            return record;
        }
    }
}
