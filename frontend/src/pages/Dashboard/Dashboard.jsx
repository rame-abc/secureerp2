import React, { useState, useEffect } from 'react';
import { Card, Row, Col, Statistic, Typography, Table, Spin, Alert } from 'antd';
import {
  DollarOutlined,
  CreditCardOutlined,
  RiseOutlined,
  BankOutlined,
  FileTextOutlined,
} from '@ant-design/icons';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, BarChart, Bar } from 'recharts';
import api from '../../api/axios';

const { Title } = Typography;

const Dashboard = () => {
  const [loading, setLoading] = useState(true);
  const [data, setData] = useState({
    totalRevenue: 0,
    totalExpenses: 0,
    profit: 0,
    activeCompanies: 0,
    journalCount: 0,
    recentTransactions: [],
    monthlyData: [],
  });
  const [error, setError] = useState(null);

  useEffect(() => {
    fetchDashboardData();
  }, []);

  const fetchDashboardData = async () => {
    try {
      setLoading(true);
      setError(null);
      
      // Fetch dashboard metrics
      const [incomeStatement, journals, companies] = await Promise.all([
        api.get('/api/finance/income-statement'),
        api.get('/api/finance/journals'),
        api.get('/api/companies'),
      ]);

      const revenue = incomeStatement.data.revenue?.totalRevenue || 0;
      const expenses = incomeStatement.data.expenses?.totalExpenses || 0;
      const profit = revenue - expenses;

      // Generate sample monthly data (in real app, this would come from API)
      const monthlyData = [
        { month: 'Jan', revenue: 4000, expenses: 2400 },
        { month: 'Feb', revenue: 3000, expenses: 1398 },
        { month: 'Mar', revenue: 2000, expenses: 9800 },
        { month: 'Apr', revenue: 2780, expenses: 3908 },
        { month: 'May', revenue: 1890, expenses: 4800 },
        { month: 'Jun', revenue: 2390, expenses: 3800 },
      ];

      // Sample recent transactions (in real app, this would come from API)
      const recentTransactions = journals.data.slice(0, 5).map(journal => ({
        key: journal.id,
        date: new Date(journal.createdAt).toLocaleDateString(),
        description: journal.description,
        amount: journal.entries?.reduce((sum, entry) => sum + entry.debit, 0) || 0,
        status: journal.status,
      }));

      setData({
        totalRevenue: revenue,
        totalExpenses: expenses,
        profit: profit,
        activeCompanies: companies.data.length || 1,
        journalCount: journals.data.length || 0,
        recentTransactions,
        monthlyData,
      });
    } catch (err) {
      console.error('Error fetching dashboard data:', err);
      setError('Failed to load dashboard data. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const transactionColumns = [
    {
      title: 'Date',
      dataIndex: 'date',
      key: 'date',
    },
    {
      title: 'Description',
      dataIndex: 'description',
      key: 'description',
    },
    {
      title: 'Amount',
      dataIndex: 'amount',
      key: 'amount',
      render: (amount) => `$${amount.toFixed(2)}`,
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status) => (
        <span style={{ 
          color: status === 'Posted' ? '#52c41a' : '#faad14',
          fontWeight: 'bold'
        }}>
          {status}
        </span>
      ),
    },
  ];

  if (loading) {
    return (
      <div style={{ textAlign: 'center', padding: '50px' }}>
        <Spin size="large" />
      </div>
    );
  }

  if (error) {
    return (
      <Alert 
        message="Error" 
        description={error} 
        type="error" 
        showIcon 
        style={{ marginBottom: '16px' }}
      />
    );
  }

  return (
    <div>
      <Title level={2}>Dashboard</Title>
      
      {/* KPI Cards */}
      <Row gutter={[16, 16]} style={{ marginBottom: '24px' }}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Total Revenue"
              value={data.totalRevenue}
              precision={2}
              valueStyle={{ color: '#3f8600' }}
              prefix={<DollarOutlined />}
              formatter={(value) => `$${value}`}
            />
          </Card>
        </Col>
        
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Total Expenses"
              value={data.totalExpenses}
              precision={2}
              valueStyle={{ color: '#cf1322' }}
              prefix={<CreditCardOutlined />}
              formatter={(value) => `$${value}`}
            />
          </Card>
        </Col>
        
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Net Profit"
              value={data.profit}
              precision={2}
              valueStyle={{ color: data.profit >= 0 ? '#3f8600' : '#cf1322' }}
              prefix={<RiseOutlined />}
              formatter={(value) => `$${value}`}
            />
          </Card>
        </Col>
        
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Active Companies"
              value={data.activeCompanies}
              valueStyle={{ color: '#1890ff' }}
              prefix={<BankOutlined />}
            />
          </Card>
        </Col>
      </Row>

      {/* Charts */}
      <Row gutter={[16, 16]} style={{ marginBottom: '24px' }}>
        <Col xs={24} lg={16}>
          <Card title="Revenue vs Expenses Trend">
            <ResponsiveContainer width="100%" height={300}>
              <LineChart data={data.monthlyData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="month" />
                <YAxis />
                <Tooltip formatter={(value) => `$${value}`} />
                <Line 
                  type="monotone" 
                  dataKey="revenue" 
                  stroke="#3f8600" 
                  strokeWidth={2}
                  name="Revenue"
                />
                <Line 
                  type="monotone" 
                  dataKey="expenses" 
                  stroke="#cf1322" 
                  strokeWidth={2}
                  name="Expenses"
                />
              </LineChart>
            </ResponsiveContainer>
          </Card>
        </Col>
        
        <Col xs={24} lg={8}>
          <Card title="Monthly Comparison">
            <ResponsiveContainer width="100%" height={300}>
              <BarChart data={data.monthlyData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="month" />
                <YAxis />
                <Tooltip formatter={(value) => `$${value}`} />
                <Bar dataKey="revenue" fill="#3f8600" name="Revenue" />
                <Bar dataKey="expenses" fill="#cf1322" name="Expenses" />
              </BarChart>
            </ResponsiveContainer>
          </Card>
        </Col>
      </Row>

      {/* Recent Transactions */}
      <Card 
        title={
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <FileTextOutlined />
            Recent Transactions
          </div>
        }
      >
        <Table 
          columns={transactionColumns} 
          dataSource={data.recentTransactions} 
          pagination={false}
          size="small"
        />
      </Card>
    </div>
  );
};

export default Dashboard;
