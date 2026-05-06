import React, { useState } from 'react';
import {
  Layout,
  Button,
  Avatar,
  Dropdown,
  Badge,
  Space,
  Typography,
  Tooltip,
  Drawer,
  List,
  Empty,
  Tag,
  Divider,
  theme
} from 'antd';
import {
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  BellOutlined,
  UserOutlined,
  SettingOutlined,
  LogoutOutlined,
  SearchOutlined,
  GlobalOutlined,
  BulbOutlined,
  QuestionCircleOutlined
} from '@ant-design/icons';
import { useAuth } from '../../context/AuthContext';
import { useNavigate } from 'react-router-dom';

const { Header } = Layout;
const { Text } = Typography;

const AppHeader = ({ collapsed, onCollapse }) => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [notificationDrawerOpen, setNotificationDrawerOpen] = useState(false);
  const [searchVisible, setSearchVisible] = useState(false);
  
  const {
    token: { colorBgContainer, colorPrimary },
  } = theme.useToken();

  // Mock notifications data
  const notifications = [
    {
      id: 1,
      title: 'New Invoice Created',
      description: 'Invoice #INV-2024-001 has been created for $1,500.00',
      time: '5 minutes ago',
      type: 'success',
      read: false,
    },
    {
      id: 2,
      title: 'Payroll Processing Complete',
      description: 'Monthly payroll for October 2024 has been processed successfully',
      time: '1 hour ago',
      type: 'info',
      read: false,
    },
    {
      id: 3,
      title: 'Tax Report Ready',
      description: 'Monthly tax report for September 2024 is ready for review',
      time: '2 hours ago',
      type: 'warning',
      read: true,
    },
  ];

  const unreadCount = notifications.filter(n => !n.read).length;

  const userMenuItems = [
    {
      key: 'profile',
      icon: <UserOutlined />,
      label: 'Profile',
      onClick: () => navigate('/profile'),
    },
    {
      key: 'settings',
      icon: <SettingOutlined />,
      label: 'Settings',
      onClick: () => navigate('/settings'),
    },
    {
      type: 'divider',
    },
    {
      key: 'help',
      icon: <QuestionCircleOutlined />,
      label: 'Help & Documentation',
      onClick: () => window.open('/docs', '_blank'),
    },
    {
      key: 'theme',
      icon: <BulbOutlined />,
      label: 'Toggle Theme',
      onClick: () => {
        // Theme toggle logic here
      },
    },
    {
      type: 'divider',
    },
    {
      key: 'logout',
      icon: <LogoutOutlined />,
      label: 'Logout',
      onClick: () => {
        logout();
        navigate('/login');
      },
      danger: true,
    },
  ];

  const handleNotificationClick = (notification) => {
    // Mark notification as read logic here
    console.log('Notification clicked:', notification);
  };

  const getNotificationIcon = (type) => {
    switch (type) {
      case 'success':
        return <Tag color="success">Success</Tag>;
      case 'warning':
        return <Tag color="warning">Warning</Tag>;
      case 'error':
        return <Tag color="error">Error</Tag>;
      default:
        return <Tag color="blue">Info</Tag>;
    }
  };

  return (
    <>
      <Header
        style={{
          padding: '0 24px',
          background: colorBgContainer,
          borderBottom: '1px solid #f0f0f0',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          position: 'sticky',
          top: 0,
          zIndex: 1000,
        }}
      >
        {/* Left Section */}
        <div style={{ display: 'flex', alignItems: 'center' }}>
          <Button
            type="text"
            icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
            onClick={onCollapse}
            style={{
              fontSize: '16px',
              width: 40,
              height: 40,
            }}
          />
          
          {/* Search Bar */}
          <div style={{ marginLeft: '16px' }}>
            <Button
              type="text"
              icon={<SearchOutlined />}
              onClick={() => setSearchVisible(true)}
              style={{ borderRadius: '6px' }}
            >
              Search...
            </Button>
          </div>
        </div>

        {/* Right Section */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
          {/* Quick Actions */}
          <Space>
            <Tooltip title="Global Settings">
              <Button type="text" icon={<GlobalOutlined />} />
            </Tooltip>
            
            <Tooltip title="Help">
              <Button type="text" icon={<QuestionCircleOutlined />} />
            </Tooltip>
          </Space>

          {/* Notifications */}
          <Tooltip title="Notifications">
            <Badge count={unreadCount} size="small">
              <Button
                type="text"
                icon={<BellOutlined />}
                onClick={() => setNotificationDrawerOpen(true)}
                style={{ fontSize: '16px' }}
              />
            </Badge>
          </Tooltip>

          {/* User Menu */}
          <Dropdown
            menu={{ items: userMenuItems }}
            placement="bottomRight"
            trigger={['click']}
          >
            <div style={{ 
              display: 'flex', 
              alignItems: 'center', 
              cursor: 'pointer',
              padding: '4px 8px',
              borderRadius: '6px',
              transition: 'background-color 0.2s',
            }}
            onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#f5f5f5'}
            onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
          >
            <Avatar 
              size="small" 
              icon={<UserOutlined />}
              style={{ 
                backgroundColor: colorPrimary,
                marginRight: '8px'
              }}
            />
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start' }}>
              <Text strong style={{ fontSize: '14px', lineHeight: '20px' }}>
                {user?.username || 'Guest User'}
              </Text>
              <Text type="secondary" style={{ fontSize: '12px', lineHeight: '16px' }}>
                {user?.role || 'User'}
              </Text>
            </div>
          </div>
          </Dropdown>
        </div>
      </Header>

      {/* Notification Drawer */}
      <Drawer
        title={
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span>Notifications</span>
            <Space>
              <Button type="text" size="small">
                Mark all as read
              </Button>
              <Button type="text" size="small">
                Clear all
              </Button>
            </Space>
          </div>
        }
        placement="right"
        onClose={() => setNotificationDrawerOpen(false)}
        open={notificationDrawerOpen}
        width={400}
      >
        {notifications.length > 0 ? (
          <List
            dataSource={notifications}
            renderItem={(item) => (
              <List.Item
                style={{
                  padding: '12px 0',
                  cursor: 'pointer',
                  backgroundColor: item.read ? 'transparent' : '#f6ffed',
                  borderRadius: '6px',
                  paddingLeft: '12px',
                }}
                onClick={() => handleNotificationClick(item)}
                actions={[getNotificationIcon(item.type)]}
              >
                <List.Item.Meta
                  title={
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <Text strong={!item.read}>{item.title}</Text>
                      <Text type="secondary" style={{ fontSize: '12px' }}>
                        {item.time}
                      </Text>
                    </div>
                  }
                  description={
                    <Text type="secondary" style={{ fontSize: '14px' }}>
                      {item.description}
                    </Text>
                  }
                />
              </List.Item>
            )}
          />
        ) : (
          <Empty
            description="No notifications"
            image={Empty.PRESENTED_IMAGE_SIMPLE}
          />
        )}
      </Drawer>

      {/* Search Modal/Drawer */}
      <Drawer
        title="Search"
        placement="top"
        onClose={() => setSearchVisible(false)}
        open={searchVisible}
        height={200}
        style={{ borderRadius: '0 0 8px 8px' }}
      >
        <div style={{ padding: '20px 0' }}>
          <Text type="secondary">
            Search functionality coming soon... Search across invoices, customers, transactions, and more.
          </Text>
        </div>
      </Drawer>
    </>
  );
};

export default AppHeader;
