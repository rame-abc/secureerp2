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
  Tag,
  Progress,
  Alert,
  Modal,
  Form,
  Input,
  Select,
  InputNumber,
  DatePicker,
  message,
  Drawer,
  Tooltip,
  Badge,
  Divider,
  Tabs,
  List,
  Timeline
} from 'antd';
import {
  PercentageOutlined,
  DollarOutlined,
  FileTextOutlined,
  CalculatorOutlined,
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  EyeOutlined,
  DownloadOutlined,
  ReloadOutlined,
  CheckCircleOutlined,
  ClockCircleOutlined,
  ExclamationCircleOutlined,
  WarningOutlined,
  CalendarOutlined,
  BankOutlined,
  ReceiptOutlined,
  BarChartOutlined,
  SettingOutlined,
  SyncOutlined
} from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { taxService } from '../../api/services/apiServices';
import { LineChart, Line, AreaChart, Area, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, Legend, ResponsiveContainer, PieChart, Pie, Cell } from 'recharts';
import dayjs from 'dayjs';

const { Title, Text } = Typography;
const { Option } = Select;
const { TabPane } = Tabs;

const TaxDashboard = () => {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [activeTab, setActiveTab] = useState('overview');
  const [taxRules, setTaxRules] = useState([]);
  const [taxCalculations, setTaxCalculations] = useState([]);
  const [taxReports, setTaxReports] = useState([]);
  const [statistics, setStatistics] = useState({
    totalTaxLiability: 0,
    paidTax: 0,
    pendingTax: 0,
    nextFilingDate: null,
    totalVATCollected: 0,
    totalIncomeTaxWithheld: 0,
    totalWithholdingTaxCollected: 0
  });
  const [ruleModalVisible, setRuleModalVisible] = useState(false);
  const [selectedRule, setSelectedRule] = useState(null);
  const [ruleForm] = Form.useForm();
  const [reportModalVisible, setReportModalVisible] = useState(false);
  const [selectedReport, setSelectedReport] = useState(null);
  const [monthlyData, setMonthlyData] = useState([]);
  const [taxBreakdown, setTaxBreakdown] = useState([]);

  // Fetch tax data
  const fetchTaxData = async () => {
    setLoading(true);
    try {
      // Mock data for demonstration
      const mockTaxRules = [
        {
          id: 1,
          name: 'Standard VAT',
          code: 'VAT_STD',
          type: 'VAT',
          rate: 15.0,
          rateType: 'percentage',
          jurisdiction: 'National',
          status: 'active',
          effectiveDate: '2024-01-01',
          expiryDate: null,
          threshold: 0,
          maxAmount: null,
          description: 'Standard Value Added Tax on all taxable goods and services',
          isCompound: false,
          isRecoverable: true
        },
        {
          id: 2,
          name: 'Income Tax - Employee',
          code: 'IT_EMP',
          type: 'Income Tax',
          rate: 20.0,
          rateType: 'percentage',
          jurisdiction: 'National',
          status: 'active',
          effectiveDate: '2024-01-01',
          expiryDate: null,
          threshold: 5000,
          maxAmount: null,
          description: 'Income tax withholding for employees',
          isCompound: false,
          isRecoverable: false
        },
        {
          id: 3,
          name: 'Withholding Tax - Services',
          code: 'WT_SVC',
          type: 'Withholding Tax',
          rate: 10.0,
          rateType: 'percentage',
          jurisdiction: 'National',
          status: 'active',
          effectiveDate: '2024-01-01',
          expiryDate: null,
          threshold: 1000,
          maxAmount: null,
          description: 'Withholding tax on professional services',
          isCompound: false,
          isRecoverable: false
        },
        {
          id: 4,
          name: 'Reduced VAT - Essential Goods',
          code: 'VAT_RED',
          type: 'VAT',
          rate: 5.0,
          rateType: 'percentage',
          jurisdiction: 'National',
          status: 'active',
          effectiveDate: '2024-01-01',
          expiryDate: null,
          threshold: 0,
          maxAmount: null,
          description: 'Reduced VAT rate for essential goods',
          isCompound: false,
          isRecoverable: true
        }
      ];

      const mockTaxCalculations = [
        {
          id: 1,
          taxRuleId: 1,
          taxRuleName: 'Standard VAT',
          documentType: 'Invoice',
          documentId: 'INV-2024-001',
          baseAmount: 15000,
          taxableAmount: 15000,
          taxRate: 15.0,
          taxAmount: 2250,
          totalAmount: 17250,
          calculationDate: '2024-10-15',
          status: 'calculated',
          isRecoverable: true,
          dueDate: '2024-11-20',
          paidDate: null,
          notes: 'VAT on consulting services'
        },
        {
          id: 2,
          taxRuleId: 2,
          taxRuleName: 'Income Tax - Employee',
          documentType: 'Payroll',
          documentId: 'PR-2024-10',
          baseAmount: 45000,
          taxableAmount: 40000,
          taxRate: 20.0,
          taxAmount: 8000,
          totalAmount: 37000,
          calculationDate: '2024-10-31',
          status: 'withheld',
          isRecoverable: false,
          dueDate: '2024-11-15',
          paidDate: '2024-11-10',
          notes: 'Monthly income tax withholding'
        },
        {
          id: 3,
          taxRuleId: 3,
          taxRuleName: 'Withholding Tax - Services',
          documentType: 'Invoice',
          documentId: 'INV-2024-002',
          baseAmount: 12000,
          taxableAmount: 12000,
          taxRate: 10.0,
          taxAmount: 1200,
          totalAmount: 10800,
          calculationDate: '2024-10-20',
          status: 'calculated',
          isRecoverable: false,
          dueDate: '2024-11-25',
          paidDate: null,
          notes: 'Withholding tax on professional services'
        }
      ];

      const mockTaxReports = [
        {
          id: 1,
          reportNumber: 'TAX-RPT-2024-10',
          name: 'October 2024 Tax Report',
          type: 'Monthly',
          period: '2024-10',
          generatedDate: '2024-11-01',
          status: 'generated',
          totalVAT: 18750,
          totalIncomeTax: 9000,
          totalWithholdingTax: 3500,
          totalTax: 31250,
          taxPayable: 12500,
          taxPaid: 8500,
          taxBalance: 4000,
          dueDate: '2024-11-20',
          filedDate: null,
          paymentDate: null,
          notes: 'Monthly tax summary for October 2024',
          generatedBy: 'System'
        },
        {
          id: 2,
          reportNumber: 'TAX-RPT-2024-09',
          name: 'September 2024 Tax Report',
          type: 'Monthly',
          period: '2024-09',
          generatedDate: '2024-10-01',
          status: 'filed',
          totalVAT: 16500,
          totalIncomeTax: 8500,
          totalWithholdingTax: 3200,
          totalTax: 28200,
          taxPayable: 11200,
          taxPaid: 11200,
          taxBalance: 0,
          dueDate: '2024-10-20',
          filedDate: '2024-10-18',
          paymentDate: '2024-10-15',
          notes: 'Monthly tax summary for September 2024',
          generatedBy: 'System'
        }
      ];

      const mockMonthlyData = [
        { month: 'Jan', vat: 12000, incomeTax: 7500, withholdingTax: 2800, total: 22300 },
        { month: 'Feb', vat: 13500, incomeTax: 7800, withholdingTax: 2900, total: 24200 },
        { month: 'Mar', vat: 14200, incomeTax: 8200, withholdingTax: 3100, total: 25500 },
        { month: 'Apr', vat: 13800, incomeTax: 8000, withholdingTax: 3000, total: 24800 },
        { month: 'May', vat: 15000, incomeTax: 8500, withholdingTax: 3200, total: 26700 },
        { month: 'Jun', vat: 16200, incomeTax: 8800, withholdingTax: 3400, total: 28400 },
        { month: 'Jul', vat: 15800, incomeTax: 8600, withholdingTax: 3300, total: 27700 },
        { month: 'Aug', vat: 16500, incomeTax: 8900, withholdingTax: 3500, total: 28900 },
        { month: 'Sep', vat: 17200, incomeTax: 9200, withholdingTax: 3600, total: 30000 },
        { month: 'Oct', vat: 18750, incomeTax: 9000, withholdingTax: 3500, total: 31250 }
      ];

      const mockTaxBreakdown = [
        { name: 'VAT', value: 18750, color: '#52c41a' },
        { name: 'Income Tax', value: 9000, color: '#1890ff' },
        { name: 'Withholding Tax', value: 3500, color: '#722ed1' }
      ];

      setTaxRules(mockTaxRules);
      setTaxCalculations(mockTaxCalculations);
      setTaxReports(mockTaxReports);
      setMonthlyData(mockMonthlyData);
      setTaxBreakdown(mockTaxBreakdown);

      // Calculate statistics
      const stats = {
        totalTaxLiability: mockTaxCalculations.reduce((sum, calc) => sum + calc.taxAmount, 0),
        paidTax: mockTaxCalculations.filter(calc => calc.paidDate).reduce((sum, calc) => sum + calc.taxAmount, 0),
        pendingTax: mockTaxCalculations.filter(calc => !calc.paidDate).reduce((sum, calc) => sum + calc.taxAmount, 0),
        nextFilingDate: dayjs().endOf('month').format('MMMM DD, YYYY'),
        totalVATCollected: mockTaxCalculations.filter(calc => calc.taxRuleName.includes('VAT')).reduce((sum, calc) => sum + calc.taxAmount, 0),
        totalIncomeTaxWithheld: mockTaxCalculations.filter(calc => calc.taxRuleName.includes('Income')).reduce((sum, calc) => sum + calc.taxAmount, 0),
        totalWithholdingTaxCollected: mockTaxCalculations.filter(calc => calc.taxRuleName.includes('Withholding')).reduce((sum, calc) => sum + calc.taxAmount, 0)
      };
      setStatistics(stats);
    } catch (error) {
      message.error('Failed to fetch tax data');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchTaxData();
  }, []);

  // Tax rules table columns
  const taxRuleColumns = [
    {
      title: 'Tax Rule',
      dataIndex: 'name',
      key: 'name',
      render: (_, record) => (
        <div>
          <div style={{ fontWeight: 'bold' }}>{record.name}</div>
          <div style={{ fontSize: '12px', color: '#666' }}>{record.code}</div>
        </div>
      )
    },
    {
      title: 'Type',
      dataIndex: 'type',
      key: 'type',
      render: (type) => <Tag color="blue">{type}</Tag>
    },
    {
      title: 'Rate',
      dataIndex: 'rate',
      key: 'rate',
      render: (rate, record) => (
        <div>
          <Text strong>{rate}%</Text>
          {record.rateType && (
            <div style={{ fontSize: '12px', color: '#666' }}>
              {record.rateType}
            </div>
          )}
        </div>
      )
    },
    {
      title: 'Jurisdiction',
      dataIndex: 'jurisdiction',
      key: 'jurisdiction'
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status) => (
        <Tag 
          color={status === 'active' ? 'green' : 'red'}
          icon={status === 'active' ? <CheckCircleOutlined /> : <ExclamationCircleOutlined />}
        >
          {status.toUpperCase()}
        </Tag>
      )
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, record) => (
        <Space>
          <Tooltip title="View Details">
            <Button 
              type="text" 
              icon={<EyeOutlined />} 
              onClick={() => {
                setSelectedRule(record);
                setRuleModalVisible(true);
              }}
            />
          </Tooltip>
          <Tooltip title="Edit Rule">
            <Button 
              type="text" 
              icon={<EditOutlined />} 
              onClick={() => navigate(`/tax/rules/${record.id}`)}
            />
          </Tooltip>
        </Space>
      )
    }
  ];

  // Tax calculations table columns
  const taxCalculationColumns = [
    {
      title: 'Document',
      dataIndex: 'documentId',
      key: 'document',
      render: (_, record) => (
        <div>
          <div style={{ fontWeight: 'bold' }}>{record.documentId}</div>
          <div style={{ fontSize: '12px', color: '#666' }}>{record.documentType}</div>
        </div>
      )
    },
    {
      title: 'Tax Rule',
      dataIndex: 'taxRuleName',
      key: 'taxRuleName'
    },
    {
      title: 'Base Amount',
      dataIndex: 'baseAmount',
      key: 'baseAmount',
      align: 'right',
      render: (amount) => <Text>${amount.toLocaleString()}</Text>
    },
    {
      title: 'Tax Amount',
      dataIndex: 'taxAmount',
      key: 'taxAmount',
      align: 'right',
      render: (amount) => <Text strong style={{ color: '#ff4d4f' }}>${amount.toLocaleString()}</Text>
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status) => {
        const statusConfig = {
          calculated: { color: 'blue', icon: <CalculatorOutlined />, text: 'Calculated' },
          withheld: { color: 'green', icon: <CheckCircleOutlined />, text: 'Withheld' },
          paid: { color: 'green', icon: <CheckCircleOutlined />, text: 'Paid' },
          pending: { color: 'orange', icon: <ClockCircleOutlined />, text: 'Pending' }
        };
        const config = statusConfig[status] || statusConfig.calculated;
        return (
          <Tag color={config.color} icon={config.icon}>
            {config.text}
          </Tag>
        );
      }
    },
    {
      title: 'Due Date',
      dataIndex: 'dueDate',
      key: 'dueDate',
      render: (date) => dayjs(date).format('MMM DD, YYYY')
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, record) => (
        <Space>
          <Tooltip title="View Details">
            <Button 
              type="text" 
              icon={<EyeOutlined />} 
              onClick={() => navigate(`/tax/calculations/${record.id}`)}
            />
          </Tooltip>
        </Space>
      )
    }
  ];

  // Tax reports table columns
  const taxReportColumns = [
    {
      title: 'Report',
      dataIndex: 'name',
      key: 'name',
      render: (_, record) => (
        <div>
          <div style={{ fontWeight: 'bold' }}>{record.name}</div>
          <div style={{ fontSize: '12px', color: '#666' }}>{record.reportNumber}</div>
        </div>
      )
    },
    {
      title: 'Period',
      dataIndex: 'period',
      key: 'period'
    },
    {
      title: 'Total Tax',
      dataIndex: 'totalTax',
      key: 'totalTax',
      align: 'right',
      render: (amount) => <Text strong>${amount.toLocaleString()}</Text>
    },
    {
      title: 'Tax Balance',
      dataIndex: 'taxBalance',
      key: 'taxBalance',
      align: 'right',
      render: (balance) => (
        <Text strong style={{ color: balance > 0 ? '#ff4d4f' : '#52c41a' }}>
          ${balance.toLocaleString()}
        </Text>
      )
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status) => {
        const statusConfig = {
          generated: { color: 'blue', icon: <FileTextOutlined />, text: 'Generated' },
          filed: { color: 'green', icon: <CheckCircleOutlined />, text: 'Filed' },
          paid: { color: 'green', icon: <CheckCircleOutlined />, text: 'Paid' }
        };
        const config = statusConfig[status] || statusConfig.generated;
        return (
          <Tag color={config.color} icon={config.icon}>
            {config.text}
          </Tag>
        );
      }
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, record) => (
        <Space>
          <Tooltip title="View Report">
            <Button 
              type="text" 
              icon={<EyeOutlined />} 
              onClick={() => {
                setSelectedReport(record);
                setReportModalVisible(true);
              }}
            />
          </Tooltip>
          <Tooltip title="Download">
            <Button 
              type="text" 
              icon={<DownloadOutlined />} 
              onClick={() => downloadTaxReport(record.id)}
            />
          </Tooltip>
        </Space>
      )
    }
  ];

  // Download tax report
  const downloadTaxReport = (reportId) => {
    message.info('Tax report download feature coming soon');
  };

  // Generate monthly tax report
  const generateMonthlyReport = async () => {
    try {
      await taxService.generateMonthlyTaxReport(dayjs().year(), dayjs().month() + 1);
      message.success('Monthly tax report generated successfully');
      fetchTaxData();
    } catch (error) {
      message.error('Failed to generate tax report');
    }
  };

  const pieColors = ['#52c41a', '#1890ff', '#722ed1'];

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
          <Title level={2} style={{ margin: 0 }}>Tax Management</Title>
          <Text type="secondary">Manage tax rules, calculations, and compliance</Text>
        </div>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={fetchTaxData}>
            Refresh
          </Button>
          <Button icon={<SyncOutlined />} onClick={generateMonthlyReport}>
            Generate Report
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate('/tax/rules/create')}>
            Add Tax Rule
          </Button>
        </Space>
      </div>

      {/* Alert for upcoming tax filing */}
      {statistics.pendingTax > 0 && (
        <Alert
          message="Tax Payment Due"
          description={`You have $${statistics.pendingTax.toLocaleString()} in pending tax payments due by ${statistics.nextFilingDate}.`}
          type="warning"
          showIcon
          style={{ marginBottom: '24px' }}
          action={
            <Button size="small" type="primary" onClick={() => navigate('/tax/payments')}>
              Make Payment
            </Button>
          }
        />
      )}

      {/* Statistics Cards */}
      <Row gutter={[16, 16]} style={{ marginBottom: '24px' }}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Total Tax Liability"
              value={statistics.totalTaxLiability}
              prefix={<DollarOutlined />}
              formatter={(value) => `$${value.toLocaleString()}`}
              valueStyle={{ color: '#ff4d4f' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Paid Tax"
              value={statistics.paidTax}
              prefix={<CheckCircleOutlined />}
              formatter={(value) => `$${value.toLocaleString()}`}
              valueStyle={{ color: '#52c41a' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Pending Tax"
              value={statistics.pendingTax}
              prefix={<ClockCircleOutlined />}
              formatter={(value) => `$${value.toLocaleString()}`}
              valueStyle={{ color: '#fa8c16' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <div style={{ textAlign: 'center' }}>
              <Text type="secondary" style={{ display: 'block', marginBottom: '8px' }}>
                Next Filing Date
              </Text>
              <Text strong style={{ fontSize: '16px' }}>
                {statistics.nextFilingDate}
              </Text>
              <Progress 
                percent={65} 
                size="small" 
                style={{ marginTop: '8px' }}
                format={() => '65% complete'}
              />
            </div>
          </Card>
        </Col>
      </Row>

      {/* Main Content Tabs */}
      <Card>
        <Tabs activeKey={activeTab} onChange={setActiveTab}>
          <TabPane tab="Overview" key="overview">
            <Row gutter={[24, 24]}>
              <Col xs={24} lg={16}>
                <Card title="Tax Trend Analysis" style={{ marginBottom: '24px' }}>
                  <ResponsiveContainer width="100%" height={300}>
                    <LineChart data={monthlyData}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="month" />
                      <YAxis />
                      <RechartsTooltip />
                      <Legend />
                      <Line type="monotone" dataKey="vat" stroke="#52c41a" strokeWidth={2} name="VAT" />
                      <Line type="monotone" dataKey="incomeTax" stroke="#1890ff" strokeWidth={2} name="Income Tax" />
                      <Line type="monotone" dataKey="withholdingTax" stroke="#722ed1" strokeWidth={2} name="Withholding Tax" />
                      <Line type="monotone" dataKey="total" stroke="#ff4d4f" strokeWidth={2} name="Total Tax" />
                    </LineChart>
                  </ResponsiveContainer>
                </Card>
              </Col>
              <Col xs={24} lg={8}>
                <Card title="Tax Breakdown" style={{ marginBottom: '24px' }}>
                  <ResponsiveContainer width="100%" height={300}>
                    <PieChart>
                      <Pie
                        data={taxBreakdown}
                        cx="50%"
                        cy="50%"
                        labelLine={false}
                        label={({ name, value }) => `${name}: $${value.toLocaleString()}`}
                        outerRadius={80}
                        fill="#8884d8"
                        dataKey="value"
                      >
                        {taxBreakdown.map((entry, index) => (
                          <Cell key={`cell-${index}`} fill={pieColors[index % pieColors.length]} />
                        ))}
                      </Pie>
                      <RechartsTooltip />
                    </PieChart>
                  </ResponsiveContainer>
                </Card>
              </Col>
            </Row>

            <Row gutter={[24, 24]}>
              <Col xs={24} lg={12}>
                <Card title="Recent Tax Calculations">
                  <List
                    dataSource={taxCalculations.slice(0, 5)}
                    renderItem={(item) => (
                      <List.Item>
                        <List.Item.Meta
                          avatar={<ReceiptOutlined />}
                          title={item.documentId}
                          description={`${item.taxRuleName} - $${item.taxAmount.toLocaleString()}`}
                        />
                        <div>
                          <Tag color={item.status === 'paid' ? 'green' : 'orange'}>
                            {item.status}
                          </Tag>
                        </div>
                      </List.Item>
                    )}
                  />
                </Card>
              </Col>
              <Col xs={24} lg={12}>
                <Card title="Upcoming Tax Filings">
                  <Timeline>
                    <Timeline.Item color="blue">
                      <Text strong>VAT Filing</Text>
                      <div>Due: {statistics.nextFilingDate}</div>
                      <div>Amount: ${statistics.totalVATCollected.toLocaleString()}</div>
                    </Timeline.Item>
                    <Timeline.Item color="green">
                      <Text strong>Income Tax Filing</Text>
                      <div>Due: {statistics.nextFilingDate}</div>
                      <div>Amount: ${statistics.totalIncomeTaxWithheld.toLocaleString()}</div>
                    </Timeline.Item>
                    <Timeline.Item color="purple">
                      <Text strong>Withholding Tax Filing</Text>
                      <div>Due: {statistics.nextFilingDate}</div>
                      <div>Amount: ${statistics.totalWithholdingTaxCollected.toLocaleString()}</div>
                    </Timeline.Item>
                  </Timeline>
                </Card>
              </Col>
            </Row>
          </TabPane>

          <TabPane tab="Tax Rules" key="rules">
            <Table
              columns={taxRuleColumns}
              dataSource={taxRules}
              rowKey="id"
              loading={loading}
              pagination={{
                pageSize: 10,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (total, range) => `${range[0]}-${range[1]} of ${total} rules`
              }}
            />
          </TabPane>

          <TabPane tab="Calculations" key="calculations">
            <Table
              columns={taxCalculationColumns}
              dataSource={taxCalculations}
              rowKey="id"
              loading={loading}
              pagination={{
                pageSize: 10,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (total, range) => `${range[0]}-${range[1]} of ${total} calculations`
              }}
            />
          </TabPane>

          <TabPane tab="Reports" key="reports">
            <Table
              columns={taxReportColumns}
              dataSource={taxReports}
              rowKey="id"
              loading={loading}
              pagination={{
                pageSize: 10,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (total, range) => `${range[0]}-${range[1]} of ${total} reports`
              }}
            />
          </TabPane>
        </Tabs>
      </Card>

      {/* Tax Rule Detail Modal */}
      <Modal
        title={`Tax Rule Details - ${selectedRule?.name}`}
        open={ruleModalVisible}
        onCancel={() => setRuleModalVisible(false)}
        footer={[
          <Button key="close" onClick={() => setRuleModalVisible(false)}>
            Close
          </Button>,
          <Button key="edit" type="primary" onClick={() => navigate(`/tax/rules/${selectedRule?.id}`)}>
            Edit Rule
          </Button>
        ]}
        width={800}
      >
        {selectedRule && (
          <div>
            <Row gutter={[24, 24]}>
              <Col span={12}>
                <div style={{ marginBottom: '16px' }}>
                  <Text type="secondary">Tax Code:</Text>
                  <div style={{ fontWeight: 'bold' }}>{selectedRule.code}</div>
                </div>
                <div style={{ marginBottom: '16px' }}>
                  <Text type="secondary">Tax Type:</Text>
                  <div><Tag color="blue">{selectedRule.type}</Tag></div>
                </div>
                <div style={{ marginBottom: '16px' }}>
                  <Text type="secondary">Tax Rate:</Text>
                  <div style={{ fontSize: '18px', fontWeight: 'bold', color: '#ff4d4f' }}>
                    {selectedRule.rate}%
                  </div>
                </div>
                <div style={{ marginBottom: '16px' }}>
                  <Text type="secondary">Jurisdiction:</Text>
                  <div>{selectedRule.jurisdiction}</div>
                </div>
              </Col>
              <Col span={12}>
                <div style={{ marginBottom: '16px' }}>
                  <Text type="secondary">Status:</Text>
                  <div>
                    <Tag 
                      color={selectedRule.status === 'active' ? 'green' : 'red'}
                      icon={selectedRule.status === 'active' ? <CheckCircleOutlined /> : <ExclamationCircleOutlined />}
                    >
                      {selectedRule.status.toUpperCase()}
                    </Tag>
                  </div>
                </div>
                <div style={{ marginBottom: '16px' }}>
                  <Text type="secondary">Effective Date:</Text>
                  <div>{dayjs(selectedRule.effectiveDate).format('MMMM DD, YYYY')}</div>
                </div>
                {selectedRule.expiryDate && (
                  <div style={{ marginBottom: '16px' }}>
                    <Text type="secondary">Expiry Date:</Text>
                    <div>{dayjs(selectedRule.expiryDate).format('MMMM DD, YYYY')}</div>
                  </div>
                )}
                <div style={{ marginBottom: '16px' }}>
                  <Text type="secondary">Threshold:</Text>
                  <div>${selectedRule.threshold.toLocaleString()}</div>
                </div>
              </Col>
            </Row>
            
            <Divider />
            
            <div style={{ marginBottom: '16px' }}>
              <Text type="secondary">Description:</Text>
              <div>{selectedRule.description}</div>
            </div>
            
            <Row gutter={16}>
              <Col span={8}>
                <div style={{ marginBottom: '16px' }}>
                  <Text type="secondary">Compound Tax:</Text>
                  <div>{selectedRule.isCompound ? 'Yes' : 'No'}</div>
                </div>
              </Col>
              <Col span={8}>
                <div style={{ marginBottom: '16px' }}>
                  <Text type="secondary">Recoverable:</Text>
                  <div>{selectedRule.isRecoverable ? 'Yes' : 'No'}</div>
                </div>
              </Col>
              <Col span={8}>
                <div style={{ marginBottom: '16px' }}>
                  <Text type="secondary">Rate Type:</Text>
                  <div>{selectedRule.rateType}</div>
                </div>
              </Col>
            </Row>
          </div>
        )}
      </Modal>

      {/* Tax Report Detail Modal */}
      <Modal
        title={`Tax Report - ${selectedReport?.name}`}
        open={reportModalVisible}
        onCancel={() => setReportModalVisible(false)}
        footer={[
          <Button key="close" onClick={() => setReportModalVisible(false)}>
            Close
          </Button>,
          <Button key="download" type="primary" icon={<DownloadOutlined />} onClick={() => downloadTaxReport(selectedReport?.id)}>
            Download Report
          </Button>
        ]}
        width={800}
      >
        {selectedReport && (
          <div>
            <Row gutter={[24, 24]}>
              <Col span={12}>
                <div style={{ marginBottom: '16px' }}>
                  <Text type="secondary">Report Number:</Text>
                  <div style={{ fontWeight: 'bold' }}>{selectedReport.reportNumber}</div>
                </div>
                <div style={{ marginBottom: '16px' }}>
                  <Text type="secondary">Report Type:</Text>
                  <div><Tag color="blue">{selectedReport.type}</Tag></div>
                </div>
                <div style={{ marginBottom: '16px' }}>
                  <Text type="secondary">Period:</Text>
                  <div>{selectedReport.period}</div>
                </div>
                <div style={{ marginBottom: '16px' }}>
                  <Text type="secondary">Generated Date:</Text>
                  <div>{dayjs(selectedReport.generatedDate).format('MMMM DD, YYYY')}</div>
                </div>
              </Col>
              <Col span={12}>
                <div style={{ marginBottom: '16px' }}>
                  <Text type="secondary">Status:</Text>
                  <div>
                    <Tag 
                      color={selectedReport.status === 'generated' ? 'blue' : 'green'}
                      icon={selectedReport.status === 'generated' ? <FileTextOutlined /> : <CheckCircleOutlined />}
                    >
                      {selectedReport.status.toUpperCase()}
                    </Tag>
                  </div>
                </div>
                <div style={{ marginBottom: '16px' }}>
                  <Text type="secondary">Due Date:</Text>
                  <div>{dayjs(selectedReport.dueDate).format('MMMM DD, YYYY')}</div>
                </div>
                {selectedReport.filedDate && (
                  <div style={{ marginBottom: '16px' }}>
                    <Text type="secondary">Filed Date:</Text>
                    <div>{dayjs(selectedReport.filedDate).format('MMMM DD, YYYY')}</div>
                  </div>
                )}
                {selectedReport.paymentDate && (
                  <div style={{ marginBottom: '16px' }}>
                    <Text type="secondary">Payment Date:</Text>
                    <div>{dayjs(selectedReport.paymentDate).format('MMMM DD, YYYY')}</div>
                  </div>
                )}
              </Col>
            </Row>
            
            <Divider />
            
            <Row gutter={[24, 24]}>
              <Col span={8}>
                <Card size="small">
                  <Statistic
                    title="VAT"
                    value={selectedReport.totalVAT}
                    prefix={<DollarOutlined />}
                    formatter={(value) => `$${value.toLocaleString()}`}
                    valueStyle={{ color: '#52c41a' }}
                  />
                </Card>
              </Col>
              <Col span={8}>
                <Card size="small">
                  <Statistic
                    title="Income Tax"
                    value={selectedReport.totalIncomeTax}
                    prefix={<DollarOutlined />}
                    formatter={(value) => `$${value.toLocaleString()}`}
                    valueStyle={{ color: '#1890ff' }}
                  />
                </Card>
              </Col>
              <Col span={8}>
                <Card size="small">
                  <Statistic
                    title="Withholding Tax"
                    value={selectedReport.totalWithholdingTax}
                    prefix={<DollarOutlined />}
                    formatter={(value) => `$${value.toLocaleString()}`}
                    valueStyle={{ color: '#722ed1' }}
                  />
                </Card>
              </Col>
            </Row>
            
            <Divider />
            
            <Row gutter={[24, 24]}>
              <Col span={8}>
                <div style={{ textAlign: 'center' }}>
                  <Text type="secondary">Total Tax</Text>
                  <div style={{ fontSize: '20px', fontWeight: 'bold', color: '#ff4d4f' }}>
                    ${selectedReport.totalTax.toLocaleString()}
                  </div>
                </div>
              </Col>
              <Col span={8}>
                <div style={{ textAlign: 'center' }}>
                  <Text type="secondary">Tax Paid</Text>
                  <div style={{ fontSize: '20px', fontWeight: 'bold', color: '#52c41a' }}>
                    ${selectedReport.taxPaid.toLocaleString()}
                  </div>
                </div>
              </Col>
              <Col span={8}>
                <div style={{ textAlign: 'center' }}>
                  <Text type="secondary">Tax Balance</Text>
                  <div style={{ fontSize: '20px', fontWeight: 'bold', color: selectedReport.taxBalance > 0 ? '#ff4d4f' : '#52c41a' }}>
                    ${selectedReport.taxBalance.toLocaleString()}
                  </div>
                </div>
              </Col>
            </Row>
            
            {selectedReport.notes && (
              <>
                <Divider />
                <div>
                  <Text type="secondary">Notes:</Text>
                  <div>{selectedReport.notes}</div>
                </div>
              </>
            )}
          </div>
        )}
      </Modal>
    </div>
  );
};

export default TaxDashboard;
