using System;
using System.Globalization;
using System.Reflection;

namespace ObjectPrinting.Solved;

public class PropertyPrintingConfig<TOwner, TPropType>(PrintingConfig<TOwner> printingConfig, MemberInfo? memberInfo)
    : IPropertyPrintingConfig<TOwner, TPropType>
{
    public PrintingConfig<TOwner> Using(Func<TPropType, string> print)
    {
        if (memberInfo == null)
            printingConfig.AddTypeSerializer(print);
        else
            printingConfig.AddMemberSerializer(memberInfo, print);
        return printingConfig;
    }

    public PrintingConfig<TOwner> Using(CultureInfo culture)
    {
        if (memberInfo == null)
            printingConfig.AddTypeCulture<TPropType>(culture);
        else
            printingConfig.AddMemberCulture(memberInfo, culture);
        return printingConfig;
    }

    PrintingConfig<TOwner> IPropertyPrintingConfig<TOwner, TPropType>.ParentConfig => printingConfig;
    MemberInfo? IPropertyPrintingConfig<TOwner, TPropType>.MemberInfo => memberInfo;
}