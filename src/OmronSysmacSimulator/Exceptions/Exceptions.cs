using System;

namespace OmronSysmacSimulator.Exceptions
{
    /// <summary>
    /// Base exception for all Sysmac simulator operations.
    /// </summary>
    public class SysmacException : Exception
    {
        /// <summary>
        /// Creates a new SysmacException.
        /// </summary>
        public SysmacException() { }

        /// <summary>
        /// Creates a new SysmacException with a message.
        /// </summary>
        public SysmacException(string message) : base(message) { }

        /// <summary>
        /// Creates a new SysmacException with a message and inner exception.
        /// </summary>
        public SysmacException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when a connection to the simulator fails.
    /// </summary>
    public class SysmacConnectionException : SysmacException
    {
        /// <summary>
        /// Creates a new SysmacConnectionException.
        /// </summary>
        public SysmacConnectionException() { }

        /// <summary>
        /// Creates a new SysmacConnectionException with a message.
        /// </summary>
        public SysmacConnectionException(string message) : base(message) { }

        /// <summary>
        /// Creates a new SysmacConnectionException with a message and inner exception.
        /// </summary>
        public SysmacConnectionException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when communication with the simulator fails.
    /// </summary>
    public class SysmacCommunicationException : SysmacException
    {
        /// <summary>
        /// Gets the error message from the simulator, if available.
        /// </summary>
        public string SimulatorError { get; }

        /// <summary>
        /// Creates a new SysmacCommunicationException.
        /// </summary>
        public SysmacCommunicationException() { }

        /// <summary>
        /// Creates a new SysmacCommunicationException with a message.
        /// </summary>
        public SysmacCommunicationException(string message) : base(message) { }

        /// <summary>
        /// Creates a new SysmacCommunicationException with a message and simulator error.
        /// </summary>
        public SysmacCommunicationException(string message, string simulatorError) : base(message)
        {
            SimulatorError = simulatorError;
        }

        /// <summary>
        /// Creates a new SysmacCommunicationException with a message and inner exception.
        /// </summary>
        public SysmacCommunicationException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when a type layout or serialization error occurs.
    /// </summary>
    public class SysmacTypeException : SysmacException
    {
        /// <summary>
        /// Gets the type that caused the error, if available.
        /// </summary>
        public Type OffendingType { get; }

        /// <summary>
        /// Gets the member name that caused the error, if available.
        /// </summary>
        public string MemberName { get; }

        /// <summary>
        /// Creates a new SysmacTypeException.
        /// </summary>
        public SysmacTypeException() { }

        /// <summary>
        /// Creates a new SysmacTypeException with a message.
        /// </summary>
        public SysmacTypeException(string message) : base(message) { }

        /// <summary>
        /// Creates a new SysmacTypeException with type and member information.
        /// </summary>
        public SysmacTypeException(string message, Type offendingType, string memberName = null) : base(message)
        {
            OffendingType = offendingType;
            MemberName = memberName;
        }

        /// <summary>
        /// Creates a new SysmacTypeException with a message and inner exception.
        /// </summary>
        public SysmacTypeException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when a PLC variable is not found or has an invalid address.
    /// </summary>
    public class SysmacVariableException : SysmacException
    {
        /// <summary>
        /// Gets the variable name that caused the error.
        /// </summary>
        public string VariableName { get; }

        /// <summary>
        /// Creates a new SysmacVariableException.
        /// </summary>
        public SysmacVariableException() { }

        /// <summary>
        /// Creates a new SysmacVariableException with a message.
        /// </summary>
        public SysmacVariableException(string message) : base(message) { }

        /// <summary>
        /// Creates a new SysmacVariableException with variable name and message.
        /// </summary>
        public SysmacVariableException(string message, string variableName) : base(message)
        {
            VariableName = variableName;
        }

        /// <summary>
        /// Creates a new SysmacVariableException with a message and inner exception.
        /// </summary>
        public SysmacVariableException(string message, Exception innerException) : base(message, innerException) { }
    }
}
