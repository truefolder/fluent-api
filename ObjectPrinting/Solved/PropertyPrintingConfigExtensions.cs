using System;

namespace ObjectPrinting.Solved;

public static class PropertyPrintingConfigExtensions
{
    public static string? PrintToString<T>(this T obj, Func<PrintingConfig<T>, PrintingConfig<T>> config)
    {
        return config(ObjectPrinter.For<T>()).PrintToString(obj);
    }

    public static PrintingConfig<TOwner> TrimmedToLength<TOwner>(this PropertyPrintingConfig<TOwner, string> propConfig, int maxLen)
    {
        IPropertyPrintingConfig<TOwner, string> config = propConfig;
        var parent = config.ParentConfig;
        var memberInfo = config.MemberInfo;

        parent.AddStringTrimming(memberInfo, maxLen);
        return parent;
    }
}