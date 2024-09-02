#nullable enable

using System;
using System.IO;

namespace DarkConfig {
    public class YamlFileException : Exception {
        public YamlFileException(string filename, YamlDotNet.Core.YamlException inner)
            : base($"Encountered error parsing YAML file '{filename}': {inner.Message}", inner) {
            Filename = filename;
            YamlException = inner;
        }

        public string Filename { get; private set; }
        public YamlDotNet.Core.YamlException YamlException { get; private set; }
    }

    /// The reason for this strange structure is that when Unity prints
    /// exceptions raised in the course of Reifying, often the info you need
    /// (the line number) is buried in the second or Nth exception.  Unity
    /// prints exceptions by printing the message and StackTrace starting from
    /// the innermost exception, and working outwards. So this instead makes it
    /// more readable by putting all the messages up top, and then all the
    /// stack traces in a big line, still from inner at the top to outer at the
    /// bottom.  It's a bit more readable, and most importantly the line
    /// numbers in the config files are much more prominent.
    public class ParseException : Exception {
        public ParseException(DocNode? exceptionNode, string message, Exception? inner = null) : base((inner != null ? inner.Message + "\n" : "") + message) {
            Node = exceptionNode;
            wrappedException = inner;
        }

        public override string? StackTrace => wrappedException == null ? base.StackTrace : wrappedException.StackTrace + "\n-----\n" + base.StackTrace;
        public override string Message => base.Message + (Node != null ? $" from {Node.SourceInformation}" : "");
        public string RawMessage => base.Message;
        public bool HasNode => Node != null;

        public readonly DocNode? Node;
        readonly Exception? wrappedException;
    }

    public class TypedParseException : ParseException {
        public Type ParsedType;
        public TypedParseException(Type type, DocNode node, string message) : base(node, message) {
            ParsedType = type;
        }
    }

    public class MissingFieldsException : TypedParseException {
        public MissingFieldsException(Type type, DocNode node, string message) : base(type, node, message) { }
    }

    public class ExtraFieldsException : TypedParseException {
        public ExtraFieldsException(Type type, DocNode node, string message) : base(type, node, message) { }
    }

    public class ConfigFileNotFoundException : FileNotFoundException {
        public ConfigFileNotFoundException(string filename) : base("Couldn't find file " + filename + ". Perhaps it isn't in the index, or wasn't preloaded.", filename) { }
    }

    public class NotPreloadedException : InvalidOperationException {
        public NotPreloadedException(string message) : base(message) { }
    }
}
