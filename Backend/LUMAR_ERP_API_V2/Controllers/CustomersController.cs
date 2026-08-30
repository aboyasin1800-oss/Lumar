using LUMAR_ERP_API_V2.DTOs.Customers;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("customers")]
public sealed class CustomersController(ICustomerService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CustomerListDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<CustomerListDto>> GetCustomers(CancellationToken cancellationToken) => service.GetListAsync(cancellationToken);

    [HttpGet("{id:int}")]
    [ProducesResponseType<CustomerDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDetailsDto>> GetCustomer(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest("Customer id must be positive.");
        var customer = await service.GetByIdAsync(id, cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPost]
    [ProducesResponseType<CustomerDetailsDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CustomerDetailsDto>> CreateCustomer(CreateCustomerDto customer, CancellationToken cancellationToken)
    {
        try
        {
            var created = await service.CreateAsync(customer, cancellationToken);
            return CreatedAtAction(nameof(GetCustomer), new { id = created.CustomerId }, created);
        }
        catch (ArgumentException exception) { return BadRequest(exception.Message); }
        catch (Microsoft.Data.SqlClient.SqlException exception) when (exception.Number is 2601 or 2627) { return Conflict("CustomerCode already exists."); }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CustomerDetailsDto>> UpdateCustomer(int id, UpdateCustomerDto customer, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest("Customer id must be positive.");
        try
        {
            var updated = await service.UpdateAsync(id, customer, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException exception) { return BadRequest(exception.Message); }
        catch (Microsoft.Data.SqlClient.SqlException exception) when (exception.Number is 2601 or 2627) { return Conflict("CustomerCode already exists."); }
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<CustomerListDto>>> Search([FromQuery] string term, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(term)) return BadRequest("Search term must not be empty.");
        return Ok(await service.SearchAsync(term, cancellationToken));
    }

    [HttpGet("{id:int}/referral-hierarchy")]
    public async Task<ActionResult<ReferralHierarchyDto>> GetReferralHierarchy(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest("Customer id must be positive.");
        var hierarchy = await service.GetReferralHierarchyAsync(id, cancellationToken);
        return hierarchy is null ? NotFound() : Ok(hierarchy);
    }

    [HttpGet("{id:int}/measurements")]
    [ProducesResponseType<IReadOnlyList<CustomerMeasurementDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<CustomerMeasurementDto>> GetMeasurements(int id, CancellationToken cancellationToken) => service.GetMeasurementsAsync(id, cancellationToken);

    [HttpPut("{id:int}/measurements")]
    [ProducesResponseType<IReadOnlyList<CustomerMeasurementDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CustomerMeasurementDto>>> UpsertMeasurements(int id, UpsertCustomerMeasurementsDto measurements, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest("Customer id must be positive.");
        if (measurements.Measurements.Select(item => item.MeasurementName?.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != measurements.Measurements.Count)
            return BadRequest("Measurement names must be unique within the request.");
        var saved = await service.UpsertMeasurementsAsync(id, measurements, cancellationToken);
        return saved is null ? NotFound() : Ok(saved);
    }

    [HttpGet("{id:int}/ledger")]
    [ProducesResponseType<IReadOnlyList<CustomerLedgerEntryDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<CustomerLedgerEntryDto>> GetLedger(int id, CancellationToken cancellationToken) => service.GetLedgerAsync(id, cancellationToken);

    [HttpGet("{id:int}/loyalty")]
    [ProducesResponseType<CustomerLoyaltyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerLoyaltyDto>> GetLoyalty(int id, CancellationToken cancellationToken)
    {
        var loyalty = await service.GetLoyaltyAsync(id, cancellationToken);
        return loyalty is null ? NotFound() : Ok(loyalty);
    }

    [HttpGet("{id:int}/referrals")]
    [ProducesResponseType<CustomerReferralDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerReferralDto>> GetReferrals(int id, CancellationToken cancellationToken)
    {
        var referrals = await service.GetReferralsAsync(id, cancellationToken);
        return referrals is null ? NotFound() : Ok(referrals);
    }
}