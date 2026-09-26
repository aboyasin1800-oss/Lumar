using System.Net.Sockets;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.ErrorHandling;

public static class ApiErrorResponseFactory
{
    public static ApiErrorResponse Create(string requestPath, string correlationId, Exception exception)
    {
        var (errorCode, message) = exception switch
        {
            SqlException => ("DB-001", "تعذر الوصول إلى البيانات المطلوبة. حاول مرة أخرى."),
            TimeoutException or SocketException => ("NET-001", "تعذر الاتصال بالخادم. تحقق من الاتصال ثم أعد المحاولة."),
            _ => CreateOperationError(requestPath)
        };

        return new ApiErrorResponse(false, errorCode, message, correlationId);
    }

    public static ApiErrorResponse CreateForStatus(string requestPath, string correlationId)
    {
        var (errorCode, message) = CreateOperationError(requestPath);
        return new ApiErrorResponse(false, errorCode, message, correlationId);
    }

    private static (string ErrorCode, string Message) CreateOperationError(string requestPath)
    {
        if (requestPath.Contains("/delivery/confirm", StringComparison.OrdinalIgnoreCase))
            return ("DLV-001", "تعذر إكمال تسليم الطلب. لم يتم تنفيذ تحصيل جديد.");
        if (requestPath.Contains("/delivery/revenue-recognize", StringComparison.OrdinalIgnoreCase))
            return ("REV-001", "تعذر إثبات إيراد الطلب.");
        if (requestPath.Contains("/collect", StringComparison.OrdinalIgnoreCase) ||
            requestPath.Contains("/settle", StringComparison.OrdinalIgnoreCase))
            return ("PAY-001", "تعذر تسجيل التحصيل. راجع حالة الطلب ثم أعد المحاولة.");

        return ("GEN-001", "حدث خطأ غير متوقع. يرجى إعادة المحاولة.");
    }
}