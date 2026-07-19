using System;
using System.Collections.Generic;
using System.Linq;

namespace Lux.Management.Console.Core.Security.Models;

/// <summary>
/// كائن نطاق يغلف مجموعة ميزات الترخيص ويوفر طرق معالجة سريعة وآمنة.
/// </summary>
public class FeatureSet
{
    private readonly HashSet<FeatureId> _features;

    public FeatureSet(IEnumerable<FeatureId> features)
    {
        _features = new HashSet<FeatureId>(features);
    }

    /// <summary>التحقق من توفر ميزة معينة</summary>
    public bool Has(FeatureId featureId)
    {
        return _features.Contains(featureId);
    }

    /// <summary>فرض توفر ميزة معينة ورمي استثناء في حال غيابها</summary>
    public void Require(FeatureId featureId)
    {
        if (!Has(featureId))
        {
            throw new UnauthorizedAccessException($"❌ الميزة المطلوبة '{featureId}' غير مفعلة في ترخيصك الحالي.");
        }
    }

    /// <summary>سرد كافة الميزات المتاحة حالياً</summary>
    public IEnumerable<FeatureId> Enumerate()
    {
        return _features;
    }

    /// <summary>تحويل مجموعة الميزات إلى سلسلة نصية للتخزين</summary>
    public string Serialize()
    {
        return string.Join(",", _features.Select(f => f.ToString()));
    }

    /// <summary>تحليل وسحب مجموعة الميزات من سلسلة نصية مخزنة</summary>
    public static FeatureSet Deserialize(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return new FeatureSet(Enumerable.Empty<FeatureId>());
        }

        var list = new List<FeatureId>();
        var items = data.Split(',');
        foreach (var item in items)
        {
            if (Enum.TryParse<FeatureId>(item.Trim(), true, out var feature))
            {
                list.Add(feature);
            }
        }
        return new FeatureSet(list);
    }

    /// <summary>إنشاء مجموعة ميزات النسخة الاحترافية الكاملة</summary>
    public static FeatureSet CreateProSet()
    {
        return new FeatureSet(Enum.GetValues<FeatureId>());
    }

    /// <summary>إنشاء مجموعة ميزات النسخة المجانية المحدودة</summary>
    public static FeatureSet CreateFreeSet()
    {
        return new FeatureSet(new[]
        {
            FeatureId.VoucherGeneration,
            FeatureId.VoucherPrinting
        });
    }
}
