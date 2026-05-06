using Microsoft.EntityFrameworkCore;
using SecureERP2.Modules.Payroll.Entities;
using SecureERP2.Modules.Finance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SecureERP2.Modules.Payroll.Services
{
    public class PayrollService
    {
        private readonly ERPDbContext _context;
        private readonly AccountingEngine _accountingEngine;

        public PayrollService(ERPDbContext context, AccountingEngine accountingEngine)
        {
            _context = context;
            _accountingEngine = accountingEngine;
        }

        // Employee Management
        public async Task<Employee> CreateEmployeeAsync(Employee employee)
        {
            // Generate employee number if not provided
            if (string.IsNullOrEmpty(employee.EmployeeNumber))
            {
                employee.EmployeeNumber = await GenerateEmployeeNumberAsync();
            }

            // Set default values
            employee.HireDate = DateTime.UtcNow;
            employee.EmploymentStatus = "Active";

            // Calculate hourly rate from base salary if not provided
            if (employee.BaseSalary > 0 && employee.HourlyRate == 0)
            {
                employee.HourlyRate = employee.BaseSalary / 160; // Assuming 160 hours/month
            }

            // Create initial salary record
            var salary = new Salary
            {
                EmployeeId = employee.Id,
                BaseSalary = employee.BaseSalary,
                HourlyRate = employee.HourlyRate,
                OvertimeRate = employee.HourlyRate * 1.5m, // 1.5x for overtime
                TaxRate = employee.TaxRate,
                InsuranceRate = employee.InsuranceRate,
                PensionRate = employee.PensionRate,
                EffectiveDate = DateTime.UtcNow,
                Status = "Active"
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            salary.EmployeeId = employee.Id; // Set after employee is created
            salary.CompanyId = employee.CompanyId;
            _context.Salaries.Add(salary);
            await _context.SaveChangesAsync();

            return employee;
        }

        public async Task<Employee?> GetEmployeeAsync(int id, int companyId)
        {
            return await _context.Employees
                .Include(e => e.Salaries.Where(s => s.Status == "Active"))
                .FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == companyId);
        }

        public async Task<List<Employee>> GetEmployeesAsync(int companyId, string status = null)
        {
            var query = _context.Employees
                .Include(e => e.Salaries.Where(s => s.Status == "Active"))
                .Where(e => e.CompanyId == companyId);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(e => e.EmploymentStatus == status);
            }

            return await query.OrderBy(e => e.LastName).ThenBy(e => e.FirstName).ToListAsync();
        }

        public async Task<Employee> UpdateEmployeeAsync(Employee employee)
        {
            var existingEmployee = await _context.Employees
                .Include(e => e.Salaries)
                .FirstOrDefaultAsync(e => e.Id == employee.Id && e.CompanyId == employee.CompanyId);

            if (existingEmployee == null)
            {
                throw new Exception("Employee not found");
            }

            // Update employee properties
            existingEmployee.FirstName = employee.FirstName;
            existingEmployee.LastName = employee.LastName;
            existingEmployee.Email = employee.Email;
            existingEmployee.Phone = employee.Phone;
            existingEmployee.Address = employee.Address;
            existingEmployee.Department = employee.Department;
            existingEmployee.Position = employee.Position;
            existingEmployee.EmploymentStatus = employee.EmploymentStatus;
            existingEmployee.EmploymentType = employee.EmploymentType;
            existingEmployee.BaseSalary = employee.BaseSalary;
            existingEmployee.HourlyRate = employee.HourlyRate;
            existingEmployee.TaxRate = employee.TaxRate;
            existingEmployee.InsuranceRate = employee.InsuranceRate;
            existingEmployee.PensionRate = employee.PensionRate;
            existingEmployee.BankAccountNumber = employee.BankAccountNumber;
            existingEmployee.BankName = employee.BankName;
            existingEmployee.PaymentMethod = employee.PaymentMethod;

            // Update salary if changed
            var activeSalary = existingEmployee.Salaries.FirstOrDefault(s => s.Status == "Active");
            if (activeSalary != null && 
                (activeSalary.BaseSalary != employee.BaseSalary ||
                 activeSalary.HourlyRate != employee.HourlyRate ||
                 activeSalary.TaxRate != employee.TaxRate ||
                 activeSalary.InsuranceRate != employee.InsuranceRate ||
                 activeSalary.PensionRate != employee.PensionRate))
            {
                // End current salary
                activeSalary.EndDate = DateTime.UtcNow.AddDays(-1);
                activeSalary.Status = "Expired";

                // Create new salary
                var newSalary = new Salary
                {
                    EmployeeId = existingEmployee.Id,
                    CompanyId = existingEmployee.CompanyId,
                    BaseSalary = employee.BaseSalary,
                    HourlyRate = employee.HourlyRate,
                    OvertimeRate = employee.HourlyRate * 1.5m,
                    TaxRate = employee.TaxRate,
                    InsuranceRate = employee.InsuranceRate,
                    PensionRate = employee.PensionRate,
                    EffectiveDate = DateTime.UtcNow,
                    Status = "Active"
                };

                _context.Salaries.Add(newSalary);
            }

            await _context.SaveChangesAsync();
            return existingEmployee;
        }

        public async Task DeleteEmployeeAsync(int id, int companyId)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == companyId);

            if (employee == null)
            {
                throw new Exception("Employee not found");
            }

            // Soft delete by marking as terminated
            employee.EmploymentStatus = "Terminated";
            employee.TerminationDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        // Payroll Run Management
        public async Task<PayrollRun> CreatePayrollRunAsync(PayrollRun payrollRun)
        {
            // Generate payroll number if not provided
            if (string.IsNullOrEmpty(payrollRun.PayrollNumber))
            {
                payrollRun.PayrollNumber = await GeneratePayrollNumberAsync();
            }

            // Get active employees for the period
            var employees = await _context.Employees
                .Include(e => e.Salaries.Where(s => s.Status == "Active"))
                .Where(e => e.CompanyId == payrollRun.CompanyId && 
                           e.EmploymentStatus == "Active" &&
                           e.HireDate <= payrollRun.PeriodEnd &&
                           (!e.TerminationDate.HasValue || e.TerminationDate >= payrollRun.PeriodStart))
                .ToListAsync();

            payrollRun.Status = "Processing";
            payrollRun.ProcessDate = DateTime.UtcNow;
            payrollRun.TotalEmployees = employees.Count;

            decimal totalGrossPay = 0;
            decimal totalTaxDeductions = 0;
            decimal totalInsuranceDeductions = 0;
            decimal totalPensionDeductions = 0;
            decimal totalOtherDeductions = 0;
            decimal totalNetPay = 0;
            decimal totalOvertimePay = 0;

            // Process each employee
            foreach (var employee in employees)
            {
                var salary = employee.Salaries.FirstOrDefault(s => s.Status == "Active");
                if (salary == null) continue;

                // Calculate payroll for employee
                var payrollRunEmployee = await CalculateEmployeePayrollAsync(employee, salary, payrollRun);
                
                totalGrossPay += payrollRunEmployee.GrossPay;
                totalTaxDeductions += payrollRunEmployee.TaxDeductions;
                totalInsuranceDeductions += payrollRunEmployee.InsuranceDeductions;
                totalPensionDeductions += payrollRunEmployee.PensionDeductions;
                totalOtherDeductions += payrollRunEmployee.OtherDeductions;
                totalNetPay += payrollRunEmployee.NetPay;
                totalOvertimePay += payrollRunEmployee.OvertimePay;

                payrollRun.PayrollRunEmployees.Add(payrollRunEmployee);
            }

            // Set totals
            payrollRun.TotalGrossPay = totalGrossPay;
            payrollRun.TotalTaxDeductions = totalTaxDeductions;
            payrollRun.TotalInsuranceDeductions = totalInsuranceDeductions;
            payrollRun.TotalPensionDeductions = totalPensionDeductions;
            payrollRun.TotalOtherDeductions = totalOtherDeductions;
            payrollRun.TotalNetPay = totalNetPay;
            payrollRun.TotalOvertimePay = totalOvertimePay;
            payrollRun.Status = "Processed";

            // Save payroll run
            _context.PayrollRuns.Add(payrollRun);
            await _context.SaveChangesAsync();

            // Create journal entry for payroll
            await CreateJournalEntryForPayrollAsync(payrollRun);

            return payrollRun;
        }

        public async Task<PayrollRun?> GetPayrollRunAsync(int id, int companyId)
        {
            return await _context.PayrollRuns
                .Include(pr => pr.PayrollRunEmployees)
                    .ThenInclude(pre => pre.Employee)
                .FirstOrDefaultAsync(pr => pr.Id == id && pr.CompanyId == companyId);
        }

        public async Task<List<PayrollRun>> GetPayrollRunsAsync(int companyId)
        {
            return await _context.PayrollRuns
                .Include(pr => pr.PayrollRunEmployees)
                    .ThenInclude(pre => pre.Employee)
                .Where(pr => pr.CompanyId == companyId)
                .OrderByDescending(pr => pr.ProcessDate)
                .ToListAsync();
        }

        public async Task<PayrollSummary> GetPayrollSummaryAsync(int companyId)
        {
            var employees = await _context.Employees
                .Where(e => e.CompanyId == companyId && e.EmploymentStatus == "Active")
                .ToListAsync();

            var payrollRuns = await _context.PayrollRuns
                .Where(pr => pr.CompanyId == companyId && pr.Status == "Processed")
                .ToListAsync();

            return new PayrollSummary
            {
                TotalEmployees = employees.Count,
                ActiveEmployees = employees.Count(e => e.EmploymentStatus == "Active"),
                TotalPayrollRuns = payrollRuns.Count,
                LastPayrollRun = payrollRuns.OrderByDescending(pr => pr.ProcessDate).FirstOrDefault(),
                TotalMonthlyPayroll = payrollRuns.Sum(pr => pr.TotalNetPay),
                AverageSalary = employees.Any() ? employees.Average(e => e.BaseSalary) : 0
            };
        }

        private async Task<PayrollRunEmployee> CalculateEmployeePayrollAsync(Employee employee, Salary salary, PayrollRun payrollRun)
        {
            // Calculate base pay (monthly salary)
            var basePay = salary.BaseSalary;

            // Calculate overtime (simplified - would need actual overtime tracking)
            var overtimeHours = 0; // This would come from time tracking system
            var overtimePay = overtimeHours * salary.OvertimeRate;

            // Calculate gross pay
            var grossPay = basePay + overtimePay;

            // Calculate deductions
            var taxDeductions = grossPay * salary.TaxRate;
            var insuranceDeductions = grossPay * salary.InsuranceRate;
            var pensionDeductions = grossPay * salary.PensionRate;
            var otherDeductions = grossPay * salary.OtherDeductionRate;

            // Calculate total deductions and net pay
            var totalDeductions = taxDeductions + insuranceDeductions + pensionDeductions + otherDeductions;
            var netPay = grossPay - totalDeductions;

            return new PayrollRunEmployee
            {
                PayrollRunId = payrollRun.Id,
                EmployeeId = employee.Id,
                CompanyId = employee.CompanyId,
                BaseSalary = basePay,
                OvertimeHours = overtimeHours,
                OvertimeRate = salary.OvertimeRate,
                OvertimePay = overtimePay,
                GrossPay = grossPay,
                TaxDeductions = taxDeductions,
                InsuranceDeductions = insuranceDeductions,
                PensionDeductions = pensionDeductions,
                OtherDeductions = otherDeductions,
                TotalDeductions = totalDeductions,
                NetPay = netPay,
                TaxRate = salary.TaxRate,
                InsuranceRate = salary.InsuranceRate,
                PensionRate = salary.PensionRate,
                OtherDeductionRate = salary.OtherDeductionRate
            };
        }

        private async Task<string> GenerateEmployeeNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"EMP-{year}";

            var lastEmployee = await _context.Employees
                .Where(e => e.EmployeeNumber.StartsWith(prefix))
                .OrderByDescending(e => e.EmployeeNumber)
                .FirstOrDefaultAsync();

            if (lastEmployee == null)
            {
                return $"{prefix}-001";
            }

            var lastNumber = lastEmployee.EmployeeNumber.Split('-').Last();
            if (int.TryParse(lastNumber, out int number))
            {
                return $"{prefix}-{(number + 1):D3}";
            }

            return $"{prefix}-001";
        }

        private async Task<string> GeneratePayrollNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var month = DateTime.UtcNow.Month;
            
            var prefix = $"PAY-{year:D4}-{month:D2}";
            
            var lastPayroll = await _context.PayrollRuns
                .Where(pr => pr.PayrollNumber.StartsWith(prefix))
                .OrderByDescending(pr => pr.PayrollNumber)
                .FirstOrDefaultAsync();

            if (lastPayroll == null)
            {
                return $"{prefix}-001";
            }

            var lastNumber = lastPayroll.PayrollNumber.Split('-').Last();
            if (int.TryParse(lastNumber, out int number))
            {
                return $"{prefix}-{(number + 1):D3}";
            }

            return $"{prefix}-001";
        }

        private async Task CreateJournalEntryForPayrollAsync(PayrollRun payrollRun)
        {
            // Find or create appropriate accounts
            var accounts = await _context.FinanceAccounts
                .Where(a => a.CompanyId == payrollRun.CompanyId)
                .ToListAsync();

            var cashAccount = accounts.FirstOrDefault(a => a.AccountName.Contains("Cash") || a.AccountName.Contains("Bank"))
                ?? accounts.FirstOrDefault(a => a.AccountType == AccountType.Asset);
            
            var salaryExpenseAccount = accounts.FirstOrDefault(a => a.AccountName.Contains("Salary") || a.AccountName.Contains("Payroll"))
                ?? accounts.FirstOrDefault(a => a.AccountType == AccountType.Expense);
            
            var taxPayableAccount = accounts.FirstOrDefault(a => a.AccountName.Contains("Tax Payable"))
                ?? accounts.FirstOrDefault(a => a.AccountType == AccountType.Liability);
            
            var insurancePayableAccount = accounts.FirstOrDefault(a => a.AccountName.Contains("Insurance Payable"))
                ?? accounts.FirstOrDefault(a => a.AccountType == AccountType.Liability);

            if (cashAccount == null || salaryExpenseAccount == null || taxPayableAccount == null)
            {
                throw new Exception("Required accounts not found for payroll journal entry");
            }

            var transaction = new Transaction
            {
                CompanyId = payrollRun.CompanyId,
                Description = $"Payroll Run {payrollRun.PayrollNumber} for {payrollRun.PeriodDescription}",
                TransactionDate = payrollRun.PeriodEnd,
                TransactionStatus = TransactionStatus.Approved,
                TransactionType = TransactionType.JournalEntry,
                ProcessedAt = DateTime.Now
            };

            // Create ledger entries for payroll
            var ledgerEntries = new List<LedgerEntry>
            {
                new LedgerEntry
                {
                    AccountId = salaryExpenseAccount.Id,
                    DebitAmount = payrollRun.TotalGrossPay,
                    CreditAmount = 0,
                    Description = $"Gross salaries for {payrollRun.PeriodDescription}",
                    TransactionId = transaction.Id
                },
                new LedgerEntry
                {
                    AccountId = taxPayableAccount.Id,
                    DebitAmount = 0,
                    CreditAmount = payrollRun.TotalTaxDeductions,
                    Description = $"Tax deductions for {payrollRun.PeriodDescription}",
                    TransactionId = transaction.Id
                },
                new LedgerEntry
                {
                    AccountId = insurancePayableAccount.Id,
                    DebitAmount = 0,
                    CreditAmount = payrollRun.TotalInsuranceDeductions,
                    Description = $"Insurance deductions for {payrollRun.PeriodDescription}",
                    TransactionId = transaction.Id
                },
                new LedgerEntry
                {
                    AccountId = cashAccount.Id,
                    DebitAmount = 0,
                    CreditAmount = payrollRun.TotalNetPay,
                    Description = $"Net pay for {payrollRun.PeriodDescription}",
                    TransactionId = transaction.Id
                }
            };

            // Save transaction and ledger entries
            _context.Transactions.Add(transaction);
            _context.LedgerEntries.AddRange(ledgerEntries);
            await _context.SaveChangesAsync();
        }
    }

    public class PayrollSummary
    {
        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }
        public int TotalPayrollRuns { get; set; }
        public PayrollRun? LastPayrollRun { get; set; }
        public decimal TotalMonthlyPayroll { get; set; }
        public decimal AverageSalary { get; set; }
    }
}
