import React from 'react';
import { Card, Typography, Table, Button, Space, Tag, Input, Select, DatePicker, Progress } from 'antd';
import { PlusOutlined, CalculatorOutlined, FileTextOutlined, DownloadOutlined } from '@ant-design/icons';

const { Title } = Typography;
const { Search } = Input;
const { RangePicker } = DatePicker;

const Tax = () => {
  // Sample data - in real app, this would come from API
  const taxRecords = [
    {
      id: 1,
      taxType: 'Income Tax',
      description: 'Q4 2023 Income Tax',
      period: 'Q4 2023',
      dueDate: '2024-01-15',
      amount: 15000,
      status: 'Paid',
      paidDate: '2024-01-14',
    },
    {
      id: 2,
      taxType: 'Sales Tax',
      description: 'December 2023 Sales Tax',
      period: 'December 2023',
      dueDate: '2024-01-20',
      amount: 3200,
      status: 'Pending',
      paidDate: null,
    },
    {
      id: 3,
      taxType: 'Property Tax',
      description: '2024 Property Tax',
      period: '2024',
      dueDate: '2024-03-31',
      amount: 8500,
      status: 'Upcoming',
      paidDate: null,
    },
    {
      id: 4,
      taxType: 'Payroll Tax',
      description: 'Q4 2023 Payroll Tax',
      period: 'Q4 2023',
      dueDate: '2024-01-31',
      amount: 6800,
      status: 'Pending',
      paidDate: null,
    },
    {
      id: 5,
      taxType: 'VAT',
      description: 'December 2023 VAT Return',
      period: 'December 2023',
      dueDate: '2024-01-25',
      amount: 4500,
      status: 'Overdue',
      paidDate: null,
    },
    {
      id: 6,
      taxType: 'Corporate Tax',
      description: '2023 Corporate Tax',
      period: '2023',
      dueDate: '2024-03-15',
      amount: 25000,
      status: 'Upcoming',
      paidDate: null,
    },
  ];

  const taxSummary = {
    totalTax: 63000,
    paidTax: 15000,
    pendingTax: 14500,
    overdueTax: 4500,
    upcomingTax: 29000,
  };

  const columns = [
    {
      title: 'Tax Type',
      dataIndex: 'taxType',
      key: 'taxType',
      width: 120,
      render: (type) => {
        const color = {
          'Income Tax': 'blue',
          'Sales Tax': 'green',
          'Property Tax': 'orange',
          'Payroll Tax': 'purple',
          'VAT': 'cyan',
          'Corporate Tax': 'red',
        }[type] || 'default';
        return <Tag color={color}>{type}</Tag>;
      },
    },
    {
      title: 'Description',
      dataIndex: 'description',
      key: 'description',
    },
    {
      title: 'Period',
      dataIndex: 'period',
      key: 'period',
      width: 100,
    },
    {
      title: 'Due Date',
      dataIndex: 'dueDate',
      key: 'dueDate',
      width: 120,
    },
    {
      title: 'Amount',
      dataIndex: 'amount',
      key: 'amount',
      width: 120,
      render: (amount) => <strong>${amount.toFixed(2)}</strong>,
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      width: 100,
      render: (status) => {
        const color = {
          'Paid': 'green',
          'Pending': 'orange',
          'Overdue': 'red',
          'Upcoming': 'blue',
        }[status] || 'default';
        return <Tag color={color}>{status}</Tag>;
      },
    },
    {
      title: 'Paid Date',
      dataIndex: 'paidDate',
      key: 'paidDate',
      width: 120,
      render: (date) => date || '-',
    },
    {
      title: 'Actions',
      key: 'actions',
      width: 150,
      render: (_, record) => (
        <Space>
          <Button
            type="text"
            icon={<CalculatorOutlined />}
            size="small"
            title="Calculate Tax"
          />
          <Button
            type="text"
            icon={<FileTextOutlined />}
            size="small"
            title="View Details"
          />
          <Button
            type="text"
            icon={<DownloadOutlined />}
            size="small"
            title="Download Report"
          />
        </Space>
      ),
    },
  ];

  const paidPercentage = (taxSummary.paidTax / taxSummary.totalTax) * 100;
  const pendingPercentage = (taxSummary.pendingTax / taxSummary.totalTax) * 100;

  return (
    <div>
      <Title level={2}>Tax Management</Title>
      
      {/* Tax Summary Cards */}
      <div style={{ marginBottom: 24 }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 16 }}>
          <Card size="small">
            <div style={{ textAlign: 'center' }}>
              <div style={{ fontSize: 24, fontWeight: 'bold', color: '#1890ff' }}>
                ${taxSummary.totalTax.toLocaleString()}
              </div>
              <div style={{ color: '#666' }}>Total Tax Liability</div>
            </div>
          </Card>
          
          <Card size="small">
            <div style={{ textAlign: 'center' }}>
              <div style={{ fontSize: 24, fontWeight: 'bold', color: '#52c41a' }}>
                ${taxSummary.paidTax.toLocaleString()}
              </div>
              <div style={{ color: '#666' }}>Paid Tax</div>
              <Progress percent={paidPercentage} size="small" style={{ marginTop: 8 }} />
            </div>
          </Card>
          
          <Card size="small">
            <div style={{ textAlign: 'center' }}>
              <div style={{ fontSize: 24, fontWeight: 'bold', color: '#faad14' }}>
                ${taxSummary.pendingTax.toLocaleString()}
              </div>
              <div style={{ color: '#666' }}>Pending Tax</div>
              <Progress percent={pendingPercentage} size="small" style={{ marginTop: 8 }} />
            </div>
          </Card>
          
          <Card size="small">
            <div style={{ textAlign: 'center' }}>
              <div style={{ fontSize: 24, fontWeight: 'bold', color: '#f5222d' }}>
                ${taxSummary.overdueTax.toLocaleString()}
              </div>
              <div style={{ color: '#666' }}>Overdue Tax</div>
            </div>
          </Card>
        </div>
      </div>
      
      <Card>
        <div style={{ marginBottom: 16, display: 'flex', justifyContent: 'space-between' }}>
          <Space>
            <Search
              placeholder="Search tax records..."
              allowClear
              style={{ width: 250 }}
            />
            <Select
              placeholder="Filter by tax type"
              style={{ width: 150 }}
              allowClear
            >
              <Select.Option value="Income Tax">Income Tax</Select.Option>
              <Select.Option value="Sales Tax">Sales Tax</Select.Option>
              <Select.Option value="Property Tax">Property Tax</Select.Option>
              <Select.Option value="Payroll Tax">Payroll Tax</Select.Option>
              <Select.Option value="VAT">VAT</Select.Option>
              <Select.Option value="Corporate Tax">Corporate Tax</Select.Option>
            </Select>
            <Select
              placeholder="Filter by status"
              style={{ width: 120 }}
              allowClear
            >
              <Select.Option value="Paid">Paid</Select.Option>
              <Select.Option value="Pending">Pending</Select.Option>
              <Select.Option value="Overdue">Overdue</Select.Option>
              <Select.Option value="Upcoming">Upcoming</Select.Option>
            </Select>
            <RangePicker placeholder={['Start Date', 'End Date']} />
          </Space>
          
          <Space>
            <Button icon={<CalculatorOutlined />}>
              Calculate Taxes
            </Button>
            <Button icon={<FileTextOutlined />}>
              Generate Report
            </Button>
            <Button type="primary" icon={<PlusOutlined />}>
              Add Tax Record
            </Button>
          </Space>
        </div>

        <Table
          columns={columns}
          dataSource={taxRecords}
          rowKey="id"
          pagination={{
            pageSize: 10,
            showSizeChanger: true,
            showQuickJumper: true,
          }}
          summary={(pageData) => {
            const totalAmount = pageData.reduce((sum, record) => sum + record.amount, 0);
            const paidCount = pageData.filter(record => record.status === 'Paid').length;
            const pendingCount = pageData.filter(record => record.status === 'Pending').length;
            const overdueCount = pageData.filter(record => record.status === 'Overdue').length;

            return (
              <Table.Summary.Row>
                <Table.Summary.Cell index={0} colSpan={4}>
                  <strong>Summary</strong>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={4}>
                  <strong>${totalAmount.toFixed(2)}</strong>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={5} colSpan={2}>
                  <Space>
                    <Tag color="green">Paid: {paidCount}</Tag>
                    <Tag color="orange">Pending: {pendingCount}</Tag>
                    <Tag color="red">Overdue: {overdueCount}</Tag>
                  </Space>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={7} colSpan={1}></Table.Summary.Cell>
              </Table.Summary.Row>
            );
          }}
        />
      </Card>
    </div>
  );
};

export default Tax;
