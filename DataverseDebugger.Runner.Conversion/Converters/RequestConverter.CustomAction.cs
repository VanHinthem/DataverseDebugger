using System;
using System.Linq;
using System.Text.Json;
using DataverseDebugger.Runner.Conversion.Model;
using Microsoft.OData.Edm;
using Microsoft.Xrm.Sdk;

namespace DataverseDebugger.Runner.Conversion.Converters
{
    /// <summary>
    /// Partial class containing conversion routines specific to classic custom actions.
    /// </summary>
    public partial class RequestConverter
    {
        /// <summary>
        /// Converts a custom action invocation to an <see cref="OrganizationRequest"/>.
        /// </summary>
        /// <param name="operation">The EDM operation definition.</param>
        /// <param name="conversionResult">The conversion result to populate.</param>
        /// <param name="target">The bound target entity reference, if any.</param>
        private void ConvertToCustomAction(IEdmOperation operation, RequestConversionResult conversionResult, EntityReference target)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            var request = new OrganizationRequest(operation.Name);
            string boundParameterName = null;
            if (target != null)
            {
                boundParameterName = operation.Parameters.First().Name;
            }

            if (conversionResult.SrcRequest.Body != null) { 
                using (JsonDocument json = JsonDocument.Parse(conversionResult.SrcRequest.Body))
                {
                    foreach (var node in json.RootElement.EnumerateObject())
                    {
                        var parameter = operation.FindParameter(node.Name) ?? throw new NotSupportedException($"parameter {node.Name} not found!");
                        var metadata = this.Context.MetadataCache.GetOperationRequestParameter(operation.Name, node.Name);

                        if (boundParameterName != null && node.Name == boundParameterName)
                        {
                            if (target != null)
                            {
                                request["Target"] = target;
                                continue;
                            }

                            var converted = ConvertValueToAttribute(node.Value, metadata, parameter.Type);
                            if (converted is Entity entity)
                            {
                                request["Target"] = entity.ToEntityReference();
                            }
                            else if (converted is EntityReference er)
                            {
                                request["Target"] = er;
                            }
                            else
                            {
                                throw new NotSupportedException("Bound parameter must be an entity reference.");
                            }
                            continue;
                        }

                        request[node.Name] = ConvertValueToAttribute(node.Value, metadata, parameter.Type);
                    }
                }
            }

            if (target != null && !request.Parameters.Contains("Target"))
            {
                request["Target"] = target;
            }

            conversionResult.ConvertedRequest = request;
        }
    }
}
