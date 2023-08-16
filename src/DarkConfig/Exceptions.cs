using System;
using System.IO;

namespace DarkConfig {
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
        public ParseException(string message) : base(message) {
            wrappedException = null;
        }

        public ParseException(string message, Exception inner) : base((inner != null ? inner.Message : "") + "\n" + message) {
            wrappedException = inner;
        }

        public override string StackTrace => wrappedException == null ? base.StackTrace : wrappedException.StackTrace + "\n-----\n" + base.StackTrace;

        readonly Exception wrappedException;
    }

    public class MissingFieldsException : ParseException {
        public MissingFieldsException(string message) : base(message) { }
        public MissingFieldsException(string message, Exception inner) : base(message, inner) { }
    }

    public class ExtraFieldsException : ParseException {
        public ExtraFieldsException(string message) : base(message) { }
        public ExtraFieldsException(string message, Exception inner) : base(message, inner) { }
    }

    public class ConfigFileNotFoundException : FileNotFoundException {
        public ConfigFileNotFoundException(string filename) : base("Couldn't find file " + filename + ". Perhaps it isn't in the index, or wasn't preloaded.", filename) { }
    }

    public class NotPreloadedException : InvalidOperationException {
        public NotPreloadedException(string message) : base(message) { }
    }
}
