using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DarkConfig.Internal {
    internal static class DocumentationGenerator {
        const string FileFooter = @"
<style>
    table {
        width: 100%;
        border: 1px solid black;
    }
    th {
        border: 1px solid black;
        padding: 0.5em;
        background-color: #999999;
        color: white;
        font-weight: bold;
        font-size: 125%;
    }
    td {
        border: 1px solid grey;
        padding: 0.5em;
    }
    tr {
      background-color: #EEEEEE;
    }
    td.description {
        background-color: white;
        padding-left: 1.5em;
    }
    div.note {
        border-left: 1em solid #2e8b57;
        border-top: 1px solid #2e8b57;
        border-bottom: 1px solid #2e8b57;
        border-right: 1px solid #2e8b57;
        padding: 4px;
        margin-bottom: 1em;
        margin-top: 1em;
    }
    div.note:before {
        content: ""Note"";
        color: #2e8b57;
        font-size: 150%;
    }
    div.noteText {
        padding: 0.5em 1em 1em 1em;
    }
    p {
        text-indent: 0.5em;
        line-height: 1.6em;
        padding-left: 1em;
        padding-right: 1em;
    }
</style>
<!-- Markdeep: --><script src=""https://casual-effects.com/markdeep/latest/markdeep.min.js?"" charset=""utf-8""></script>
";


        internal static void Document(string documentationRoot, ReflectionCache cache, Type[] rootTypes) {

            // ensure the folder exists and is empty
            if (Directory.Exists(documentationRoot)) {
                Directory.Delete(documentationRoot, true);
            }
            Directory.CreateDirectory(documentationRoot);
            Directory.CreateDirectory(Path.Combine(documentationRoot, "Types"));

            // find all types and how they relate to each other
            HashSet<Type> types = new();
            HashSet<Type> allTypes = new();
            Dictionary<Type, HashSet<Type>> relatedTypes = new();
            for (int rootTypeIndex = 0; rootTypeIndex < rootTypes.Length; rootTypeIndex++) {
                types.Clear();
                CollectTypes(cache, rootTypes[rootTypeIndex], null, rootTypes[rootTypeIndex], types, relatedTypes);
                allTypes.UnionWith(types);
            }

            // write out index file
            {
                StringBuilder builder = new();
                GenerateIndex(builder, rootTypes, allTypes, 1);
                WriteToFile(Path.Combine(documentationRoot, $"Index.md.html"), builder);
            }

            // write out a file for each type
            foreach (var type in allTypes) {
                StringBuilder builder = new();
                DocumentType(cache, type, builder, allTypes, relatedTypes, 1);
                WriteToFile(GetDocumentationPath(documentationRoot, type), builder);
            }
        }

        private static void WriteToFile(string filePath, StringBuilder builder) {
            builder.AppendLine(FileFooter);

            // HACK: squash double newlines
            string document = builder.ToString()
                .Replace("\r\n", "\n")
                .Replace("\n\n\n\n", "\n")
                .Replace("\n\n\n", "\n")
                .Replace("\n\n", "\n");

            File.WriteAllText(filePath, document);
        }

        internal static string GetDocumentationPath(string documentationRoot, Type type) {
            string relativePath = Path.Combine(documentationRoot, "Types", $"{FormatTypeName(type, null, false)}.md.html");
            return Path.GetFullPath(relativePath);
        }

        private static void CollectTypes(ReflectionCache cache, Type rootType, Type parentType, Type currentType, HashSet<Type> types, Dictionary<Type, HashSet<Type>> relatedTypes) {
            if (types.Contains(currentType)) {
                return;
            }
            if (currentType.IsGenericType) {
                foreach (var genericArgumentType in currentType.GetGenericArguments()) {
                    CollectTypes(cache, rootType, parentType, genericArgumentType, types, relatedTypes);
                }
            }
            else if (currentType.IsArray) {
                CollectTypes(cache, rootType, parentType, currentType.GetElementType(), types, relatedTypes);
            }
            else if (currentType.IsEnum) {
                types.Add(currentType);
                AddRelatedType(currentType, rootType, relatedTypes);
                if (parentType != null) {
                    AddRelatedType(currentType, parentType, relatedTypes);
                }
            }
            else if (currentType.Assembly == rootType.Assembly) {
                types.Add(currentType);
                AddRelatedType(currentType, rootType, relatedTypes);
                if (parentType != null) {
                    AddRelatedType(currentType, parentType, relatedTypes);
                }

                var typeInfo = cache.GetTypeInfo(currentType);
                if (typeInfo.UnionKeys != null) {
                    foreach (var unionChild in typeInfo.UnionKeys) {
                        CollectTypes(cache, rootType, currentType, unionChild.Item2, types, relatedTypes);

                        // all union types relate to each other
                        foreach (var unionChild2 in typeInfo.UnionKeys) {
                            AddRelatedType(unionChild.Item2, unionChild2.Item2, relatedTypes);
                        }
                    }
                }
                for (int memberIndex = 0; memberIndex < typeInfo.MemberNames.Count; memberIndex++) {
                    Type memberType = typeInfo.GetMemberType(memberIndex);
                    CollectTypes(cache, rootType, currentType, memberType, types, relatedTypes);
                    AddRelatedType(memberType, currentType, relatedTypes);
                }
            }
        }

        private static void AddRelatedType(Type hostType, Type relatedType, Dictionary<Type, HashSet<Type>> relatedTypes) {
            if (hostType == relatedType) {
                return;
            }

            if (!relatedTypes.TryGetValue(hostType, out HashSet<Type> relatedTypeList)) {
                relatedTypes.Add(hostType, new());
                relatedTypeList = relatedTypes[hostType];
            }

            relatedTypeList.Add(relatedType);
        }

        private static void GenerateIndex(StringBuilder builder, Type[] rootTypes, HashSet<Type> allTypes, int headerDepth) {
            builder.AppendLine($"{FormatHeader(headerDepth)} Spry Fox Yaml Config Documentation");

            builder.AppendLine($"{FormatHeader(headerDepth + 1)} Top Level Types");
            foreach (var type in rootTypes) {
                string typeFriendlyName = FormatTypeName(type, allTypes, true, true);
                builder.AppendLine($"- {typeFriendlyName}");
            }

            builder.AppendLine($"{FormatHeader(headerDepth + 1)} All Types");
            var typesList = allTypes.ToList();
            typesList.Sort((A, B) => String.CompareOrdinal(A.Name, B.Name));
            foreach (var type in typesList) {
                string typeFriendlyName = FormatTypeName(type, allTypes, true, true);
                builder.AppendLine($"- {typeFriendlyName}");
            }
        }

        private static void DocumentType(ReflectionCache cache, Type type, StringBuilder builder, HashSet<Type> allTypes, Dictionary<Type, HashSet<Type>> relatedTypes, int headerDepth) {

            string typeFriendlyName = FormatTypeName(type, allTypes, false);
            builder.AppendLine($"{FormatHeader(headerDepth)} {typeFriendlyName}");

            var descriptionAttribute = type.GetCustomAttribute<ConfigDocumentationDescriptionAttribute>();
            if (descriptionAttribute != null) {
                builder.AppendLine(descriptionAttribute.Value);
            }

            var typeInfo = allTypes.Contains(type) ? cache.GetTypeInfo(type) : null;

            if (type.IsEnum) {
                builder.AppendLine();
                builder.AppendLine("Enumeration with the following values:");
                foreach (string enumName in Enum.GetNames(type)) {
                    builder.AppendLine($"- `{enumName}`");
                }
            } else {
                if (typeInfo != null && typeInfo.UnionKeys != null) {
                    builder.AppendLine();
                    builder.AppendLine($"{FormatHeader(headerDepth + 1)} Multi Type");
                    TableStart(builder, "Key", "Type");
                    foreach (var unionChild in typeInfo.UnionKeys) {
                        string memberName = FormatName(unionChild.Item1);
                        string memberTypeName = FormatTypeName(unionChild.Item2, allTypes, true);
                        TableAddRow(builder, memberName, memberTypeName);

                        string description = "";
                        var memberDescriptionAttribute = unionChild.Item2.GetCustomAttribute<ConfigDocumentationDescriptionAttribute>();
                        if (memberDescriptionAttribute != null) {
                            description = memberDescriptionAttribute.Value;
                        }
                        TableAddDescriptionRow(builder, description, 2);
                    }
                    TableEnd(builder);
                    builder.AppendLine();
                }

                if (typeInfo != null && typeInfo.MemberNames.Count > 0) {
                    bool singleProperty = TypeIsSingleProperty(typeInfo, out int singlePropertyMemberIndex);

                    builder.AppendLine();
                    builder.AppendLine($"{FormatHeader(headerDepth + 1)} Fields");
                    if (typeInfo.FromDoc != null) {
                        builder.AppendLine("This type used custom parsing logic and so it's fields cannot be extracted for generated documentation.");
                    } else {
                        TableStart(builder, "Field", "Type", "Attributes");
                        FieldTableRows(builder, cache, allTypes, typeInfo, singlePropertyMemberIndex);
                        TableEnd(builder);
                    }

                    if (typeInfo.FromDocString != null) {
                        Note(builder, $"This type supports being authored as a single string in addition to as an object. " +
                            "If the above description does not cover how this works (or is missing) talk to your local gameplay engineer and ask them to add one.");
                    }

                    if (singleProperty) {
                        string memberName = FormatName(typeInfo.MemberNames[singlePropertyMemberIndex]);
                        Note(builder, $"With this type you may specify the value of \"{memberName}\" as the value of the whole type.");
                    }
                }

                builder.AppendLine($"{FormatHeader(headerDepth + 1)} Examples");

                int exampleCount = ExamplesFromAttributes(builder, type, 0);

                if (exampleCount == 0) {
                    StringBuilder exampleBuilderMinimal = new();
                    StringBuilder exampleBuilderExpanded = new();
                    GenerateExampleForType(cache, type, null, exampleBuilderMinimal, new HashSet<Type>(), 0, true, false, false);
                    GenerateExampleForType(cache, type, null, exampleBuilderExpanded, new HashSet<Type>(), 0, false, false, false);

                    string exampleMinimal = exampleBuilderMinimal.ToString();
                    string exampleExpanded = exampleBuilderExpanded.ToString();

                    if (exampleMinimal != exampleExpanded) {
                        builder.AppendLine();
                        builder.AppendLine("<details><summary>(Generated Example) Minimal</summary>");
                        builder.AppendLine("```yaml");
                        builder.AppendLine(exampleMinimal);
                        builder.AppendLine("```");
                        builder.AppendLine("</details>");

                        builder.AppendLine("<details><summary>(Generated Example) Fully Expanded</summary>");
                        builder.AppendLine("```yaml");
                        builder.AppendLine(exampleExpanded);
                        builder.AppendLine("```");
                        builder.AppendLine("</details>");
                    } else {
                        builder.AppendLine("<details><summary>(Generated Example)</summary>");
                        builder.AppendLine();
                        builder.AppendLine("```yaml");
                        builder.AppendLine(exampleMinimal);
                        builder.AppendLine("```");
                        builder.AppendLine("</details>");
                    }
                }

                builder.AppendLine();
            }

            // related types (for type not displayType)
            if (relatedTypes.TryGetValue(type, out HashSet<Type> relatedTypeList)) {
                builder.AppendLine();
                builder.AppendLine($"{FormatHeader(headerDepth + 1)} See Also");
                builder.AppendLine( "- [Types index](../Index.md.html)");
                foreach (var relatedType in relatedTypeList) {
                    builder.AppendLine($"- {FormatTypeName(relatedType, allTypes, true)}");
                }
            }

            builder.AppendLine();
        }

        static void FieldTableRows(StringBuilder builder, ReflectionCache cache, HashSet<Type> allTypes, ReflectionCache.TypeInfo typeInfo, int singlePropertyPropertyIndex)
        {
            StringBuilder attributesStringBuilder = new();
            for (int memberIndex = 0; memberIndex < typeInfo.MemberNames.Count; memberIndex++)
            {
                Type memberType = typeInfo.GetMemberType(memberIndex);
                string memberName = FormatName(typeInfo.MemberNames[memberIndex]);
                string memberTypeName = FormatTypeName(memberType, allTypes, true);

                bool isInline = (typeInfo.MemberOptions[memberIndex] & ReflectionCache.TypeInfo.MemberOptionFlags.Inline) != 0;
                if (isInline && singlePropertyPropertyIndex != memberIndex)
                {
                    var memberTypeInfo = allTypes.Contains(memberType) ? cache.GetTypeInfo(memberType) : null;
                    if (memberTypeInfo != null && memberTypeInfo.MemberNames.Count > 0 && memberTypeInfo.UnionKeys == null) {
                        FieldTableRows(builder, cache, allTypes, memberTypeInfo, -1);
                        continue;
                    }
                }
                attributesStringBuilder.Clear();
                if (!typeInfo.IsRequired(memberIndex, false)) {
                    attributesStringBuilder.AppendLine("`Optional`  ");
                }
                if (isInline) {
                    attributesStringBuilder.AppendLine("`Inline`  ");
                }
                TableAddRow(builder, $"`{memberName}`", memberTypeName, attributesStringBuilder.ToString());

                string description = GetMemberDescription(typeInfo.MemberInfos[memberIndex], memberType);
                TableAddDescriptionRow(builder, description, 3);
            }
        }

        private static int ExamplesFromAttributes(StringBuilder builder, Type type, int examplesSoFar) {
            var customExampleAttributes = type.GetCustomAttributes<ConfigDocumentationExampleAttribute>();

            int exampleNumber = examplesSoFar + 1;
            foreach (var attribute in customExampleAttributes) {
                builder.AppendLine($"<details><summary>Example {exampleNumber}</summary>");
                builder.AppendLine("```yaml");
                builder.AppendLine(attribute.Value);
                builder.AppendLine("```");
                builder.AppendLine(" ");
                builder.AppendLine("</details>");
                exampleNumber++;
            }

            return exampleNumber - 1;
        }

        #region EXAMPLE GENERATION
        private static void GenerateExampleForType(ReflectionCache cache, Type type, MemberInfo memberInfo, StringBuilder builder, HashSet<Type> loopProtection, int depth, bool drawMinimal, bool isOptional, bool isList) {
            bool hasNote = false;
            if (loopProtection.Contains(type)) {
                if (isOptional) {
                    if (isList) {
                        builder.Append($"{FormatTabs(depth)}- ");
                    }

                    GenerateNote(builder, "Optional, same as a parent type, clipped from example", ref hasNote);
                    return;
                }
            }
            loopProtection = new HashSet<Type>(loopProtection);
            loopProtection.Add(type);

            var customExampleAttribute = memberInfo?.GetCustomAttributes<ConfigDocumentationExampleAttribute>().FirstOrDefault();
            if (customExampleAttribute == null) {
                customExampleAttribute = type.GetCustomAttributes<ConfigDocumentationExampleAttribute>().FirstOrDefault();
            }

            if (customExampleAttribute != null) {
                if (isList) {
                    builder.Append($"{FormatTabs(depth)}- ");
                }
                builder.Append(customExampleAttribute.Value);
                if (isOptional) {
                    GenerateNote(builder, "Optional", ref hasNote);
                }
                return;
            }

            if (type == typeof(string)) {
                GenerateExampleForString(builder, depth, drawMinimal, isOptional, isList);
            }
            else if (type == typeof(int)) {
                GenerateExampleForInt(builder, depth, drawMinimal, isOptional, isList);
            }
            else if (type == typeof(float)) {
                GenerateExampleForFloat(builder, depth, drawMinimal, isOptional, isList);
            }
            else if (type == typeof(bool)) {
                GenerateExampleForBool(builder, depth, drawMinimal, isOptional, isList);
            }
            else if (type.IsEnum) {
                GenerateExampleForEnum(type, builder, depth, drawMinimal, isOptional, isList);
            }
            else if (type.IsArray) {
                GenerateExampleForList(cache, type.GetElementType(), builder, loopProtection, depth, drawMinimal, isOptional, isList);
            }
            else if (type.IsGenericType) {
                if (TypeIsOrExtendsGeneric(type, typeof(List<>), out var listType)) {
                    GenerateExampleForList(cache, listType.GetGenericArguments()[0], builder, loopProtection, depth, drawMinimal, isOptional, isList);
                }
                else if (TypeIsOrExtendsGeneric(type, typeof(Dictionary<,>), out var dictionaryType)) {
                    GenerateExampleForDictionary(cache, dictionaryType.GetGenericArguments()[1], builder, loopProtection, depth, drawMinimal, isOptional, isList);
                }
                else if (TypeIsOrExtendsGeneric(type, typeof(Nullable<>), out var nullableType)) {
                    GenerateExampleForType(cache, nullableType.GetGenericArguments()[0], memberInfo, builder, loopProtection, depth, drawMinimal, isOptional, isList);
                }
            }
            else {
                if (isOptional) {
                    GenerateNote(builder, "Optional", ref hasNote);
                }
                var typeInfo = cache.GetTypeInfo(type);
                if (typeInfo.UnionKeys != null) {
                    GenerateExampleForUnion(cache, builder, loopProtection, depth, drawMinimal, isOptional, isList, typeInfo, memberInfo);
                }
                else {
                    if (typeInfo.FromDoc != null) {
                        GenerateNote(builder, "unknown format - type uses custom parsing", ref hasNote);
                    }
                    else {
                        bool isSinglePropertyWrapper = TypeIsSingleProperty(typeInfo, out int singlePropertyIndex);
                        int numOptionalMembers = typeInfo.NumOptionalFields;
                        if (isSinglePropertyWrapper && (numOptionalMembers == 0 || drawMinimal)) {
                            Type memberType = typeInfo.GetMemberType(singlePropertyIndex);
                            GenerateExampleForType(cache, memberType, typeInfo.MemberInfos[singlePropertyIndex], builder, loopProtection, depth + 1, drawMinimal,
                                false, false);
                        }
                        else {
                            int childDepth = depth;
                            for (int memberIndex = 0; memberIndex < typeInfo.MemberNames.Count; memberIndex++) {

                                Type memberType = typeInfo.GetMemberType(memberIndex);
                                string memberName = FormatName(typeInfo.MemberNames[memberIndex]);
                                if ((typeInfo.MemberOptions[memberIndex] & ReflectionCache.TypeInfo.MemberOptionFlags.Inline) != 0) {
                                    var memberTypeInfo = cache.GetTypeInfo(memberType);
                                    GenerateExampleForType(cache, memberType, memberInfo, builder, loopProtection, depth, drawMinimal, false, false);
                                    continue;
                                }

                                bool isMemberOptional = !typeInfo.IsRequired(memberIndex, false);
                                if (drawMinimal && isMemberOptional && depth > 1) {
                                    continue;
                                }

                                builder.AppendLine();
                                if (isList) {
                                    builder.Append($"{FormatTabs(childDepth)}- {memberName}: ");
                                    isList = false;
                                    childDepth++;
                                } else {
                                    builder.Append($"{FormatTabs(childDepth)}{memberName}: ");
                                }
                                GenerateExampleForType(cache, memberType, typeInfo.MemberInfos[memberIndex], builder, loopProtection, childDepth + 1, drawMinimal,
                                    isMemberOptional, false);
                            }
                        }
                    }
                }
            }
            builder.AppendLine();
        }

        static void GenerateNote(StringBuilder builder, string note, ref bool hasNote)
        {
            if (!hasNote) {
                builder.Append($" # {note}");
            } else {
                builder.Append($", {note}");
            }
            hasNote = true;
        }

        static void GenerateExampleForUnion(ReflectionCache cache, StringBuilder builder, HashSet<Type> loopProtection, int depth, bool drawMinimal, bool isOptional, bool isList, ReflectionCache.TypeInfo typeInfo, MemberInfo memberInfo)
        {
            foreach ((string childKey, Type childType) in typeInfo.UnionKeys)
            {
                if (drawMinimal) {
                    if (isList)
                    {
                        builder.AppendLine($"{FormatTabs(depth)}- {childKey} # clipped");
                    }
                    else
                    {
                        builder.AppendLine($"{FormatTabs(depth)}{childKey}: {{}} # clipped");
                    }
                    continue;
                }

                if (loopProtection.Contains(childType))
                {
                    if (isList)
                    {
                        builder.AppendLine($"{FormatTabs(depth)}- {childKey} # clipped contents to prevent infinite recursion");
                    }
                    else
                    {
                        builder.AppendLine($"{FormatTabs(depth)}{childKey}: {{}} # clipped contents to prevent infinite recursion");
                    }
                    continue;
                }
                int childDepth = depth;

                var childTypeInfo = cache.GetTypeInfo(childType);

                builder.AppendLine();


                if (childTypeInfo.FromDoc == null && (childTypeInfo.IsUnionInline || childTypeInfo.MemberNames.Count == 1))
                {
                    GenerateExampleForType(cache, childType, memberInfo, builder, loopProtection, childDepth, drawMinimal, isOptional, isList);
                }
                else
                {
                    if (isList)
                    {
                        builder.Append($"{FormatTabs(childDepth)}- {childKey}");
                        childDepth++;
                    }
                    else
                    {
                        builder.Append($"{FormatTabs(childDepth)}{childKey}");
                    }

                    bool hasMembers = childTypeInfo.MemberNames.Count > 0;
                    if (hasMembers)
                    {
                        builder.Append(":");
                    }

                    if (hasMembers)
                    {
                        GenerateExampleForType(cache, childType, memberInfo, builder, loopProtection, childDepth + 1, drawMinimal, false, false);
                    }
                }
            }
        }

        static void GenerateExampleForList(ReflectionCache cache, Type type, StringBuilder builder, HashSet<Type> loopProtection, int depth, bool drawMinimal, bool isOptional, bool isList)
        {
            if (isOptional)
            {
                builder.AppendLine(" # Optional");
            }
            else {
                builder.AppendLine();
            }

            GenerateExampleForType(cache, type, null, builder, loopProtection, depth, drawMinimal, false, true);
        }

        static void GenerateExampleForDictionary(ReflectionCache cache, Type type, StringBuilder builder, HashSet<Type> loopProtection, int depth, bool drawMinimal, bool isOptional, bool isList)
        {
            if (isOptional)
            {
                builder.Append(" # Optional");
                builder.AppendLine();
            }
            else {
                builder.AppendLine();
            }

            if (isList)
            {
                builder.AppendLine($"{FormatTabs(depth)}- \"stringKey\": ");
                GenerateExampleForType(cache, type, null, builder, loopProtection, depth + 1, drawMinimal, false, false);
            } else {
                builder.Append($"{FormatTabs(depth)}\"stringKey\": ");
                GenerateExampleForType(cache, type, null, builder, loopProtection, depth + 1, drawMinimal, false, false);
            }
        }

        static void GenerateExampleForEnum(Type type, StringBuilder builder, int depth, bool drawMinimal, bool isOptional, bool isList)
        {
            foreach (string EnumName in Enum.GetNames(type))
            {
                if (isList)
                {
                    builder.Append($"{FormatTabs(depth)}- ");
                }

                builder.Append(EnumName);

                if (isOptional)
                {
                    builder.Append(" # Optional");
                }

                builder.AppendLine();

                if (!isList)
                {
                    break;
                }
            }
        }

        static void GenerateExampleForBool(StringBuilder builder, int depth, bool drawMinimal, bool isOptional, bool isList)
        {
            for (int counter = 0; counter < (isList ? 3 : 1); counter++) {
                if (isList) {
                    builder.Append($"{FormatTabs(depth)}- ");
                }

                builder.Append("false");

                if (isOptional) {
                    builder.Append(" # Optional");
                }

                builder.AppendLine();
            }
        }

        static void GenerateExampleForFloat(StringBuilder builder, int depth, bool drawMinimal, bool isOptional, bool isList)
        {
            for (int counter = 0; counter < (isList ? 3 : 1); counter++) {
                if (isList) {
                    builder.Append($"{FormatTabs(depth)}- ");
                }

                builder.Append("0.0");

                if (isOptional) {
                    builder.Append(" # Optional");
                }

                builder.AppendLine();
            }
        }

        static void GenerateExampleForInt(StringBuilder builder, int depth, bool drawMinimal, bool isOptional, bool isList)
        {
            for (int counter = 0; counter < (isList ? 3 : 1); counter++) {
                if (isList) {
                    builder.Append($"{FormatTabs(depth)}- ");
                }

                builder.Append("0");

                if (isOptional) {
                    builder.Append(" # Optional");
                }
                builder.AppendLine();
            }
        }

        static void GenerateExampleForString(StringBuilder builder, int depth, bool drawMinimal, bool isOptional, bool isList)
        {
            for (int counter = 0; counter < (isList ? 3 : 1); counter++) {
                if (isList) {
                    builder.Append($"{FormatTabs(depth)}- ");
                }

                builder.Append("\"text\"");

                if (isOptional) {
                    builder.Append(" # Optional");
                }
                builder.AppendLine();
            }
        }
        #endregion

        private static string FormatTabs(int depth) => new string(' ', depth * 2);
        private static string FormatHeader(int depth) => new string('#', depth);

        private static string FormatName(string name) {
            return name.Length >= 1 ? name.Substring(0, 1).ToLower() + name.Substring(1, name.Length - 1) : "";
        }
        
        private static string FormatTypeName(Type type, HashSet<Type> types, bool linkify, bool isIndex=false) {
            if (type == typeof(float))
            {
                return "Number";
            }

            if (type == typeof(int))
            {
                return "Integer";
            }

            var customNameAttribute = type.GetCustomAttribute<ConfigDocumentationNameAttribute>();
            if (type.IsGenericType) {
                if (type.GetGenericTypeDefinition() == typeof(List<>)) {
                    return $"(List) {FormatTypeName(type.GetGenericArguments()[0], types, linkify)}";
                }
                if (type.GetGenericTypeDefinition() == typeof(Dictionary<,>)) {
                    return $"(Dictionary) string: {FormatTypeName(type.GetGenericArguments()[1], types, linkify)}";
                }
                if (type.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                    return FormatTypeName(type.GetGenericArguments()[0], types,linkify);
                }
                if (customNameAttribute != null) {
                    string friendlyName = customNameAttribute.Value;
                    var genericArguments = type.GetGenericArguments();
                    for (int argumentIndex = 0; argumentIndex < genericArguments.Length; argumentIndex++) {
                        friendlyName = friendlyName.Replace($"<{argumentIndex}>", FormatTypeName(type.GetGenericArguments()[0], types, linkify));
                    }
                    return friendlyName;
                }
            }

            if (customNameAttribute != null) {
                string friendlyName = customNameAttribute.Value;
                if (linkify) {
                    return MakeLinkString(friendlyName, isIndex);
                } else {
                    return friendlyName;
                }
            }
            if (type.IsEnum) {
                string enumFriendlyName = type.Name;
                if (linkify) {
                    return MakeLinkString(enumFriendlyName, isIndex);
                } else {
                    return enumFriendlyName;
                }
            }
            if (types?.Contains(type) ?? true) {
                if (linkify) {
                    return MakeLinkString(type.Name, isIndex);
                } else {
                    return type.Name;
                }
            }
            if (type.IsArray) {
                return $"(List) {FormatTypeName(type.GetElementType(), types, linkify)}";
            }
            return type.Name.ToLower().Replace("`", "");
        }

        private static string MakeLinkString(string name, bool isIndex) {
            if (isIndex) {
                return $"[{name}](./Types/{name}.md.html)";
            } else {
                return $"[{name}](./{name}.md.html)";
            }
        }

        private static string GetTypeDescription(Type type) {
            if (type.IsGenericType) {
                var genericArgs = type.GetGenericArguments();
                if (genericArgs.Length == 1) {
                    type = genericArgs[0];
                } else {
                    return "";
                }
            }

            var descriptionAttribute = type.GetCustomAttribute<ConfigDocumentationDescriptionAttribute>();
            if (descriptionAttribute != null) {
                return descriptionAttribute.Value;
            }
            return "";
        }
        
        private static string GetMemberDescription(MemberInfo member, Type memberType) {
            var descriptionAttribute = member.GetCustomAttribute<ConfigDocumentationDescriptionAttribute>();
            if (descriptionAttribute != null) {
                return descriptionAttribute.Value;
            }
            return GetTypeDescription(memberType);
        }

        private static void TableStart(StringBuilder builder, params string[] headings) {
            builder.AppendLine("<table>");
            builder.AppendLine("<tr>");

            foreach (string heading in headings) {
                builder.AppendLine($"<th>{heading}</th>");
            }
            builder.AppendLine("</tr>");
        }

        private static void TableAddRow(StringBuilder builder, params string[] items) {
            builder.AppendLine("<tr>");

            foreach (string item in items) {
                builder.AppendLine($"<td>{item}</td>");
            }
            builder.AppendLine("</tr>");
        }

        private static void TableAddDescriptionRow(StringBuilder builder, string description, int spanCount) {
            builder.AppendLine($"<tr><td class=\"description\" colspan=\"{spanCount}\">{description}</td></tr>");
        }

        private static void TableEnd(StringBuilder builder) {
            builder.AppendLine($"</table>");
        }

        private static void Note(StringBuilder builder, string note) {
            builder.AppendLine($"<div class=\"note\"><div class=\"noteText\">{note}</div></div>");
        }

        private static bool TypeIsOrExtendsGeneric(Type testType, Type baseType, out Type outInstanceType) {
            outInstanceType = null;
            while (testType != null && testType.IsGenericType) {
                if (testType.GetGenericTypeDefinition() == baseType) {
                    outInstanceType = testType;
                    return true;
                }
                testType = testType.BaseType;
            }
            return false;
        }

        private static bool TypeIsSingleProperty(ReflectionCache.TypeInfo typeInfo, out int outRequiredMemerIndex) {
            outRequiredMemerIndex = -1;
            int numRequiredMembers = typeInfo.NumRequiredFields + typeInfo.NumRequiredProperties;
            if (numRequiredMembers > 1 || numRequiredMembers == 0) {
                return false;
            }

            for (int memberIndex = 0; memberIndex < typeInfo.MemberNames.Count; memberIndex++) {
                if (typeInfo.IsRequired(memberIndex, false)) {
                    outRequiredMemerIndex = memberIndex;
                    Type memberType = typeInfo.GetMemberType(memberIndex);
                    if (memberType.IsPrimitive) {
                        return true;
                    }
                    if (memberType == typeof(string)) {
                        return true;
                    }

                    if ((typeInfo.MemberOptions[memberIndex] & ReflectionCache.TypeInfo.MemberOptionFlags.Inline) != 0) {
                        return true;
                    }

                    // other tests?

                    break;
                }
            }
            return false;
        }
    }
}
