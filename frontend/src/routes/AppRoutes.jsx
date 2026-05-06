import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import MainLayout from '../layouts/MainLayout';
import Dashboard from '../pages/Dashboard/Dashboard';
import Journal from '../pages/Journal/Journal';
import Reports from '../pages/Reports/Reports';
import Login from '../components/Login';

// Placeholder components for future modules
const Accounts = () => <div style={{ padding: '24px' }}>Accounts Module - Coming Soon</div>;
const Invoices = () => <div style={{ padding: '24px' }}>Invoices Module - Coming Soon</div>;
const Payroll = () => <div style={{ padding: '24px' }}>Payroll Module - Coming Soon</div>;
const Tax = () => <div style={{ padding: '24px' }}>Tax Module - Coming Soon</div>;

const ProtectedRoute = ({ children }) => {
  const { isAuthenticated, loading } = useAuth();
  
  if (loading) {
    return <div>Loading...</div>;
  }
  
  return isAuthenticated ? children : <Navigate to="/login" />;
};

const AppRoutes = () => {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route
        path="/*"
        element={
          <ProtectedRoute>
            <MainLayout>
              <Routes>
                <Route path="/" element={<Navigate to="/dashboard" replace />} />
                <Route path="/dashboard" element={<Dashboard />} />
                <Route path="/journal" element={<Journal />} />
                <Route path="/accounts" element={<Accounts />} />
                <Route path="/reports" element={<Reports />} />
                <Route path="/invoices" element={<Invoices />} />
                <Route path="/payroll" element={<Payroll />} />
                <Route path="/tax" element={<Tax />} />
              </Routes>
            </MainLayout>
          </ProtectedRoute>
        }
      />
    </Routes>
  );
};

export default AppRoutes;
