using System.Globalization;

namespace LUMAR_ERP_API_V2.Services;

public interface IHijriDateProvider
{
    string FormatForSanaa(DateOnly sanaaDate);
}

public sealed class HijriDateProvider : IHijriDateProvider
{
    private static readonly UmAlQuraCalendar Calendar = new();

    private static readonly string[] MonthNames =
    [
        "محرم", "صفر", "ربيع الأول", "ربيع الآخر", "جمادى الأولى", "جمادى الآخرة",
        "رجب", "شعبان", "رمضان", "شوال", "ذو القعدة", "ذو الحجة"
    ];

    public string FormatForSanaa(DateOnly sanaaDate)
    {
        var date = sanaaDate.ToDateTime(TimeOnly.MinValue);
        var year = Calendar.GetYear(date);
        var month = Calendar.GetMonth(date);
        var day = Calendar.GetDayOfMonth(date);
        var monthName = month >= 1 && month <= MonthNames.Length ? MonthNames[month - 1] : month.ToString(CultureInfo.InvariantCulture);
        return $"{day} {monthName} {year} هـ";
    }
}
