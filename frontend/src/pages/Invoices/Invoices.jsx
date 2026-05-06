import React from 'react';
import { Card, Typography, Table, Button, Space, Tag, Input, DatePicker, Select } from 'antd';
import { PlusOutlined, EyeOutlined, EditOutlined, DeleteOutlined, FileTextOutlined } from '@ant-design/icons';

const { Title } = Typography;
const { Search } = Input;
const { RangePicker } = DatePicker;

const Invoices = () => {
  // Sample data - in real app, this would come from API
  const invoices = [
    {
      id: 1,
      invoiceNumber: 'INV-001',
      customerName: 'ABC Company',
      amount: 5000,
      status: 'Paid',
      dueDate: '2024-01-15',
      createdDate: '2024-01-01',
    },
    {
      id: 2,
      invoiceNumber: 'INV-002',
      customerName: 'XYZ Corporation',
      amount: 7500,
      status: 'Pending',
      dueDate: '2024-01-20',
      createdDate: '2024-01-05',
    },
    {
      id: 3,
      invoiceNumber: 'INV-003',
      customerName: 'DEF Industries',
      amount: 3200,
      status: 'Overdue',
      dueDate: '2024-01-10',
      createdDate: '2024-01-02',
    },
    {
      id: 4,
      invoiceNumber: 'INV-004',
      customerName: 'GHI Limited',
      amount: 8900,
      status: 'Paid',
      dueDate: '2024-01-25',
      createdDate: '2024-01-08',
    },
    {
      id: 5,
      invoiceNumber: 'INV-005',
      customerName: 'JKL Enterprises',
      amount: 12000,
      status: 'Draft',
      dueDate: '2024-02-01',
      createdDate: '2024-01-12',
    },
  ];

  const columns = [
    {
      title: 'Invoice Number',
      dataIndex: 'invoiceNumber',
      key: 'invoiceNumber',
      width: 120,
    },
    {
      title: 'Customer Name',
      dataIndex: 'customerName',
      key: 'customerName',
    },
    {
      title: 'Amount',
      dataIndex: 'amount',
      key: 'amount',
      width: 120,
      render: (amount) => `$${amount.toFixed(2)}`,
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
          'Draft': 'blue',
        }[status] || 'default';
        return <Tag color={color}>{status}</Tag>;
      },
    },
    {
      title: 'Due Date',
      dataIndex: 'dueDate',
      key: 'dueDate',
      width: 120,
    },
    {
      title: 'Created Date',
      dataIndex: 'createdDate',
      key: 'createdDate',
      width: 120,
    },
    {
      title: 'Actions',
      key: 'actions',
      width: 150,
      render: (_, record) => (
        <Space>
          <Button
            type="text"
            icon={<EyeOutlined />}
            size="small"
            title="View Invoice"
          />
          <Button
            type="text"
            icon={<EditOutlined />}
            size="small"
            title="Edit Invoice"
            disabled={record.status === 'Paid'}
          />
          <Button
            type="text"
            danger
            icon={<DeleteOutlined />}
            size="small"
            title="Delete Invoice"
            disabled={record.status === 'Paid'}
          />
          <Button
            type="text"
            icon={<FileTextOutlined />}
            size="small"
            title="Download PDF"
          />
        </Space>
      ),
    },
  ];

  return (
    <div>
      <Title level={2}>Invoice Management</Title>
      
      <Card>
        <div style={{ marginBottom: 16, display: 'flex', justifyContent: 'space-between' }}>
          <Space>
            <Search
              placeholder="Search invoices..."
              allowClear
              style={{ width: 250 }}
            />
            <Select
              placeholder="Filter by status"
              style={{ width: 150 }}
              allowClear
            >
              <Select.Option value="Paid">Paid</Select.Option>
              <Select.Option value="Pending">Pending</Select.Option>
              <Select.Option value="Overdue">Overdue</Select.Option>
              <Select.Option value="Draft">Draft</Select.Option>
            </Select>
            <RangePicker placeholder={['Start Date', 'End Date']} />
          </Space>
          
          <Button type="primary" icon={<PlusOutlined />}>
            Create Invoice
          </Button>
        </div>

        <Table
          columns={columns}
          dataSource={invoices}
          rowKey="id"
          pagination={{
            pageSize: 10,
            showSizeChanger: true,
            showQuickJumper: true,
          }}
        />
      </Card>
    </div>
  );
};

export default Invoices;
