import axios from './axios';

// Base API service class
class BaseApiService {
  constructor() {
    this.axios = axios;
  }

  // Generic error handler
  handleError(error) {
    console.error('API Error:', error);
    
    if (error.response) {
      // Server responded with error status
      const message = error.response.data?.message || 'Server error occurred';
      throw new Error(message);
    } else if (error.request) {
      // Network error
      throw new Error('Network error. Please check your connection.');
    } else {
      // Other error
      throw new Error(error.message || 'An unexpected error occurred');
    }
  }

  // Generic GET request
  async get(url, params = {}) {
    try {
      const response = await this.axios.get(url, { params });
      return response.data;
    } catch (error) {
      this.handleError(error);
    }
  }

  // Generic POST request
  async post(url, data = {}) {
    try {
      const response = await this.axios.post(url, data);
      return response.data;
    } catch (error) {
      this.handleError(error);
    }
  }

  // Generic PUT request
  async put(url, data = {}) {
    try {
      const response = await this.axios.put(url, data);
      return response.data;
    } catch (error) {
      this.handleError(error);
    }
  }

  // Generic DELETE request
  async delete(url) {
    try {
      const response = await this.axios.delete(url);
      return response.data;
    } catch (error) {
      this.handleError(error);
    }
  }
}

// Dashboard API Service
class DashboardService extends BaseApiService {
  async getDashboardSummary() {
    return this.get('/api/dashboard/summary');
  }

  async getRecentTransactions(limit = 10) {
    return this.get('/api/finance/transactions/recent', { limit });
  }

  async getUpcomingInvoices() {
    return this.get('/api/invoice/upcoming');
  }

  async getPayrollSummary() {
    return this.get('/api/payroll/summary');
  }

  async getTaxSummary() {
    return this.get('/api/tax/summary');
  }

  async getMonthlyRevenue(year = new Date().getFullYear()) {
    return this.get('/api/finance/reports/monthly-revenue', { year });
  }

  async getExpenseBreakdown() {
    return this.get('/api/finance/reports/expense-breakdown');
  }

  async getAccountBalances() {
    return this.get('/api/finance/accounts/balances');
  }
}

// Finance API Service
class FinanceService extends BaseApiService {
  // Chart of Accounts
  async getAccounts() {
    return this.get('/api/finance/accounts');
  }

  async getAccount(id) {
    return this.get(`/api/finance/accounts/${id}`);
  }

  async createAccount(accountData) {
    return this.post('/api/finance/accounts', accountData);
  }

  async updateAccount(id, accountData) {
    return this.put(`/api/finance/accounts/${id}`, accountData);
  }

  async deleteAccount(id) {
    return this.delete(`/api/finance/accounts/${id}`);
  }

  // Journal Entries
  async getJournalEntries(params = {}) {
    return this.get('/api/finance/journal', params);
  }

  async getJournalEntry(id) {
    return this.get(`/api/finance/journal/${id}`);
  }

  async createJournalEntry(journalData) {
    return this.post('/api/finance/journal', journalData);
  }

  async updateJournalEntry(id, journalData) {
    return this.put(`/api/finance/journal/${id}`, journalData);
  }

  async postJournalEntry(id) {
    return this.post(`/api/finance/journal/${id}/post`);
  }

  async deleteJournalEntry(id) {
    return this.delete(`/api/finance/journal/${id}`);
  }

  // Financial Reports
  async getTrialBalance(params = {}) {
    return this.get('/api/finance/reports/trial-balance', params);
  }

  async getBalanceSheet(params = {}) {
    return this.get('/api/finance/reports/balance-sheet', params);
  }

  async getIncomeStatement(params = {}) {
    return this.get('/api/finance/reports/income-statement', params);
  }

  async getCashFlowStatement(params = {}) {
    return this.get('/api/finance/reports/cash-flow', params);
  }

  async closePeriod(periodData) {
    return this.post('/api/finance/close-period', periodData);
  }
}

// Invoice API Service
class InvoiceService extends BaseApiService {
  async getInvoices(params = {}) {
    return this.get('/api/invoice', params);
  }

  async getInvoice(id) {
    return this.get(`/api/invoice/${id}`);
  }

  async createInvoice(invoiceData) {
    return this.post('/api/invoice', invoiceData);
  }

  async updateInvoice(id, invoiceData) {
    return this.put(`/api/invoice/${id}`, invoiceData);
  }

  async deleteInvoice(id) {
    return this.delete(`/api/invoice/${id}`);
  }

  async updateInvoiceStatus(id, status) {
    return this.patch(`/api/invoice/${id}/status`, { status });
  }

  async getInvoiceSummary() {
    return this.get('/api/invoice/summary');
  }

  async calculateInvoiceTaxes(id) {
    return this.post(`/api/invoice/${id}/calculate-taxes`);
  }

  async sendInvoice(id) {
    return this.post(`/api/invoice/${id}/send`);
  }

  async generateInvoicePDF(id) {
    return this.get(`/api/invoice/${id}/pdf`, { responseType: 'blob' });
  }
}

// Payroll API Service
class PayrollService extends BaseApiService {
  // Employee Management
  async getEmployees(params = {}) {
    return this.get('/api/payroll/employees', params);
  }

  async getEmployee(id) {
    return this.get(`/api/payroll/employees/${id}`);
  }

  async createEmployee(employeeData) {
    return this.post('/api/payroll/employees', employeeData);
  }

  async updateEmployee(id, employeeData) {
    return this.put(`/api/payroll/employees/${id}`, employeeData);
  }

  async deleteEmployee(id) {
    return this.delete(`/api/payroll/employees/${id}`);
  }

  async getEmployeeSalary(id) {
    return this.get(`/api/payroll/employees/${id}/salary`);
  }

  async updateEmployeeSalary(id, salaryData) {
    return this.put(`/api/payroll/employees/${id}/salary`, salaryData);
  }

  // Payroll Runs
  async getPayrollRuns(params = {}) {
    return this.get('/api/payroll/runs', params);
  }

  async getPayrollRun(id) {
    return this.get(`/api/payroll/runs/${id}`);
  }

  async createPayrollRun(payrollData) {
    return this.post('/api/payroll/runs', payrollData);
  }

  async processPayrollRun(id) {
    return this.post(`/api/payroll/runs/${id}/process`);
  }

  async getPayrollSummary() {
    return this.get('/api/payroll/summary');
  }

  async getPayrollReports(params = {}) {
    return this.get('/api/payroll/reports', params);
  }
}

// Tax API Service
class TaxService extends BaseApiService {
  // Tax Rules
  async getTaxRules(params = {}) {
    return this.get('/api/tax/rules', params);
  }

  async getTaxRule(id) {
    return this.get(`/api/tax/rules/${id}`);
  }

  async createTaxRule(taxRuleData) {
    return this.post('/api/tax/rules', taxRuleData);
  }

  async updateTaxRule(id, taxRuleData) {
    return this.put(`/api/tax/rules/${id}`, taxRuleData);
  }

  async deleteTaxRule(id) {
    return this.delete(`/api/tax/rules/${id}`);
  }

  // Tax Calculations
  async getTaxCalculations(params = {}) {
    return this.get('/api/tax/calculations', params);
  }

  async calculateInvoiceTaxes(invoiceId) {
    return this.post(`/api/tax/calculate/invoice/${invoiceId}`);
  }

  async calculatePayrollTaxes(payrollRunId) {
    return this.post(`/api/tax/calculate/payroll/${payrollRunId}`);
  }

  // Tax Reports
  async getTaxReports(params = {}) {
    return this.get('/api/tax/reports', params);
  }

  async getTaxReport(id) {
    return this.get(`/api/tax/reports/${id}`);
  }

  async generateMonthlyTaxReport(year, month) {
    return this.post('/api/tax/reports/monthly', { year, month });
  }

  async getTaxSummary() {
    return this.get('/api/tax/summary');
  }

  async getTaxDashboard() {
    return this.get('/api/tax/dashboard');
  }

  async seedDefaultTaxRules() {
    return this.post('/api/tax/seed-default-rules');
  }
}

// Company API Service
class CompanyService extends BaseApiService {
  async getCompany() {
    return this.get('/api/company/profile');
  }

  async updateCompany(companyData) {
    return this.put('/api/company/profile', companyData);
  }

  async getUsers(params = {}) {
    return this.get('/api/company/users', params);
  }

  async createUser(userData) {
    return this.post('/api/company/users', userData);
  }

  async updateUser(id, userData) {
    return this.put(`/api/company/users/${id}`, userData);
  }

  async deleteUser(id) {
    return this.delete(`/api/company/users/${id}`);
  }

  async getCompanySettings() {
    return this.get('/api/company/settings');
  }

  async updateCompanySettings(settingsData) {
    return this.put('/api/company/settings', settingsData);
  }
}

// Inventory API Service
class InventoryService extends BaseApiService {
  async getProducts(params = {}) {
    return this.get('/api/inventory/products', params);
  }

  async getProduct(id) {
    return this.get(`/api/inventory/products/${id}`);
  }

  async createProduct(productData) {
    return this.post('/api/inventory/products', productData);
  }

  async updateProduct(id, productData) {
    return this.put(`/api/inventory/products/${id}`, productData);
  }

  async deleteProduct(id) {
    return this.delete(`/api/inventory/products/${id}`);
  }

  async getStockLevels(params = {}) {
    return this.get('/api/inventory/stock', params);
  }

  async getStockMovements(params = {}) {
    return this.get('/api/inventory/movements', params);
  }

  async createStockMovement(movementData) {
    return this.post('/api/inventory/movements', movementData);
  }

  async getLowStockAlerts() {
    return this.get('/api/inventory/alerts/low-stock');
  }
}

// Authentication API Service
class AuthService extends BaseApiService {
  async login(credentials) {
    return this.post('/api/auth/login', credentials);
  }

  async logout() {
    return this.post('/api/auth/logout');
  }

  async refreshToken() {
    return this.post('/api/auth/refresh');
  }

  async getCurrentUser() {
    return this.get('/api/auth/me');
  }

  async changePassword(passwordData) {
    return this.post('/api/auth/change-password', passwordData);
  }

  async forgotPassword(email) {
    return this.post('/api/auth/forgot-password', { email });
  }

  async resetPassword(resetData) {
    return this.post('/api/auth/reset-password', resetData);
  }
}

// Create service instances
const dashboardService = new DashboardService();
const financeService = new FinanceService();
const invoiceService = new InvoiceService();
const payrollService = new PayrollService();
const taxService = new TaxService();
const companyService = new CompanyService();
const inventoryService = new InventoryService();
const authService = new AuthService();

export {
  dashboardService,
  financeService,
  invoiceService,
  payrollService,
  taxService,
  companyService,
  inventoryService,
  authService
};

// Export base service for custom implementations
export { BaseApiService };
