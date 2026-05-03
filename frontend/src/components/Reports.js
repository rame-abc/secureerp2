import React, { useState, useEffect } from 'react';
import { 
  Card, 
  Row, 
  Col, 
  Select, 
  DatePicker, 
  Table, 
  Statistic, 
  Button,
  Space,
  Tabs,
  Tag
} from 'antd';
import { 
  BarChartOutlined, 
  PieChartOutlined, 
  FileTextOutlined,
  DownloadOutlined 
} from '@ant-design/icons';
import axios from 'axios';
import moment from 'moment';

const { RangePicker } = DatePicker;
const { TabPane } = Tabs;

const Reports = () => {
  const [loading, setLoading] = useState(false);
  const [dateRange, setDateRange] = useState([
    moment().startOf('month'),
    moment().endOf('month')
  ]);
  const [trialBalance, setTrialBalance] = useState([]);
  const [incomeStatement, setIncomeStatement] = useState(null);
  const [balanceSheet, setBalanceSheet] = useState(null);

  useEffect(() => {
    fetchAllReports();
  }, [dateRange]);

  const fetchAllReports = async () => {
    setLoading(true);
    try {
      const token = localStorage.getItem('token');
      const headers = { 'Authorization': `Bearer ${token}` };
      const [from, to] = dateRange;

      // Fetch all reports
      const [trialResponse, incomeResponse, balanceResponse] = await Promise.all([
        axios.get('/api/finance/trial-balance', { 
          headers,
          params: { from: from.format('YYYY-MM-DD'), to: to.format('YYYY-MM-DD') }
        }),
        axios.get('/api/finance/income-statement', { 
          headers,
          params: { from: from.format('YYYY-MM-DD'), to: to.format('YYYY-MM-DD') }
        }),
        axios.get('/api/finance/balance-sheet', { 
          headers,
          params: { from: from.format('YYYY-MM-DD'), to: to.format('YYYY-MM-DD') }
        })
      ]);

      setTrialBalance(trialResponse.data.accounts || []);
      setIncomeStatement(incomeResponse.data);
      setBalanceSheet(balanceResponse.data);
    } catch (error) {
      console.error('Failed to fetch reports:', error);
    } finally {
      setLoading(false);
    }
  };

  const trialBalanceColumns = [
    {
      title: 'Account',
      dataIndex: 'account',
      key: 'account',
    },
    {
      title: 'Account Code',
      dataIndex: 'accountCode',
      key: 'accountCode',
    },
    {
      title: 'Type',
      dataIndex: 'accountType',
      key: 'accountType',
      render: (type) => <Tag color="blue">{type}</Tag>,
    },
    {
      title: 'Debit',
      dataIndex: 'debit',
      key: 'debit',
      align: 'right',
      render: (debit) => debit > 0 ? `$${debit.toFixed(2)}` : '-',
    },
    {
      title: 'Credit',
      dataIndex: 'credit',
      key: 'credit',
      align: 'right',
      render: (credit) => credit > 0 ? `$${credit.toFixed(2)}` : '-',
    },
    {
      title: 'Balance',
      dataIndex: 'balance',
      key: 'balance',
      align: 'right',
      render: (balance) => `$${Math.abs(balance).toFixed(2)}`,
    },
  ];

  return (
    <div>
      <Card style={{ marginBottom: 16 }}>
        <Row gutter={[16, 16]} align="middle">
          <Col>
            <label style={{ marginRight: 8 }}>Date Range:</label>
            <RangePicker
              value={dateRange}
              onChange={setDateRange}
              format="YYYY-MM-DD"
            />
          </Col>
          <Col>
            <Button 
              type="primary" 
              icon={<BarChartOutlined />}
              onClick={fetchAllReports}
              loading={loading}
            >
              Refresh Reports
            </Button>
          </Col>
        </Row>
      </Card>

      <Tabs defaultActiveKey="1">
        <TabPane tab={<span><BarChartOutlined />Trial Balance</span>} key="1">
          <Card title="Trial Balance" loading={loading}>
            <Table
              columns={trialBalanceColumns}
              dataSource={trialBalance}
              pagination={false}
              size="small"
              summary={(pageData) => {
                const totalDebit = pageData.reduce((sum, item) => sum + item.debit, 0);
                const totalCredit = pageData.reduce((sum, item) => sum + item.credit, 0);
                return (
                  <Table.Summary.Row>
                    <Table.Summary.Cell index={0} colSpan={3}>
                      <strong>Total</strong>
                    </Table.Summary.Cell>
                    <Table.Summary.Cell index={3}>
                      <strong>${totalDebit.toFixed(2)}</strong>
                    </Table.Summary.Cell>
                    <Table.Summary.Cell index={4}>
                      <strong>${totalCredit.toFixed(2)}</strong>
                    </Table.Summary.Cell>
                    <Table.Summary.Cell index={5}></Table.Summary.Cell>
                  </Table.Summary.Row>
                );
              }}
            />
          </Card>
        </TabPane>

        <TabPane tab={<span><PieChartOutlined />Income Statement</span>} key="2">
          {incomeStatement && (
            <Row gutter={[16, 16]}>
              <Col xs={24} md={12}>
                <Card title="Revenue" loading={loading}>
                  <Statistic
                    title="Total Revenue"
                    value={incomeStatement.revenue?.totalRevenue || 0}
                    precision={2}
                    valueStyle={{ color: '#3f8600' }}
                    prefix="$"
                  />
                  <div style={{ marginTop: 16 }}>
                    <h4>Revenue Breakdown:</h4>
                    {incomeStatement.revenue?.revenueAccounts?.map(account => (
                      <div key={account.accountCode} style={{ display: 'flex', justifyContent: 'space-between' }}>
                        <span>{account.account}</span>
                        <span>${account.balance.toFixed(2)}</span>
                      </div>
                    ))}
                  </div>
                </Card>
              </Col>
              <Col xs={24} md={12}>
                <Card title="Expenses" loading={loading}>
                  <Statistic
                    title="Total Expenses"
                    value={incomeStatement.expenses?.totalExpenses || 0}
                    precision={2}
                    valueStyle={{ color: '#cf1322' }}
                    prefix="$"
                  />
                  <div style={{ marginTop: 16 }}>
                    <h4>Expense Breakdown:</h4>
                    {incomeStatement.expenses?.expenseAccounts?.map(account => (
                      <div key={account.accountCode} style={{ display: 'flex', justifyContent: 'space-between' }}>
                        <span>{account.account}</span>
                        <span>${account.balance.toFixed(2)}</span>
                      </div>
                    ))}
                  </div>
                </Card>
              </Col>
              <Col xs={24}>
                <Card title="Profit Summary" loading={loading}>
                  <Row gutter={[16, 16]}>
                    <Col xs={24} md={8}>
                      <Statistic
                        title="Gross Profit"
                        value={incomeStatement.profitSummary?.grossProfit || 0}
                        precision={2}
                        valueStyle={{ color: '#3f8600' }}
                        prefix="$"
                      />
                    </Col>
                    <Col xs={24} md={8}>
                      <Statistic
                        title="Net Profit"
                        value={incomeStatement.profitSummary?.netProfit || 0}
                        precision={2}
                        valueStyle={{ 
                          color: (incomeStatement.profitSummary?.netProfit || 0) >= 0 ? '#3f8600' : '#cf1322' 
                        }}
                        prefix="$"
                      />
                    </Col>
                    <Col xs={24} md={8}>
                      <Statistic
                        title="Profit Margin"
                        value={incomeStatement.profitSummary?.profitMargin || 0}
                        precision={2}
                        suffix="%"
                        valueStyle={{ 
                          color: (incomeStatement.profitSummary?.profitMargin || 0) >= 0 ? '#3f8600' : '#cf1322' 
                        }}
                      />
                    </Col>
                  </Row>
                </Card>
              </Col>
            </Row>
          )}
        </TabPane>

        <TabPane tab={<span><FileTextOutlined />Balance Sheet</span>} key="3">
          {balanceSheet && (
            <Row gutter={[16, 16]}>
              <Col xs={24} md={8}>
                <Card title="Assets" loading={loading}>
                  <Statistic
                    title="Total Assets"
                    value={balanceSheet.assets?.totalAssets || 0}
                    precision={2}
                    valueStyle={{ color: '#3f8600' }}
                    prefix="$"
                  />
                  <div style={{ marginTop: 16 }}>
                    <h4>Current Assets:</h4>
                    {balanceSheet.assets?.currentAssets?.map(account => (
                      <div key={account.accountCode} style={{ display: 'flex', justifyContent: 'space-between' }}>
                        <span>{account.account}</span>
                        <span>${account.balance.toFixed(2)}</span>
                      </div>
                    ))}
                  </div>
                </Card>
              </Col>
              <Col xs={24} md={8}>
                <Card title="Liabilities" loading={loading}>
                  <Statistic
                    title="Total Liabilities"
                    value={balanceSheet.liabilities?.totalLiabilities || 0}
                    precision={2}
                    valueStyle={{ color: '#cf1322' }}
                    prefix="$"
                  />
                  <div style={{ marginTop: 16 }}>
                    <h4>Current Liabilities:</h4>
                    {balanceSheet.liabilities?.currentLiabilities?.map(account => (
                      <div key={account.accountCode} style={{ display: 'flex', justifyContent: 'space-between' }}>
                        <span>{account.account}</span>
                        <span>${account.balance.toFixed(2)}</span>
                      </div>
                    ))}
                  </div>
                </Card>
              </Col>
              <Col xs={24} md={8}>
                <Card title="Equity" loading={loading}>
                  <Statistic
                    title="Total Equity"
                    value={balanceSheet.equity?.totalEquity || 0}
                    precision={2}
                    valueStyle={{ color: '#3f8600' }}
                    prefix="$"
                  />
                  <div style={{ marginTop: 16 }}>
                    <h4>Equity Accounts:</h4>
                    {balanceSheet.equity?.equityAccounts?.map(account => (
                      <div key={account.accountCode} style={{ display: 'flex', justifyContent: 'space-between' }}>
                        <span>{account.account}</span>
                        <span>${account.balance.toFixed(2)}</span>
                      </div>
                    ))}
                  </div>
                </Card>
              </Col>
              <Col xs={24}>
                <Card title="Balance Check" loading={loading}>
                  <Row gutter={[16, 16]}>
                    <Col xs={24} md={8}>
                      <Statistic
                        title="Assets"
                        value={balanceSheet.balanceCheck?.assets || 0}
                        precision={2}
                        prefix="$"
                      />
                    </Col>
                    <Col xs={24} md={8}>
                      <Statistic
                        title="Liabilities + Equity"
                        value={balanceSheet.balanceCheck?.liabilitiesPlusEquity || 0}
                        precision={2}
                        prefix="$"
                      />
                    </Col>
                    <Col xs={24} md={8}>
                      <Statistic
                        title="Balance Status"
                        value={balanceSheet.balanceCheck?.isBalanced ? 'Balanced' : 'Not Balanced'}
                        valueStyle={{ 
                          color: balanceSheet.balanceCheck?.isBalanced ? '#3f8600' : '#cf1322' 
                        }}
                      />
                    </Col>
                  </Row>
                </Card>
              </Col>
            </Row>
          )}
        </TabPane>
      </Tabs>
    </div>
  );
};

export default Reports;
