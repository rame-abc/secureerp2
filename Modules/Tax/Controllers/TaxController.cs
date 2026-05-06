using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureERP2.Modules.Tax.Entities;
using SecureERP2.Modules.Tax.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SecureERP2.Modules.Tax.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TaxController : ControllerBase
    {
        private readonly TaxService _taxService;

        public TaxController(TaxService taxService)
        {
            _taxService = taxService;
        }

        // Tax Rule Management
        [HttpPost("rules")]
        public async Task<ActionResult<TaxRule>> CreateTaxRule([FromBody] TaxRule taxRule)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                taxRule.CompanyId = companyId;

                var createdRule = await _taxService.CreateTaxRuleAsync(taxRule);
                return CreatedAtAction(nameof(GetTaxRule), new { id = createdRule.Id }, createdRule);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("rules/{id}")]
        public async Task<ActionResult<TaxRule>> GetTaxRule(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var taxRule = await _taxService.GetTaxRuleAsync(id, companyId);
                
                if (taxRule == null)
                {
                    return NotFound();
                }

                return Ok(taxRule);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("rules")]
        public async Task<ActionResult<List<TaxRule>>> GetTaxRules([FromQuery] string taxType = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var taxRules = await _taxService.GetTaxRulesAsync(companyId, taxType);
                return Ok(taxRules);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("rules/{id}")]
        public async Task<ActionResult<TaxRule>> UpdateTaxRule(int id, [FromBody] TaxRule taxRule)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                taxRule.Id = id;
                taxRule.CompanyId = companyId;

                var updatedRule = await _taxService.UpdateTaxRuleAsync(taxRule);
                return Ok(updatedRule);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Tax Calculation
        [HttpPost("calculate/invoice/{invoiceId}")]
        public async Task<ActionResult<List<TaxCalculation>>> CalculateInvoiceTaxes(int invoiceId)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                
                // Get invoice (would need to inject InvoiceService or use context)
                // For now, we'll return a placeholder response
                var taxCalculations = new List<TaxCalculation>();
                
                return Ok(taxCalculations);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("calculate/payroll/{payrollRunId}")]
        public async Task<ActionResult<List<TaxCalculation>>> CalculatePayrollTaxes(int payrollRunId)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                
                // Get payroll run (would need to inject PayrollService or use context)
                // For now, we'll return a placeholder response
                var taxCalculations = new List<TaxCalculation>();
                
                return Ok(taxCalculations);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Tax Reports
        [HttpPost("reports/monthly")]
        public async Task<ActionResult<TaxReport>> GenerateMonthlyTaxReport([FromBody] MonthlyReportRequest request)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var taxReport = await _taxService.GenerateMonthlyTaxReportAsync(companyId, request.Year, request.Month);
                return CreatedAtAction(nameof(GetTaxReport), new { id = taxReport.Id }, taxReport);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("reports/{id}")]
        public async Task<ActionResult<TaxReport>> GetTaxReport(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var taxReports = await _taxService.GetTaxReportsAsync(companyId);
                var taxReport = taxReports.FirstOrDefault(tr => tr.Id == id);
                
                if (taxReport == null)
                {
                    return NotFound();
                }

                return Ok(taxReport);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("reports")]
        public async Task<ActionResult<List<TaxReport>>> GetTaxReports([FromQuery] string reportType = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var taxReports = await _taxService.GetTaxReportsAsync(companyId, reportType);
                return Ok(taxReports);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Tax Summary
        [HttpGet("summary")]
        public async Task<ActionResult<TaxSummary>> GetTaxSummary()
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var summary = await _taxService.GetTaxSummaryAsync(companyId);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Tax Dashboard Data
        [HttpGet("dashboard")]
        public async Task<ActionResult<TaxDashboardData>> GetTaxDashboard()
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var summary = await _taxService.GetTaxSummaryAsync(companyId);
                var taxRules = await _taxService.GetTaxRulesAsync(companyId);
                var reports = await _taxService.GetTaxReportsAsync(companyId);

                var dashboardData = new TaxDashboardData
                {
                    Summary = summary,
                    TaxRulesByType = taxRules.GroupBy(tr => tr.TaxType)
                                            .ToDictionary(g => g.Key, g => g.Count()),
                    RecentReports = reports.Take(5).ToList(),
                    UpcomingFilingDeadlines = reports.Where(r => !r.FiledDate.HasValue && r.DueDate.HasValue)
                                                  .Where(r => r.DueDate.Value > DateTime.UtcNow)
                                                  .OrderBy(r => r.DueDate)
                                                  .Take(3)
                                                  .ToList(),
                    TaxLiabilityTrend = reports.TakeLast(12)
                                             .Select(r => new TaxTrendData
                                             {
                                                 Period = r.PeriodDescription,
                                                 Liability = r.TotalTaxPayable,
                                                 Paid = r.TotalTaxPaid,
                                                 Balance = r.TaxBalance
                                             })
                                             .ToList()
                };

                return Ok(dashboardData);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Seed Default Tax Rules
        [HttpPost("seed-default-rules")]
        public async Task<ActionResult<List<TaxRule>>> SeedDefaultTaxRules()
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var defaultRules = new List<TaxRule>
                {
                    new TaxRule
                    {
                        CompanyId = companyId,
                        TaxCode = "VAT-STD",
                        TaxName = "Standard VAT",
                        TaxType = "VAT",
                        TaxRate = 15m,
                        RateType = "Percentage",
                        Jurisdiction = "National",
                        Description = "Standard Value Added Tax at 15%",
                        IsRecoverable = true,
                        Applicability = "All"
                    },
                    new TaxRule
                    {
                        CompanyId = companyId,
                        TaxCode = "INC-TAX",
                        TaxName = "Income Tax Withholding",
                        TaxType = "IncomeTax",
                        TaxRate = 20m,
                        RateType = "Percentage",
                        Jurisdiction = "Federal",
                        Description = "Income tax withholding at 20%",
                        IsRecoverable = false,
                        Applicability = "Payroll"
                    },
                    new TaxRule
                    {
                        CompanyId = companyId,
                        TaxCode = "WH-TAX",
                        TaxName = "Withholding Tax",
                        TaxType = "WithholdingTax",
                        TaxRate = 10m,
                        RateType = "Percentage",
                        Jurisdiction = "State",
                        Description = "Withholding tax at 10%",
                        IsRecoverable = false,
                        Applicability = "Invoice"
                    }
                };

                var createdRules = new List<TaxRule>();
                foreach (var rule in defaultRules)
                {
                    var createdRule = await _taxService.CreateTaxRuleAsync(rule);
                    createdRules.Add(createdRule);
                }

                return Ok(createdRules);
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

    public class MonthlyReportRequest
    {
        public int Year { get; set; }
        public int Month { get; set; }
    }

    public class TaxDashboardData
    {
        public TaxSummary Summary { get; set; }
        public Dictionary<string, int> TaxRulesByType { get; set; }
        public List<TaxReport> RecentReports { get; set; }
        public List<TaxReport> UpcomingFilingDeadlines { get; set; }
        public List<TaxTrendData> TaxLiabilityTrend { get; set; }
    }

    public class TaxTrendData
    {
        public string Period { get; set; }
        public decimal Liability { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
    }
}
