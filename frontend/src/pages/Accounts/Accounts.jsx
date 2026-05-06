import React from 'react';
import { Card, Typography, Table, Button, Space, Tag, Input, Select } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined, SearchOutlined } from '@ant-design/icons';

const { Title } = Typography;
const { Search } = Input;

const Accounts = () => {
  // Sample data - in real app, this would come from API
  const accounts = [
    {
      id: 1,
      accountCode: '1000',
      accountName: 'Cash',
      accountType: 'Asset',
      normalBalance: 'Debit',
      balance: 10000,
      isActive: true,
    },
    {
      id: 2,
      accountCode: '1200',
      accountName: 'Accounts Receivable',
      accountType: 'Asset',
      normalBalance: 'Debit',
      balance: 5000,
      isActive: true,
    },
    {
      id: 3,
      accountCode: '2000',
      accountName: 'Accounts Payable',
      accountType: 'Liability',
      normalBalance: 'Credit',
      balance: 3000,
      isActive: true,
    },
    {
      id: 4,
      accountCode: '3000',
      accountName: 'Owner\'s Equity',
      accountType: 'Equity',
      normalBalance: 'Credit',
      balance: 12000,
      isActive: true,
    },
    {
      id: 5,
      accountCode: '4000',
      accountName: 'Sales Revenue',
      accountType: 'Revenue',
      normalBalance: 'Credit',
      balance: 8000,
      isActive: true,
    },
    {
      id: 6,
      accountCode: '5000',
      accountName: 'Cost of Goods Sold',
      accountType: 'Expense',
      normalBalance: 'Debit',
      balance: 2000,
      isActive: true,
    },
  ];

  const columns = [
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
      render: (type) => {
        const color = {
          'Asset': 'blue',
          'Liability': 'orange',
          'Equity': 'green',
          'Revenue': 'purple',
          'Expense': 'red',
        }[type] || 'default';
        return <Tag color={color}>{type}</Tag>;
      },
    },
    {
      title: 'Normal Balance',
      dataIndex: 'normalBalance',
      key: 'normalBalance',
      width: 120,
      render: (balance) => (
        <Tag color={balance === 'Debit' ? 'green' : 'red'}>
          {balance}
        </Tag>
      ),
    },
    {
      title: 'Current Balance',
      dataIndex: 'balance',
      key: 'balance',
      width: 120,
      render: (balance) => `$${balance.toFixed(2)}`,
    },
    {
      title: 'Status',
      dataIndex: 'isActive',
      key: 'isActive',
      width: 80,
      render: (isActive) => (
        <Tag color={isActive ? 'green' : 'red'}>
          {isActive ? 'Active' : 'Inactive'}
        </Tag>
      ),
    },
    {
      title: 'Actions',
      key: 'actions',
      width: 120,
      render: (_, record) => (
        <Space>
          <Button
            type="text"
            icon={<EditOutlined />}
            size="small"
          />
          <Button
            type="text"
            danger
            icon={<DeleteOutlined />}
            size="small"
          />
        </Space>
      ),
    },
  ];

  return (
    <div>
      <Title level={2}>Chart of Accounts</Title>
      
      <Card>
        <div style={{ marginBottom: 16, display: 'flex', justifyContent: 'space-between' }}>
          <Space>
            <Search
              placeholder="Search accounts..."
              allowClear
              style={{ width: 300 }}
              prefix={<SearchOutlined />}
            />
            <Select
              placeholder="Filter by type"
              style={{ width: 150 }}
              allowClear
            >
              <Select.Option value="Asset">Asset</Select.Option>
              <Select.Option value="Liability">Liability</Select.Option>
              <Select.Option value="Equity">Equity</Select.Option>
              <Select.Option value="Revenue">Revenue</Select.Option>
              <Select.Option value="Expense">Expense</Select.Option>
            </Select>
          </Space>
          
          <Button type="primary" icon={<PlusOutlined />}>
            Add Account
          </Button>
        </div>

        <Table
          columns={columns}
          dataSource={accounts}
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

export default Accounts;
