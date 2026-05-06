using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureERP2.Modules.Payroll.Entities;
using SecureERP2.Modules.Payroll.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SecureERP2.Modules.Payroll.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PayrollController : ControllerBase
    {
        private readonly PayrollService _payrollService;

        public PayrollController(PayrollService payrollService)
        {
            _payrollService = payrollService;
        }

        // Employee Management Endpoints
        [HttpPost("employees")]
        public async Task<ActionResult<Employee>> CreateEmployee([FromBody] Employee employee)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                employee.CompanyId = companyId;

                var createdEmployee = await _payrollService.CreateEmployeeAsync(employee);
                return CreatedAtAction(nameof(GetEmployee), new { id = createdEmployee.Id }, createdEmployee);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("employees/{id}")]
        public async Task<ActionResult<Employee>> GetEmployee(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var employee = await _payrollService.GetEmployeeAsync(id, companyId);
                
                if (employee == null)
                {
                    return NotFound();
                }

                return Ok(employee);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("employees")]
        public async Task<ActionResult<List<Employee>>> GetEmployees([FromQuery] string status = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var employees = await _payrollService.GetEmployeesAsync(companyId, status);
                return Ok(employees);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("employees/{id}")]
        public async Task<ActionResult<Employee>> UpdateEmployee(int id, [FromBody] Employee employee)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                employee.Id = id;
                employee.CompanyId = companyId;

                var updatedEmployee = await _payrollService.UpdateEmployeeAsync(employee);
                return Ok(updatedEmployee);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("employees/{id}")]
        public async Task<ActionResult> DeleteEmployee(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                await _payrollService.DeleteEmployeeAsync(id, companyId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Payroll Run Management Endpoints
        [HttpPost("runs")]
        public async Task<ActionResult<PayrollRun>> CreatePayrollRun([FromBody] PayrollRun payrollRun)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                payrollRun.CompanyId = companyId;

                var createdPayrollRun = await _payrollService.CreatePayrollRunAsync(payrollRun);
                return CreatedAtAction(nameof(GetPayrollRun), new { id = createdPayrollRun.Id }, createdPayrollRun);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("runs/{id}")]
        public async Task<ActionResult<PayrollRun>> GetPayrollRun(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var payrollRun = await _payrollService.GetPayrollRunAsync(id, companyId);
                
                if (payrollRun == null)
                {
                    return NotFound();
                }

                return Ok(payrollRun);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("runs")]
        public async Task<ActionResult<List<PayrollRun>>> GetPayrollRuns()
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var payrollRuns = await _payrollService.GetPayrollRunsAsync(companyId);
                return Ok(payrollRuns);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("summary")]
        public async Task<ActionResult<PayrollSummary>> GetPayrollSummary()
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var summary = await _payrollService.GetPayrollSummaryAsync(companyId);
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
}
