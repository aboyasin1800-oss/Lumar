using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("api/loyalty-management")]
public sealed class LoyaltyManagementSettingsController(ILoyaltyManagementSettingsService service) : ControllerBase
{
    [HttpGet("program-settings")]
    public async Task<ActionResult<IReadOnlyList<LoyaltyProgramSettingsDto>>> GetProgramSettings(CancellationToken cancellationToken)
        => Ok(await service.GetProgramSettingsAsync(cancellationToken));

    [HttpGet("program-settings/{id:int}")]
    public async Task<ActionResult<LoyaltyProgramSettingsDto>> GetProgramSettingById(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest("Program setting id must be positive.");
        var setting = await service.GetProgramSettingByIdAsync(id, cancellationToken);
        return setting is null ? NotFound() : Ok(setting);
    }

    [HttpPost("program-settings")]
    public async Task<ActionResult<LoyaltyProgramSettingsDto>> CreateProgramSetting([FromBody] CreateLoyaltyProgramSettingsDto request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await service.CreateProgramSettingsAsync(request, cancellationToken);
            return created is null ? StatusCode(500, "Unable to create program settings.") : CreatedAtAction(nameof(GetProgramSettingById), new { id = created.LoyaltyProgramSettingId }, created);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("program-settings/{id:int}")]
    public async Task<ActionResult<LoyaltyProgramSettingsDto>> UpdateProgramSetting(int id, [FromBody] UpdateLoyaltyProgramSettingsDto request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await service.UpdateProgramSettingsAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("program-settings/{id:int}/activate")]
    public async Task<ActionResult<LoyaltyProgramSettingsDto>> ActivateProgramSetting(int id, CancellationToken cancellationToken)
    {
        var setting = await service.ActivateProgramSettingsAsync(id, cancellationToken);
        return setting is null ? NotFound() : Ok(setting);
    }

    [HttpPut("program-settings/{id:int}/deactivate")]
    public async Task<ActionResult<LoyaltyProgramSettingsDto>> DeactivateProgramSetting(int id, CancellationToken cancellationToken)
    {
        var setting = await service.DeactivateProgramSettingsAsync(id, cancellationToken);
        return setting is null ? NotFound() : Ok(setting);
    }

    [HttpGet("piece-point-settings")]
    public async Task<ActionResult<IReadOnlyList<LoyaltyPiecePointSettingDto>>> GetPiecePointSettings(CancellationToken cancellationToken)
        => Ok(await service.GetPiecePointSettingsAsync(cancellationToken));

    [HttpGet("piece-point-settings/{id:int}")]
    public async Task<ActionResult<LoyaltyPiecePointSettingDto>> GetPiecePointSettingById(int id, CancellationToken cancellationToken)
    {
        var setting = await service.GetPiecePointSettingByIdAsync(id, cancellationToken);
        return setting is null ? NotFound() : Ok(setting);
    }

    [HttpPost("piece-point-settings")]
    public async Task<ActionResult<LoyaltyPiecePointSettingDto>> CreatePiecePointSetting([FromBody] CreateLoyaltyPiecePointSettingDto request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await service.CreatePiecePointSettingAsync(request, cancellationToken);
            return created is null ? StatusCode(500, "Unable to create piece point setting.") : CreatedAtAction(nameof(GetPiecePointSettingById), new { id = created.LoyaltyPiecePointSettingId }, created);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("piece-point-settings/{id:int}")]
    public async Task<ActionResult<LoyaltyPiecePointSettingDto>> UpdatePiecePointSetting(int id, [FromBody] UpdateLoyaltyPiecePointSettingDto request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await service.UpdatePiecePointSettingAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("piece-point-settings/{id:int}/activate")]
    public async Task<ActionResult<LoyaltyPiecePointSettingDto>> ActivatePiecePointSetting(int id, CancellationToken cancellationToken)
    {
        var setting = await service.ActivatePiecePointSettingAsync(id, cancellationToken);
        return setting is null ? NotFound() : Ok(setting);
    }

    [HttpPut("piece-point-settings/{id:int}/deactivate")]
    public async Task<ActionResult<LoyaltyPiecePointSettingDto>> DeactivatePiecePointSetting(int id, CancellationToken cancellationToken)
    {
        var setting = await service.DeactivatePiecePointSettingAsync(id, cancellationToken);
        return setting is null ? NotFound() : Ok(setting);
    }

    [HttpGet("vip-levels")]
    public async Task<ActionResult<IReadOnlyList<VipLevelDto>>> GetVipLevels(CancellationToken cancellationToken)
        => Ok(await service.GetVipLevelsAsync(cancellationToken));

    [HttpGet("vip-levels/{id:int}")]
    public async Task<ActionResult<VipLevelDto>> GetVipLevelById(int id, CancellationToken cancellationToken)
    {
        var level = await service.GetVipLevelByIdAsync(id, cancellationToken);
        return level is null ? NotFound() : Ok(level);
    }

    [HttpPost("vip-levels")]
    public async Task<ActionResult<VipLevelDto>> CreateVipLevel([FromBody] CreateVipLevelDto request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await service.CreateVipLevelAsync(request, cancellationToken);
            return created is null ? StatusCode(500, "Unable to create vip level.") : CreatedAtAction(nameof(GetVipLevelById), new { id = created.VipLevelId }, created);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("vip-levels/{id:int}")]
    public async Task<ActionResult<VipLevelDto>> UpdateVipLevel(int id, [FromBody] UpdateVipLevelDto request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await service.UpdateVipLevelAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("vip-levels/{id:int}/activate")]
    public async Task<ActionResult<VipLevelDto>> ActivateVipLevel(int id, CancellationToken cancellationToken)
    {
        var level = await service.ActivateVipLevelAsync(id, cancellationToken);
        return level is null ? NotFound() : Ok(level);
    }

    [HttpPut("vip-levels/{id:int}/deactivate")]
    public async Task<ActionResult<VipLevelDto>> DeactivateVipLevel(int id, CancellationToken cancellationToken)
    {
        var level = await service.DeactivateVipLevelAsync(id, cancellationToken);
        return level is null ? NotFound() : Ok(level);
    }

    [HttpGet("loyalty-rules")]
    public async Task<ActionResult<IReadOnlyList<LoyaltyRuleDto>>> GetLoyaltyRules(CancellationToken cancellationToken)
        => Ok(await service.GetLoyaltyRulesAsync(cancellationToken));

    [HttpGet("loyalty-rules/{id:int}")]
    public async Task<ActionResult<LoyaltyRuleDto>> GetLoyaltyRuleById(int id, CancellationToken cancellationToken)
    {
        var rule = await service.GetLoyaltyRuleByIdAsync(id, cancellationToken);
        return rule is null ? NotFound() : Ok(rule);
    }

    [HttpPost("loyalty-rules")]
    public async Task<ActionResult<LoyaltyRuleDto>> CreateLoyaltyRule([FromBody] CreateLoyaltyRuleDto request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await service.CreateLoyaltyRuleAsync(request, cancellationToken);
            return created is null ? StatusCode(500, "Unable to create loyalty rule.") : CreatedAtAction(nameof(GetLoyaltyRuleById), new { id = created.LoyaltyRuleId }, created);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("loyalty-rules/{id:int}")]
    public async Task<ActionResult<LoyaltyRuleDto>> UpdateLoyaltyRule(int id, [FromBody] UpdateLoyaltyRuleDto request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await service.UpdateLoyaltyRuleAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("loyalty-rules/{id:int}/activate")]
    public async Task<ActionResult<LoyaltyRuleDto>> ActivateLoyaltyRule(int id, CancellationToken cancellationToken)
    {
        var rule = await service.ActivateLoyaltyRuleAsync(id, cancellationToken);
        return rule is null ? NotFound() : Ok(rule);
    }

    [HttpPut("loyalty-rules/{id:int}/deactivate")]
    public async Task<ActionResult<LoyaltyRuleDto>> DeactivateLoyaltyRule(int id, CancellationToken cancellationToken)
    {
        var rule = await service.DeactivateLoyaltyRuleAsync(id, cancellationToken);
        return rule is null ? NotFound() : Ok(rule);
    }

    [HttpGet("referral-rewards")]
    public async Task<ActionResult<IReadOnlyList<ReferralRewardDto>>> GetReferralRewards(CancellationToken cancellationToken)
        => Ok(await service.GetReferralRewardsAsync(cancellationToken));

    [HttpGet("referral-rewards/{id:int}")]
    public async Task<ActionResult<ReferralRewardDto>> GetReferralRewardById(int id, CancellationToken cancellationToken)
    {
        var reward = await service.GetReferralRewardByIdAsync(id, cancellationToken);
        return reward is null ? NotFound() : Ok(reward);
    }

    [HttpPost("referral-rewards")]
    public async Task<ActionResult<ReferralRewardDto>> CreateReferralReward([FromBody] CreateReferralRewardDto request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await service.CreateReferralRewardAsync(request, cancellationToken);
            return created is null ? StatusCode(500, "Unable to create referral reward.") : CreatedAtAction(nameof(GetReferralRewardById), new { id = created.ReferralRewardId }, created);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("referral-rewards/{id:int}")]
    public async Task<ActionResult<ReferralRewardDto>> UpdateReferralReward(int id, [FromBody] UpdateReferralRewardDto request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await service.UpdateReferralRewardAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("referral-rewards/{id:int}/activate")]
    public async Task<ActionResult<ReferralRewardDto>> ActivateReferralReward(int id, CancellationToken cancellationToken)
    {
        var reward = await service.ActivateReferralRewardAsync(id, cancellationToken);
        return reward is null ? NotFound() : Ok(reward);
    }

    [HttpPut("referral-rewards/{id:int}/deactivate")]
    public async Task<ActionResult<ReferralRewardDto>> DeactivateReferralReward(int id, CancellationToken cancellationToken)
    {
        var reward = await service.DeactivateReferralRewardAsync(id, cancellationToken);
        return reward is null ? NotFound() : Ok(reward);
    }
}
