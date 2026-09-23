using LUMAR_ERP_API_V2.DTOs.Consumption;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("consumption-rules")]
public sealed class ConsumptionRulesController(IConsumptionRulesService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ConsumptionRulesDashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        return Ok(await service.GetDashboardAsync(cancellationToken));
    }

    [HttpGet("integrity")]
    public async Task<ActionResult<ConsumptionRulesIntegrityReportDto>> GetIntegrityReport(CancellationToken cancellationToken)
    {
        return Ok(await service.GetIntegrityReportAsync(cancellationToken));
    }

    [HttpPost("evaluate")]
    public async Task<ActionResult<EvaluateConsumptionResponseDto>> Evaluate([FromBody] EvaluateConsumptionRequestDto request, CancellationToken cancellationToken)
    {
        if (request is null || request.ProductTypeId <= 0)
        {
            return BadRequest(new { message = "نوع القطعة مطلوب." });
        }

        try
        {
            var result = await service.EvaluateAsync(request, cancellationToken);
            return result is null
                ? BadRequest(new { message = "لا توجد قاعدة استهلاك مطابقة للقياسات المدخلة." })
                : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("batch")]
    public async Task<ActionResult<IReadOnlyList<ConsumptionRuleDto>>> SaveBatch([FromBody] SaveProductRulesBatchDto request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "البيانات مطلوبة." });
        }

        if (request.ProductTypeId <= 0)
        {
            return BadRequest(new { message = "نوع القطعة مطلوب." });
        }

        if (request.Rules is null || request.Rules.Count == 0)
        {
            return BadRequest(new { message = "يجب إدخال قائمة قواعد القطعة." });
        }

        try
        {
            var saved = await service.SaveProductRulesBatchAsync(request, cancellationToken);
            return Ok(saved);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<ConsumptionRuleDto>> Create([FromBody] CreateConsumptionRuleDto request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "البيانات مطلوبة." });
        }

        if (request.ProductTypeId <= 0)
        {
            return BadRequest(new { message = "نوع القطعة مطلوب." });
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Formula) || string.IsNullOrWhiteSpace(request.ResultUnit))
        {
            return BadRequest(new { message = "يجب تعبئة الاسم والصيغة ووحدة القياس." });
        }

        if (request.FabricWidth is <= 0)
        {
            return BadRequest(new { message = "عرض القماش يجب أن يكون رقماً موجباً." });
        }

        try
        {
            var created = await service.CreateAsync(request, cancellationToken);
            return created is null ? NotFound(new { message = "نوع القطعة غير موجود أو فشل التحقق من سلامة القاعدة." }) : Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ConsumptionRuleDto>> Update(int id, [FromBody] UpdateConsumptionRuleDto request, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "معرف القاعدة غير صالح." });
        }

        if (request is null)
        {
            return BadRequest(new { message = "البيانات مطلوبة." });
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Formula) || string.IsNullOrWhiteSpace(request.ResultUnit))
        {
            return BadRequest(new { message = "يجب تعبئة الاسم والصيغة ووحدة القياس." });
        }

        if (request.FabricWidth is <= 0)
        {
            return BadRequest(new { message = "عرض القماش يجب أن يكون رقماً موجباً." });
        }

        try
        {
            var updated = await service.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound(new { message = "القاعدة غير موجودة أو فشل التحقق من سلامة القاعدة." }) : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("measurement-types")]
    public async Task<ActionResult<MeasurementTypeWriteResultDto>> CreateMeasurementType([FromBody] CreateMeasurementTypeDto request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("البيانات مطلوبة.");
        }

        if (string.IsNullOrWhiteSpace(request.NameAr))
        {
            return BadRequest("اسم القطعة مطلوب.");
        }

        if (request.FieldCount <= 0 && (request.FieldNames is null || request.FieldNames.Count == 0))
        {
            return BadRequest("يجب تحديد عدد الحقول أو أسماء الحقول.");
        }

        var created = await service.CreateMeasurementTypeAsync(request, cancellationToken);
        return created is null ? BadRequest("تعذر إنشاء القطعة والقياسات في الجداول الرسمية.") : Ok(created);
    }

    [HttpPut("measurement-types/{productTypeId:int}")]
    public async Task<ActionResult<MeasurementTypeWriteResultDto>> UpdateMeasurementType(int productTypeId, [FromBody] UpdateMeasurementTypeDto request, CancellationToken cancellationToken)
    {
        if (productTypeId <= 0)
        {
            return BadRequest("معرف القطعة غير صالح.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.NameAr))
        {
            return BadRequest("اسم القطعة مطلوب.");
        }

        if (request.FieldNames is null || request.FieldNames.Count == 0)
        {
            return BadRequest("يجب إدخال أسماء الحقول على الأقل.");
        }

        var updated = await service.UpdateMeasurementTypeAsync(productTypeId, request, cancellationToken);
        return updated is null ? NotFound("القطعة غير موجودة أو لا يوجد ملف قياسات مرتبط بها.") : Ok(updated);
    }

    [HttpDelete("measurement-types/{productTypeId:int}")]
    public async Task<IActionResult> DeleteMeasurementType(int productTypeId, CancellationToken cancellationToken)
    {
        if (productTypeId <= 0)
        {
            return BadRequest("معرف القطعة غير صالح.");
        }

        var deleted = await service.DeleteMeasurementTypeAsync(productTypeId, cancellationToken);
        return deleted ? Ok(new { success = true }) : Conflict(new { success = false, message = "لا يمكن حذف القطعة لأن هناك قواعد مرتبطة بها أو لم توجد." });
    }
}
