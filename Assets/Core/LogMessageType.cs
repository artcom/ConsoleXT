using System.IO;

namespace ArtCom.Logging {

    public enum LogMessageType {
        /// <summary>
        /// A debug-only message that should be absent in non-development builds.
        /// </summary>
        Debug,
        /// <summary>
        /// A regular log message.
        /// </summary>
        Message,
        /// <summary>
        /// A warning indicates that something might be wrong, though the
        /// application can potentially continue to function as intended.
        /// </summary>
        Warning,
        /// <summary>
        /// An error indicates a recoverable failure that may, however,
        /// lead to limited program functionality or undefined behavior.
        /// </summary>
        Error,
        /// <summary>
        /// A fatal error indicates a non-recoverable failure that will
        /// likely cause the application to crash or reach a state where
        /// no meaningful operation is possible.
        /// </summary>
        Fatal
    }

}