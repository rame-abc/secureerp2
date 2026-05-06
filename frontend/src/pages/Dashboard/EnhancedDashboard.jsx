import React, { useState, useEffect } from 'react';
import {
  Row,
  Col,
  Card,
  Statistic,
  Table,
  Button,
  Space,
  Typography,
  Progress,
  Avatar,
  Tag,
  List,
  Timeline,
  Alert,
  Spin,
  Empty,
  Tooltip,
  Dropdown,
  Menu,
  Divider
} from 'antd';
import {
  DollarOutlined,
  UserOutlined,
  FileTextOutlined,
  TrendingUpOutlined,
  TrendingDownOutlined,
  EyeOutlined,
  MoreOutlined,
  ReloadOutlined,
  DownloadOutlined,
  CalendarOutlined,
  ArrowUpOutlined,
  ArrowDownOutlined,
  BankOutlined,
  TeamOutlined,
  PayCircleOutlined,
  TaxOutlined
} from '@ant-design/icons';
import { LineChart, Line, AreaChart, Area, BarChart, Bar, PieChart, Pie, Cell, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, Legend, ResponsiveContainer } from 'recharts';
import { useNavigate } from 'react-router-dom';
import axios from '../../api/axios';
import { useAuth } from '../../context/AuthContext';

const { Title, Text } = Typography;

const EnhancedDashboard = () => {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  
  // Dashboard data states
  const [dashboardData, setDashboardData] = useState({
    summary: null,
    recentTransactions: [],
    upcomingInvoices: [],
    payrollSummary: null,
    taxSummary: null,
    monthlyRevenue: [],
    expenseBreakdown: [],
    accountBalances: []
  });

  // Fetch dashboard data
  const fetchDashboardData = async () => {
    try {
      setLoading(true);
      
      // Mock data for now - replace with actual API calls
      const mockData = {
        summary: {
          totalRevenue: 125000,
          totalExpenses: 87500,
          netProfit: 37500,
          totalInvoices: 45,
          paidInvoices: 38,
          pendingInvoices: 7,
          totalEmployees: 24,
          activeEmployees: 22,
          totalTaxLiability: 12500,
          paidTax: 8500,
          pendingTax: 4000
        },
        recentTransactions: [
          {
            id: 1,
            date: '2024-10-28',
            description: 'Client Payment - ABC Corp',
            amount: 15000,
            type: 'income',
            status: 'completed'
          },
          {
            id: 2,
            date: '2024-10-27',
            description: 'Office Rent',
            amount: 5000,
            type: 'expense',
            status: 'completed'
          },
          {
            id: 3,
            date: '2024-10-26',
            description: 'Software Licenses',
            amount: 1200,
            type: 'expense',
            status: 'completed'
          },
          {
            id: 4,
            date: '2024-10-25',
            description: 'Consulting Services',
            amount: 8500,
            type: 'income',
            status: 'completed'
          },
          {
            id: 5,
            date: '2024-10-24',
            description: 'Employee Salaries',
            amount: 35000,
            type: 'expense',
            status: 'completed'
          }
        ],
        upcomingInvoices: [
          {
            id: 1,
            invoiceNumber: 'INV-2024-045',
            customer: 'XYZ Industries',
            amount: 12500,
            dueDate: '2024-11-02',
            status: 'pending'
          },
          {
            id: 2,
            invoiceNumber: 'INV-2024-046',
            customer: 'Global Services Ltd',
            amount: 8750,
            dueDate: '2024-11-05',
            status: 'pending'
          },
          {
            id: 3,
            invoiceNumber: 'INV-2024-047',
            customer: 'Tech Solutions Inc',
            amount: 15000,
            dueDate: '2024-11-08',
            status: 'pending'
          }
        ],
        payrollSummary: {
          nextPayrollDate: '2024-11-01',
          totalGrossPay: 45000,
          totalNetPay: 36000,
          totalDeductions: 9000,
          employeeCount: 24
        },
        taxSummary: {
          nextFilingDate: '2024-11-20',
          totalVATCollected: 18750,
          totalVATPaid: 12500,
          netVATLiability: 6250,
          totalIncomeTaxWithheld: 9000,
          totalWithholdingTaxCollected: 3500
        },
        monthlyRevenue: [
          { month: 'Jan', revenue: 95000, expenses: 65000 },
          { month: 'Feb', revenue: 102000, expenses: 68000 },
          { month: 'Mar', revenue: 108000, expenses: 72000 },
          { month: 'Apr', revenue: 98000, expenses: 69000 },
          { month: 'May', revenue: 115000, expenses: 75000 },
          { month: 'Jun', revenue: 122000, expenses: 78000 },
          { month: 'Jul', revenue: 118000, expenses: 76000 },
          { month: 'Aug', revenue: 125000, expenses: 80000 },
          { month: 'Sep', revenue: 132000, expenses: 85000 },
          { month: 'Oct', revenue: 125000, expenses: 87500 }
        ],
        expenseBreakdown: [
          { category: 'Salaries', amount: 35000, percentage: 40 },
          { category: 'Rent', amount: 15000, percentage: 17 },
          { category: 'Marketing', amount: 12000, percentage: 14 },
          { category: 'Operations', amount: 10000, percentage: 11 },
          { category: 'Technology', amount: 8000, percentage: 9 },
          { category: 'Other', amount: 7500, percentage: 9 }
        ],
        accountBalances: [
          { account: 'Cash & Bank', balance: 45000, type: 'asset' },
          { account: 'Accounts Receivable', balance: 28000, type: 'asset' },
          { account: 'Inventory', balance: 15000, type: 'asset' },
          { account: 'Accounts Payable', balance: 12000, type: 'liability' },
          { account: 'Taxes Payable', balance: 4000, type: 'liability' },
          { account: 'Owner\'s Equity', balance: 72000, type: 'equity' }
        ]
      };

      setDashboardData(mockData);
    } catch (error) {
      console.error('Error fetching dashboard data:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleRefresh = async () => {
    setRefreshing(true);
    await fetchDashboardData();
    setRefreshing(false);
  };

  useEffect(() => {
    fetchDashboardData();
  }, []);

  const transactionColumns = [
    {
      title: 'Date',
      dataIndex: 'date',
      key: 'date',
      render: (date) => <Text>{new Date(date).toLocaleDateString()}</Text>
    },
    {
      title: 'Description',
      dataIndex: 'description',
      key: 'description'
    },
    {
      title: 'Amount',
      dataIndex: 'amount',
      key: 'amount',
      render: (amount, record) => (
        <Text strong style={{ 
          color: record.type === 'income' ? '#52c41a' : '#ff4d4f' 
        }}>
          {record.type === 'income' ? '+' : '-'}${amount.toLocaleString()}
        </Text>
      )
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status) => (
        <Tag color={status === 'completed' ? 'green' : 'orange'}>
          {status}
        </Tag>
      )
    }
  ];

  const invoiceColumns = [
    {
      title: 'Invoice #',
      dataIndex: 'invoiceNumber',
      key: 'invoiceNumber'
    },
    {
      title: 'Customer',
      dataIndex: 'customer',
      key: 'customer'
    },
    {
      title: 'Amount',
      dataIndex: 'amount',
      key: 'amount',
      render: (amount) => <Text strong>${amount.toLocaleString()}</Text>
    },
    {
      title: 'Due Date',
      dataIndex: 'dueDate',
      key: 'dueDate',
      render: (date) => <Text>{new Date(date).toLocaleDateString()}</Text>
    },
    {
      title: 'Action',
      key: 'action',
      render: (_, record) => (
        <Button 
          type="link" 
          icon={<EyeOutlined />}
          onClick={() => navigate(`/invoices/${record.id}`)}
        >
          View
        </Button>
      )
    }
  ];

  const pieColors = ['#0088FE', '#00C49F', '#FFBB28', '#FF8042', '#8884D8', '#82CA9D'];

  if (loading) {
    return (
      <div style={{ 
        display: 'flex', 
        justifyContent: 'center', 
        alignItems: 'center', 
        height: '400px' 
      }}>
        <Spin size="large" />
      </div>
    );
  }

  return (
    <div style={{ padding: '24px' }}>
      {/* Header */}
      <div style={{ 
        display: 'flex', 
        justifyContent: 'space-between', 
        alignItems: 'center', 
        marginBottom: '24px' 
      }}>
        <div>
          <Title level={2} style={{ margin: 0 }}>
            Dashboard Overview
          </Title>
          <Text type="secondary">
            Welcome back, {user?.username || 'User'}! Here's your business summary.
          </Text>
        </div>
        <Space>
          <Button 
            icon={<ReloadOutlined />} 
            loading={refreshing}
            onClick={handleRefresh}
          >
            Refresh
          </Button>
          <Button 
            icon={<DownloadOutlined />}
            onClick={() => console.log('Export dashboard data')}
          >
            Export
          </Button>
        </Space>
      </div>

      {/* Key Metrics */}
      <Row gutter={[16, 16]} style={{ marginBottom: '24px' }}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Total Revenue"
              value={dashboardData.summary?.totalRevenue}
              prefix={<DollarOutlined />}
              valueStyle={{ color: '#3f8600' }}
              formatter={(value) => `$${value?.toLocaleString()}`}
            />
            <div style={{ marginTop: '8px' }}>
              <Text type="secondary">
                <ArrowUpOutlined style={{ color: '#52c41a' }} /> 12.5% from last month
              </Text>
            </div>
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Total Expenses"
              value={dashboardData.summary?.totalExpenses}
              prefix={<DollarOutlined />}
              valueStyle={{ color: '#cf1322' }}
              formatter={(value) => `$${value?.toLocaleString()}`}
            />
            <div style={{ marginTop: '8px' }}>
              <Text type="secondary">
                <ArrowUpOutlined style={{ color: '#ff4d4f' }} /> 8.3% from last month
              </Text>
            </div>
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Net Profit"
              value={dashboardData.summary?.netProfit}
              prefix={<DollarOutlined />}
              valueStyle={{ color: '#3f8600' }}
              formatter={(value) => `$${value?.toLocaleString()}`}
            />
            <div style={{ marginTop: '8px' }}>
              <Text type="secondary">
                <ArrowUpOutlined style={{ color: '#52c41a' }} /> 18.2% from last month
              </Text>
            </div>
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Active Employees"
              value={dashboardData.summary?.activeEmployees}
              prefix={<UserOutlined />}
              suffix={`/ ${dashboardData.summary?.totalEmployees}`}
              valueStyle={{ color: '#1890ff' }}
            />
            <div style={{ marginTop: '8px' }}>
              <Progress 
                percent={(dashboardData.summary?.activeEmployees / dashboardData.summary?.totalEmployees) * 100} 
                size="small" 
                showInfo={false}
              />
            </div>
          </Card>
        </Col>
      </Row>

      {/* Charts Row */}
      <Row gutter={[16, 16]} style={{ marginBottom: '24px' }}>
        <Col xs={24} lg={16}>
          <Card title="Revenue vs Expenses Trend" extra={
            <Button type="text" icon={<MoreOutlined />} />
          }>
            <ResponsiveContainer width="100%" height={300}>
              <LineChart data={dashboardData.monthlyRevenue}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="month" />
                <YAxis />
                <RechartsTooltip />
                <Legend />
                <Line 
                  type="monotone" 
                  dataKey="revenue" 
                  stroke="#52c41a" 
                  strokeWidth={2}
                  name="Revenue"
                />
                <Line 
                  type="monotone" 
                  dataKey="expenses" 
                  stroke="#ff4d4f" 
                  strokeWidth={2}
                  name="Expenses"
                />
              </LineChart>
            </ResponsiveContainer>
          </Card>
        </Col>
        <Col xs={24} lg={8}>
          <Card title="Expense Breakdown" extra={
            <Button type="text" icon={<MoreOutlined />} />
          }>
            <ResponsiveContainer width="100%" height={300}>
              <PieChart>
                <Pie
                  data={dashboardData.expenseBreakdown}
                  cx="50%"
                  cy="50%"
                  labelLine={false}
                  label={({ name, percentage }) => `${name} ${percentage}%`}
                  outerRadius={80}
                  fill="#8884d8"
                  dataKey="amount"
                >
                  {dashboardData.expenseBreakdown.map((entry, index) => (
                    <Cell key={`cell-${index}`} fill={pieColors[index % pieColors.length]} />
                  ))}
                </Pie>
                <RechartsTooltip />
              </PieChart>
            </ResponsiveContainer>
          </Card>
        </Col>
      </Row>

      {/* Tables Row */}
      <Row gutter={[16, 16]} style={{ marginBottom: '24px' }}>
        <Col xs={24} lg={12}>
          <Card 
            title="Recent Transactions" 
            extra={
              <Button 
                type="text" 
                onClick={() => navigate('/finance/journal')}
              >
                View All
              </Button>
            }
          >
            <Table
              dataSource={dashboardData.recentTransactions}
              columns={transactionColumns}
              pagination={false}
              size="small"
            />
          </Card>
        </Col>
        <Col xs={24} lg={12}>
          <Card 
            title="Upcoming Invoices" 
            extra={
              <Button 
                type="text" 
                onClick={() => navigate('/invoices')}
              >
                View All
              </Button>
            }
          >
            <Table
              dataSource={dashboardData.upcomingInvoices}
              columns={invoiceColumns}
              pagination={false}
              size="small"
            />
          </Card>
        </Col>
      </Row>

      {/* Bottom Cards */}
      <Row gutter={[16, 16]}>
        <Col xs={24} lg={8}>
          <Card title="Payroll Summary" extra={
            <Button 
              type="text" 
              onClick={() => navigate('/payroll')}
            >
              Details
            </Button>
          }>
            <div style={{ marginBottom: '16px' }}>
              <Text strong>Next Payroll Date:</Text>
              <Text style={{ marginLeft: '8px' }}>
                {new Date(dashboardData.payrollSummary?.nextPayrollDate).toLocaleDateString()}
              </Text>
            </div>
            <Row gutter={16}>
              <Col span={8}>
                <Statistic
                  title="Gross Pay"
                  value={dashboardData.payrollSummary?.totalGrossPay}
                  prefix={<DollarOutlined />}
                  precision={0}
                />
              </Col>
              <Col span={8}>
                <Statistic
                  title="Net Pay"
                  value={dashboardData.payrollSummary?.totalNetPay}
                  prefix={<DollarOutlined />}
                  precision={0}
                  valueStyle={{ color: '#3f8600' }}
                />
              </Col>
              <Col span={8}>
                <Statistic
                  title="Deductions"
                  value={dashboardData.payrollSummary?.totalDeductions}
                  prefix={<DollarOutlined />}
                  precision={0}
                  valueStyle={{ color: '#ff4d4f' }}
                />
              </Col>
            </Row>
          </Card>
        </Col>
        <Col xs={24} lg={8}>
          <Card title="Tax Summary" extra={
            <Button 
              type="text" 
              onClick={() => navigate('/tax')}
            >
              Details
            </Button>
          }>
            <div style={{ marginBottom: '16px' }}>
              <Text strong>Next Filing Date:</Text>
              <Text style={{ marginLeft: '8px' }}>
                {new Date(dashboardData.taxSummary?.nextFilingDate).toLocaleDateString()}
              </Text>
            </div>
            <Row gutter={16}>
              <Col span={12}>
                <Statistic
                  title="VAT Collected"
                  value={dashboardData.taxSummary?.totalVATCollected}
                  prefix={<DollarOutlined />}
                  precision={0}
                />
              </Col>
              <Col span={12}>
                <Statistic
                  title="VAT Paid"
                  value={dashboardData.taxSummary?.totalVATPaid}
                  prefix={<DollarOutlined />}
                  precision={0}
                  valueStyle={{ color: '#ff4d4f' }}
                />
              </Col>
              <Col span={24} style={{ marginTop: '8px' }}>
                <Statistic
                  title="Net VAT Liability"
                  value={dashboardData.taxSummary?.netVATLiability}
                  prefix={<DollarOutlined />}
                  precision={0}
                  valueStyle={{ color: '#fa8c16' }}
                />
              </Col>
            </Row>
          </Card>
        </Col>
        <Col xs={24} lg={8}>
          <Card title="Account Balances" extra={
            <Button 
              type="text" 
              onClick={() => navigate('/finance/accounts')}
            >
              Details
            </Button>
          }>
            <List
              dataSource={dashboardData.accountBalances}
              renderItem={(item) => (
                <List.Item>
                  <div style={{ display: 'flex', justifyContent: 'space-between', width: '100%' }}>
                    <Text>{item.account}</Text>
                    <Text strong style={{ 
                      color: item.type === 'asset' ? '#52c41a' : 
                             item.type === 'liability' ? '#ff4d4f' : '#1890ff'
                    }}>
                      ${item.balance.toLocaleString()}
                    </Text>
                  </div>
                </List.Item>
              )}
            />
          </Card>
        </Col>
      </Row>
    </div>
  );
};

export default EnhancedDashboard;
