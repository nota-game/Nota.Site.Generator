using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stasistium;
using Stasistium.Documents;
using Stasistium.Helper;
using Stasistium.Stages;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Nota.Site.Generator.Stages
{
    public class CacheDocumentStringStage<T> : Stasistium.Stages.StageBase<T, string>
    {
        private readonly SubPipline<T, string> pipeline;
        private readonly IStageBaseOutput<string> result;

        [StageName("CacheDocument")]
        public CacheDocumentStringStage(string id, Func<IStageBaseOutput<T>, IStageBaseOutput<string>> toCache, IGeneratorContext context) : base(context, id)
        {
            (this.pipeline, this.result) = SubPipeline.Create(toCache, context);

        }


        protected override async Task<ImmutableList<IDocument<string>>> Work(ImmutableList<IDocument<T>> inputList, OptionToken options)
        {


            var cache = this.Context.CacheFolder;

            var currentDir = new DirectoryInfo(Path.Combine(cache.FullName, this.Name));
            if (!currentDir.Exists)
                currentDir.Create();

            var list = ImmutableList.CreateBuilder<IDocument<string>>();

            foreach (var input in inputList) {
                var subOptions = options.CreateSubToken();
                var fileName = $"{input.Id}-{input.Hash}";


                var fileInfo = new FileInfo(Path.Combine(currentDir.FullName, fileName));
                ImmutableList<IDocument<string>>? result = null;
                if (fileInfo.Exists && Nota.UseCache) {
                    try {
                        using var stream = fileInfo.OpenRead();
                        result = (await JsonSerelizer.Load<(string value, string id, string hash, Dictionary<Type, object> metadata)[]>(stream))

                            .Select(x => this.Context.CreateDocument(x.value, x.hash, x.id, ToContainer(x.metadata)))
                            .ToImmutableList();

                        MetadataContainer ToContainer(Dictionary<Type, object> dic)
                        {
                            var container = Context.EmptyMetadata;
                            foreach (var (key, value) in dic) {
                                container.Add(key, value);
                            }
                            return container;
                        }
                    } catch (System.Exception e) {
                        Context.Logger.Error($"Faild to load cache {e.Message} ({e.GetType()}) {e.ToString()}");
                    }
                }
                if (result is null) {
                    var completion = new TaskCompletionSource<ImmutableList<IDocument<string>>>();

                    this.result.PostStages += Result_PostStages;
                    Task Result_PostStages(ImmutableList<IDocument<string>> input, OptionToken options)
                    {
                        if (options.IsSubTokenOf(subOptions) || options == subOptions)
                            completion.SetResult(input);
                        return Task.CompletedTask;
                    }

                    await this.pipeline.Invoke(ImmutableList.Create(input), subOptions);
                    result = await completion.Task;
                    this.result.PostStages -= Result_PostStages;

                    if (!fileInfo.Directory?.Exists ?? false)
                        fileInfo.Directory.Create();
                    using var stream = fileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.None);
                    var serelize = result.Select(x => (x.Value, x.Id, x.Hash, ToDic(x.Metadata))).ToArray();

                    Dictionary<Type, object> ToDic(MetadataContainer container)
                    {
                        var dic = new Dictionary<Type, object>();
                        foreach (var item in container.Keys) {
                            dic.Add(item, container.GetValue(item));
                        }
                        return dic;
                    }

                    await JsonSerelizer.Write(serelize, stream);
                }
                list.AddRange(result);
            }
            return list.ToImmutable();
        }
    }

    public class CacheDocumentStreamStage<T> : Stasistium.Stages.StageBase<T, Stream>
    {
        private readonly SubPipline<T, Stream> pipeline;
        private readonly IStageBaseOutput<Stream> result;

        [StageName("CacheDocument")]
        public CacheDocumentStreamStage(string id, Func<IStageBaseOutput<T>, IStageBaseOutput<Stream>> toCache, IGeneratorContext context) : base(context, id)
        {
            (this.pipeline, this.result) = SubPipeline.Create(toCache, context);

        }


        protected override async Task<ImmutableList<IDocument<Stream>>> Work(ImmutableList<IDocument<T>> inputList, OptionToken options)
        {


            var cache = this.Context.CacheFolder;

            var currentDir = new DirectoryInfo(Path.Combine(cache.FullName, this.Name));
            if (!currentDir.Exists)
                currentDir.Create();

            var list = ImmutableList.CreateBuilder<IDocument<Stream>>();

            foreach (var input in inputList) {
                var subOptions = options.CreateSubToken();
                var fileName = $"{input.Id}-{input.Hash}";


                var fileInfo = new FileInfo(Path.Combine(currentDir.FullName, fileName));
                ImmutableList<IDocument<Stream>>? result = null;
                if (fileInfo.Exists && Nota.UseCache) {
                    try {
                        using var stream = fileInfo.OpenRead();
                        (byte[] value, string id, string hash, Dictionary<Type, object> metadata)[] values = await JsonSerelizer.Load<(byte[] value, string id, string hash, Dictionary<Type, object> metadata)[]>(stream);
                        result = values

                            .Select(x => this.Context.CreateDocument<Stream>(null!, x.hash, x.id, ToContainer(x.metadata)).With(() => new MemoryStream(x.value) as Stream, x.hash))
                            .ToImmutableList();

                        MetadataContainer ToContainer(Dictionary<Type, object> dic)
                        {
                            var container = Context.EmptyMetadata;
                            foreach (var (key, value) in dic) {
                                container = container.Add(key, value);
                            }
                            return container;
                        }
                    } catch (System.Exception e) {
                        Context.Logger.Error($"Faild to load cache {e.Message} ({e.GetType()}) {e.ToString()}");
                    }
                }
                if (result is null) {
                    var completion = new TaskCompletionSource<ImmutableList<IDocument<Stream>>>();

                    this.result.PostStages += Result_PostStages;
                    Task Result_PostStages(ImmutableList<IDocument<Stream>> input, OptionToken options)
                    {
                        if (options.IsSubTokenOf(subOptions) || options == subOptions)
                            completion.SetResult(input);
                        return Task.CompletedTask;
                    }

                    await this.pipeline.Invoke(ImmutableList.Create(input), subOptions);
                    result = await completion.Task;
                    this.result.PostStages -= Result_PostStages;

                    if (!fileInfo.Directory?.Exists ?? false)
                        fileInfo.Directory.Create();
                    using var stream = fileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.None);
                    var serelize = await Task.WhenAll(result.Select(async x => (await ToArray(x.Value), x.Id, x.Hash, ToDic(x.Metadata))));

                    async Task<byte[]> ToArray(Stream s)
                    {
                        using (s) {
                            using var ms = new MemoryStream();
                            await s.CopyToAsync(ms);
                            return ms.ToArray();
                        }

                    }

                    Dictionary<Type, object> ToDic(MetadataContainer container)
                    {
                        var dic = new Dictionary<Type, object>();
                        foreach (var item in container.Keys) {
                            dic.Add(item, container.GetValue(item));
                        }
                        return dic;
                    }

                    await JsonSerelizer.Write(serelize, stream);
                }
                list.AddRange(result);
            }
            return list.ToImmutable();
        }
    }

    internal class JsonSerelizer
    {
        public static async Task Write(object baseCache, System.IO.Stream stream, bool indented = false)
        {


            var array = Write(baseCache);

            using var textWriter = new System.IO.StreamWriter(stream);
            using var jsonWriter = new Newtonsoft.Json.JsonTextWriter(textWriter) { Formatting = indented ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None };
            var ser = new JsonSerializer();

            var type = baseCache.GetType();
            var typeName = type.AssemblyQualifiedName;

            //ser.Serialize(jsonWriter, (typeName, baseCache));
            await array.WriteToAsync(jsonWriter).ConfigureAwait(false);

        }

        private static JArray Write(object baseCache)
        {
            var result = new JArray();
            var queu = new Queue<object>();
            int index = 0;
            var idLookup = new Dictionary<object, int>();
            Enqueue(baseCache);
            int Enqueue(object current)
            {
                if (idLookup.ContainsKey(current))
                    return idLookup[current];

                queu.Enqueue(current);
                idLookup.Add(current, index);
                var currentIndex = index;
                index++;
                return currentIndex;
            }

            var refKind = JValue.CreateString("ref");
            var scalarKind = JValue.CreateString("scalar");

            while (queu.TryDequeue(out var current)) {

                var type = current.GetType();
                var typeName = type.AssemblyQualifiedName;

                var implementedInterfaces = new HashSet<Type>(type.GetInterfaces().Where(x => x.IsGenericType).Select(x => x.GetGenericTypeDefinition()).Concat(type.GetInterfaces().Where(x => !x.IsGenericType)));

                var currentJObject = new JObject();
                currentJObject.Add("type", typeName);
                result.Add(currentJObject);

                if (type.IsArray) {
                    var arrayElements = new JArray();
                    currentJObject.Add("elements", arrayElements);

                    var array = (System.Collections.IList)current;
                    for (int i = 0; i < array.Count; i++)
                        arrayElements.Add(GetValueObject(array[i]));

                } else if (current is System.Runtime.CompilerServices.ITuple tuple) {
                    var tupleArray = new JArray();
                    currentJObject.Add("tuple", tupleArray);
                    for (int i = 0; i < tuple.Length; i++)
                        tupleArray.Add(GetValueObject(tuple[i]));
                } else if (current.GetType().IsGenericType && current.GetType().GetGenericTypeDefinition() == typeof(System.Collections.Immutable.ImmutableList<>)) {
                    var arrayElements = new JArray();
                    currentJObject.Add("elements", arrayElements);

                    var array = (System.Collections.IList)current;
                    for (int i = 0; i < array.Count; i++)
                        arrayElements.Add(GetValueObject(array[i]));
                } else if (implementedInterfaces.Contains(typeof(IDictionary<,>))) {
                    var arrayElements = new JArray();
                    currentJObject.Add("map", arrayElements);

                    var enumerable = (System.Collections.IEnumerable)current;
                    foreach (var item in enumerable) {
                        if (item is null)
                            continue;

                        var keyValuePairType = item.GetType();

                        var keyProperty = keyValuePairType.GetProperty(nameof(KeyValuePair<object, object>.Key));
                        var ValueProperty = keyValuePairType.GetProperty(nameof(KeyValuePair<object, object>.Value));

                        if (keyProperty is null || ValueProperty is null)
                            continue;

                        var key = keyProperty.GetValue(item);
                        var value = ValueProperty.GetValue(item);

                        var entry = new JObject();
                        arrayElements.Add(entry);

                        entry.Add("key", GetValueObject(key));
                        entry.Add("value", GetValueObject(value));
                    }
                } else if (implementedInterfaces.Contains(typeof(ICollection<>))) {
                    var constructor = type.GetConstructor(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, Type.EmptyTypes, null);
                    if (constructor is null && !type.IsValueType)
                        throw new ArgumentException($"Graphe contains a type that has no default constructor. ({typeName})");

                    var arrayElements = new JArray();
                    currentJObject.Add("elements", arrayElements);


                    var array = (System.Collections.IEnumerable)current;
                    foreach (var item in array)
                        arrayElements.Add(GetValueObject(item));



                } else {
                    var constructor = type.GetConstructor(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, Type.EmptyTypes, null);
                    if (constructor is null && !type.IsValueType)
                        throw new ArgumentException($"Graphe contains a type that has no default constructor. ({typeName})");

                    var properties = type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);


                    var propertyArray = new JObject();
                    currentJObject.Add("propertys", propertyArray);

                    foreach (var property in properties) {

                        var setMethod = property.GetSetMethod();


                        if (setMethod is null) {
                            var backiongFiled = GetBackingField(property);
                            if (backiongFiled is null)
                                continue;
                        }



                        var name = property.Name;
                        var value = property.GetValue(current);

                        propertyArray.Add(name, GetValueObject(value));
                    }
                }



                JObject GetValueObject(object? value)
                {
                    var valueObject = new JObject();
                    switch (value) {
                        case null:
                            valueObject.Add("Kind", refKind);
                            valueObject.Add("value", JValue.FromObject(-1));
                            break;

                        case string s:
                            valueObject.Add("Kind", scalarKind);
                            valueObject.Add("value", JValue.CreateString(s));
                            break;

                        case Type t:
                            valueObject.Add("Kind", scalarKind);
                            valueObject.Add("value", JValue.CreateString(t.AssemblyQualifiedName));
                            break;

                        case DateTime dateTime:
                            valueObject.Add("Kind", scalarKind);
                            valueObject.Add("value", JValue.CreateString($"{dateTime.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{dateTime.Kind.ToString()}"));
                            break;

                        case DateTimeOffset dateTime:
                            valueObject.Add("Kind", scalarKind);
                            valueObject.Add("value", JValue.CreateString($"{dateTime.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{dateTime.Offset.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
                            break;

                        case TimeSpan dateTime:
                            valueObject.Add("Kind", scalarKind);
                            valueObject.Add("value", JValue.FromObject(dateTime.Ticks));
                            break;

                        case byte[] array:
                            valueObject.Add("Kind", scalarKind);
                            valueObject.Add("value", JValue.FromObject(Convert.ToBase64String(array)));
                            break;

                        case int _:
                        case byte _:
                        case short _:
                        case long _:
                        case uint _:
                        case ushort _:
                        case ulong _:
                        case float _:
                        case double _:
                        case decimal _:
                        case bool _:
                            valueObject.Add("Kind", scalarKind);
                            valueObject.Add("value", JValue.FromObject(value));
                            break;

                        default:
                            if (value.GetType().IsEnum) {
                                valueObject.Add("Kind", scalarKind);
                                valueObject.Add("value", JValue.FromObject(Convert.ChangeType(value, typeof(long))));
                            } else {
                                valueObject.Add("Kind", refKind);
                                valueObject.Add("value", JValue.FromObject(Enqueue(value)));
                            }
                            break;
                    }
                    return valueObject;
                }
            }

            return result;
        }

        internal static async Task<T> Load<T>(System.IO.Stream stream)
        {
            using var textReader = new System.IO.StreamReader(stream);
            using var jsonReadr = new Newtonsoft.Json.JsonTextReader(textReader);

            var array = await JArray.LoadAsync(jsonReadr).ConfigureAwait(false);

            //var ser = new JsonSerializer()
            //{


            //};
            //var (typeName, obj) = await Task.Run(() => ser.Deserialize<(string type, T obj)>(jsonReadr)).ConfigureAwait(false);

            //if (typeName is null)
            //    throw new ArgumentException($"type unknown does not have a type!");

            //var type = Type.GetType(typeName);
            //if (type is null)
            //    throw new ArgumentException($"Can't find Type {typeName}");

            //if (type != typeof(T))
            //    throw new ArgumentException($"Wrong type {typeName}");

            //return obj;

            return Load<T>(array);
        }


        internal static T Load<T>(JArray json)
        {
            if (json.Count == 0)
                throw new ArgumentException("There must be at least on value", nameof(json));

            if (json[0]["type"]?.ToObject<string>() != typeof(T).AssemblyQualifiedName)
                throw new ArgumentException($"JSON will not deserelize to {typeof(T).AssemblyQualifiedName} but instead to {json[0]["type"]?.ToObject<string>() ?? "<null>"}", nameof(json));


            var deserelizedObjects = new object[json.Count];

            // create Objects
            for (int i = 0; i < json.Count; i++) {
                var entry = json[i];

                var typeName = entry["type"]?.ToObject<string>();
                if (typeName is null)
                    throw new ArgumentException($"Object at index {i} does not have a type!");

                var type = Type.GetType(typeName);
                if (type is null)
                    throw new ArgumentException($"Can't find Type {typeName}");

                if (entry["propertys"] is JObject || entry["map"] is JArray || entry["tuple"] is JArray) {
                    var constructor = type.GetConstructor(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, Type.EmptyTypes, null);
                    object currentObject;
                    if (constructor != null)
                        currentObject = constructor.Invoke(null);
                    else if (type.IsValueType)
                        currentObject = type.GetDefault()!; // value Type is never null
                    else
                        throw new NotSupportedException($"Type {type} must contain parameterless constructor.");

                    deserelizedObjects[i] = currentObject;


                } else if (entry["elements"] is JArray jsonElements) {
                    var implementedInterfaces = new HashSet<Type>(type.GetInterfaces().Where(x => x.IsGenericType).Select(x => x.GetGenericTypeDefinition()).Concat(type.GetInterfaces().Where(x => !x.IsGenericType)));

                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableList<>)) {
                        var elementType = type.GetGenericArguments().FirstOrDefault();
                        if (elementType is null)
                            throw new InvalidOperationException($"Type {type} should be an Array and have an ElementType");


                        var listType = typeof(ImmutableList);
                        var createBuilderMethod = listType.GetMethod(nameof(ImmutableList.CreateBuilder), 1, Type.EmptyTypes)
                            .MakeGenericMethod(elementType);
                        var builder = createBuilderMethod.Invoke(null, Array.Empty<object>());



                        deserelizedObjects[i] = builder;
                    } else if (type.IsArray) {

                        var elementType = type.GetElementType();
                        if (elementType is null)
                            throw new InvalidOperationException($"Type {type} should be an Array and have an ElementType");

                        deserelizedObjects[i] = Array.CreateInstance(elementType, jsonElements.Count);
                    } else if (implementedInterfaces.Contains(typeof(System.Collections.ICollection))) {
                        var constructor = type.GetConstructor(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, Type.EmptyTypes, null);
                        if (constructor is null && !type.IsValueType)
                            throw new ArgumentException($"Graphe contains a type that has no default constructor. ({typeName})");

                        object currentObject;
                        if (constructor != null)
                            currentObject = constructor.Invoke(null);
                        else if (type.IsValueType)
                            currentObject = type.GetDefault()!; // value Type is never null
                        else
                            throw new NotSupportedException($"Type {type} must contain parameterless constructor.");

                        deserelizedObjects[i] = currentObject;
                    }

                } else {
                    throw new NotSupportedException();
                }
            }

            // Fill Objects
            // do it twice so we also get the later populated structs
            for (int i = json.Count - 1; i >= 0; i--)
                PopulateProperties(json, deserelizedObjects, i, true);
            for (int i = 0; i < json.Count; i++) {
                PopulateProperties(json, deserelizedObjects, i, true);
            }
            for (int i = json.Count - 1; i >= 0; i--)
                PopulateProperties(json, deserelizedObjects, i, false);



            return (T)deserelizedObjects[0];

            static void PopulateProperties(JArray json, object[] deserelizedObjects, int i, bool onlyStructs)
            {
                var entry = json[i];

                var typeName = entry["type"]?.ToObject<string>();
                if (typeName is null)
                    throw new ArgumentException($"Object at index {i} does not have a type!");


                var type = Type.GetType(typeName)!; // Type wasn't null the first time, so this time it will also be not null.
                if (onlyStructs && !type.IsValueType)
                    return;
                var currentObject = deserelizedObjects[i];

                object? GetValue(ValueWrapper valueWrapper, Type targetType)
                {
                    object value;
                    if (valueWrapper.Kind == ValueKind.@ref) {
                        if ((long)valueWrapper.Value == -1)
                            return null;
                        value = deserelizedObjects[(int)(long)valueWrapper.Value];
                    } else if (valueWrapper.Kind == ValueKind.scalar) {
                        if (targetType == typeof(DateTime)) {
                            var txt = (string)valueWrapper.Value;
                            var splited = txt.Split('|');
                            var ticks = long.Parse(splited[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture);
                            var kind = (DateTimeKind)Enum.Parse(typeof(DateTimeKind), splited[1]);

                            value = new DateTime(ticks, kind);
                        } else if (targetType == typeof(DateTimeOffset)) {
                            var txt = (string)valueWrapper.Value;
                            var splited = txt.Split('|');
                            var ticks = long.Parse(splited[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture);
                            var offset = long.Parse(splited[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture);

                            value = new DateTimeOffset(ticks, new TimeSpan(offset));
                        } else if (targetType == typeof(TimeSpan)) {
                            value = TimeSpan.FromTicks((long)valueWrapper.Value);
                        } else if (targetType == typeof(Type)) {
                            value = Type.GetType((string)valueWrapper.Value) ?? throw new ReflectionTypeLoadException(null, null, $"Failed to load {valueWrapper.Value}");
                        } else if (targetType == typeof(byte[])) {
                            var data = ((string)valueWrapper.Value);
                            value = Convert.FromBase64String(data);
                        } else if (targetType.IsEnum) {
                            var l = (long)valueWrapper.Value;
                            value = Enum.ToObject(targetType, l);
                            //value = Convert.ChangeType(l, targetType);
                        } else
                            value = valueWrapper.Value;
                    } else
                        throw new NotSupportedException($"Type {targetType} is not supported");

                    if (!targetType.IsAssignableFrom(value.GetType())) {
                        var converter = System.ComponentModel.TypeDescriptor.GetConverter(targetType);
                        if (targetType == typeof(byte)) {
                            value = (byte)Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture);
                            //value = (byte)value;
                        } else if (targetType == typeof(short)) {
                            value = (short)value;
                        } else {
                            try {
                                value = converter.ConvertFrom(value);

                            } catch (NotSupportedException) {
                                value = Convert.ChangeType(value, targetType);
                            }
                        }

                    }

                    return value;
                }

                if (entry["propertys"] is JObject jsonPropertys) {

                    foreach (var pair in jsonPropertys) {
                        var property = type.GetProperty(pair.Key);
                        if (property is null)
                            throw new ArgumentException($"Type {type} does not contains property {pair.Key}");

                        var valueWrapper = pair.Value?.ToObject<ValueWrapper>();
                        if (valueWrapper is null)
                            throw new ArgumentException($"Type {type} does not contains correct wrapper for property {pair.Key}");

                        var setMethod = property.GetSetMethod();
                        var getMethod = property.GetGetMethod();

                        var value = GetValue(valueWrapper, property.PropertyType);

                        if (getMethod is null)
                            throw new ArgumentException($"Type {type} does not have getter for property {pair.Key}");
                        if (setMethod is null) {
                            var backiongFiled = GetBackingField(property);
                            if (backiongFiled is null)
                                throw new ArgumentException($"Type {type} does not have setter for property {pair.Key}");

                            backiongFiled.SetValue(currentObject, value);
                        } else {
                            property.SetValue(currentObject, value);

                        }






                    }
                } else if (entry["elements"] is JArray jsonElements) {
                    var implementedInterfaces = new HashSet<Type>(type.GetInterfaces().Where(x => x.IsGenericType).Select(x => x.GetGenericTypeDefinition()).Concat(type.GetInterfaces().Where(x => !x.IsGenericType)));

                    if (currentObject.GetType().IsGenericType && currentObject.GetType().GetGenericTypeDefinition() == typeof(ImmutableList<>)) {
                        // continue;
                        // it is currently not supported to fill an imutable list twice...
                    } else if (currentObject.GetType().Name.Contains("Builder")) {

                        var listType = currentObject.GetType();
                        var addMethod = listType.GetMethod("Add");


                        for (int j = 0; j < jsonElements.Count; j++) {
                            var valueWrapper = jsonElements[j].ToObject<ValueWrapper>();
                            if (valueWrapper is null)
                                throw new ArgumentException($"Type {type} does not contains correct wrapper for element {j}");
                            var value = GetValue(valueWrapper, listType.GetGenericArguments().First()); // Elementtype wasn't null the first time.
                            addMethod.Invoke(currentObject, new[] { value });
                        }

                        var toImmutableMethod = listType.GetMethod(nameof(ImmutableList<object>.Builder.ToImmutable));
                        var immutableList = toImmutableMethod.Invoke(currentObject, null);
                        deserelizedObjects[i] = immutableList;


                    } else if (currentObject is Array array) {
                        for (int j = 0; j < array.Length; j++) {
                            var valueWrapper = jsonElements[j].ToObject<ValueWrapper>();
                            if (valueWrapper is null)
                                throw new ArgumentException($"Type {type} does not contains correct wrapper for element {j}");
                            var value = GetValue(valueWrapper, type.GetElementType()!); // Elementtype wasn't null the first time.

                            array.SetValue(value, j);
                        }
                    } else if (implementedInterfaces.Contains(typeof(ICollection<>))) {
                        var collectionInterface = currentObject.GetType().GetInterfaces().Where(x => x.IsGenericType)
                                                    .Where(x => x.GetGenericTypeDefinition() == typeof(ICollection<>)).Single();


                        var addMethod = collectionInterface.GetMethod(nameof(ICollection<object>.Add));
                        var elementType = collectionInterface.GetGenericArguments().Single();

                        for (int j = 0; j < jsonElements.Count; j++) {
                            var valueWrapper = jsonElements[j].ToObject<ValueWrapper>();
                            if (valueWrapper is null)
                                throw new ArgumentException($"Type {type} does not contains correct wrapper for element {j}");
                            var value = GetValue(valueWrapper, elementType); // Elementtype wasn't null the first time.

                            addMethod.Invoke(currentObject, new[] { value });
                            //array.SetValue(value, j);
                        }


                        //var add = typeof(System.Collections.ICollection).GetMethod(nameof(System.Collections.ICollection.Add));

                        //var arrayElements = new JArray();
                        //currentJObject.Add("elements", arrayElements);

                        //var array = (System.Collections.ICollection)current;
                        //foreach (var item in array)
                        //    arrayElements.Add(GetValueObject(item));
                    } else throw new NotSupportedException($"{currentObject} is not an array nor an immutableList builder.");
                } else if (entry["map"] is JArray jsonMap) {

                    var interfaceType = type.GetInterfaces().Where(x => x.IsGenericType).FirstOrDefault(x => x.GetGenericTypeDefinition() == typeof(IDictionary<,>));
                    if (interfaceType is null)
                        throw new ArgumentException($"Type {type} does not implement IDctionary<,>");

                    var mapping = type.GetInterfaceMap(interfaceType);

                    var setMethod = mapping.TargetMethods.Where(x => x.GetParameters().Length == 2).First(x => x.Name == "set_Item");



                    var genericArguments = interfaceType.GetGenericArguments();
                    var keyType = genericArguments[0];
                    var valueType = genericArguments[1];

                    for (int j = 0; j < jsonMap.Count; j++) {
                        var mapEntry = (JObject)jsonMap[j];


                        var keyWrapper = mapEntry["key"]?.ToObject<ValueWrapper>();
                        var valueWrapper = mapEntry["value"]?.ToObject<ValueWrapper>();

                        if (keyWrapper is null)
                            throw new ArgumentException($"Map entry did not contain key value.");
                        if (valueWrapper is null)
                            throw new ArgumentException($"Map entry did not contain 'value' value.");

                        var key = GetValue(keyWrapper, keyType);
                        var value = GetValue(valueWrapper, valueType);

                        setMethod.Invoke(currentObject, new object?[] { key, value });

                    }
                } else if (entry["tuple"] is JArray jsonTuple) {
                    for (int j = 0; j < jsonTuple.Count; j++) {

                        var valueWrapper = jsonTuple[j].ToObject<ValueWrapper>();
                        if (valueWrapper is null)
                            throw new ArgumentException($"Type {type} does not contains correct wrapper for element {j}");


                        if (type.IsValueType) {
                            var filed = type.GetField("Item" + (j + 1));
                            if (filed is null)
                                throw new ArgumentException("The tuple size is incorrect");
                            var value = GetValue(valueWrapper, filed.FieldType);
                            filed.SetValue(currentObject, value);
                        } else {
                            var filed = type.GetProperty("Item" + (j + 1));
                            if (filed is null)
                                throw new ArgumentException("The tuple size is incorrect");
                            var value = GetValue(valueWrapper, filed.PropertyType);
                            filed.SetValue(currentObject, value);
                        }
                    }
                } else {
                    throw new NotSupportedException();
                }
            }
        }


        public static FieldInfo? GetBackingField(PropertyInfo propertyInfo)
        {
            const String Prefix = "<";
            const String Suffix = ">k__BackingField";

            static String GetBackingFieldName(String propertyName) => $"{Prefix}{propertyName}{Suffix}";


            if (propertyInfo == null)
                throw new ArgumentNullException(nameof(propertyInfo));
            if (!propertyInfo.CanRead
                || (!propertyInfo.GetGetMethod(nonPublic: true)?.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: true) ?? false))
                return null;
            var backingFieldName = GetBackingFieldName(propertyInfo.Name);
            var backingField = propertyInfo.DeclaringType?.GetField(backingFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (backingField == null)
                return null;
            if (!backingField.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: true))
                return null;
            return backingField;
        }

        private enum ValueKind
        {
            @ref,
            scalar
        }

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
        private class ValueWrapper
        {
            public ValueKind Kind { get; set; }

            public object Value { get; set; }
        }
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

    }

}
