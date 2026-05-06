import React, { useState, useEffect } from 'react';
import {
  Form,
  Input,
  Button,
  Card,
  Typography,
  Space,
  Divider,
  Alert,
  Checkbox,
  Row,
  Col,
  message,
  Spin
} from 'antd';
import {
  UserOutlined,
  LockOutlined,
  EyeOutlined,
  EyeInvisibleOutlined,
  GoogleOutlined,
  MicrosoftOutlined,
  AppleOutlined
} from '@ant-design/icons';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { authService } from '../api/services/apiServices';

const { Title, Text, Link } = Typography;

const EnhancedLogin = () => {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [rememberMe, setRememberMe] = useState(false);
  const [error, setError] = useState('');
  const [loginAttempts, setLoginAttempts] = useState(0);
  
  const { login, isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  // Redirect if already authenticated
  useEffect(() => {
    if (isAuthenticated) {
      const from = location.state?.from?.pathname || '/dashboard';
      navigate(from, { replace: true });
    }
  }, [isAuthenticated, navigate, location]);

  // Check for remembered credentials
  useEffect(() => {
    const rememberedEmail = localStorage.getItem('rememberedEmail');
    if (rememberedEmail) {
      form.setFieldsValue({ email: rememberedEmail });
      setRememberMe(true);
    }
  }, [form]);

  const handleSubmit = async (values) => {
    setLoading(true);
    setError('');

    try {
      // Call authentication service
      const response = await authService.login({
        email: values.email,
        password: values.password,
        rememberMe: rememberMe
      });

      // Store token and user info
      if (response.token) {
        // Handle remember me
        if (rememberMe) {
          localStorage.setItem('rememberedEmail', values.email);
        } else {
          localStorage.removeItem('rememberedEmail');
        }

        // Login using auth context
        await login(response.token, response.user);

        message.success('Login successful!');

        // Redirect to intended page
        const from = location.state?.from?.pathname || '/dashboard';
        navigate(from, { replace: true });
      } else {
        throw new Error('Invalid login response');
      }
    } catch (err) {
      const errorMessage = err.message || 'Login failed. Please try again.';
      setError(errorMessage);
      
      // Increment login attempts for potential lockout
      setLoginAttempts(prev => prev + 1);
      
      // Show error message
      message.error(errorMessage);
    } finally {
      setLoading(false);
    }
  };

  const handleForgotPassword = async () => {
    const email = form.getFieldValue('email');
    
    if (!email) {
      message.warning('Please enter your email address first.');
      return;
    }

    try {
      await authService.forgotPassword(email);
      message.success('Password reset instructions have been sent to your email.');
    } catch (err) {
      message.error('Failed to send reset instructions. Please try again.');
    }
  };

  const handleSocialLogin = (provider) => {
    // Placeholder for social login
    message.info(`${provider} login coming soon!`);
  };

  // Lock user after too many failed attempts
  if (loginAttempts >= 5) {
    return (
      <div style={{
        minHeight: '100vh',
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)'
      }}>
        <Card style={{ width: 400, textAlign: 'center' }}>
          <LockOutlined style={{ fontSize: '48px', color: '#ff4d4f', marginBottom: '16px' }} />
          <Title level={3}>Account Locked</Title>
          <Text>
            Too many failed login attempts. Please try again later or contact support.
          </Text>
          <div style={{ marginTop: '16px' }}>
            <Button type="primary" onClick={() => setLoginAttempts(0)}>
              Try Again
            </Button>
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div style={{
      minHeight: '100vh',
      display: 'flex',
      justifyContent: 'center',
      alignItems: 'center',
      background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
      padding: '20px'
    }}>
      <Card 
        style={{ 
          width: '100%', 
          maxWidth: 450,
          boxShadow: '0 8px 32px rgba(0, 0, 0, 0.1)'
        }}
        bodyStyle={{ padding: '32px' }}
      >
        {/* Logo and Title */}
        <div style={{ textAlign: 'center', marginBottom: '32px' }}>
          <div style={{
            width: '64px',
            height: '64px',
            borderRadius: '12px',
            background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            margin: '0 auto 16px',
            fontSize: '24px',
            color: 'white',
            fontWeight: 'bold'
          }}>
            ERP
          </div>
          <Title level={2} style={{ margin: 0 }}>
            Welcome Back
          </Title>
          <Text type="secondary">
            Sign in to your ERP dashboard
          </Text>
        </div>

        {/* Error Alert */}
        {error && (
          <Alert
            message={error}
            type="error"
            showIcon
            style={{ marginBottom: '24px' }}
            closable
            onClose={() => setError('')}
          />
        )}

        {/* Login Form */}
        <Form
          form={form}
          name="login"
          onFinish={handleSubmit}
          layout="vertical"
          size="large"
        >
          <Form.Item
            name="email"
            label="Email Address"
            rules={[
              { required: true, message: 'Please enter your email address' },
              { type: 'email', message: 'Please enter a valid email address' }
            ]}
          >
            <Input
              prefix={<UserOutlined />}
              placeholder="your.email@company.com"
              autoComplete="email"
            />
          </Form.Item>

          <Form.Item
            name="password"
            label="Password"
            rules={[
              { required: true, message: 'Please enter your password' },
              { min: 6, message: 'Password must be at least 6 characters' }
            ]}
          >
            <Input.Password
              prefix={<LockOutlined />}
              placeholder="Enter your password"
              autoComplete="current-password"
              visibilityToggle={{
                visible: showPassword,
                onVisibleChange: setShowPassword,
              }}
              iconRender={(visible) => (visible ? <EyeOutlined /> : <EyeInvisibleOutlined />)}
            />
          </Form.Item>

          <Form.Item>
            <Row justify="space-between" align="middle">
              <Col>
                <Checkbox
                  checked={rememberMe}
                  onChange={(e) => setRememberMe(e.target.checked)}
                >
                  Remember me
                </Checkbox>
              </Col>
              <Col>
                <Link onClick={handleForgotPassword}>
                  Forgot password?
                </Link>
              </Col>
            </Row>
          </Form.Item>

          <Form.Item>
            <Button
              type="primary"
              htmlType="submit"
              loading={loading}
              block
              style={{ height: '48px', fontSize: '16px' }}
            >
              {loading ? 'Signing in...' : 'Sign In'}
            </Button>
          </Form.Item>
        </Form>

        {/* Divider */}
        <Divider style={{ margin: '24px 0' }}>Or continue with</Divider>

        {/* Social Login Buttons */}
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
          <Button
            icon={<GoogleOutlined />}
            onClick={() => handleSocialLogin('Google')}
            block
            size="large"
            style={{ height: '44px' }}
          >
            Continue with Google
          </Button>
          
          <Button
            icon={<MicrosoftOutlined />}
            onClick={() => handleSocialLogin('Microsoft')}
            block
            size="large"
            style={{ height: '44px' }}
          >
            Continue with Microsoft
          </Button>
          
          <Button
            icon={<AppleOutlined />}
            onClick={() => handleSocialLogin('Apple')}
            block
            size="large"
            style={{ height: '44px' }}
          >
            Continue with Apple
          </Button>
        </Space>

        {/* Footer */}
        <div style={{ textAlign: 'center', marginTop: '24px' }}>
          <Text type="secondary">
            Don't have an account? <Link href="/register">Sign up</Link>
          </Text>
          <br />
          <Text type="secondary" style={{ fontSize: '12px' }}>
            By signing in, you agree to our{' '}
            <Link href="/terms">Terms of Service</Link> and{' '}
            <Link href="/privacy">Privacy Policy</Link>
          </Text>
        </div>
      </Card>
    </div>
  );
};

export default EnhancedLogin;
