using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Xml;

namespace AdoAsync.Common;

public static class XmlExtensions
{
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PublicInstancePropertiesCache = new();

    public static string ToXml<T>(
        this IEnumerable<T> source,
        string rootElementName,
        string itemElementName)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(rootElementName)) throw new ArgumentException("Root element name is required.", nameof(rootElementName));
        if (string.IsNullOrWhiteSpace(itemElementName)) throw new ArgumentException("Item element name is required.", nameof(itemElementName));

        using var sw = new StringWriter(InvariantCulture);
        using var writer = XmlWriter.Create(sw, new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true
        });

        writer.WriteStartElement(rootElementName);

        var props = GetPublicInstanceProperties(typeof(T));
        foreach (var item in source)
        {
            writer.WriteStartElement(itemElementName);

            if (item is not null)
            {
                foreach (var prop in props)
                {
                    var value = prop.GetValue(item);
                    var text = FormatValue(value);
                    if (text is not null)
                    {
                        writer.WriteElementString(prop.Name, text);
                    }
                }
            }

            writer.WriteEndElement(); // itemElementName
        }

        writer.WriteEndElement(); // rootElementName
        writer.Flush();

        return sw.ToString();
    }

    private static PropertyInfo[] GetPublicInstanceProperties(Type type) =>
        PublicInstancePropertiesCache.GetOrAdd(
            type,
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

    private static string? FormatValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is DBNull)
        {
            return null;
        }

        return value switch
        {
            DateTime dt => dt.ToString("O", InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", InvariantCulture),
            TimeSpan ts => ts.ToString("c", InvariantCulture),
            Guid g => g.ToString("D"),
            byte[] bytes => Convert.ToBase64String(bytes),
            IFormattable f => f.ToString(null, InvariantCulture),
            _ => value.ToString()
        };
    }
}

