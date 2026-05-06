import React, { useState } from 'react';
import {
  Layout,
  Menu,
  Avatar,
  Typography,
  Badge,
  Tooltip,
  Button,
  Drawer,
  theme
} from 'antd';
import {
  DashboardOutlined,
  FileTextOutlined,
  UserOutlined,
  PayrollOutlined,
  CalculatorOutlined,
  SettingOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  BellOutlined,
  LogoutOutlined,
  CompanyOutlined,
  WalletOutlined,
  FileSearchOutlined,
  TeamOutlined,
  BarChartOutlined,
  FileProtectOutlined,
  BankOutlined,
  TaxOutlined,
  InboxOutlined,
  ReconciliationOutlined
} from '@ant-design/icons';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';

const { Sider } = Layout;
const { Text } = Typography;

const Sidebar = ({ collapsed, onCollapse }) => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const {
    token: { colorBgContainer, colorPrimary },
  } = theme.useToken();

  const [notifications] = useState(3); // Mock notification count

  const menuItems = [
    {
      key: '/dashboard',
      icon: <DashboardOutlined />,
      label: 'Dashboard',
      tooltip: 'Main dashboard with overview',
    },
    {
      key: '/finance',
      icon: <WalletOutlined />,
      label: 'Finance',
      children: [
        {
          key: '/finance/accounts',
          icon: <BankOutlined />,
          label: 'Chart of Accounts',
        },
        {
          key: '/finance/journal',
          icon: <ReconciliationOutlined />,
          label: 'Journal Entries',
        },
        {
          key: '/finance/reports',
          icon: <BarChartOutlined />,
          label: 'Financial Reports',
        },
      ],
    },
    {
      key: '/sales',
      icon: <FileTextOutlined />,
      label: 'Sales & Invoicing',
      children: [
        {
          key: '/invoices',
          icon: <FileTextOutlined />,
          label: 'Invoices',
          badge: 'New',
        },
        {
          key: '/customers',
          icon: <TeamOutlined />,
          label: 'Customers',
        },
        {
          key: '/sales-reports',
          icon: <BarChartOutlined />,
          label: 'Sales Reports',
        },
      ],
    },
    {
      key: '/payroll',
      icon: <PayrollOutlined />,
      label: 'Payroll',
      children: [
        {
          key: '/payroll/employees',
          icon: <UserOutlined />,
          label: 'Employees',
        },
        {
          key: '/payroll/runs',
          icon: <CalculatorOutlined />,
          label: 'Payroll Runs',
        },
        {
          key: '/payroll/reports',
          icon: <FileSearchOutlined />,
          label: 'Payroll Reports',
        },
      ],
    },
    {
      key: '/tax',
      icon: <TaxOutlined />,
      label: 'Tax Management',
      children: [
        {
          key: '/tax/rules',
          icon: <SettingOutlined />,
          label: 'Tax Rules',
        },
        {
          key: '/tax/calculations',
          icon: <CalculatorOutlined />,
          label: 'Tax Calculations',
        },
        {
          key: '/tax/reports',
          icon: <FileProtectOutlined />,
          label: 'Tax Reports',
        },
      ],
    },
    {
      key: '/inventory',
      icon: <InboxOutlined />,
      label: 'Inventory',
      children: [
        {
          key: '/inventory/products',
          icon: <InboxOutlined />,
          label: 'Products',
        },
        {
          key: '/inventory/stock',
          icon: <InboxOutlined />,
          label: 'Stock Levels',
        },
        {
          key: '/inventory/movements',
          icon: <ReconciliationOutlined />,
          label: 'Stock Movements',
        },
      ],
    },
    {
      key: '/company',
      icon: <CompanyOutlined />,
      label: 'Company',
      children: [
        {
          key: '/company/profile',
          icon: <CompanyOutlined />,
          label: 'Company Profile',
        },
        {
          key: '/company/users',
          icon: <TeamOutlined />,
          label: 'User Management',
        },
        {
          key: '/company/settings',
          icon: <SettingOutlined />,
          label: 'Settings',
        },
      ],
    },
  ];

  const handleMenuClick = ({ key }) => {
    navigate(key);
  };

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const getSelectedKeys = () => {
    const pathname = location.pathname;
    return [pathname];
  };

  const getOpenKeys = () => {
    const pathname = location.pathname;
    const openKeys = [];
    
    menuItems.forEach(item => {
      if (item.children) {
        const childKey = item.children.find(child => child.key === pathname)?.key;
        if (childKey) {
          openKeys.push(item.key);
        }
      }
    });
    
    return openKeys;
  };

  return (
    <>
      <Sider
        trigger={null}
        collapsible
        collapsed={collapsed}
        width={256}
        style={{
          overflow: 'auto',
          height: '100vh',
          position: 'fixed',
          left: 0,
          top: 0,
          bottom: 0,
          background: colorBgContainer,
          borderRight: '1px solid #f0f0f0',
        }}
      >
        {/* Company Logo/Header */}
        <div style={{
          padding: '16px',
          borderBottom: '1px solid #f0f0f0',
          display: 'flex',
          alignItems: 'center',
          justifyContent: collapsed ? 'center' : 'space-between',
        }}>
          {!collapsed && (
            <div style={{ display: 'flex', alignItems: 'center' }}>
              <div style={{
                width: '32px',
                height: '32px',
                borderRadius: '6px',
                background: colorPrimary,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                marginRight: '12px',
              }}>
                <span style={{ color: 'white', fontWeight: 'bold', fontSize: '16px' }}>E</span>
              </div>
              <div>
                <Text strong style={{ fontSize: '16px', display: 'block' }}>ERP System</Text>
                <Text type="secondary" style={{ fontSize: '12px' }}>Enterprise Edition</Text>
              </div>
            </div>
          )}
          {collapsed && (
            <div style={{
              width: '32px',
              height: '32px',
              borderRadius: '6px',
              background: colorPrimary,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}>
              <span style={{ color: 'white', fontWeight: 'bold', fontSize: '16px' }}>E</span>
            </div>
          )}
        </div>

        {/* User Profile Section */}
        {!collapsed && (
          <div style={{
            padding: '16px',
            borderBottom: '1px solid #f0f0f0',
          }}>
            <div style={{ display: 'flex', alignItems: 'center' }}>
              <Avatar 
                size="small" 
                icon={<UserOutlined />}
                style={{ marginRight: '12px', backgroundColor: colorPrimary }}
              />
              <div style={{ flex: 1 }}>
                <Text strong style={{ fontSize: '14px', display: 'block' }}>
                  {user?.username || 'Guest User'}
                </Text>
                <Text type="secondary" style={{ fontSize: '12px', display: 'block' }}>
                  {user?.companyName || 'Demo Company'}
                </Text>
              </div>
            </div>
          </div>
        )}

        {/* Navigation Menu */}
        <Menu
          mode="inline"
          selectedKeys={getSelectedKeys()}
          defaultOpenKeys={getOpenKeys()}
          items={menuItems.map(item => ({
            ...item,
            children: item.children?.map(child => ({
              ...child,
              label: child.badge ? (
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span>{child.label}</span>
                  <Badge size="small" count={child.badge} style={{ backgroundColor: '#52c41a' }} />
                </div>
              ) : child.label,
            }))
          }))}
          onClick={handleMenuClick}
          style={{
            border: 'none',
            padding: '0 8px',
          }}
        />

        {/* Bottom Actions */}
        <div style={{
          position: 'absolute',
          bottom: 0,
          left: 0,
          right: 0,
          padding: '16px',
          borderTop: '1px solid #f0f0f0',
          background: colorBgContainer,
        }}>
          {!collapsed && (
            <div style={{ marginBottom: '12px' }}>
              <Tooltip title="Notifications">
                <Button 
                  type="text" 
                  icon={<BellOutlined />} 
                  style={{ width: '100%', textAlign: 'left' }}
                >
                  <span style={{ marginRight: '8px' }}>Notifications</span>
                  <Badge count={notifications} size="small" />
                </Button>
              </Tooltip>
            </div>
          )}
          
          <Tooltip title={collapsed ? 'Logout' : ''}>
            <Button 
              type="text" 
              icon={<LogoutOutlined />} 
              onClick={handleLogout}
              style={{ 
                width: '100%', 
                textAlign: 'left',
                color: '#ff4d4f'
              }}
            >
              {!collapsed && 'Logout'}
            </Button>
          </Tooltip>
        </div>
      </Sider>
    </>
  );
};

export default Sidebar;
