using System;

namespace DarkConfig {
    public static class DefaultFromDocs {
        public static void RegisterAll() {
            Config.Register<DateTime>(FromDateTime);
            Config.Register<TimeSpan>(FromTimeSpan);
        }

        public static object FromDateTime(object existing, DarkConfig.DocNode doc) {
            return DateTime.Parse(doc.StringValue,
                                  System.Globalization.CultureInfo.InvariantCulture);
        }

        public static object FromTimeSpan(object existing, DarkConfig.DocNode doc) {
            TimeSpan newSpan;
            bool isSuccess = TimeSpan.TryParse(doc.StringValue, out newSpan);
            if(!isSuccess) {
                throw new ParseException("expected parsable timespan string " + doc.StringValue, null);
            }
            return newSpan;
        }
    }

}