namespace MikroTikVoucherPrinter.Infrastructure.Data;

/// <summary>معرّفات ثابتة للقوالب النظامية (بذور قاعدة البيانات).</summary>
public static class BuiltInTemplateIds
{
    public static readonly Guid A4HawaeIsp = Guid.Parse("b1000001-0000-4000-8000-000000000001");
    public static readonly Guid A4SimpleGrid = Guid.Parse("b1000001-0000-4000-8000-000000000002");
    public static readonly Guid Thermal58 = Guid.Parse("b1000001-0000-4000-8000-000000000003");
    public static readonly Guid Thermal80 = Guid.Parse("b1000001-0000-4000-8000-000000000004");
    public static readonly Guid TxtTemplate = Guid.Parse("b1000001-0000-4000-8000-000000000005");
}
