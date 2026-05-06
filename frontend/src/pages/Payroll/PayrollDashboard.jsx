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
  Avatar,
  Tooltip,
  Modal,
  Form,
  Input,
  Select,
  DatePicker,
  InputNumber,
  message,
  Drawer,
  Badge,
  Alert,
  Divider
} from 'antd';
import {
  UserOutlined,
  DollarOutlined,
  CalendarOutlined,
  TeamOutlined,
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  EyeOutlined,
  PlayCircleOutlined,
  PauseCircleOutlined,
  FileTextOutlined,
  DownloadOutlined,
  ReloadOutlined,
  CheckCircleOutlined,
  ClockCircleOutlined,
  ExclamationCircleOutlined,
  PercentageOutlined,
  WalletOutlined,
  BankOutlined
} from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { payrollService } from '../../api/services/apiServices';
import dayjs from 'dayjs';

const { Title, Text } = Typography;
const { Option } = Select;

const PayrollDashboard = () => {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [employees, setEmployees] = useState([]);
  const [payrollRuns, setPayrollRuns] = useState([]);
  const [statistics, setStatistics] = useState({
    totalEmployees: 0,
    activeEmployees: 0,
    totalPayroll: 0,
    nextPayrollDate: null,
    pendingRuns: 0,
    processedRuns: 0
  });
  const [runModalVisible, setRunModalVisible] = useState(false);
  const [selectedEmployee, setSelectedEmployee] = useState(null);
  const [employeeDrawerVisible, setEmployeeDrawerVisible] = useState(false);
  const [runForm] = Form.useForm();

  // Fetch payroll data
  const fetchPayrollData = async () => {
    setLoading(true);
    try {
      // Mock data for demonstration
      const mockEmployees = [
        {
          id: 1,
          firstName: 'John',
          lastName: 'Smith',
          email: 'john.smith@company.com',
          department: 'Engineering',
          position: 'Senior Developer',
          salary: 85000,
          status: 'active',
          hireDate: '2022-03-15',
          bankAccount: '****1234',
          taxId: '***-**-1234'
        },
        {
          id: 2,
          firstName: 'Sarah',
          lastName: 'Johnson',
          email: 'sarah.johnson@company.com',
          department: 'Marketing',
          position: 'Marketing Manager',
          salary: 75000,
          status: 'active',
          hireDate: '2021-07-20',
          bankAccount: '****5678',
          taxId: '***-**-5678'
        },
        {
          id: 3,
          firstName: 'Michael',
          lastName: 'Brown',
          email: 'michael.brown@company.com',
          department: 'Sales',
          position: 'Sales Representative',
          salary: 65000,
          status: 'active',
          hireDate: '2023-01-10',
          bankAccount: '****9012',
          taxId: '***-**-9012'
        },
        {
          id: 4,
          firstName: 'Emily',
          lastName: 'Davis',
          email: 'emily.davis@company.com',
          department: 'HR',
          position: 'HR Specialist',
          salary: 55000,
          status: 'active',
          hireDate: '2022-11-05',
          bankAccount: '****3456',
          taxId: '***-**-3456'
        },
        {
          id: 5,
          firstName: 'David',
          lastName: 'Wilson',
          email: 'david.wilson@company.com',
          department: 'Finance',
          position: 'Financial Analyst',
          salary: 70000,
          status: 'inactive',
          hireDate: '2020-09-12',
          bankAccount: '****7890',
          taxId: '***-**-7890'
        }
      ];

      const mockPayrollRuns = [
        {
          id: 1,
          runNumber: 'PR-2024-10',
          period: 'October 2024',
          runDate: '2024-10-31',
          status: 'processed',
          totalGrossPay: 45000,
          totalNetPay: 36000,
          totalDeductions: 9000,
          employeeCount: 24,
          processedDate: '2024-11-01'
        },
        {
          id: 2,
          runNumber: 'PR-2024-09',
          period: 'September 2024',
          runDate: '2024-09-30',
          status: 'processed',
          totalGrossPay: 43500,
          totalNetPay: 34800,
          totalDeductions: 8700,
          employeeCount: 23,
          processedDate: '2024-10-01'
        },
        {
          id: 3,
          runNumber: 'PR-2024-11',
          period: 'November 2024',
          runDate: '2024-11-30',
          status: 'pending',
          totalGrossPay: 46200,
          totalNetPay: 36960,
          totalDeductions: 9240,
          employeeCount: 25,
          processedDate: null
        }
      ];

      setEmployees(mockEmployees);
      setPayrollRuns(mockPayrollRuns);

      // Calculate statistics
      const stats = {
        totalEmployees: mockEmployees.length,
        activeEmployees: mockEmployees.filter(emp => emp.status === 'active').length,
        totalPayroll: mockEmployees
          .filter(emp => emp.status === 'active')
          .reduce((sum, emp) => sum + (emp.salary / 12), 0),
        nextPayrollDate: dayjs().endOf('month').format('MMMM DD, YYYY'),
        pendingRuns: mockPayrollRuns.filter(run => run.status === 'pending').length,
        processedRuns: mockPayrollRuns.filter(run => run.status === 'processed').length
      };
      setStatistics(stats);
    } catch (error) {
      message.error('Failed to fetch payroll data');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchPayrollData();
  }, []);

  // Employee table columns
  const employeeColumns = [
    {
      title: 'Employee',
      dataIndex: 'firstName',
      key: 'employee',
      render: (_, record) => (
        <div style={{ display: 'flex', alignItems: 'center' }}>
          <Avatar 
            icon={<UserOutlined />} 
            style={{ marginRight: '12px' }}
          />
          <div>
            <div style={{ fontWeight: 'bold' }}>
              {record.firstName} {record.lastName}
            </div>
            <div style={{ fontSize: '12px', color: '#666' }}>
              {record.email}
            </div>
          </div>
        </div>
      )
    },
    {
      title: 'Position',
      dataIndex: 'position',
      key: 'position'
    },
    {
      title: 'Department',
      dataIndex: 'department',
      key: 'department',
      render: (dept) => <Tag color="blue">{dept}</Tag>
    },
    {
      title: 'Annual Salary',
      dataIndex: 'salary',
      key: 'salary',
      align: 'right',
      render: (salary) => <Text strong>${salary.toLocaleString()}</Text>
    },
    {
      title: 'Monthly Pay',
      key: 'monthlyPay',
      align: 'right',
      render: (_, record) => (
        <Text>${(record.salary / 12).toLocaleString()}</Text>
      )
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
          {status.charAt(0).toUpperCase() + status.slice(1)}
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
                setSelectedEmployee(record);
                setEmployeeDrawerVisible(true);
              }}
            />
          </Tooltip>
          <Tooltip title="Edit Employee">
            <Button 
              type="text" 
              icon={<EditOutlined />} 
              onClick={() => navigate(`/payroll/employees/${record.id}`)}
            />
          </Tooltip>
        </Space>
      )
    }
  ];

  // Payroll runs table columns
  const payrollRunColumns = [
    {
      title: 'Run #',
      dataIndex: 'runNumber',
      key: 'runNumber',
      render: (text) => <Text strong>{text}</Text>
    },
    {
      title: 'Period',
      dataIndex: 'period',
      key: 'period'
    },
    {
      title: 'Run Date',
      dataIndex: 'runDate',
      key: 'runDate',
      render: (date) => dayjs(date).format('MMM DD, YYYY')
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status) => {
        const statusConfig = {
          pending: { color: 'orange', icon: <ClockCircleOutlined />, text: 'Pending' },
          processing: { color: 'blue', icon: <PlayCircleOutlined />, text: 'Processing' },
          processed: { color: 'green', icon: <CheckCircleOutlined />, text: 'Processed' },
          failed: { color: 'red', icon: <ExclamationCircleOutlined />, text: 'Failed' }
        };
        const config = statusConfig[status] || statusConfig.pending;
        return (
          <Tag color={config.color} icon={config.icon}>
            {config.text}
          </Tag>
        );
      }
    },
    {
      title: 'Employees',
      dataIndex: 'employeeCount',
      key: 'employeeCount',
      align: 'center'
    },
    {
      title: 'Gross Pay',
      dataIndex: 'totalGrossPay',
      key: 'totalGrossPay',
      align: 'right',
      render: (amount) => <Text strong>${amount.toLocaleString()}</Text>
    },
    {
      title: 'Net Pay',
      dataIndex: 'totalNetPay',
      key: 'totalNetPay',
      align: 'right',
      render: (amount) => <Text style={{ color: '#52c41a' }}>${amount.toLocaleString()}</Text>
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
              onClick={() => navigate(`/payroll/runs/${record.id}`)}
            />
          </Tooltip>
          {record.status === 'pending' && (
            <Tooltip title="Process Payroll">
              <Button 
                type="text" 
                icon={<PlayCircleOutlined />} 
                onClick={() => processPayrollRun(record.id)}
              />
            </Tooltip>
          )}
          <Tooltip title="Download Report">
            <Button 
              type="text" 
              icon={<DownloadOutlined />} 
              onClick={() => downloadPayrollReport(record.id)}
            />
          </Tooltip>
        </Space>
      )
    }
  ];

  // Process payroll run
  const processPayrollRun = async (runId) => {
    try {
      Modal.confirm({
        title: 'Process Payroll Run',
        content: 'Are you sure you want to process this payroll run? This will calculate and prepare payments for all active employees.',
        okText: 'Process',
        cancelText: 'Cancel',
        onOk: async () => {
          await payrollService.processPayrollRun(runId);
          message.success('Payroll run processed successfully');
          fetchPayrollData();
        }
      });
    } catch (error) {
      message.error('Failed to process payroll run');
    }
  };

  // Download payroll report
  const downloadPayrollReport = (runId) => {
    message.info('Payroll report download feature coming soon');
  };

  // Create new payroll run
  const createPayrollRun = async () => {
    try {
      const values = await runForm.validateFields();
      await payrollService.createPayrollRun({
        period: values.period.format('YYYY-MM'),
        runDate: values.runDate.format('YYYY-MM-DD'),
        includeAllEmployees: values.includeAllEmployees,
        employeeIds: values.includeAllEmployees ? [] : values.employeeIds
      });
      message.success('Payroll run created successfully');
      setRunModalVisible(false);
      runForm.resetFields();
      fetchPayrollData();
    } catch (error) {
      message.error('Failed to create payroll run');
    }
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
          <Title level={2} style={{ margin: 0 }}>Payroll Management</Title>
          <Text type="secondary">Manage employee payroll and compensation</Text>
        </div>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={fetchPayrollData}>
            Refresh
          </Button>
          <Button icon={<PlusOutlined />} onClick={() => navigate('/payroll/employees/create')}>
            Add Employee
          </Button>
          <Button type="primary" icon={<CalendarOutlined />} onClick={() => setRunModalVisible(true)}>
            New Payroll Run
          </Button>
        </Space>
      </div>

      {/* Statistics Cards */}
      <Row gutter={[16, 16]} style={{ marginBottom: '24px' }}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Total Employees"
              value={statistics.totalEmployees}
              prefix={<TeamOutlined />}
              valueStyle={{ color: '#1890ff' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Active Employees"
              value={statistics.activeEmployees}
              prefix={<UserOutlined />}
              valueStyle={{ color: '#52c41a' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Monthly Payroll"
              value={statistics.totalPayroll}
              prefix={<DollarOutlined />}
              formatter={(value) => `$${value.toLocaleString()}`}
              valueStyle={{ color: '#722ed1' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <div style={{ textAlign: 'center' }}>
              <Text type="secondary" style={{ display: 'block', marginBottom: '8px' }}>
                Next Payroll Date
              </Text>
              <Text strong style={{ fontSize: '16px' }}>
                {statistics.nextPayrollDate}
              </Text>
              <Progress 
                percent={75} 
                size="small" 
                style={{ marginTop: '8px' }}
                format={() => '75% processed'}
              />
            </div>
          </Card>
        </Col>
      </Row>

      {/* Alert for pending payroll */}
      {statistics.pendingRuns > 0 && (
        <Alert
          message="Pending Payroll Runs"
          description={`You have ${statistics.pendingRuns} payroll run(s) pending processing. Please process them to ensure timely employee payments.`}
          type="warning"
          showIcon
          style={{ marginBottom: '24px' }}
          action={
            <Button size="small" type="primary" onClick={() => setRunModalVisible(true)}>
              Process Now
            </Button>
          }
        />
      )}

      {/* Employees Section */}
      <Row gutter={[24, 24]}>
        <Col xs={24} lg={12}>
          <Card 
            title="Employees" 
            extra={
              <Button type="text" onClick={() => navigate('/payroll/employees')}>
                View All
              </Button>
            }
          >
            <Table
              columns={employeeColumns}
              dataSource={employees}
              rowKey="id"
              loading={loading}
              pagination={{
                pageSize: 5,
                showSizeChanger: false,
                showQuickJumper: false
              }}
              size="small"
            />
          </Card>
        </Col>

        {/* Payroll Runs Section */}
        <Col xs={24} lg={12}>
          <Card 
            title="Recent Payroll Runs" 
            extra={
              <Button type="text" onClick={() => navigate('/payroll/runs')}>
                View All
              </Button>
            }
          >
            <Table
              columns={payrollRunColumns}
              dataSource={payrollRuns}
              rowKey="id"
              loading={loading}
              pagination={{
                pageSize: 5,
                showSizeChanger: false,
                showQuickJumper: false
              }}
              size="small"
            />
          </Card>
        </Col>
      </Row>

      {/* Quick Actions */}
      <Card title="Quick Actions" style={{ marginTop: '24px' }}>
        <Row gutter={[16, 16]}>
          <Col xs={24} sm={8}>
            <Button 
              size="large" 
              block 
              icon={<FileTextOutlined />}
              onClick={() => navigate('/payroll/reports')}
            >
              Generate Reports
            </Button>
          </Col>
          <Col xs={24} sm={8}>
            <Button 
              size="large" 
              block 
              icon={<PercentageOutlined />}
              onClick={() => navigate('/payroll/settings')}
            >
              Tax Settings
            </Button>
          </Col>
          <Col xs={24} sm={8}>
            <Button 
              size="large" 
              block 
              icon={<BankOutlined />}
              onClick={() => navigate('/payroll/banking')}
            >
              Banking Setup
            </Button>
          </Col>
        </Row>
      </Card>

      {/* Employee Detail Drawer */}
      <Drawer
        title={`Employee Details - ${selectedEmployee?.firstName} ${selectedEmployee?.lastName}`}
        placement="right"
        width={500}
        onClose={() => setEmployeeDrawerVisible(false)}
        open={employeeDrawerVisible}
      >
        {selectedEmployee && (
          <div>
            {/* Employee Info */}
            <Card style={{ marginBottom: '16px' }}>
              <div style={{ textAlign: 'center', marginBottom: '16px' }}>
                <Avatar size={64} icon={<UserOutlined />} />
                <Title level={4} style={{ margin: '8px 0' }}>
                  {selectedEmployee.firstName} {selectedEmployee.lastName}
                </Title>
                <Tag color={selectedEmployee.status === 'active' ? 'green' : 'red'}>
                  {selectedEmployee.status.toUpperCase()}
                </Tag>
              </div>
              
              <Row gutter={16}>
                <Col span={12}>
                  <div style={{ marginBottom: '12px' }}>
                    <Text type="secondary">Email:</Text>
                    <div>{selectedEmployee.email}</div>
                  </div>
                  <div style={{ marginBottom: '12px' }}>
                    <Text type="secondary">Department:</Text>
                    <div>{selectedEmployee.department}</div>
                  </div>
                </Col>
                <Col span={12}>
                  <div style={{ marginBottom: '12px' }}>
                    <Text type="secondary">Position:</Text>
                    <div>{selectedEmployee.position}</div>
                  </div>
                  <div style={{ marginBottom: '12px' }}>
                    <Text type="secondary">Hire Date:</Text>
                    <div>{dayjs(selectedEmployee.hireDate).format('MMMM DD, YYYY')}</div>
                  </div>
                </Col>
              </Row>
            </Card>

            {/* Compensation */}
            <Card style={{ marginBottom: '16px' }}>
              <Title level={5}>Compensation</Title>
              <Row gutter={16}>
                <Col span={12}>
                  <div style={{ marginBottom: '12px' }}>
                    <Text type="secondary">Annual Salary:</Text>
                    <div style={{ fontSize: '18px', fontWeight: 'bold', color: '#52c41a' }}>
                      ${selectedEmployee.salary.toLocaleString()}
                    </div>
                  </div>
                </Col>
                <Col span={12}>
                  <div style={{ marginBottom: '12px' }}>
                    <Text type="secondary">Monthly Pay:</Text>
                    <div style={{ fontSize: '18px', fontWeight: 'bold' }}>
                      ${(selectedEmployee.salary / 12).toLocaleString()}
                    </div>
                  </div>
                </Col>
              </Row>
              
              <Divider />
              
              <Row gutter={16}>
                <Col span={12}>
                  <div style={{ marginBottom: '12px' }}>
                    <Text type="secondary">Estimated Tax (20%):</Text>
                    <div>${((selectedEmployee.salary / 12) * 0.2).toLocaleString()}</div>
                  </div>
                </Col>
                <Col span={12}>
                  <div style={{ marginBottom: '12px' }}>
                    <Text type="secondary">Estimated Net Pay:</Text>
                    <div style={{ color: '#52c41a', fontWeight: 'bold' }}>
                      ${((selectedEmployee.salary / 12) * 0.8).toLocaleString()}
                    </div>
                  </div>
                </Col>
              </Row>
            </Card>

            {/* Banking Information */}
            <Card style={{ marginBottom: '16px' }}>
              <Title level={5}>Banking Information</Title>
              <Row gutter={16}>
                <Col span={12}>
                  <div style={{ marginBottom: '12px' }}>
                    <Text type="secondary">Bank Account:</Text>
                    <div>{selectedEmployee.bankAccount}</div>
                  </div>
                </Col>
                <Col span={12}>
                  <div style={{ marginBottom: '12px' }}>
                    <Text type="secondary">Tax ID:</Text>
                    <div>{selectedEmployee.taxId}</div>
                  </div>
                </Col>
              </Row>
            </Card>

            {/* Actions */}
            <div style={{ textAlign: 'center' }}>
              <Space>
                <Button icon={<EditOutlined />} onClick={() => navigate(`/payroll/employees/${selectedEmployee.id}`)}>
                  Edit Employee
                </Button>
                <Button icon={<FileTextOutlined />} onClick={() => navigate(`/payroll/employees/${selectedEmployee.id}/payslips`)}>
                  View Payslips
                </Button>
              </Space>
            </div>
          </div>
        )}
      </Drawer>

      {/* New Payroll Run Modal */}
      <Modal
        title="Create New Payroll Run"
        open={runModalVisible}
        onCancel={() => {
          setRunModalVisible(false);
          runForm.resetFields();
        }}
        footer={[
          <Button key="cancel" onClick={() => setRunModalVisible(false)}>
            Cancel
          </Button>,
          <Button key="create" type="primary" onClick={createPayrollRun}>
            Create Run
          </Button>
        ]}
      >
        <Form form={runForm} layout="vertical">
          <Form.Item
            name="period"
            label="Payroll Period"
            rules={[{ required: true, message: 'Please select payroll period' }]}
          >
            <DatePicker.MonthPicker style={{ width: '100%' }} />
          </Form.Item>
          
          <Form.Item
            name="runDate"
            label="Run Date"
            rules={[{ required: true, message: 'Please select run date' }]}
          >
            <DatePicker style={{ width: '100%' }} />
          </Form.Item>

          <Form.Item
            name="includeAllEmployees"
            label="Include Employees"
            valuePropName="checked"
            initialValue={true}
          >
            <Select>
              <Option value={true}>All Active Employees</Option>
              <Option value={false}>Selected Employees Only</Option>
            </Select>
          </Form.Item>

          <Form.Item
            noStyle
            shouldUpdate={(prevValues, currentValues) => prevValues.includeAllEmployees !== currentValues.includeAllEmployees}
          >
            {({ getFieldValue }) => 
              !getFieldValue('includeAllEmployees') && (
                <Form.Item
                  name="employeeIds"
                  label="Select Employees"
                  rules={[{ required: true, message: 'Please select employees' }]}
                >
                  <Select
                    mode="multiple"
                    placeholder="Select employees for this payroll run"
                    style={{ width: '100%' }}
                  >
                    {employees
                      .filter(emp => emp.status === 'active')
                      .map(emp => (
                        <Option key={emp.id} value={emp.id}>
                          {emp.firstName} {emp.lastName}
                        </Option>
                      ))}
                  </Select>
                </Form.Item>
              )
            }
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
};

export default PayrollDashboard;
