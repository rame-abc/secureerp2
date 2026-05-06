import React, { useState, useEffect } from 'react';
import {
  Table,
  Card,
  Button,
  Space,
  Typography,
  Tag,
  Input,
  Select,
  DatePicker,
  Row,
  Col,
  Statistic,
  Tooltip,
  Dropdown,
  Menu,
  Modal,
  message,
  Drawer,
  Badge
} from 'antd';
import {
  PlusOutlined,
  SearchOutlined,
  FilterOutlined,
  ExportOutlined,
  EyeOutlined,
  EditOutlined,
  DeleteOutlined,
  FileTextOutlined,
  SendOutlined,
  DownloadOutlined,
  MoreOutlined,
  ReloadOutlined,
  DollarOutlined,
  ClockCircleOutlined,
  CheckCircleOutlined,
  ExclamationCircleOutlined
} from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { invoiceService } from '../../api/services/apiServices';
import dayjs from 'dayjs';

const { Title, Text } = Typography;
const { Search } = Input;
const { RangePicker } = DatePicker;
const { Option } = Select;

const InvoiceList = () => {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [invoices, setInvoices] = useState([]);
  const [filteredData, setFilteredData] = useState([]);
  const [selectedRowKeys, setSelectedRowKeys] = useState([]);
  const [filters, setFilters] = useState({
    status: 'all',
    customer: '',
    dateRange: null,
    search: ''
  });
  const [statistics, setStatistics] = useState({
    totalInvoices: 0,
    totalAmount: 0,
    paidAmount: 0,
    pendingAmount: 0,
    overdueAmount: 0
  });
  const [detailDrawerVisible, setDetailDrawerVisible] = useState(false);
  const [selectedInvoice, setSelectedInvoice] = useState(null);

  // Fetch invoices
  const fetchInvoices = async () => {
    setLoading(true);
    try {
      const response = await invoiceService.getInvoices({
        status: filters.status !== 'all' ? filters.status : undefined,
        search: filters.search || undefined,
        startDate: filters.dateRange?.[0]?.format('YYYY-MM-DD'),
        endDate: filters.dateRange?.[1]?.format('YYYY-MM-DD')
      });
      
      // Mock data for now
      const mockInvoices = [
        {
          id: 1,
          invoiceNumber: 'INV-2024-001',
          customer: 'ABC Corporation',
          email: 'billing@abc-corp.com',
          amount: 15000,
          taxAmount: 1500,
          totalAmount: 16500,
          status: 'paid',
          issueDate: '2024-10-15',
          dueDate: '2024-11-15',
          paidDate: '2024-11-10',
          items: [
            { description: 'Consulting Services', quantity: 100, unitPrice: 150, amount: 15000 }
          ],
          notes: 'Payment received via bank transfer'
        },
        {
          id: 2,
          invoiceNumber: 'INV-2024-002',
          customer: 'XYZ Industries',
          email: 'accounts@xyz-ind.com',
          amount: 8750,
          taxAmount: 875,
          totalAmount: 9625,
          status: 'pending',
          issueDate: '2024-10-20',
          dueDate: '2024-11-20',
          items: [
            { description: 'Software License', quantity: 1, unitPrice: 8750, amount: 8750 }
          ],
          notes: 'Payment due in 30 days'
        },
        {
          id: 3,
          invoiceNumber: 'INV-2024-003',
          customer: 'Global Services Ltd',
          email: 'finance@global-services.com',
          amount: 12500,
          taxAmount: 1250,
          totalAmount: 13750,
          status: 'overdue',
          issueDate: '2024-09-25',
          dueDate: '2024-10-25',
          items: [
            { description: 'Project Management', quantity: 50, unitPrice: 250, amount: 12500 }
          ],
          notes: 'Follow up required - payment overdue'
        },
        {
          id: 4,
          invoiceNumber: 'INV-2024-004',
          customer: 'Tech Solutions Inc',
          email: 'billing@tech-solutions.com',
          amount: 22000,
          taxAmount: 2200,
          totalAmount: 24200,
          status: 'sent',
          issueDate: '2024-10-28',
          dueDate: '2024-11-28',
          items: [
            { description: 'Development Services', quantity: 200, unitPrice: 110, amount: 22000 }
          ],
          notes: 'Invoice sent via email'
        },
        {
          id: 5,
          invoiceNumber: 'INV-2024-005',
          customer: 'Innovation Labs',
          email: 'accounts@innovation-labs.com',
          amount: 18500,
          taxAmount: 1850,
          totalAmount: 20350,
          status: 'draft',
          issueDate: '2024-10-30',
          dueDate: '2024-11-30',
          items: [
            { description: 'Research Services', quantity: 75, unitPrice: 246.67, amount: 18500 }
          ],
          notes: 'Draft invoice - pending review'
        }
      ];

      setInvoices(mockInvoices);
      setFilteredData(mockInvoices);
      calculateStatistics(mockInvoices);
    } catch (error) {
      message.error('Failed to fetch invoices');
    } finally {
      setLoading(false);
    }
  };

  // Calculate statistics
  const calculateStatistics = (data) => {
    const stats = {
      totalInvoices: data.length,
      totalAmount: data.reduce((sum, inv) => sum + inv.totalAmount, 0),
      paidAmount: data.filter(inv => inv.status === 'paid').reduce((sum, inv) => sum + inv.totalAmount, 0),
      pendingAmount: data.filter(inv => inv.status === 'pending').reduce((sum, inv) => sum + inv.totalAmount, 0),
      overdueAmount: data.filter(inv => inv.status === 'overdue').reduce((sum, inv) => sum + inv.totalAmount, 0)
    };
    setStatistics(stats);
  };

  // Apply filters
  const applyFilters = () => {
    let filtered = [...invoices];

    if (filters.status !== 'all') {
      filtered = filtered.filter(inv => inv.status === filters.status);
    }

    if (filters.search) {
      filtered = filtered.filter(inv => 
        inv.invoiceNumber.toLowerCase().includes(filters.search.toLowerCase()) ||
        inv.customer.toLowerCase().includes(filters.search.toLowerCase()) ||
        inv.email.toLowerCase().includes(filters.search.toLowerCase())
      );
    }

    if (filters.dateRange && filters.dateRange.length === 2) {
      const [start, end] = filters.dateRange;
      filtered = filtered.filter(inv => {
        const invDate = dayjs(inv.issueDate);
        return invDate.isAfter(start.startOf('day')) && invDate.isBefore(end.endOf('day'));
      });
    }

    setFilteredData(filtered);
    calculateStatistics(filtered);
  };

  // Handle filter changes
  const handleFilterChange = (key, value) => {
    setFilters(prev => ({ ...prev, [key]: value }));
  };

  // Apply filters when they change
  useEffect(() => {
    applyFilters();
  }, [filters, invoices]);

  // Initial fetch
  useEffect(() => {
    fetchInvoices();
  }, []);

  // Table columns
  const columns = [
    {
      title: 'Invoice #',
      dataIndex: 'invoiceNumber',
      key: 'invoiceNumber',
      width: 150,
      render: (text, record) => (
        <Button type="link" onClick={() => viewInvoice(record)}>
          {text}
        </Button>
      )
    },
    {
      title: 'Customer',
      dataIndex: 'customer',
      key: 'customer',
      width: 200,
      render: (text, record) => (
        <div>
          <div style={{ fontWeight: 'bold' }}>{text}</div>
          <div style={{ fontSize: '12px', color: '#666' }}>{record.email}</div>
        </div>
      )
    },
    {
      title: 'Amount',
      dataIndex: 'totalAmount',
      key: 'totalAmount',
      width: 120,
      align: 'right',
      render: (amount, record) => (
        <div>
          <div style={{ fontWeight: 'bold' }}>${amount.toLocaleString()}</div>
          {record.taxAmount > 0 && (
            <div style={{ fontSize: '11px', color: '#666' }}>
              Tax: ${record.taxAmount.toLocaleString()}
            </div>
          )}
        </div>
      )
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      width: 100,
      render: (status) => {
        const statusConfig = {
          draft: { color: 'default', icon: <FileTextOutlined />, text: 'Draft' },
          sent: { color: 'blue', icon: <SendOutlined />, text: 'Sent' },
          pending: { color: 'orange', icon: <ClockCircleOutlined />, text: 'Pending' },
          paid: { color: 'green', icon: <CheckCircleOutlined />, text: 'Paid' },
          overdue: { color: 'red', icon: <ExclamationCircleOutlined />, text: 'Overdue' }
        };
        const config = statusConfig[status] || statusConfig.draft;
        return (
          <Tag color={config.color} icon={config.icon}>
            {config.text}
          </Tag>
        );
      }
    },
    {
      title: 'Issue Date',
      dataIndex: 'issueDate',
      key: 'issueDate',
      width: 100,
      render: (date) => dayjs(date).format('MMM DD, YYYY')
    },
    {
      title: 'Due Date',
      dataIndex: 'dueDate',
      key: 'dueDate',
      width: 100,
      render: (date, record) => {
        const isOverdue = dayjs().isAfter(dayjs(date)) && record.status !== 'paid';
        return (
          <div style={{ color: isOverdue ? '#ff4d4f' : 'inherit' }}>
            {dayjs(date).format('MMM DD, YYYY')}
          </div>
        );
      }
    },
    {
      title: 'Actions',
      key: 'actions',
      width: 120,
      render: (_, record) => {
        const menuItems = [
          {
            key: 'view',
            icon: <EyeOutlined />,
            label: 'View Details',
            onClick: () => viewInvoice(record)
          },
          {
            key: 'edit',
            icon: <EditOutlined />,
            label: 'Edit',
            onClick: () => editInvoice(record),
            disabled: record.status === 'paid'
          },
          {
            key: 'duplicate',
            icon: <FileTextOutlined />,
            label: 'Duplicate',
            onClick: () => duplicateInvoice(record)
          },
          {
            type: 'divider'
          },
          {
            key: 'send',
            icon: <SendOutlined />,
            label: 'Send Email',
            onClick: () => sendInvoice(record),
            disabled: record.status === 'draft'
          },
          {
            key: 'download',
            icon: <DownloadOutlined />,
            label: 'Download PDF',
            onClick: () => downloadPDF(record)
          },
          {
            type: 'divider'
          },
          {
            key: 'delete',
            icon: <DeleteOutlined />,
            label: 'Delete',
            onClick: () => deleteInvoice(record),
            danger: true,
            disabled: record.status === 'paid'
          }
        ];

        return (
          <Dropdown
            menu={{ items: menuItems }}
            trigger={['click']}
            placement="bottomRight"
          >
            <Button type="text" icon={<MoreOutlined />} />
          </Dropdown>
        );
      }
    }
  ];

  // Action handlers
  const viewInvoice = (invoice) => {
    setSelectedInvoice(invoice);
    setDetailDrawerVisible(true);
  };

  const editInvoice = (invoice) => {
    navigate(`/invoices/${invoice.id}/edit`);
  };

  const duplicateInvoice = (invoice) => {
    navigate(`/invoices/create?duplicate=${invoice.id}`);
  };

  const sendInvoice = async (invoice) => {
    try {
      await invoiceService.sendInvoice(invoice.id);
      message.success('Invoice sent successfully');
      fetchInvoices();
    } catch (error) {
      message.error('Failed to send invoice');
    }
  };

  const downloadPDF = async (invoice) => {
    try {
      const blob = await invoiceService.generateInvoicePDF(invoice.id);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${invoice.invoiceNumber}.pdf`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    } catch (error) {
      message.error('Failed to download PDF');
    }
  };

  const deleteInvoice = (invoice) => {
    Modal.confirm({
      title: 'Delete Invoice',
      content: `Are you sure you want to delete invoice ${invoice.invoiceNumber}? This action cannot be undone.`,
      okText: 'Delete',
      okType: 'danger',
      cancelText: 'Cancel',
      onOk: async () => {
        try {
          await invoiceService.deleteInvoice(invoice.id);
          message.success('Invoice deleted successfully');
          fetchInvoices();
        } catch (error) {
          message.error('Failed to delete invoice');
        }
      }
    });
  };

  const createNewInvoice = () => {
    navigate('/invoices/create');
  };

  const rowSelection = {
    selectedRowKeys,
    onChange: (keys) => setSelectedRowKeys(keys),
    getCheckboxProps: (record) => ({
      disabled: record.status === 'paid'
    })
  };

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
          <Title level={2} style={{ margin: 0 }}>Invoices</Title>
          <Text type="secondary">Manage your customer invoices</Text>
        </div>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={fetchInvoices}>
            Refresh
          </Button>
          <Button icon={<ExportOutlined />}>
            Export
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={createNewInvoice}>
            New Invoice
          </Button>
        </Space>
      </div>

      {/* Statistics Cards */}
      <Row gutter={[16, 16]} style={{ marginBottom: '24px' }}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Total Invoices"
              value={statistics.totalInvoices}
              prefix={<FileTextOutlined />}
              valueStyle={{ color: '#1890ff' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Total Amount"
              value={statistics.totalAmount}
              prefix={<DollarOutlined />}
              formatter={(value) => `$${value.toLocaleString()}`}
              valueStyle={{ color: '#52c41a' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Paid Amount"
              value={statistics.paidAmount}
              prefix={<CheckCircleOutlined />}
              formatter={(value) => `$${value.toLocaleString()}`}
              valueStyle={{ color: '#52c41a' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Overdue Amount"
              value={statistics.overdueAmount}
              prefix={<ExclamationCircleOutlined />}
              formatter={(value) => `$${value.toLocaleString()}`}
              valueStyle={{ color: '#ff4d4f' }}
            />
          </Card>
        </Col>
      </Row>

      {/* Filters */}
      <Card style={{ marginBottom: '24px' }}>
        <Row gutter={[16, 16]} align="middle">
          <Col xs={24} sm={12} md={6}>
            <Search
              placeholder="Search invoices..."
              value={filters.search}
              onChange={(e) => handleFilterChange('search', e.target.value)}
              prefix={<SearchOutlined />}
            />
          </Col>
          <Col xs={24} sm={12} md={4}>
            <Select
              style={{ width: '100%' }}
              value={filters.status}
              onChange={(value) => handleFilterChange('status', value)}
              placeholder="Status"
            >
              <Option value="all">All Status</Option>
              <Option value="draft">Draft</Option>
              <Option value="sent">Sent</Option>
              <Option value="pending">Pending</Option>
              <Option value="paid">Paid</Option>
              <Option value="overdue">Overdue</Option>
            </Select>
          </Col>
          <Col xs={24} sm={12} md={6}>
            <RangePicker
              style={{ width: '100%' }}
              value={filters.dateRange}
              onChange={(dates) => handleFilterChange('dateRange', dates)}
              placeholder={['Start Date', 'End Date']}
            />
          </Col>
          <Col xs={24} sm={12} md={4}>
            <Button icon={<FilterOutlined />} block>
              More Filters
            </Button>
          </Col>
        </Row>
      </Card>

      {/* Invoices Table */}
      <Card>
        {selectedRowKeys.length > 0 && (
          <div style={{ marginBottom: '16px', padding: '12px', background: '#f6ffed', borderRadius: '6px' }}>
            <Space>
              <Text>{selectedRowKeys.length} invoices selected</Text>
              <Button size="small">Send Email</Button>
              <Button size="small">Download PDFs</Button>
              <Button size="small" danger>Delete</Button>
            </Space>
          </div>
        )}
        
        <Table
          columns={columns}
          dataSource={filteredData}
          rowKey="id"
          loading={loading}
          rowSelection={rowSelection}
          pagination={{
            total: filteredData.length,
            pageSize: 10,
            showSizeChanger: true,
            showQuickJumper: true,
            showTotal: (total, range) => `${range[0]}-${range[1]} of ${total} invoices`
          }}
          scroll={{ x: 1200 }}
        />
      </Card>

      {/* Invoice Detail Drawer */}
      <Drawer
        title={`Invoice Details - ${selectedInvoice?.invoiceNumber}`}
        placement="right"
        width={600}
        onClose={() => setDetailDrawerVisible(false)}
        open={detailDrawerVisible}
      >
        {selectedInvoice && (
          <div>
            {/* Invoice Header */}
            <Card style={{ marginBottom: '16px' }}>
              <Row justify="space-between" align="middle">
                <Col>
                  <Title level={4} style={{ margin: 0 }}>{selectedInvoice.invoiceNumber}</Title>
                  <Text type="secondary">{selectedInvoice.customer}</Text>
                </Col>
                <Col>
                  <Tag color={selectedInvoice.status === 'paid' ? 'green' : 'orange'}>
                    {selectedInvoice.status.toUpperCase()}
                  </Tag>
                </Col>
              </Row>
            </Card>

            {/* Invoice Details */}
            <Card style={{ marginBottom: '16px' }}>
              <Row gutter={16}>
                <Col span={12}>
                  <div style={{ marginBottom: '16px' }}>
                    <Text strong>Issue Date:</Text>
                    <div>{dayjs(selectedInvoice.issueDate).format('MMMM DD, YYYY')}</div>
                  </div>
                  <div>
                    <Text strong>Due Date:</Text>
                    <div>{dayjs(selectedInvoice.dueDate).format('MMMM DD, YYYY')}</div>
                  </div>
                </Col>
                <Col span={12}>
                  <div style={{ marginBottom: '16px' }}>
                    <Text strong>Customer Email:</Text>
                    <div>{selectedInvoice.email}</div>
                  </div>
                  {selectedInvoice.paidDate && (
                    <div>
                      <Text strong>Paid Date:</Text>
                      <div>{dayjs(selectedInvoice.paidDate).format('MMMM DD, YYYY')}</div>
                    </div>
                  )}
                </Col>
              </Row>
            </Card>

            {/* Invoice Items */}
            <Card style={{ marginBottom: '16px' }}>
              <Title level={5}>Invoice Items</Title>
              <div style={{ marginBottom: '16px' }}>
                {selectedInvoice.items.map((item, index) => (
                  <Row key={index} style={{ marginBottom: '8px', padding: '8px', background: '#fafafa', borderRadius: '4px' }}>
                    <Col span={12}>
                      <div>{item.description}</div>
                      <Text type="secondary">Qty: {item.quantity} × ${item.unitPrice}</Text>
                    </Col>
                    <Col span={12} style={{ textAlign: 'right' }}>
                      <Text strong>${item.amount.toLocaleString()}</Text>
                    </Col>
                  </Row>
                ))}
              </div>
            </Card>

            {/* Totals */}
            <Card>
              <Row gutter={16}>
                <Col span={12}>
                  <div style={{ marginBottom: '8px' }}>
                    <Text>Subtotal:</Text>
                  </div>
                  <div style={{ marginBottom: '8px' }}>
                    <Text>Tax:</Text>
                  </div>
                  <div>
                    <Text strong>Total:</Text>
                  </div>
                </Col>
                <Col span={12} style={{ textAlign: 'right' }}>
                  <div style={{ marginBottom: '8px' }}>
                    <Text>${selectedInvoice.amount.toLocaleString()}</Text>
                  </div>
                  <div style={{ marginBottom: '8px' }}>
                    <Text>${selectedInvoice.taxAmount.toLocaleString()}</Text>
                  </div>
                  <div>
                    <Text strong style={{ fontSize: '16px', color: '#52c41a' }}>
                      ${selectedInvoice.totalAmount.toLocaleString()}
                    </Text>
                  </div>
                </Col>
              </Row>
            </Card>

            {/* Notes */}
            {selectedInvoice.notes && (
              <Card style={{ marginTop: '16px' }}>
                <Title level={5}>Notes</Title>
                <Text>{selectedInvoice.notes}</Text>
              </Card>
            )}

            {/* Actions */}
            <div style={{ marginTop: '24px', textAlign: 'center' }}>
              <Space>
                <Button icon={<EditOutlined />} onClick={() => editInvoice(selectedInvoice)}>
                  Edit
                </Button>
                <Button icon={<SendOutlined />} onClick={() => sendInvoice(selectedInvoice)}>
                  Send Email
                </Button>
                <Button icon={<DownloadOutlined />} onClick={() => downloadPDF(selectedInvoice)}>
                  Download PDF
                </Button>
              </Space>
            </div>
          </div>
        )}
      </Drawer>
    </div>
  );
};

export default InvoiceList;
