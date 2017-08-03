using System;

namespace DarkConfig {

    // The reason for this strange structure is that when Unity prints
    // exceptions raised in the course of Reifying, often the info you need
    // (the line number) is buried in the second or Nth exception.  Unity
    // prints exceptions by printing the message and StackTrace starting from
    // the innermost exception, and working outwards. So this instead makes it
    // more readable by putting all the messages up top, and then all the
    // stack traces in a big line, still from inner at the top to outer at the
    // bottom.  It's a bit more readable, and most importantly the line
    // numbers in the config files are much more prominent.

    public class ParseException : Exception {
        Exception privateInner;

        public ParseException(string message)
            : base(message) {

            privateInner = null;
        }

        public ParseException(string message, Exception inner)
            : base((inner != null ? inner.Message : "") + "\n" + message) {

            privateInner = inner;
        }

        public override string StackTrace {
            get {
                if(privateInner == null) {
                    return base.StackTrace;
                } else {
                    return privateInner.StackTrace + "\n-----\n" + base.StackTrace;
                }
            }
        }
    }
}