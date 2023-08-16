using System;

namespace DarkConfig.Internal {
    static class BuiltInTypeReifiers {
        internal static object FromDateTime(object existing, DocNode doc) {
            return DateTime.Parse(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture);
        }

        internal static object FromTimeSpan(object existing, DocNode doc) {
            bool isSuccess = TimeSpan.TryParse(doc.StringValue, out var newSpan);
            if (!isSuccess) {
                throw new ParseException(doc, "expected parseable timespan string " + doc.StringValue);
            }

            return newSpan;
        }
    }
}
