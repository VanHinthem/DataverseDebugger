
namespace DataverseDebugger.Runner.Conversion.Utils.Constants
{
    /// <summary>
    /// Defines constants for activity party participation type mask values.
    /// </summary>
    /// <remarks>
    /// These values correspond to the participationtypemask attribute on the activityparty entity.
    /// See: https://learn.microsoft.com/en-us/power-apps/developer/data-platform/activityparty-entity#activity-party-types
    /// </remarks>
    public static class ActivityPartyType
    {
        /// <summary>Specifies the sender (From field).</summary>
        public const int Sender = 1;

        /// <summary>Specifies the recipient in the To field.</summary>
        public const int ToRecipient = 2;

        /// <summary>Specifies the recipient in the Cc field.</summary>
        public const int CCRecipient = 3;

        /// <summary>Specifies the recipient in the Bcc field.</summary>
        public const int BccRecipient = 4;

        /// <summary>Specifies a required attendee.</summary>
        public const int RequiredAttendee = 5;

        /// <summary>Specifies an optional attendee.</summary>
        public const int OptionalAttendee = 6;

        /// <summary>Specifies the activity organizer.</summary>
        public const int Organizer = 7;

        /// <summary>Specifies the regarding item.</summary>
        public const int Regarding = 8;

        /// <summary>Specifies the activity owner.</summary>
        public const int Owner = 9;

        /// <summary>Specifies a resource.</summary>
        public const int Resource = 10;

        /// <summary>Specifies a customer.</summary>
        public const int Customer = 11;

        /// <summary>Specifies a participant in a Teams chat.</summary>
        public const int ChatParticipant = 12;
    }
}
