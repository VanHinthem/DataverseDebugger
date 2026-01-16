using System;
using Microsoft.Xrm.Sdk;

namespace DataverseDebugger.Runner.Services.Hybrid
{
    internal static class EntityMergeUtility
    {
        public static Entity Merge(Entity baseEntity, Entity overlayEntity)
        {
            if (baseEntity == null) throw new ArgumentNullException(nameof(baseEntity));
            if (overlayEntity == null) throw new ArgumentNullException(nameof(overlayEntity));

            var logicalName = string.IsNullOrWhiteSpace(baseEntity.LogicalName)
                ? overlayEntity.LogicalName
                : baseEntity.LogicalName;
            var id = baseEntity.Id != Guid.Empty ? baseEntity.Id : overlayEntity.Id;
            var merged = new Entity(logicalName) { Id = id };

            foreach (var attr in baseEntity.Attributes)
            {
                merged[attr.Key] = attr.Value;
            }

            foreach (var formatted in baseEntity.FormattedValues)
            {
                merged.FormattedValues[formatted.Key] = formatted.Value;
            }

            if (baseEntity.KeyAttributes != null && merged.KeyAttributes != null)
            {
                foreach (var keyAttr in baseEntity.KeyAttributes)
                {
                    merged.KeyAttributes[keyAttr.Key] = keyAttr.Value;
                }
            }

            foreach (var attr in overlayEntity.Attributes)
            {
                merged[attr.Key] = attr.Value;
            }

            foreach (var formatted in overlayEntity.FormattedValues)
            {
                merged.FormattedValues[formatted.Key] = formatted.Value;
            }

            if (overlayEntity.KeyAttributes != null && merged.KeyAttributes != null)
            {
                foreach (var keyAttr in overlayEntity.KeyAttributes)
                {
                    merged.KeyAttributes[keyAttr.Key] = keyAttr.Value;
                }
            }

            return merged;
        }
    }
}
