import React, { Suspense } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { Spin } from 'antd';
import { useAuth } from '../context/AuthContext';

// Layout Components
import MainLayout from '../components/Layout/MainLayout';
import Login from '../components/Login';

// Lazy load page components for better performance
const Dashboard = React.lazy(() => import('../pages/Dashboard/EnhancedDashboard'));

// Finance Module
const ChartOfAccounts = React.lazy(() => import('../pages/Finance/ChartOfAccounts'));
const JournalEntries = React.lazy(() => import('../pages/Finance/JournalEntries'));
const FinancialReports = React.lazy(() => import('../pages/Finance/FinancialReports'));

// Sales & Invoicing Module
const Invoices = React.lazy(() => import('../pages/Invoices/Invoices'));
const InvoiceDetail = React.lazy(() => import('../pages/Invoices/InvoiceDetail'));
const CreateInvoice = React.lazy(() => import('../pages/Invoices/CreateInvoice'));
const Customers = React.lazy(() => import('../pages/Invoices/Customers'));
const SalesReports = React.lazy(() => import('../pages/Invoices/SalesReports'));

// Payroll Module
const Employees = React.lazy(() => import('../pages/Payroll/Employees'));
const EmployeeDetail = React.lazy(() => import('../pages/Payroll/EmployeeDetail'));
const PayrollRuns = React.lazy(() => import('../pages/Payroll/PayrollRuns'));
const PayrollReports = React.lazy(() => import('../pages/Payroll/PayrollReports'));

// Tax Module
const TaxRules = React.lazy(() => import('../pages/Tax/TaxRules'));
const TaxCalculations = React.lazy(() => import('../pages/Tax/TaxCalculations'));
const TaxReports = React.lazy(() => import('../pages/Tax/TaxReports'));

// Inventory Module
const Products = React.lazy(() => import('../pages/Inventory/Products'));
const StockLevels = React.lazy(() => import('../pages/Inventory/StockLevels'));
const StockMovements = React.lazy(() => import('../pages/Inventory/StockMovements'));

// Company Module
const CompanyProfile = React.lazy(() => import('../pages/Company/CompanyProfile'));
const UserManagement = React.lazy(() => import('../pages/Company/UserManagement'));
const CompanySettings = React.lazy(() => import('../pages/Company/CompanySettings'));

// Settings & Profile
const UserProfile = React.lazy(() => import('../pages/Settings/UserProfile'));
const Settings = React.lazy(() => import('../pages/Settings/Settings'));

// Loading component
const PageLoading = () => (
  <div style={{ 
    display: 'flex', 
    justifyContent: 'center', 
    alignItems: 'center', 
    height: '200px' 
  }}>
    <Spin size="large" />
  </div>
);

// Protected Route Component
const ProtectedRoute = ({ children }) => {
  const { isAuthenticated } = useAuth();
  
  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }
  
  return <Suspense fallback={<PageLoading />}>{children}</Suspense>;
};

// Public Route Component (accessible without login)
const PublicRoute = ({ children }) => {
  const { isAuthenticated } = useAuth();
  
  if (isAuthenticated) {
    return <Navigate to="/dashboard" replace />;
  }
  
  return <Suspense fallback={<PageLoading />}>{children}</Suspense>;
};

const EnhancedAppRoutes = () => {
  return (
    <Routes>
      {/* Public Routes */}
      <Route 
        path="/login" 
        element={
          <PublicRoute>
            <Login />
          </PublicRoute>
        } 
      />

      {/* Protected Routes with Main Layout */}
      <Route path="/" element={<MainLayout />}>
        <Route index element={<Navigate to="/dashboard" replace />} />
        
        {/* Dashboard */}
        <Route 
          path="dashboard" 
          element={
            <ProtectedRoute>
              <Dashboard />
            </ProtectedRoute>
          } 
        />

        {/* Finance Module Routes */}
        <Route path="finance">
          <Route 
            path="accounts" 
            element={
              <ProtectedRoute>
                <ChartOfAccounts />
              </ProtectedRoute>
            } 
          />
          <Route 
            path="journal" 
            element={
              <ProtectedRoute>
                <JournalEntries />
              </ProtectedRoute>
            } 
          />
          <Route 
            path="reports" 
            element={
              <ProtectedRoute>
                <FinancialReports />
              </ProtectedRoute>
            } 
          />
        </Route>

        {/* Sales & Invoicing Module Routes */}
        <Route path="invoices">
          <Route 
            index 
            element={
              <ProtectedRoute>
                <Invoices />
              </ProtectedRoute>
            } 
          />
          <Route 
            path="create" 
            element={
              <ProtectedRoute>
                <CreateInvoice />
              </ProtectedRoute>
            } 
          />
          <Route 
            path=":id" 
            element={
              <ProtectedRoute>
                <InvoiceDetail />
              </ProtectedRoute>
            } 
          />
        </Route>
        <Route 
          path="customers" 
          element={
            <ProtectedRoute>
              <Customers />
            </ProtectedRoute>
          } 
        />
        <Route 
          path="sales-reports" 
          element={
            <ProtectedRoute>
              <SalesReports />
            </ProtectedRoute>
          } 
        />

        {/* Payroll Module Routes */}
        <Route path="payroll">
          <Route 
            path="employees" 
            element={
              <ProtectedRoute>
                <Employees />
              </ProtectedRoute>
            } 
          />
          <Route 
            path="employees/:id" 
            element={
              <ProtectedRoute>
                <EmployeeDetail />
              </ProtectedRoute>
            } 
          />
          <Route 
            path="runs" 
            element={
              <ProtectedRoute>
                <PayrollRuns />
              </ProtectedRoute>
            } 
          />
          <Route 
            path="reports" 
            element={
              <ProtectedRoute>
                <PayrollReports />
              </ProtectedRoute>
            } 
          />
        </Route>

        {/* Tax Module Routes */}
        <Route path="tax">
          <Route 
            path="rules" 
            element={
              <ProtectedRoute>
                <TaxRules />
              </ProtectedRoute>
            } 
          />
          <Route 
            path="calculations" 
            element={
              <ProtectedRoute>
                <TaxCalculations />
              </ProtectedRoute>
            } 
          />
          <Route 
            path="reports" 
            element={
              <ProtectedRoute>
                <TaxReports />
              </ProtectedRoute>
            } 
          />
        </Route>

        {/* Inventory Module Routes */}
        <Route path="inventory">
          <Route 
            path="products" 
            element={
              <ProtectedRoute>
                <Products />
              </ProtectedRoute>
            } 
          />
          <Route 
            path="stock" 
            element={
              <ProtectedRoute>
                <StockLevels />
              </ProtectedRoute>
            } 
          />
          <Route 
            path="movements" 
            element={
              <ProtectedRoute>
                <StockMovements />
              </ProtectedRoute>
            } 
          />
        </Route>

        {/* Company Module Routes */}
        <Route path="company">
          <Route 
            path="profile" 
            element={
              <ProtectedRoute>
                <CompanyProfile />
              </ProtectedRoute>
            } 
          />
          <Route 
            path="users" 
            element={
              <ProtectedRoute>
                <UserManagement />
              </ProtectedRoute>
            } 
          />
          <Route 
            path="settings" 
            element={
              <ProtectedRoute>
                <CompanySettings />
              </ProtectedRoute>
            } 
          />
        </Route>

        {/* Settings & Profile Routes */}
        <Route 
          path="profile" 
          element={
            <ProtectedRoute>
              <UserProfile />
            </ProtectedRoute>
          } 
        />
        <Route 
          path="settings" 
          element={
            <ProtectedRoute>
              <Settings />
            </ProtectedRoute>
          } 
        />
      </Route>

      {/* Catch-all route for 404 */}
      <Route 
        path="*" 
        element={
          <ProtectedRoute>
            <div style={{ 
              display: 'flex', 
              justifyContent: 'center', 
              alignItems: 'center', 
              height: '100vh',
              flexDirection: 'column'
            }}>
              <h1>404 - Page Not Found</h1>
              <p>The page you're looking for doesn't exist.</p>
            </div>
          </ProtectedRoute>
        } 
      />
    </Routes>
  );
};

export default EnhancedAppRoutes;
