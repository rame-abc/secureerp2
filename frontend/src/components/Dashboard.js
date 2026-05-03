import React, { useState, useEffect } from 'react';
import { Card, Row, Col, Statistic, Table, Tag, Button, Space } from 'antd';
import { 
  DollarOutlined, 
  FileTextOutlined, 
  TrendingUpOutlined,
  EyeOutlined,
  EditOutlined
} from '@ant-design/icons';
import axios from 'axios';

const Dashboard = () => {
  const [data, setData] = useState({
    totalAccounts: 0,
    totalJournals: 0,
    totalRevenue: 0,
    totalExpenses: 0,
    recentJournals: []
  });
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchDashboardData();
  }, []);

  const fetchDashboardData = async () => {
    setLoading(true);
    try {
      const token = localStorage.getItem('token');
      const headers = { 'Authorization': `Bearer ${token}` };

      // Fetch accounts
      const accountsResponse = await axios.get('/api/finance/accounts', { headers });
      const totalAccounts = accountsResponse.data.length;

      // Fetch recent journals
      const journalsResponse = await axios.get('/api/finance/journal', { headers });
      const recentJournals = journalsResponse.data.slice(0, 5);

      // Fetch income statement for totals
      const incomeResponse = await axios.get('/api/finance/income-statement', { headers });
      const { revenue, expenses, profitSummary } = incomeResponse.data;

      setData({
        totalAccounts,
        totalJournals: journalsResponse.data.length,
        totalRevenue: revenue?.totalRevenue || 0,
        totalExpenses: expenses?.totalExpenses || 0,
        recentJournals: recentJournals.map(journal => ({
          key: journal.id,
          transactionNumber: journal.transactionNumber,
          description: journal.description,
          status: journal.status,
          amount: journal.totalAmount,
          date: new Date(journal.transactionDate).toLocaleDateString()
        }))
      });
    } catch (error) {
      console.error('Failed to fetch dashboard data:', error);
    } finally {
      setLoading(false);
    }
  };

  const journalColumns = [
    {
      title: 'Transaction #',
      dataIndex: 'transactionNumber',
      key: 'transactionNumber',
    },
    {
      title: 'Description',
      dataIndex: 'description',
      key: 'description',
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status) => (
        <Tag color={status === 'Posted' ? 'green' : status === 'Draft' ? 'orange' : 'red'}>
          {status}
        </Tag>
      ),
    },
    {
      title: 'Amount',
      dataIndex: 'amount',
      key: 'amount',
      render: (amount) => `$${amount.toFixed(2)}`,
    },
    {
      title: 'Date',
      dataIndex: 'date',
      key: 'date',
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, record) => (
        <Space size="middle">
          <Button type="text" icon={<EyeOutlined />} size="small" />
          {record.status === 'Draft' && (
            <Button type="text" icon={<EditOutlined />} size="small" />
          )}
        </Space>
      ),
    },
  ];

  const netProfit = data.totalRevenue - data.totalExpenses;

  return (
    <div>
      <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
        <Col xs={24} sm={12} md={6}>
          <Card>
            <Statistic
              title="Total Accounts"
              value={data.totalAccounts}
              prefix={<DollarOutlined />}
              loading={loading}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card>
            <Statistic
              title="Total Journals"
              value={data.totalJournals}
              prefix={<FileTextOutlined />}
              loading={loading}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card>
            <Statistic
              title="Total Revenue"
              value={data.totalRevenue}
              prefix={<TrendingUpOutlined />}
              precision={2}
              valueStyle={{ color: '#3f8600' }}
              loading={loading}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card>
            <Statistic
              title="Net Profit"
              value={netProfit}
              prefix={<DollarOutlined />}
              precision={2}
              valueStyle={{ color: netProfit >= 0 ? '#3f8600' : '#cf1322' }}
              loading={loading}
            />
          </Card>
        </Col>
      </Row>

      <Card title="Recent Journal Entries" loading={loading}>
        <Table
          columns={journalColumns}
          dataSource={data.recentJournals}
          pagination={false}
          size="small"
        />
      </Card>
    </div>
  );
};

export default Dashboard;
