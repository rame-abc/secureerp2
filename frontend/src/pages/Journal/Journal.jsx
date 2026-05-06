import React, { useState, useEffect } from 'react';
import {
  Card,
  Table,
  Button,
  Input,
  Select,
  Form,
  Space,
  Typography,
  message,
  Modal,
  Tag,
  Popconfirm,
  Row,
  Col,
  Statistic,
} from 'antd';
import {
  PlusOutlined,
  DeleteOutlined,
  SaveOutlined,
  CheckOutlined,
  EditOutlined,
  EyeOutlined,
} from '@ant-design/icons';
import api from '../../api/axios';

const { Title, Text } = Typography;
const { TextArea } = Input;

const Journal = () => {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [journals, setJournals] = useState([]);
  const [accounts, setAccounts] = useState([]);
  const [entries, setEntries] = useState([
    { accountId: null, debit: 0, credit: 0, description: '' },
  ]);
  const [selectedJournal, setSelectedJournal] = useState(null);
  const [isModalVisible, setIsModalVisible] = useState(false);
  const [viewModalVisible, setViewModalVisible] = useState(false);
  const [totalDebit, setTotalDebit] = useState(0);
  const [totalCredit, setTotalCredit] = useState(0);
  const [isBalanced, setIsBalanced] = useState(false);

  useEffect(() => {
    fetchJournals();
    fetchAccounts();
  }, []);

  useEffect(() => {
    calculateTotals();
  }, [entries]);

  const fetchJournals = async () => {
    try {
      setLoading(true);
      const response = await api.get('/api/finance/journals');
      setJournals(response.data);
    } catch (error) {
      message.error('Failed to fetch journals');
    } finally {
      setLoading(false);
    }
  };

  const fetchAccounts = async () => {
    try {
      const response = await api.get('/api/finance/accounts');
      setAccounts(response.data);
    } catch (error) {
      message.error('Failed to fetch accounts');
    }
  };

  const calculateTotals = () => {
    const debit = entries.reduce((sum, entry) => sum + (parseFloat(entry.debit) || 0), 0);
    const credit = entries.reduce((sum, entry) => sum + (parseFloat(entry.credit) || 0), 0);
    setTotalDebit(debit);
    setTotalCredit(credit);
    setIsBalanced(Math.abs(debit - credit) < 0.01);
  };

  const addEntry = () => {
    setEntries([...entries, { accountId: null, debit: 0, credit: 0, description: '' }]);
  };

  const removeEntry = (index) => {
    if (entries.length > 1) {
      const newEntries = entries.filter((_, i) => i !== index);
      setEntries(newEntries);
    }
  };

  const updateEntry = (index, field, value) => {
    const newEntries = [...entries];
    newEntries[index][field] = value;
    
    // Auto-balance: if debit is entered, clear credit and vice versa
    if (field === 'debit' && value > 0) {
      newEntries[index].credit = 0;
    } else if (field === 'credit' && value > 0) {
      newEntries[index].debit = 0;
    }
    
    setEntries(newEntries);
  };

  const handleSubmit = async (values) => {
    if (!isBalanced) {
      message.error('Journal entries must be balanced (Total Debit = Total Credit)');
      return;
    }

    try {
      setLoading(true);
      const journalData = {
        description: values.description,
        entries: entries.filter(entry => entry.accountId && (entry.debit > 0 || entry.credit > 0)),
      };

      await api.post('/api/finance/journal', journalData);
      message.success('Journal entry created successfully');
      form.resetFields();
      setEntries([{ accountId: null, debit: 0, credit: 0, description: '' }]);
      fetchJournals();
    } catch (error) {
      message.error('Failed to create journal entry');
    } finally {
      setLoading(false);
    }
  };

  const handlePostJournal = async (journalId) => {
    try {
      setLoading(true);
      await api.post(`/api/finance/journal/${journalId}/post`);
      message.success('Journal posted successfully');
      fetchJournals();
    } catch (error) {
      message.error('Failed to post journal');
    } finally {
      setLoading(false);
    }
  };

  const viewJournal = (journal) => {
    setSelectedJournal(journal);
    setViewModalVisible(true);
  };

  const entryColumns = [
    {
      title: 'Account',
      dataIndex: 'accountId',
      width: 200,
      render: (value, record, index) => (
        <Select
          placeholder="Select Account"
          value={value}
          onChange={(val) => updateEntry(index, 'accountId', val)}
          style={{ width: '100%' }}
          showSearch
          filterOption={(input, option) =>
            option.children.toLowerCase().indexOf(input.toLowerCase()) >= 0
          }
        >
          {accounts.map(account => (
            <Select.Option key={account.id} value={account.id}>
              {account.accountCode} - {account.accountName}
            </Select.Option>
          ))}
        </Select>
      ),
    },
    {
      title: 'Debit',
      dataIndex: 'debit',
      width: 120,
      render: (value, record, index) => (
        <Input
          type="number"
          placeholder="0.00"
          value={value}
          onChange={(e) => updateEntry(index, 'debit', parseFloat(e.target.value) || 0)}
          min={0}
          step={0.01}
        />
      ),
    },
    {
      title: 'Credit',
      dataIndex: 'credit',
      width: 120,
      render: (value, record, index) => (
        <Input
          type="number"
          placeholder="0.00"
          value={value}
          onChange={(e) => updateEntry(index, 'credit', parseFloat(e.target.value) || 0)}
          min={0}
          step={0.01}
        />
      ),
    },
    {
      title: 'Description',
      dataIndex: 'description',
      render: (value, record, index) => (
        <Input
          placeholder="Description"
          value={value}
          onChange={(e) => updateEntry(index, 'description', e.target.value)}
        />
      ),
    },
    {
      title: 'Action',
      width: 80,
      render: (_, record, index) => (
        <Button
          type="text"
          danger
          icon={<DeleteOutlined />}
          onClick={() => removeEntry(index)}
          disabled={entries.length === 1}
        />
      ),
    },
  ];

  const journalColumns = [
    {
      title: 'Date',
      dataIndex: 'createdAt',
      render: (date) => new Date(date).toLocaleDateString(),
    },
    {
      title: 'Description',
      dataIndex: 'description',
    },
    {
      title: 'Status',
      dataIndex: 'status',
      render: (status) => (
        <Tag color={status === 'Posted' ? 'green' : status === 'Locked' ? 'red' : 'orange'}>
          {status}
        </Tag>
      ),
    },
    {
      title: 'Entries',
      dataIndex: 'entries',
      render: (entries) => entries?.length || 0,
    },
    {
      title: 'Actions',
      render: (_, record) => (
        <Space>
          <Button
            type="text"
            icon={<EyeOutlined />}
            onClick={() => viewJournal(record)}
          >
            View
          </Button>
          {record.status === 'Draft' && (
            <Popconfirm
              title="Are you sure you want to post this journal?"
              onConfirm={() => handlePostJournal(record.id)}
            >
              <Button type="text" icon={<CheckOutlined />}>
                Post
              </Button>
            </Popconfirm>
          )}
        </Space>
      ),
    },
  ];

  return (
    <div>
      <Title level={2}>Journal Entries</Title>

      {/* Create New Journal */}
      <Card title="Create Journal Entry" style={{ marginBottom: 24 }}>
        <Form form={form} onFinish={handleSubmit} layout="vertical">
          <Form.Item
            name="description"
            label="Description"
            rules={[{ required: true, message: 'Please enter description' }]}
          >
            <TextArea placeholder="Enter journal description" rows={2} />
          </Form.Item>

          <Table
            columns={entryColumns}
            dataSource={entries}
            pagination={false}
            size="small"
            style={{ marginBottom: 16 }}
          />

          <Row gutter={16} style={{ marginBottom: 16 }}>
            <Col span={8}>
              <Card size="small">
                <Statistic
                  title="Total Debit"
                  value={totalDebit}
                  precision={2}
                  valueStyle={{ color: '#3f8600' }}
                />
              </Card>
            </Col>
            <Col span={8}>
              <Card size="small">
                <Statistic
                  title="Total Credit"
                  value={totalCredit}
                  precision={2}
                  valueStyle={{ color: '#cf1322' }}
                />
              </Card>
            </Col>
            <Col span={8}>
              <Card size="small">
                <Statistic
                  title="Balance"
                  value={totalDebit - totalCredit}
                  precision={2}
                  valueStyle={{ color: isBalanced ? '#3f8600' : '#cf1322' }}
                />
              </Card>
            </Col>
          </Row>

          {!isBalanced && (
            <div style={{ marginBottom: 16, padding: 12, background: '#fff2f0', borderRadius: 6 }}>
              <Text type="danger">
                Journal entries must be balanced (Total Debit = Total Credit)
              </Text>
            </div>
          )}

          <Space>
            <Button
              type="dashed"
              icon={<PlusOutlined />}
              onClick={addEntry}
            >
              Add Entry
            </Button>
            <Button
              type="primary"
              icon={<SaveOutlined />}
              htmlType="submit"
              loading={loading}
              disabled={!isBalanced}
            >
              Create Journal
            </Button>
          </Space>
        </Form>
      </Card>

      {/* Existing Journals */}
      <Card title="Journal History">
        <Table
          columns={journalColumns}
          dataSource={journals}
          loading={loading}
          rowKey="id"
        />
      </Card>

      {/* View Journal Modal */}
      <Modal
        title="Journal Entry Details"
        open={viewModalVisible}
        onCancel={() => setViewModalVisible(false)}
        footer={null}
        width={800}
      >
        {selectedJournal && (
          <div>
            <p><strong>Description:</strong> {selectedJournal.description}</p>
            <p><strong>Date:</strong> {new Date(selectedJournal.createdAt).toLocaleDateString()}</p>
            <p><strong>Status:</strong> <Tag color={selectedJournal.status === 'Posted' ? 'green' : 'orange'}>{selectedJournal.status}</Tag></p>
            
            <Table
              columns={[
                { title: 'Account', dataIndex: 'accountName' },
                { title: 'Debit', dataIndex: 'debit', render: (val) => `$${val.toFixed(2)}` },
                { title: 'Credit', dataIndex: 'credit', render: (val) => `$${val.toFixed(2)}` },
                { title: 'Description', dataIndex: 'description' },
              ]}
              dataSource={selectedJournal.entries}
              pagination={false}
              size="small"
            />
          </div>
        )}
      </Modal>
    </div>
  );
};

export default Journal;
