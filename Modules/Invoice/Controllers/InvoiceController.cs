using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureERP2.Modules.Invoice.Entities;
using SecureERP2.Modules.Invoice.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SecureERP2.Modules.Invoice.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InvoiceController : ControllerBase
    {
        private readonly InvoiceService _invoiceService;

        public InvoiceController(InvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        [HttpPost]
        public async Task<ActionResult<SecureERP2.Modules.Invoice.Entities.Invoice>> CreateInvoice([FromBody] SecureERP2.Modules.Invoice.Entities.Invoice invoice)
        {
            try
            {
                // Set CompanyId from current user context
                var companyId = GetCurrentCompanyId();
                invoice.CompanyId = companyId;

                var createdInvoice = await _invoiceService.CreateInvoiceAsync(invoice);
                return CreatedAtAction(nameof(GetInvoice), new { id = createdInvoice.Id }, createdInvoice);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SecureERP2.Modules.Invoice.Entities.Invoice>> GetInvoice(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var invoice = await _invoiceService.GetInvoiceAsync(id, companyId);
                
                if (invoice == null)
                {
                    return NotFound();
                }

                return Ok(invoice);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<ActionResult<List<SecureERP2.Modules.Invoice.Entities.Invoice>>> GetInvoices([FromQuery] string status = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var invoices = await _invoiceService.GetInvoicesAsync(companyId, status);
                return Ok(invoices);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<SecureERP2.Modules.Invoice.Entities.Invoice>> UpdateInvoice(int id, [FromBody] SecureERP2.Modules.Invoice.Entities.Invoice invoice)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                invoice.Id = id;
                invoice.CompanyId = companyId;

                var updatedInvoice = await _invoiceService.UpdateInvoiceAsync(invoice);
                return Ok(updatedInvoice);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("{id}/status")]
        public async Task<ActionResult<SecureERP2.Modules.Invoice.Entities.Invoice>> UpdateInvoiceStatus(int id, [FromBody] UpdateStatusRequest request)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var invoice = await _invoiceService.UpdateInvoiceStatusAsync(id, companyId, request.Status);
                return Ok(invoice);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteInvoice(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                await _invoiceService.DeleteInvoiceAsync(id, companyId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("summary")]
        public async Task<ActionResult<InvoiceSummary>> GetInvoiceSummary()
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var summary = await _invoiceService.GetInvoiceSummaryAsync(companyId);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private int GetCurrentCompanyId()
        {
            // In a real implementation, this would extract CompanyId from JWT token
            // For now, we'll use a placeholder implementation
            var companyIdClaim = User.FindFirst("CompanyId")?.Value;
            if (int.TryParse(companyIdClaim, out int companyId))
            {
                return companyId;
            }

            // Fallback for development - in production this should throw an error
            return 1;
        }
    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }
}
