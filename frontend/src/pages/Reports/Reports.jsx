import React, { useState, useEffect } from 'react';
import {
  Card,
  Tabs,
  Table,
  DatePicker,
  Button,
  Space,
  Typography,
  Spin,
  Alert,
  Row,
  Col,
  Statistic,
} from 'antd';
import {
  FileTextOutlined,
  BarChartOutlined,
  AccountBookOutlined,
  DownloadOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import { PieChart, Pie, Cell, ResponsiveContainer, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend } from 'recharts';
import dayjs from 'dayjs';
import api from '../../api/axios';

const { Title } = Typography;
const { RangePicker } = DatePicker;
const { TabPane } = Tabs;

const Reports = () => {
  const [loading, setLoading] = useState(false);
  const [dateRange, setDateRange] = useState([
    dayjs().startOf('month'),
    dayjs().endOf('month'),
  ]);
  const [trialBalance, setTrialBalance] = useState([]);
  const [incomeStatement, setIncomeStatement] = useState(null);
  const [balanceSheet, setBalanceSheet] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    fetchAllReports();
  }, [dateRange]);

  const fetchAllReports = async () => {
    setLoading(true);
    setError(null);
    
    try {
      const [trialBalanceRes, incomeStatementRes, balanceSheetRes] = await Promise.all([
        api.get('/api/finance/trial-balance', {
          params: {
            startDate: dateRange[0].format('YYYY-MM-DD'),
            endDate: dateRange[1].format('YYYY-MM-DD'),
          },
        }),
        api.get('/api/finance/income-statement', {
          params: {
            startDate: dateRange[0].format('YYYY-MM-DD'),
            endDate: dateRange[1].format('YYYY-MM-DD'),
          },
        }),
        api.get('/api/finance/balance-sheet', {
          params: {
            asOfDate: dateRange[1].format('YYYY-MM-DD'),
          },
        }),
      ]);

      setTrialBalance(trialBalanceRes.data.accounts || []);
      setIncomeStatement(incomeStatementRes.data);
      setBalanceSheet(balanceSheetRes.data);
    } catch (err) {
      console.error('Error fetching reports:', err);
      setError('Failed to load reports. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const trialBalanceColumns = [
    {
      title: 'Account Code',
      dataIndex: 'accountCode',
      key: 'accountCode',
      width: 120,
    },
    {
      title: 'Account Name',
      dataIndex: 'accountName',
      key: 'accountName',
    },
    {
      title: 'Account Type',
      dataIndex: 'accountType',
      key: 'accountType',
      width: 120,
    },
    {
      title: 'Debit',
      dataIndex: 'balance',
      key: 'debit',
      width: 120,
      render: (balance, record) => 
        record.normalBalance === 'Debit' ? (
          <span style={{ color: '#3f8600' }}>
            ${balance.toFixed(2)}
          </span>
        ) : (
          <span>-</span>
        ),
    },
    {
      title: 'Credit',
      dataIndex: 'balance',
      key: 'credit',
      width: 120,
      render: (balance, record) => 
        record.normalBalance === 'Credit' ? (
          <span style={{ color: '#cf1322' }}>
            ${balance.toFixed(2)}
          </span>
        ) : (
          <span>-</span>
        ),
    },
  ];

  const incomeStatementColumns = [
    {
      title: 'Account',
      dataIndex: 'accountName',
      key: 'accountName',
    },
    {
      title: 'Amount',
      dataIndex: 'amount',
      key: 'amount',
      render: (amount) => `$${amount.toFixed(2)}`,
      align: 'right',
    },
  ];

  const balanceSheetColumns = [
    {
      title: 'Account',
      dataIndex: 'accountName',
      key: 'accountName',
    },
    {
      title: 'Amount',
      dataIndex: 'amount',
      key: 'amount',
      render: (amount) => `$${amount.toFixed(2)}`,
      align: 'right',
    },
  ];

  const renderIncomeStatementChart = () => {
    if (!incomeStatement) return null;

    const data = [
      { name: 'Revenue', value: incomeStatement.revenue?.totalRevenue || 0, color: '#3f8600' },
      { name: 'Expenses', value: incomeStatement.expenses?.totalExpenses || 0, color: '#cf1322' },
      { name: 'Net Profit', value: incomeStatement.profitSummary?.netProfit || 0, color: '#1890ff' },
    ];

    return (
      <ResponsiveContainer width="100%" height={300}>
        <PieChart>
          <Pie
            data={data}
            cx="50%"
            cy="50%"
            labelLine={false}
            label={({ name, value }) => `${name}: $${value.toFixed(2)}`}
            outerRadius={80}
            fill="#8884d8"
            dataKey="value"
          >
            {data.map((entry, index) => (
              <Cell key={`cell-${index}`} fill={entry.color} />
            ))}
          </Pie>
          <Tooltip formatter={(value) => `$${value.toFixed(2)}`} />
        </PieChart>
      </ResponsiveContainer>
    );
  };

  const renderBalanceSheetChart = () => {
    if (!balanceSheet) return null;

    const data = [
      { name: 'Assets', value: balanceSheet.assets?.total || 0, color: '#1890ff' },
      { name: 'Liabilities', value: balanceSheet.liabilities?.total || 0, color: '#faad14' },
      { name: 'Equity', value: balanceSheet.equity?.total || 0, color: '#52c41a' },
    ];

    return (
      <ResponsiveContainer width="100%" height={300}>
        <BarChart data={data}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey="name" />
          <YAxis />
          <Tooltip formatter={(value) => `$${value.toFixed(2)}`} />
          <Bar dataKey="value" fill="#1890ff">
            {data.map((entry, index) => (
              <Bar key={`bar-${index}`} fill={entry.color} />
            ))}
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    );
  };

  const calculateTrialBalanceTotals = () => {
    const totalDebit = trialBalance
      .filter(account => account.normalBalance === 'Debit')
      .reduce((sum, account) => sum + account.balance, 0);
    
    const totalCredit = trialBalance
      .filter(account => account.normalBalance === 'Credit')
      .reduce((sum, account) => sum + account.balance, 0);

    return { totalDebit, totalCredit };
  };

  const totals = calculateTrialBalanceTotals();

  return (
    <div>
      <Title level={2}>Financial Reports</Title>

      {/* Date Range Filter */}
      <Card style={{ marginBottom: 24 }}>
        <Space>
          <span>Date Range:</span>
          <RangePicker
            value={dateRange}
            onChange={setDateRange}
            format="YYYY-MM-DD"
          />
          <Button
            type="primary"
            icon={<ReloadOutlined />}
            onClick={fetchAllReports}
            loading={loading}
          >
            Refresh
          </Button>
          <Button
            icon={<DownloadOutlined />}
            disabled={loading}
          >
            Export
          </Button>
        </Space>
      </Card>

      {error && (
        <Alert
          message="Error"
          description={error}
          type="error"
          showIcon
          style={{ marginBottom: 24 }}
        />
      )}

      <Spin spinning={loading}>
        <Tabs defaultActiveKey="trial-balance">
          {/* Trial Balance Tab */}
          <TabPane
            tab={
              <span>
                <FileTextOutlined />
                Trial Balance
              </span>
            }
            key="trial-balance"
          >
            <Card>
              <Row gutter={16} style={{ marginBottom: 24 }}>
                <Col span={8}>
                  <Statistic
                    title="Total Debit"
                    value={totals.totalDebit}
                    precision={2}
                    valueStyle={{ color: '#3f8600' }}
                    prefix="$"
                  />
                </Col>
                <Col span={8}>
                  <Statistic
                    title="Total Credit"
                    value={totals.totalCredit}
                    precision={2}
                    valueStyle={{ color: '#cf1322' }}
                    prefix="$"
                  />
                </Col>
                <Col span={8}>
                  <Statistic
                    title="Difference"
                    value={Math.abs(totals.totalDebit - totals.totalCredit)}
                    precision={2}
                    valueStyle={{ color: '#1890ff' }}
                    prefix="$"
                  />
                </Col>
              </Row>

              <Table
                columns={trialBalanceColumns}
                dataSource={trialBalance}
                rowKey="id"
                pagination={{
                  pageSize: 20,
                  showSizeChanger: true,
                  showQuickJumper: true,
                }}
                summary={() => (
                  <Table.Summary.Row>
                    <Table.Summary.Cell index={0} colSpan={3}>
                      <strong>Totals</strong>
                    </Table.Summary.Cell>
                    <Table.Summary.Cell index={3}>
                      <strong style={{ color: '#3f8600' }}>
                        ${totals.totalDebit.toFixed(2)}
                      </strong>
                    </Table.Summary.Cell>
                    <Table.Summary.Cell index={4}>
                      <strong style={{ color: '#cf1322' }}>
                        ${totals.totalCredit.toFixed(2)}
                      </strong>
                    </Table.Summary.Cell>
                  </Table.Summary.Row>
                )}
              />
            </Card>
          </TabPane>

          {/* Income Statement Tab */}
          <TabPane
            tab={
              <span>
                <BarChartOutlined />
                Income Statement
              </span>
            }
            key="income-statement"
          >
            <Row gutter={16}>
              <Col span={16}>
                <Card title="Income Statement Details">
                  <Row gutter={16} style={{ marginBottom: 24 }}>
                    <Col span={8}>
                      <Statistic
                        title="Total Revenue"
                        value={incomeStatement?.revenue?.totalRevenue || 0}
                        precision={2}
                        valueStyle={{ color: '#3f8600' }}
                        prefix="$"
                      />
                    </Col>
                    <Col span={8}>
                      <Statistic
                        title="Total Expenses"
                        value={incomeStatement?.expenses?.totalExpenses || 0}
                        precision={2}
                        valueStyle={{ color: '#cf1322' }}
                        prefix="$"
                      />
                    </Col>
                    <Col span={8}>
                      <Statistic
                        title="Net Profit"
                        value={incomeStatement?.profitSummary?.netProfit || 0}
                        precision={2}
                        valueStyle={{ 
                          color: (incomeStatement?.profitSummary?.netProfit || 0) >= 0 ? '#3f8600' : '#cf1322' 
                        }}
                        prefix="$"
                      />
                    </Col>
                  </Row>

                  <Tabs defaultActiveKey="revenue">
                    <TabPane tab="Revenue" key="revenue">
                      <Table
                        columns={incomeStatementColumns}
                        dataSource={incomeStatement?.revenue?.accounts || []}
                        pagination={false}
                        size="small"
                      />
                    </TabPane>
                    <TabPane tab="Expenses" key="expenses">
                      <Table
                        columns={incomeStatementColumns}
                        dataSource={incomeStatement?.expenses?.accounts || []}
                        pagination={false}
                        size="small"
                      />
                    </TabPane>
                  </Tabs>
                </Card>
              </Col>
              
              <Col span={8}>
                <Card title="Income Statement Chart">
                  {renderIncomeStatementChart()}
                </Card>
              </Col>
            </Row>
          </TabPane>

          {/* Balance Sheet Tab */}
          <TabPane
            tab={
              <span>
                <AccountBookOutlined />
                Balance Sheet
              </span>
            }
            key="balance-sheet"
          >
            <Row gutter={16}>
              <Col span={16}>
                <Card title="Balance Sheet Details">
                  <Row gutter={16} style={{ marginBottom: 24 }}>
                    <Col span={8}>
                      <Statistic
                        title="Total Assets"
                        value={balanceSheet?.assets?.total || 0}
                        precision={2}
                        valueStyle={{ color: '#1890ff' }}
                        prefix="$"
                      />
                    </Col>
                    <Col span={8}>
                      <Statistic
                        title="Total Liabilities"
                        value={balanceSheet?.liabilities?.total || 0}
                        precision={2}
                        valueStyle={{ color: '#faad14' }}
                        prefix="$"
                      />
                    </Col>
                    <Col span={8}>
                      <Statistic
                        title="Total Equity"
                        value={balanceSheet?.equity?.total || 0}
                        precision={2}
                        valueStyle={{ color: '#52c41a' }}
                        prefix="$"
                      />
                    </Col>
                  </Row>

                  <Tabs defaultActiveKey="assets">
                    <TabPane tab="Assets" key="assets">
                      <Table
                        columns={balanceSheetColumns}
                        dataSource={balanceSheet?.assets?.accounts || []}
                        pagination={false}
                        size="small"
                      />
                    </TabPane>
                    <TabPane tab="Liabilities" key="liabilities">
                      <Table
                        columns={balanceSheetColumns}
                        dataSource={balanceSheet?.liabilities?.accounts || []}
                        pagination={false}
                        size="small"
                      />
                    </TabPane>
                    <TabPane tab="Equity" key="equity">
                      <Table
                        columns={balanceSheetColumns}
                        dataSource={balanceSheet?.equity?.accounts || []}
                        pagination={false}
                        size="small"
                      />
                    </TabPane>
                  </Tabs>
                </Card>
              </Col>
              
              <Col span={8}>
                <Card title="Balance Sheet Chart">
                  {renderBalanceSheetChart()}
                </Card>
              </Col>
            </Row>
          </TabPane>
        </Tabs>
      </Spin>
    </div>
  );
};

export default Reports;
