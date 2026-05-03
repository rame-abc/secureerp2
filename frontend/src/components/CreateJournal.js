import React, { useState, useEffect } from 'react';
import { 
  Card, 
  Form, 
  Input, 
  Button, 
  Row, 
  Col, 
  Select, 
  InputNumber, 
  Table, 
  message, 
  Space,
  Modal,
  Tag
} from 'antd';
import { PlusOutlined, DeleteOutlined, SaveOutlined } from '@ant-design/icons';
import axios from 'axios';

const { TextArea } = Input;

const CreateJournal = () => {
  const [form] = Form.useForm();
  const [accounts, setAccounts] = useState([]);
  const [entries, setEntries] = useState([]);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    fetchAccounts();
  }, []);

  const fetchAccounts = async () => {
    try {
      const token = localStorage.getItem('token');
      const headers = { 'Authorization': `Bearer ${token}` };
      const response = await axios.get('/api/finance/accounts', { headers });
      setAccounts(response.data.map(account => ({
        ...account,
        label: `${account.accountCode} - ${account.accountName}`,
        value: account.id
      })));
    } catch (error) {
      message.error('Failed to fetch accounts');
    }
  };

  const addEntry = () => {
    const newEntry = {
      key: Date.now(),
      accountId: null,
      debit: 0,
      credit: 0,
      description: ''
    };
    setEntries([...entries, newEntry]);
  };

  const removeEntry = (key) => {
    setEntries(entries.filter(entry => entry.key !== key));
  };

  const updateEntry = (key, field, value) => {
    setEntries(entries.map(entry => {
      if (entry.key === key) {
        return { ...entry, [field]: value };
      }
      return entry;
    }));
  };

  const calculateTotals = () => {
    const totalDebit = entries.reduce((sum, entry) => sum + (entry.debit || 0), 0);
    const totalCredit = entries.reduce((sum, entry) => sum + (entry.credit || 0), 0);
    return { totalDebit, totalCredit };
  };

  const { totalDebit, totalCredit } = calculateTotals();
  const isBalanced = Math.abs(totalDebit - totalCredit) < 0.01;

  const handleSubmit = async () => {
    if (!isBalanced) {
      message.error('Journal entries must be balanced (total debit = total credit)');
      return;
    }

    if (entries.length < 2) {
      message.error('Journal must have at least 2 entries');
      return;
    }

    setSubmitting(true);
    try {
      const token = localStorage.getItem('token');
      const headers = { 'Authorization': `Bearer ${token}` };

      const journalData = {
        description: form.getFieldValue('description'),
        entries: entries.map(entry => ({
          accountId: entry.accountId,
          debit: entry.debit,
          credit: entry.credit
        }))
      };

      await axios.post('/api/finance/journal', journalData, { headers });
      message.success('Journal entry created successfully!');
      
      // Reset form
      form.resetFields();
      setEntries([]);
    } catch (error) {
      message.error('Failed to create journal entry');
    } finally {
      setSubmitting(false);
    }
  };

  const entryColumns = [
    {
      title: 'Account',
      dataIndex: 'accountId',
      key: 'accountId',
      render: (value, record) => (
        <Select
          placeholder="Select account"
          style={{ width: '100%' }}
          value={value}
          onChange={(val) => updateEntry(record.key, 'accountId', val)}
          options={accounts}
        />
      ),
    },
    {
      title: 'Debit',
      dataIndex: 'debit',
      key: 'debit',
      render: (value, record) => (
        <InputNumber
          placeholder="0.00"
          style={{ width: '100%' }}
          value={value}
          onChange={(val) => updateEntry(record.key, 'debit', val || 0)}
          min={0}
          precision={2}
        />
      ),
    },
    {
      title: 'Credit',
      dataIndex: 'credit',
      key: 'credit',
      render: (value, record) => (
        <InputNumber
          placeholder="0.00"
          style={{ width: '100%' }}
          value={value}
          onChange={(val) => updateEntry(record.key, 'credit', val || 0)}
          min={0}
          precision={2}
        />
      ),
    },
    {
      title: 'Description',
      dataIndex: 'description',
      key: 'description',
      render: (value, record) => (
        <Input
          placeholder="Entry description"
          value={value}
          onChange={(e) => updateEntry(record.key, 'description', e.target.value)}
        />
      ),
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, record) => (
        <Button
          type="text"
          danger
          icon={<DeleteOutlined />}
          onClick={() => removeEntry(record.key)}
        />
      ),
    },
  ];

  return (
    <div>
      <Card title="Create Journal Entry" style={{ marginBottom: 16 }}>
        <Form form={form} layout="vertical">
          <Form.Item
            label="Description"
            name="description"
            rules={[{ required: true, message: 'Please enter journal description!' }]}
          >
            <TextArea rows={3} placeholder="Enter journal description" />
          </Form.Item>
        </Form>
      </Card>

      <Card title="Journal Entries" style={{ marginBottom: 16 }}>
        <div style={{ marginBottom: 16 }}>
          <Button type="primary" icon={<PlusOutlined />} onClick={addEntry}>
            Add Entry
          </Button>
        </div>
        
        <Table
          columns={entryColumns}
          dataSource={entries}
          pagination={false}
          size="small"
        />

        <div style={{ marginTop: 16, display: 'flex', justifyContent: 'space-between' }}>
          <div>
            <strong>Total Debit:</strong> ${totalDebit.toFixed(2)}
          </div>
          <div>
            <strong>Total Credit:</strong> ${totalCredit.toFixed(2)}
          </div>
          <div>
            <Tag color={isBalanced ? 'green' : 'red'}>
              {isBalanced ? 'Balanced' : 'Not Balanced'}
            </Tag>
          </div>
        </div>
      </Card>

      <Card>
        <Space>
          <Button 
            type="primary" 
            icon={<SaveOutlined />}
            onClick={handleSubmit}
            loading={submitting}
            disabled={!isBalanced || entries.length < 2}
          >
            Create Journal Entry
          </Button>
        </Space>
      </Card>
    </div>
  );
};

export default CreateJournal;
