using System;

namespace DarkConfig.Internal {
    public static class BuiltInTypeRefiers {
        public static void RegisterAll() {
            Config.Register<DateTime>(FromDateTime);
            Config.Register<TimeSpan>(FromTimeSpan);
        }

        static object FromDateTime(object existing, DocNode doc) {
            return DateTime.Parse(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture);
        }

        static object FromTimeSpan(object existing, DocNode doc) {
            bool isSuccess = TimeSpan.TryParse(doc.StringValue, out var newSpan);
            if (!isSuccess) {
                throw new ParseException("expected parseable timespan string " + doc.StringValue, null);
            }

            return newSpan;
        }
    }
}