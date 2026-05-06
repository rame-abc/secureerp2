import React from 'react';
import { Card, Typography, Table, Button, Space, Tag, Input, Select, DatePicker } from 'antd';
import { PlusOutlined, UserOutlined, DollarOutlined, CalendarOutlined } from '@ant-design/icons';

const { Title } = Typography;
const { Search } = Input;
const { RangePicker } = DatePicker;

const Payroll = () => {
  // Sample data - in real app, this would come from API
  const payrollRecords = [
    {
      id: 1,
      employeeName: 'John Smith',
      employeeId: 'EMP001',
      department: 'Engineering',
      position: 'Senior Developer',
      baseSalary: 8000,
      overtime: 1200,
      deductions: 1500,
      netSalary: 7700,
      payPeriod: 'January 2024',
      status: 'Processed',
      payDate: '2024-01-31',
    },
    {
      id: 2,
      employeeName: 'Sarah Johnson',
      employeeId: 'EMP002',
      department: 'Sales',
      position: 'Sales Manager',
      baseSalary: 6500,
      overtime: 800,
      deductions: 1200,
      netSalary: 6100,
      payPeriod: 'January 2024',
      status: 'Processed',
      payDate: '2024-01-31',
    },
    {
      id: 3,
      employeeName: 'Michael Brown',
      employeeId: 'EMP003',
      department: 'Marketing',
      position: 'Marketing Specialist',
      baseSalary: 5500,
      overtime: 400,
      deductions: 1000,
      netSalary: 4900,
      payPeriod: 'January 2024',
      status: 'Pending',
      payDate: '2024-01-31',
    },
    {
      id: 4,
      employeeName: 'Emily Davis',
      employeeId: 'EMP004',
      department: 'HR',
      position: 'HR Manager',
      baseSalary: 7000,
      overtime: 600,
      deductions: 1300,
      netSalary: 6300,
      payPeriod: 'January 2024',
      status: 'Processed',
      payDate: '2024-01-31',
    },
    {
      id: 5,
      employeeName: 'David Wilson',
      employeeId: 'EMP005',
      department: 'Finance',
      position: 'Accountant',
      baseSalary: 6000,
      overtime: 500,
      deductions: 1100,
      netSalary: 5400,
      payPeriod: 'January 2024',
      status: 'Processed',
      payDate: '2024-01-31',
    },
  ];

  const columns = [
    {
      title: 'Employee Name',
      dataIndex: 'employeeName',
      key: 'employeeName',
    },
    {
      title: 'Employee ID',
      dataIndex: 'employeeId',
      key: 'employeeId',
      width: 100,
    },
    {
      title: 'Department',
      dataIndex: 'department',
      key: 'department',
      width: 120,
      render: (dept) => <Tag color="blue">{dept}</Tag>,
    },
    {
      title: 'Position',
      dataIndex: 'position',
      key: 'position',
      width: 150,
    },
    {
      title: 'Base Salary',
      dataIndex: 'baseSalary',
      key: 'baseSalary',
      width: 120,
      render: (salary) => `$${salary.toFixed(2)}`,
    },
    {
      title: 'Overtime',
      dataIndex: 'overtime',
      key: 'overtime',
      width: 100,
      render: (overtime) => `$${overtime.toFixed(2)}`,
    },
    {
      title: 'Deductions',
      dataIndex: 'deductions',
      key: 'deductions',
      width: 120,
      render: (deductions) => `$${deductions.toFixed(2)}`,
    },
    {
      title: 'Net Salary',
      dataIndex: 'netSalary',
      key: 'netSalary',
      width: 120,
      render: (salary) => <strong>${salary.toFixed(2)}</strong>,
    },
    {
      title: 'Pay Period',
      dataIndex: 'payPeriod',
      key: 'payPeriod',
      width: 120,
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      width: 100,
      render: (status) => {
        const color = {
          'Processed': 'green',
          'Pending': 'orange',
          'Failed': 'red',
        }[status] || 'default';
        return <Tag color={color}>{status}</Tag>;
      },
    },
    {
      title: 'Pay Date',
      dataIndex: 'payDate',
      key: 'payDate',
      width: 120,
    },
    {
      title: 'Actions',
      key: 'actions',
      width: 100,
      render: (_, record) => (
        <Space>
          <Button
            type="text"
            icon={<UserOutlined />}
            size="small"
            title="View Details"
          />
          <Button
            type="text"
            icon={<DollarOutlined />}
            size="small"
            title="View Payslip"
          />
        </Space>
      ),
    },
  ];

  return (
    <div>
      <Title level={2}>Payroll Management</Title>
      
      <Card>
        <div style={{ marginBottom: 16, display: 'flex', justifyContent: 'space-between' }}>
          <Space>
            <Search
              placeholder="Search employees..."
              allowClear
              style={{ width: 250 }}
            />
            <Select
              placeholder="Filter by department"
              style={{ width: 150 }}
              allowClear
            >
              <Select.Option value="Engineering">Engineering</Select.Option>
              <Select.Option value="Sales">Sales</Select.Option>
              <Select.Option value="Marketing">Marketing</Select.Option>
              <Select.Option value="HR">HR</Select.Option>
              <Select.Option value="Finance">Finance</Select.Option>
            </Select>
            <Select
              placeholder="Filter by status"
              style={{ width: 120 }}
              allowClear
            >
              <Select.Option value="Processed">Processed</Select.Option>
              <Select.Option value="Pending">Pending</Select.Option>
              <Select.Option value="Failed">Failed</Select.Option>
            </Select>
            <DatePicker.MonthPicker placeholder="Pay Period" />
          </Space>
          
          <Space>
            <Button icon={<CalendarOutlined />}>
              Process Payroll
            </Button>
            <Button type="primary" icon={<PlusOutlined />}>
              Add Employee
            </Button>
          </Space>
        </div>

        <Table
          columns={columns}
          dataSource={payrollRecords}
          rowKey="id"
          pagination={{
            pageSize: 10,
            showSizeChanger: true,
            showQuickJumper: true,
          }}
          summary={(pageData) => {
            const totalBaseSalary = pageData.reduce((sum, record) => sum + record.baseSalary, 0);
            const totalOvertime = pageData.reduce((sum, record) => sum + record.overtime, 0);
            const totalDeductions = pageData.reduce((sum, record) => sum + record.deductions, 0);
            const totalNetSalary = pageData.reduce((sum, record) => sum + record.netSalary, 0);

            return (
              <Table.Summary.Row>
                <Table.Summary.Cell index={0} colSpan={4}>
                  <strong>Total</strong>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={4}>
                  <strong>${totalBaseSalary.toFixed(2)}</strong>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={5}>
                  <strong>${totalOvertime.toFixed(2)}</strong>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={6}>
                  <strong>${totalDeductions.toFixed(2)}</strong>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={7}>
                  <strong>${totalNetSalary.toFixed(2)}</strong>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={8} colSpan={3}></Table.Summary.Cell>
              </Table.Summary.Row>
            );
          }}
        />
      </Card>
    </div>
  );
};

export default Payroll;
