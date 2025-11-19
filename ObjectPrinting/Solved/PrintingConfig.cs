using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace ObjectPrinting.Solved;

public class PrintingConfig<TOwner>
{
    private readonly HashSet<Type> excludedTypes = [];
    private readonly HashSet<MemberInfo> excludedMembers = [];
    private readonly Dictionary<Type, Func<object, string>> typeSerializers = new();
    private readonly Dictionary<MemberInfo, Func<object, string?>> memberSerializers = new();
    private readonly Dictionary<Type, CultureInfo> typeCultures = new();
    private readonly Dictionary<MemberInfo, CultureInfo> memberCultures = new();

    private readonly HashSet<object?> parsedObjects = [];
        
    private readonly Type[] primitiveTypes =
    [
        typeof(int), typeof(double), typeof(float), typeof(string),
        typeof(DateTime), typeof(TimeSpan), typeof(decimal), typeof(Guid)
    ];
        
    public PropertyPrintingConfig<TOwner, TPropType> Printing<TPropType>()
    {
        return new PropertyPrintingConfig<TOwner, TPropType>(this, null);
    }

    public PropertyPrintingConfig<TOwner, TPropType> Printing<TPropType>(Expression<Func<TOwner, TPropType>> memberSelector)
    {
        return new PropertyPrintingConfig<TOwner, TPropType>(this, GetMemberInfo(memberSelector));
    }

    public PrintingConfig<TOwner> Excluding<TPropType>(Expression<Func<TOwner, TPropType>> memberSelector)
    {
        var member = GetMemberInfo(memberSelector);
        excludedMembers.Add(member);
        return this;
    }

    public PrintingConfig<TOwner> Excluding<TPropType>()
    {
        excludedTypes.Add(typeof(TPropType));
        return this;
    }

    public string? PrintToString(TOwner obj)
    {
        return PrintToString(obj, 0, null);
    }

    private string? PrintToString(object? obj, int nestingLevel, MemberInfo? member)
    {
        if (TryPrintNullOrExcluded(obj, nestingLevel, out var nullOrExcludedResult))
            return nullOrExcludedResult;
            
        var type = obj!.GetType();
            
        if (TryPrintSimpleType(obj, type, member, out var simpleResult))
            return simpleResult;
            
        if (!type.IsValueType && obj is not string)
        {
            if (!parsedObjects.Add(obj))
                return $"Cyclic reference at {type.Name}" + Environment.NewLine;
        }
            
        var indentation = new string('\t', nestingLevel + 1);

        if (TryProcessDictionary(obj, type, indentation, nestingLevel, out var dictResult))
            return dictResult;

        if (TryProcessEnumerable(obj, type, indentation, nestingLevel, out var collectionResult))
            return collectionResult;

        return PrintComplexType(obj, type, indentation, nestingLevel);
    }
        
    private string PrintComplexType(object obj, Type type, string indentation, int nestingLevel)
    {
        var sb = new StringBuilder();
        sb.AppendLine(type.Name);

        var printableMembers = GetPrintableMembers(type);
        foreach (var memberInfo in printableMembers)
        {
            var value = GetMemberValue(memberInfo, obj);
            sb.Append($"{indentation}{memberInfo.Name} = {PrintToString(value, nestingLevel + 1, memberInfo)}");
        }

        return sb.ToString();
    }

    private bool TryPrintNullOrExcluded(object? obj, int nestingLevel, out string? result)
    {
        if (obj is null)
        {
            result = "null" + Environment.NewLine;
            return true;
        }

        var type = obj.GetType();

        if (excludedTypes.Contains(type) && nestingLevel > 0)
        {
            result = string.Empty;
            return true;
        }

        result = null;
        return false;
    }

    private bool TryProcessDictionary(object? obj, Type type, string indentation,
        int nestingLevel, out string? result)
    {
        var sb = new StringBuilder();
        if (obj is IDictionary dict)
        {
            sb.AppendLine(type.Name);
            foreach (DictionaryEntry entry in dict)
            {
                sb.Append($"{indentation}Key = {PrintToString(entry.Key, nestingLevel + 1, null)}");
                sb.Append($"{indentation}Value = {PrintToString(entry.Value, nestingLevel + 1, null)}");
            }
        
            result = sb.ToString();
            return true;
        }

        result = null;
        return false;
    }

    private bool TryProcessEnumerable(object? obj, Type type, string indentation,
        int nestingLevel, out string? result)
    {
        var sb = new StringBuilder();
        if (obj is IEnumerable enumerable and not string)
        {
            sb.AppendLine(type.Name);
            var index = 0;
            foreach (var item in enumerable)
            {
                sb.Append($"{indentation}[{index}] = {PrintToString(item, nestingLevel + 1, null)}");
                index++;
            }
                
            result = sb.ToString();
            return true;
        }
        result = null;
        return false;
    }
        
    private bool TryPrintSimpleType(object obj, Type type, MemberInfo? memberInfo, out string? result)
    {
        if (memberInfo != null && memberSerializers.TryGetValue(memberInfo, out var memberSerializer))
        {
            result = memberSerializer(obj) + Environment.NewLine;
            return true;
        }
            
        if (typeSerializers.TryGetValue(type, out var typeSerializer))
        {
            result = typeSerializer(obj) + Environment.NewLine;
            return true;
        }
            
        if (memberInfo != null && memberCultures.TryGetValue(memberInfo, out var memberCulture) &&
            obj is IFormattable formattable1)
        {
            result = formattable1.ToString(null, memberCulture) + Environment.NewLine;
            return true;
        }
            
        if (typeCultures.TryGetValue(type, out var typeCulture) && obj is IFormattable formattable2)
        {
            result = formattable2.ToString(null, typeCulture) + Environment.NewLine;
            return true;
        }

        if (primitiveTypes.Contains(type) || type.IsEnum)
        {
            result = obj + Environment.NewLine;
            return true;
        }

        result = null;
        return false;
    }
        
    private MemberInfo GetMemberInfo<TPropType>(Expression<Func<TOwner, TPropType>> memberSelector)
    {
        if (memberSelector.Body is MemberExpression memberExpression)
            return memberExpression.Member;
        throw new ArgumentException();
    }
        
    private List<MemberInfo> GetPrintableMembers(Type type)
    {
        var result = new List<MemberInfo>();
        var flags = BindingFlags.Instance | BindingFlags.Public;
        foreach (var propertyInfo in type.GetProperties(flags))
        {
            if (!propertyInfo.CanRead)
                continue;

            if (IsMemberExcluded(propertyInfo, propertyInfo.PropertyType))
                continue;

            result.Add(propertyInfo);
        }

        foreach (var fieldInfo in type.GetFields(flags))
        {
            if (IsMemberExcluded(fieldInfo, fieldInfo.FieldType))
                continue;

            result.Add(fieldInfo);
        }
        return result;
    }

    private bool IsMemberExcluded(MemberInfo member, Type memberType)
    {
        return excludedMembers.Contains(member) || excludedTypes.Contains(memberType);
    }

    private object? GetMemberValue(MemberInfo member, object obj)
    {
        return member switch
        {
            PropertyInfo p => p.GetValue(obj),
            FieldInfo f => f.GetValue(obj),
            _ => null
        };
    }
        
    internal void AddTypeSerializer<TPropType>(Func<TPropType, string> serialize)
    {
        typeSerializers[typeof(TPropType)] = o => serialize((TPropType)o);
    }

    internal void AddMemberSerializer<TPropType>(MemberInfo? member, Func<TPropType, string?> serialize)
    {
        ArgumentNullException.ThrowIfNull(member);
        memberSerializers[member] = o => serialize((TPropType)o);
    }

    internal void AddTypeCulture<TPropType>(CultureInfo culture)
    {
        typeCultures[typeof(TPropType)] = culture;
    }

    internal void AddMemberCulture(MemberInfo member, CultureInfo culture)
    {
        if (member == null)
            throw new ArgumentNullException(nameof(member));

        memberCultures[member] = culture;
    }

    internal void AddStringTrimming(MemberInfo? member, int maxLen)
    {
        AddMemberSerializer<string>(member, s => s.Length <= maxLen ? s : s.Substring(0, maxLen));
    }
}