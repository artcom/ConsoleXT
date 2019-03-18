using System;

namespace ArtCom.Logging
{
    /// <summary>
    /// Bitmask / Flags enum for filtering by message type. For
    /// the actual message types, see <see cref="LogMessageType"/>.
    /// </summary>
    [Flags]
    public enum LogMessageTypeFilter
    {
        None      = 0x00,
            
        Debug     = 0x01,
        Message   = 0x02,
        Warning   = 0x04,
        Error     = 0x08,
        Fatal     = 0x10,

        /// <summary>
        /// Matches all message types that are in any way indicating an error.
        /// </summary>
        Erroneous = Fatal | Error,
        /// <summary>
        /// Matches all message types that indicate errors or warnings.
        /// </summary>
        Irregular = Fatal | Error | Warning,
        /// <summary>
        /// Matches all message types that are used as part of regular operation,
        /// including errors, warnings and messages.
        /// </summary>
        Regular   = Fatal | Error | Warning | Message,
        /// <summary>
        /// Matches all message types, even debug-only messages.
        /// </summary>
        All       = Fatal | Error | Warning | Message | Debug,
    }

    public static class LogMessageTypeFilterExtensions
    {
        /// <summary>
        /// Returns the message types equivalent <see cref="LogMessageTypeFilter"/>.
        /// </summary>
        public static LogMessageTypeFilter ToFilter(this LogMessageType type)
        {
            switch (type)
            {
            default:                     return LogMessageTypeFilter.None;
            case LogMessageType.Debug:   return LogMessageTypeFilter.Debug;
            case LogMessageType.Message: return LogMessageTypeFilter.Message;
            case LogMessageType.Warning: return LogMessageTypeFilter.Warning;
            case LogMessageType.Error:   return LogMessageTypeFilter.Error;
            case LogMessageType.Fatal:   return LogMessageTypeFilter.Fatal;
            }
        }
        /// <summary>
        /// Determines whether the message type filter matches the specified message type.
        /// </summary>
        public static bool Matches(this LogMessageTypeFilter filter, LogMessageType type)
        {
            return (filter & type.ToFilter()) != LogMessageTypeFilter.None;
        }
    }
}