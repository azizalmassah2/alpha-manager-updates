namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// طريقة بيانات اعتماد الكرت — تتحكم في كيفية توليد وطباعة وإرسال البيانات للمايكروتك
/// </summary>
public enum CredentialMode
{
    /// <summary>
    /// اسم المستخدم فقط — بدون كلمة سر.
    /// يُرسل للمايكروتك بكلمة سر فارغة أو مطابقة للـ username (حسب الإعداد).
    /// مثال الطباعة: يظهر Username فقط.
    /// </summary>
    UsernameOnly = 0,

    /// <summary>
    /// اسم المستخدم وكلمة السر متطابقان.
    /// يُرسل للمايكروتك نفس القيمة للاثنين.
    /// مثال الطباعة: يظهر Username وتوضيح "الرمز = الاسم".
    /// </summary>
    UsernameEqualsPassword = 1,

    /// <summary>
    /// اسم المستخدم وكلمة سر مختلفة (الوضع الكامل).
    /// يُرسل للمايكروتك قيمتان مختلفتان.
    /// مثال الطباعة: يظهر الاثنان بوضوح.
    /// </summary>
    UsernameAndPassword = 2
}
