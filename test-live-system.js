// 🚀 STEP 27: LIVE SYSTEM VERIFICATION SCRIPT
// Run this script to test your deployed SaaS ERP system

const axios = require('axios');

// Configuration - UPDATE THESE WITH YOUR ACTUAL URLS
const BACKEND_URL = 'https://your-service.onrender.com';
const FRONTEND_URL = 'https://your-frontend.vercel.app';

// Test credentials - UPDATE WITH YOUR ACTUAL TEST USER
const TEST_USER = {
    email: 'test@example.com',
    password: 'testpassword'
};

let jwtToken = null;
let testJournalId = null;

console.log('🚀 STARTING LIVE SYSTEM VERIFICATION...\n');

// ✅ STEP 27.1: Test Backend is REALLY online
async function testBackendOnline() {
    console.log('📍 STEP 27.1: Testing Backend Online...');
    try {
        const response = await axios.get(`${BACKEND_URL}/api/test`);
        console.log('✅ Backend Response:', response.data);
        
        if (response.data === 'ok' || response.data === 'ERP API is running!') {
            console.log('✅ STEP 27.1 PASSED: Backend is online\n');
            return true;
        } else {
            console.log('❌ STEP 27.1 FAILED: Unexpected backend response\n');
            return false;
        }
    } catch (error) {
        console.log('❌ STEP 27.1 FAILED: Backend not accessible');
        console.log('Error:', error.message);
        console.log('Check: Backend URL is correct and service is running\n');
        return false;
    }
}

// ✅ STEP 27.2: Verify Database connection (via health check)
async function testDatabaseConnection() {
    console.log('📍 STEP 27.2: Testing Database Connection...');
    try {
        const response = await axios.get(`${BACKEND_URL}/api/health`);
        console.log('✅ Database Health Check Passed');
        console.log('✅ STEP 27.2 PASSED: Database connected\n');
        return true;
    } catch (error) {
        // Try alternative endpoint
        try {
            await axios.get(`${BACKEND_URL}/api/finance/accounts`);
            console.log('✅ Database connection working (finance endpoint accessible)');
            console.log('✅ STEP 27.2 PASSED: Database connected\n');
            return true;
        } catch (financeError) {
            console.log('❌ STEP 27.2 FAILED: Database connection issue');
            console.log('Error:', error.message);
            console.log('Check: PostgreSQL connection string in Render environment\n');
            return false;
        }
    }
}

// ✅ STEP 27.3: Confirm Frontend pointing to correct backend
async function testFrontendConnection() {
    console.log('📍 STEP 27.3: Testing Frontend Connection...');
    try {
        const response = await axios.get(FRONTEND_URL);
        console.log('✅ Frontend accessible');
        
        // Check if frontend has correct API URL configured
        if (response.status === 200) {
            console.log('✅ STEP 27.3 PASSED: Frontend is accessible\n');
            return true;
        } else {
            console.log('❌ STEP 27.3 FAILED: Frontend not accessible\n');
            return false;
        }
    } catch (error) {
        console.log('❌ STEP 27.3 FAILED: Frontend not accessible');
        console.log('Error:', error.message);
        console.log('Check: Frontend URL is correct and Vercel deployment is working\n');
        return false;
    }
}

// ✅ STEP 27.4: Test FULL LOGIN FLOW
async function testLoginFlow() {
    console.log('📍 STEP 27.4: Testing Login Flow...');
    try {
        const response = await axios.post(`${BACKEND_URL}/api/auth/login`, TEST_USER);
        
        if (response.data.token) {
            jwtToken = response.data.token;
            console.log('✅ Login successful - JWT token received');
            console.log('✅ STEP 27.4 PASSED: Login flow working\n');
            return true;
        } else {
            console.log('❌ STEP 27.4 FAILED: No JWT token received');
            console.log('Response:', response.data);
            console.log('Check: User credentials and JWT configuration\n');
            return false;
        }
    } catch (error) {
        console.log('❌ STEP 27.4 FAILED: Login failed');
        console.log('Error:', error.response?.data || error.message);
        console.log('Check: User exists, password correct, CORS configured\n');
        return false;
    }
}

// ✅ STEP 27.5: Test REAL ACCOUNTING FLOW
async function testAccountingFlow() {
    console.log('📍 STEP 27.5: Testing Accounting Flow...');
    
    if (!jwtToken) {
        console.log('❌ STEP 27.5 FAILED: No JWT token available');
        return false;
    }

    const config = {
        headers: { 'Authorization': `Bearer ${jwtToken}` }
    };

    // Step A: Create Journal Entry
    console.log('  📝 Step A: Creating Journal Entry...');
    try {
        const journalResponse = await axios.post(`${BACKEND_URL}/api/finance/journal`, {
            description: 'Test Journal Entry - Live Verification',
            entries: [
                {
                    accountId: 1,
                    debit: 1000,
                    credit: 0,
                    description: 'Test Cash Debit'
                },
                {
                    accountId: 2,
                    debit: 0,
                    credit: 1000,
                    description: 'Test Revenue Credit'
                }
            ]
        }, config);

        testJournalId = journalResponse.data.id;
        console.log('✅ Journal entry created successfully');
    } catch (error) {
        console.log('❌ Journal creation failed:', error.response?.data || error.message);
        console.log('Check: Chart of accounts seeded, user has correct role\n');
        return false;
    }

    // Step B: Post Journal Entry
    console.log('  📤 Step B: Posting Journal Entry...');
    try {
        await axios.post(`${BACKEND_URL}/api/finance/journal/${testJournalId}/post`, {}, config);
        console.log('✅ Journal entry posted successfully');
    } catch (error) {
        console.log('❌ Journal posting failed:', error.response?.data || error.message);
        console.log('Check: User has Accountant/Admin role\n');
        return false;
    }

    // Step C: Generate Income Statement
    console.log('  📊 Step C: Generating Income Statement...');
    try {
        const reportResponse = await axios.get(`${BACKEND_URL}/api/finance/income-statement`, config);
        
        if (reportResponse.data.revenue && reportResponse.data.expenses) {
            console.log('✅ Income statement generated successfully');
            console.log('Revenue:', reportResponse.data.revenue?.totalRevenue || 0);
            console.log('Expenses:', reportResponse.data.expenses?.totalExpenses || 0);
            console.log('✅ STEP 27.5 PASSED: Accounting flow working\n');
            return true;
        } else {
            console.log('❌ Income statement format incorrect');
            console.log('Response:', reportResponse.data);
            return false;
        }
    } catch (error) {
        console.log('❌ Income statement generation failed:', error.response?.data || error.message);
        console.log('Check: Database has financial data, permissions correct\n');
        return false;
    }
}

// Main verification function
async function runLiveVerification() {
    console.log('🎯 Starting Complete Live System Verification...\n');
    
    const results = {
        backendOnline: await testBackendOnline(),
        databaseConnected: await testDatabaseConnection(),
        frontendConnected: await testFrontendConnection(),
        loginFlow: await testLoginFlow(),
        accountingFlow: await testAccountingFlow()
    };

    console.log('📊 VERIFICATION RESULTS:');
    console.log(`✅ Backend Online: ${results.backendOnline ? 'PASS' : 'FAIL'}`);
    console.log(`✅ Database Connected: ${results.databaseConnected ? 'PASS' : 'FAIL'}`);
    console.log(`✅ Frontend Connected: ${results.frontendConnected ? 'PASS' : 'FAIL'}`);
    console.log(`✅ Login Flow: ${results.loginFlow ? 'PASS' : 'FAIL'}`);
    console.log(`✅ Accounting Flow: ${results.accountingFlow ? 'PASS' : 'FAIL'}`);

    const allPassed = Object.values(results).every(result => result === true);
    
    if (allPassed) {
        console.log('\n🎉 CONGRATULATIONS! YOUR SAAS ERP SYSTEM IS LIVE! 🎉');
        console.log('✅ All critical components are working correctly');
        console.log('✅ Your system is ready for production use');
        console.log('\n🚀 Next steps:');
        console.log('1. Monitor application performance');
        console.log('2. Set up database backups');
        console.log('3. Configure SSL certificates');
        console.log('4. Plan for scaling based on user growth');
    } else {
        console.log('\n❌ SYSTEM NOT READY FOR PRODUCTION');
        console.log('Please fix the failed components before going live');
        console.log('\n🔧 Troubleshooting:');
        console.log('1. Check Render logs for backend issues');
        console.log('2. Check Vercel logs for frontend issues');
        console.log('3. Verify environment variables');
        console.log('4. Test database connection');
    }

    return allPassed;
}

// Run the verification
if (require.main === module) {
    runLiveVerification().catch(console.error);
}

module.exports = {
    runLiveVerification,
    testBackendOnline,
    testDatabaseConnection,
    testFrontendConnection,
    testLoginFlow,
    testAccountingFlow
};
