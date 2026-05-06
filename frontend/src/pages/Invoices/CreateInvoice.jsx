import React, { useState, useEffect } from 'react';
import {
  Form,
  Input,
  Button,
  Card,
  Row,
  Col,
  Select,
  DatePicker,
  Table,
  InputNumber,
  Typography,
  Space,
  Divider,
  message,
  Upload,
  Modal,
  Tooltip,
  Switch,
  Tag
} from 'antd';
import {
  PlusOutlined,
  DeleteOutlined,
  SaveOutlined,
  SendOutlined,
  EyeOutlined,
  UploadOutlined,
  CalculatorOutlined,
  FileTextOutlined,
  DollarOutlined,
  PercentOutlined
} from '@ant-design/icons';
import { useNavigate, useParams } from 'react-router-dom';
import { invoiceService } from '../../api/services/apiServices';
import dayjs from 'dayjs';

const { Title, Text } = Typography;
const { TextArea } = Input;
const { Option } = Select;

const CreateInvoice = () => {
  const [form] = Form.useForm();
  const navigate = useNavigate();
  const { id } = useParams();
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState([]);
  const [customers, setCustomers] = useState([]);
  const [products, setProducts] = useState([]);
  const [taxRate, setTaxRate] = useState(10);
  const [subtotal, setSubtotal] = useState(0);
  const [taxAmount, setTaxAmount] = useState(0);
  const [totalAmount, setTotalAmount] = useState(0);
  const [isDuplicate, setIsDuplicate] = useState(false);
  const [previewVisible, setPreviewVisible] = useState(false);
  const [previewData, setPreviewData] = useState(null);

  // Check if we're duplicating an invoice
  useEffect(() => {
    if (id && id.includes('duplicate')) {
      const originalId = id.split('=')[1];
      loadInvoiceForDuplication(originalId);
      setIsDuplicate(true);
    }
  }, [id]);

  // Load invoice for duplication
  const loadInvoiceForDuplication = async (invoiceId) => {
    try {
      const invoice = await invoiceService.getInvoice(invoiceId);
      form.setFieldsValue({
        customerId: invoice.customerId,
        issueDate: dayjs(invoice.issueDate),
        dueDate: dayjs(invoice.dueDate),
        notes: invoice.notes,
        taxRate: invoice.taxRate
      });
      setItems(invoice.items);
      setTaxRate(invoice.taxRate);
    } catch (error) {
      message.error('Failed to load invoice for duplication');
    }
  };

  // Mock data
  useEffect(() => {
    // Mock customers
    setCustomers([
      { id: 1, name: 'ABC Corporation', email: 'billing@abc-corp.com', phone: '+1-555-0101', address: '123 Business St, New York, NY 10001' },
      { id: 2, name: 'XYZ Industries', email: 'accounts@xyz-ind.com', phone: '+1-555-0102', address: '456 Industrial Ave, Los Angeles, CA 90001' },
      { id: 3, name: 'Global Services Ltd', email: 'finance@global-services.com', phone: '+1-555-0103', address: '789 Service Rd, Chicago, IL 60007' },
      { id: 4, name: 'Tech Solutions Inc', email: 'billing@tech-solutions.com', phone: '+1-555-0104', address: '321 Tech Blvd, Austin, TX 73301' },
      { id: 5, name: 'Innovation Labs', email: 'accounts@innovation-labs.com', phone: '+1-555-0105', address: '654 Innovation Dr, Boston, MA 02101' }
    ]);

    // Mock products/services
    setProducts([
      { id: 1, name: 'Consulting Services', description: 'Professional consulting services', unitPrice: 150, type: 'service' },
      { id: 2, name: 'Software License', description: 'Annual software license', unitPrice: 8750, type: 'product' },
      { id: 3, name: 'Project Management', description: 'Project management services', unitPrice: 250, type: 'service' },
      { id: 4, name: 'Development Services', description: 'Custom development work', unitPrice: 110, type: 'service' },
      { id: 5, name: 'Research Services', description: 'Research and analysis', unitPrice: 246.67, type: 'service' },
      { id: 6, name: 'Support Package', description: '24/7 technical support', unitPrice: 500, type: 'service' },
      { id: 7, name: 'Training Session', description: 'Employee training program', unitPrice: 1000, type: 'service' },
      { id: 8, name: 'Hardware Equipment', description: 'Computer hardware', unitPrice: 2500, type: 'product' }
    ]);

    // Set default dates
    form.setFieldsValue({
      issueDate: dayjs(),
      dueDate: dayjs().add(30, 'day'),
      taxRate: 10
    });
  }, [form]);

  // Calculate totals
  useEffect(() => {
    const newSubtotal = items.reduce((sum, item) => sum + (item.quantity * item.unitPrice), 0);
    const newTaxAmount = newSubtotal * (taxRate / 100);
    const newTotalAmount = newSubtotal + newTaxAmount;
    
    setSubtotal(newSubtotal);
    setTaxAmount(newTaxAmount);
    setTotalAmount(newTotalAmount);
  }, [items, taxRate]);

  // Add item
  const addItem = () => {
    const newItem = {
      id: Date.now(),
      productId: null,
      description: '',
      quantity: 1,
      unitPrice: 0,
      amount: 0
    };
    setItems([...items, newItem]);
  };

  // Update item
  const updateItem = (itemId, field, value) => {
    const updatedItems = items.map(item => {
      if (item.id === itemId) {
        const updatedItem = { ...item, [field]: value };
        
        // If product is selected, update description and unit price
        if (field === 'productId') {
          const product = products.find(p => p.id === value);
          if (product) {
            updatedItem.description = product.description;
            updatedItem.unitPrice = product.unitPrice;
          }
        }
        
        // Recalculate amount
        updatedItem.amount = updatedItem.quantity * updatedItem.unitPrice;
        
        return updatedItem;
      }
      return item;
    });
    setItems(updatedItems);
  };

  // Remove item
  const removeItem = (itemId) => {
    setItems(items.filter(item => item.id !== itemId));
  };

  // Handle customer selection
  const handleCustomerChange = (customerId) => {
    const customer = customers.find(c => c.id === customerId);
    if (customer) {
      // You could auto-fill customer information here
      console.log('Selected customer:', customer);
    }
  };

  // Save invoice
  const saveInvoice = async (sendEmail = false) => {
    try {
      setLoading(true);
      
      const values = await form.validateFields();
      
      const invoiceData = {
        customerId: values.customerId,
        issueDate: values.issueDate.format('YYYY-MM-DD'),
        dueDate: values.dueDate.format('YYYY-MM-DD'),
        notes: values.notes,
        taxRate: taxRate,
        items: items.map(item => ({
          productId: item.productId,
          description: item.description,
          quantity: item.quantity,
          unitPrice: item.unitPrice,
          amount: item.amount
        })),
        subtotal,
        taxAmount,
        totalAmount,
        status: sendEmail ? 'sent' : 'draft'
      };

      if (isDuplicate) {
        // Create new invoice from duplicate
        const response = await invoiceService.createInvoice(invoiceData);
        message.success('Invoice created successfully');
        navigate(`/invoices/${response.id}`);
      } else {
        // Create new invoice
        const response = await invoiceService.createInvoice(invoiceData);
        message.success('Invoice saved successfully');
        
        if (sendEmail) {
          await invoiceService.sendInvoice(response.id);
          message.success('Invoice sent to customer');
        }
        
        navigate(`/invoices/${response.id}`);
      }
    } catch (error) {
      message.error('Failed to save invoice');
    } finally {
      setLoading(false);
    }
  };

  // Preview invoice
  const previewInvoice = () => {
    const values = form.getFieldsValue();
    const customer = customers.find(c => c.id === values.customerId);
    
    setPreviewData({
      invoiceNumber: isDuplicate ? 'INV-2024-006' : 'INV-2024-005',
      customer,
      issueDate: values.issueDate,
      dueDate: values.dueDate,
      notes: values.notes,
      taxRate,
      items,
      subtotal,
      taxAmount,
      totalAmount
    });
    setPreviewVisible(true);
  };

  // Table columns for items
  const itemColumns = [
    {
      title: 'Product/Service',
      dataIndex: 'productId',
      key: 'productId',
      width: 200,
      render: (productId, record) => (
        <Select
          style={{ width: '100%' }}
          value={productId}
          onChange={(value) => updateItem(record.id, 'productId', value)}
          placeholder="Select product/service"
          showSearch
          filterOption={(input, option) =>
            option.children.toLowerCase().indexOf(input.toLowerCase()) >= 0
          }
        >
          {products.map(product => (
            <Option key={product.id} value={product.id}>
              {product.name}
            </Option>
          ))}
        </Select>
      )
    },
    {
      title: 'Description',
      dataIndex: 'description',
      key: 'description',
      render: (text, record) => (
        <Input
          value={text}
          onChange={(e) => updateItem(record.id, 'description', e.target.value)}
          placeholder="Item description"
        />
      )
    },
    {
      title: 'Quantity',
      dataIndex: 'quantity',
      key: 'quantity',
      width: 100,
      render: (value, record) => (
        <InputNumber
          style={{ width: '100%' }}
          value={value}
          min={1}
          onChange={(val) => updateItem(record.id, 'quantity', val || 1)}
        />
      )
    },
    {
      title: 'Unit Price',
      dataIndex: 'unitPrice',
      key: 'unitPrice',
      width: 120,
      render: (value, record) => (
        <InputNumber
          style={{ width: '100%' }}
          value={value}
          min={0}
          precision={2}
          prefix="$"
          onChange={(val) => updateItem(record.id, 'unitPrice', val || 0)}
        />
      )
    },
    {
      title: 'Amount',
      dataIndex: 'amount',
      key: 'amount',
      width: 120,
      align: 'right',
      render: (amount) => (
        <Text strong>${amount.toFixed(2)}</Text>
      )
    },
    {
      title: 'Actions',
      key: 'actions',
      width: 80,
      render: (_, record) => (
        <Button
          type="text"
          danger
          icon={<DeleteOutlined />}
          onClick={() => removeItem(record.id)}
        />
      )
    }
  ];

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
          <Title level={2} style={{ margin: 0 }}>
            {isDuplicate ? 'Duplicate Invoice' : 'Create New Invoice'}
          </Title>
          <Text type="secondary">
            {isDuplicate ? 'Create a new invoice based on an existing one' : 'Create and send professional invoices to your customers'}
          </Text>
        </div>
        <Space>
          <Button icon={<EyeOutlined />} onClick={previewInvoice}>
            Preview
          </Button>
          <Button onClick={() => navigate('/invoices')}>
            Cancel
          </Button>
        </Space>
      </div>

      <Form form={form} layout="vertical">
        <Row gutter={[24, 24]}>
          {/* Left Column - Customer Information */}
          <Col xs={24} lg={12}>
            <Card title="Customer Information" style={{ marginBottom: '24px' }}>
              <Form.Item
                name="customerId"
                label="Customer"
                rules={[{ required: true, message: 'Please select a customer' }]}
              >
                <Select
                  placeholder="Select customer"
                  showSearch
                  filterOption={(input, option) =>
                    option.children.toLowerCase().indexOf(input.toLowerCase()) >= 0
                  }
                  onChange={handleCustomerChange}
                >
                  {customers.map(customer => (
                    <Option key={customer.id} value={customer.id}>
                      <div>
                        <div style={{ fontWeight: 'bold' }}>{customer.name}</div>
                        <div style={{ fontSize: '12px', color: '#666' }}>{customer.email}</div>
                      </div>
                    </Option>
                  ))}
                </Select>
              </Form.Item>

              <Row gutter={16}>
                <Col span={12}>
                  <Form.Item
                    name="issueDate"
                    label="Issue Date"
                    rules={[{ required: true, message: 'Please select issue date' }]}
                  >
                    <DatePicker style={{ width: '100%' }} />
                  </Form.Item>
                </Col>
                <Col span={12}>
                  <Form.Item
                    name="dueDate"
                    label="Due Date"
                    rules={[{ required: true, message: 'Please select due date' }]}
                  >
                    <DatePicker style={{ width: '100%' }} />
                  </Form.Item>
                </Col>
              </Row>

              <Form.Item
                name="taxRate"
                label="Tax Rate (%)"
              >
                <InputNumber
                  style={{ width: '100%' }}
                  min={0}
                  max={100}
                  precision={2}
                  value={taxRate}
                  onChange={(value) => setTaxRate(value || 0)}
                  suffix="%"
                />
              </Form.Item>
            </Card>
          </Col>

          {/* Right Column - Invoice Summary */}
          <Col xs={24} lg={12}>
            <Card title="Invoice Summary" style={{ marginBottom: '24px' }}>
              <Row gutter={16}>
                <Col span={12}>
                  <div style={{ marginBottom: '16px' }}>
                    <Text type="secondary">Subtotal:</Text>
                    <div style={{ fontSize: '18px', fontWeight: 'bold' }}>
                      ${subtotal.toFixed(2)}
                    </div>
                  </div>
                </Col>
                <Col span={12}>
                  <div style={{ marginBottom: '16px' }}>
                    <Text type="secondary">Tax ({taxRate}%):</Text>
                    <div style={{ fontSize: '18px', fontWeight: 'bold' }}>
                      ${taxAmount.toFixed(2)}
                    </div>
                  </div>
                </Col>
              </Row>
              
              <Divider />
              
              <Row>
                <Col span={12}>
                  <Text strong>Total Amount:</Text>
                </Col>
                <Col span={12} style={{ textAlign: 'right' }}>
                  <Text strong style={{ fontSize: '24px', color: '#52c41a' }}>
                    ${totalAmount.toFixed(2)}
                  </Text>
                </Col>
              </Row>
            </Card>
          </Col>
        </Row>

        {/* Invoice Items */}
        <Card 
          title="Invoice Items" 
          style={{ marginBottom: '24px' }}
          extra={
            <Button icon={<PlusOutlined />} onClick={addItem}>
              Add Item
            </Button>
          }
        >
          <Table
            columns={itemColumns}
            dataSource={items}
            rowKey="id"
            pagination={false}
            size="small"
            locale={{
              emptyText: 'No items added. Click "Add Item" to get started.'
            }}
          />
        </Card>

        {/* Notes */}
        <Card title="Additional Notes" style={{ marginBottom: '24px' }}>
          <Form.Item name="notes">
            <TextArea
              rows={4}
              placeholder="Add any additional notes or terms for this invoice..."
            />
          </Form.Item>
        </Card>

        {/* Actions */}
        <Card>
          <Row justify="space-between" align="middle">
            <Col>
              <Space>
                <Button icon={<SaveOutlined />} onClick={() => saveInvoice(false)} loading={loading}>
                  Save as Draft
                </Button>
                <Button 
                  type="primary" 
                  icon={<SendOutlined />} 
                  onClick={() => saveInvoice(true)} 
                  loading={loading}
                >
                  Save & Send
                </Button>
              </Space>
            </Col>
            <Col>
              <Space>
                <Button onClick={() => navigate('/invoices')}>
                  Cancel
                </Button>
              </Space>
            </Col>
          </Row>
        </Card>
      </Form>

      {/* Preview Modal */}
      <Modal
        title="Invoice Preview"
        open={previewVisible}
        onCancel={() => setPreviewVisible(false)}
        footer={[
          <Button key="close" onClick={() => setPreviewVisible(false)}>
            Close
          </Button>,
          <Button key="edit" type="primary" onClick={() => setPreviewVisible(false)}>
            Continue Editing
          </Button>
        ]}
        width={800}
      >
        {previewData && (
          <div style={{ padding: '20px', background: '#fff' }}>
            {/* Invoice Header */}
            <div style={{ textAlign: 'center', marginBottom: '30px' }}>
              <Title level={3} style={{ margin: 0 }}>
                INVOICE
              </Title>
              <Text type="secondary">{previewData.invoiceNumber}</Text>
            </div>

            {/* Customer Info */}
            <Row gutter={24} style={{ marginBottom: '30px' }}>
              <Col span={12}>
                <div>
                  <Text strong>Bill To:</Text>
                  <div style={{ marginTop: '8px' }}>
                    <div style={{ fontWeight: 'bold' }}>{previewData.customer?.name}</div>
                    <div>{previewData.customer?.email}</div>
                    <div>{previewData.customer?.phone}</div>
                    <div>{previewData.customer?.address}</div>
                  </div>
                </div>
              </Col>
              <Col span={12} style={{ textAlign: 'right' }}>
                <div>
                  <div><Text strong>Issue Date:</Text> {previewData.issueDate?.format('MMMM DD, YYYY')}</div>
                  <div><Text strong>Due Date:</Text> {previewData.dueDate?.format('MMMM DD, YYYY')}</div>
                </div>
              </Col>
            </Row>

            {/* Items Table */}
            <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: '20px' }}>
              <thead>
                <tr style={{ background: '#fafafa' }}>
                  <th style={{ padding: '12px', textAlign: 'left', borderBottom: '1px solid #d9d9d9' }}>Description</th>
                  <th style={{ padding: '12px', textAlign: 'right', borderBottom: '1px solid #d9d9d9' }}>Quantity</th>
                  <th style={{ padding: '12px', textAlign: 'right', borderBottom: '1px solid #d9d9d9' }}>Unit Price</th>
                  <th style={{ padding: '12px', textAlign: 'right', borderBottom: '1px solid #d9d9d9' }}>Amount</th>
                </tr>
              </thead>
              <tbody>
                {previewData.items.map((item, index) => (
                  <tr key={index}>
                    <td style={{ padding: '12px', borderBottom: '1px solid #f0f0f0' }}>{item.description}</td>
                    <td style={{ padding: '12px', textAlign: 'right', borderBottom: '1px solid #f0f0f0' }}>{item.quantity}</td>
                    <td style={{ padding: '12px', textAlign: 'right', borderBottom: '1px solid #f0f0f0' }}>${item.unitPrice.toFixed(2)}</td>
                    <td style={{ padding: '12px', textAlign: 'right', borderBottom: '1px solid #f0f0f0' }}>${item.amount.toFixed(2)}</td>
                  </tr>
                ))}
              </tbody>
            </table>

            {/* Totals */}
            <div style={{ textAlign: 'right' }}>
              <div style={{ marginBottom: '8px' }}>
                <Text>Subtotal: </Text>
                <Text>${previewData.subtotal.toFixed(2)}</Text>
              </div>
              <div style={{ marginBottom: '8px' }}>
                <Text>Tax ({previewData.taxRate}%): </Text>
                <Text>${previewData.taxAmount.toFixed(2)}</Text>
              </div>
              <div style={{ marginBottom: '16px', paddingTop: '8px', borderTop: '2px solid #d9d9d9' }}>
                <Text strong style={{ fontSize: '16px' }}>Total: </Text>
                <Text strong style={{ fontSize: '16px', color: '#52c41a' }}>
                  ${previewData.totalAmount.toFixed(2)}
                </Text>
              </div>
            </div>

            {/* Notes */}
            {previewData.notes && (
              <div style={{ marginTop: '20px', padding: '16px', background: '#fafafa', borderRadius: '4px' }}>
                <Text strong>Notes:</Text>
                <div style={{ marginTop: '8px' }}>{previewData.notes}</div>
              </div>
            )}
          </div>
        )}
      </Modal>
    </div>
  );
};

export default CreateInvoice;
